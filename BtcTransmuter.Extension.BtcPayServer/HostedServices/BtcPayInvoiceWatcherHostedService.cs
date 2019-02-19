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
using BtcTransmuter.Extension.BtcPayServer.Triggers.InvoiceStatusChanged;
using BtcTransmuter.Extension.Email.ExternalServices.Pop3;
using Microsoft.Extensions.Hosting;
using NBitcoin;


namespace BtcTransmuter.Extension.Email.HostedServices
{
    public class BtcPayInvoiceWatcherHostedService : IHostedService
    {
        private readonly IExternalServiceManager _externalServiceManager;
        private readonly ITriggerDispatcher _triggerDispatcher;
        private ConcurrentDictionary<string, BtcPayServerService> _externalServices;

        public BtcPayInvoiceWatcherHostedService(IExternalServiceManager externalServiceManager,
            ITriggerDispatcher triggerDispatcher)
        {
            _externalServiceManager = externalServiceManager;
            _triggerDispatcher = triggerDispatcher;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var externalServices = await _externalServiceManager.GetExternalServicesData(new ExternalServicesDataQuery()
            {
                Type = new[]
                {
                    BtcPayServerService.BtcPayServerServiceType
                }
            });
            _externalServices = new ConcurrentDictionary<string, BtcPayServerService>(
                externalServices
                    .Select(service =>
                        new KeyValuePair<string, BtcPayServerService>(service.Id, new BtcPayServerService(service))));

            _externalServiceManager.ExternalServiceDataUpdated += ExternalServiceManagerOnExternalServiceUpdated;
            _ = Loop(cancellationToken);
        }

        private async Task Loop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tasks = _externalServices.Select(CheckInvoiceChangeInService);
                await Task.WhenAll(tasks);
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        private async Task CheckInvoiceChangeInService(KeyValuePair<string, BtcPayServerService> pair)
        {
            var (key, service) = pair;
            if (!await service.CheckAccess())
            {
                return;
            }

            var data = service.Data;
            data.LastCheck = DateTime.Now;
            var client = service.ConstructClient();

            var invoices = await client.GetInvoicesAsync();

            foreach (var invoice in invoices)
            {
                //do not trigger on first run
                if (data.LastCheck.HasValue)
                {
                    if (data.MonitoredInvoiceStatuses.ContainsKey(invoice.Id))
                    {
                        if (data.MonitoredInvoiceStatuses[invoice.Id] != invoice.Status)
                        {
                            await _triggerDispatcher.DispatchTrigger(new InvoiceStatusChangedTrigger()
                            {
                                Data = new InvoiceStatusChangedTriggerData()
                                {
                                    Invoice = invoice,
                                    ExternalServiceId = key
                                }
                            });
                        }
                    }
                    else
                    {
                        await _triggerDispatcher.DispatchTrigger(new InvoiceStatusChangedTrigger()
                        {
                            Data = new InvoiceStatusChangedTriggerData()
                            {
                                Invoice = invoice,
                                ExternalServiceId = key
                            }
                        });
                    }
                }

                data.MonitoredInvoiceStatuses.AddOrReplace(invoice.Id, invoice.Status);
            }


            service.Data = data;
            await _externalServiceManager.UpdateInternalData(key, service.Data);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void ExternalServiceManagerOnExternalServiceUpdated(object sender, UpdatedItem<ExternalServiceData> e)
        {
            if (e.Item.Type != BtcPayServerService.BtcPayServerServiceType)
            {
                return;
            }

            switch (e.Action)
            {
                case UpdatedItem<ExternalServiceData>.UpdateAction.Added:
                    _externalServices.TryAdd(e.Item.Id, new BtcPayServerService(e.Item));
                    break;
                case UpdatedItem<ExternalServiceData>.UpdateAction.Removed:
                    _externalServices.TryRemove(e.Item.Id, out var _);
                    break;
                case UpdatedItem<ExternalServiceData>.UpdateAction.Updated:
                    _externalServices.TryUpdate(e.Item.Id, new BtcPayServerService(e.Item), null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}