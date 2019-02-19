﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BtcTransmuter.Abstractions;
using BtcTransmuter.Abstractions.ExternalServices;
using BtcTransmuter.Abstractions.Triggers;
using BtcTransmuter.Data.Entities;
using BtcTransmuter.Extension.Email.ExternalServices;
using BtcTransmuter.Extension.Email.ExternalServices.Pop3;
using BtcTransmuter.Extension.Email.Triggers;
using BtcTransmuter.Extension.Email.Triggers.ReceivedEmail;
using Microsoft.Extensions.Hosting;
using Pop3;

namespace BtcTransmuter.Extension.Email.HostedServices
{
    public class ReceivingEmailHostedService : IHostedService
    {
        private readonly IExternalServiceManager _externalServiceManager;
        private readonly ITriggerDispatcher _triggerDispatcher;
        private ConcurrentDictionary<string, Pop3Service> _externalServices;

        public ReceivingEmailHostedService(IExternalServiceManager externalServiceManager,
            ITriggerDispatcher triggerDispatcher)
        {
            _externalServiceManager = externalServiceManager;
            _triggerDispatcher = triggerDispatcher;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var pop3Services = await _externalServiceManager.GetExternalServicesData(new ExternalServicesDataQuery()
            {
                Type = new[]
                {
                    Pop3Service.Pop3ExternalServiceType
                }
            });
            _externalServices = new ConcurrentDictionary<string, Pop3Service>(
                pop3Services
                    .Select(service => new KeyValuePair<string, Pop3Service>(service.Id, new Pop3Service(service))));

            _externalServiceManager.ExternalServiceDataUpdated += ExternalServiceManagerOnExternalServiceUpdated;
            _ = Loop(cancellationToken);
        }

        private async Task Loop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tasks = _externalServices.Select(Selector);

                await Task.WhenAll(tasks);
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        private async Task Selector(KeyValuePair<string, Pop3Service> service)
        {
            var pop3Client = await service.Value.CreateClientAndConnect();

            if (pop3Client == null)
            {
                return;
            }

            var emails = await pop3Client.ListAndRetrieveHeaderAsync();

            var validEmails = emails.Where(message =>
                !service.Value.Data.LastCheck.HasValue ||
                DateTime.Parse(message.Date) >= service.Value.Data.LastCheck.Value).ToList();

            await pop3Client.RetrieveAsync(validEmails);
            foreach (var email in validEmails)
            {
                var trigger = new ReceivedEmailTrigger()
                {
                    Data = new ReceivedEmailTriggerData()
                    {
                        Body = email.Body,
                        Subject = email.Subject,
                        FromEmail = email.From,
                        ExternalServiceId = service.Key
                    }
                };
                await _triggerDispatcher.DispatchTrigger(trigger);
            }

            var newData = service.Value.Data;
            newData.LastCheck = DateTime.Now;
            service.Value.Data = newData;
            await _externalServiceManager.UpdateInternalData(service.Key, service.Value.Data);
            await pop3Client.DisconnectAsync();
            pop3Client.Dispose();

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void ExternalServiceManagerOnExternalServiceUpdated(object sender, UpdatedItem<ExternalServiceData> e)
        {
            if (e.Item.Type != Pop3Service.Pop3ExternalServiceType)
            {
                return;
            }

            switch (e.Action)
            {
                case UpdatedItem<ExternalServiceData>.UpdateAction.Added:
                    _externalServices.TryAdd(e.Item.Id, new Pop3Service(e.Item));
                    break;
                case UpdatedItem<ExternalServiceData>.UpdateAction.Removed:
                    _externalServices.TryRemove(e.Item.Id, out var _);
                    break;
                case UpdatedItem<ExternalServiceData>.UpdateAction.Updated:
                    _externalServices.TryUpdate(e.Item.Id, new Pop3Service(e.Item), null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}