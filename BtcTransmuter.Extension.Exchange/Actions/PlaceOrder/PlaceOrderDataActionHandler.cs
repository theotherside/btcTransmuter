using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BtcTransmuter.Abstractions.Actions;
using BtcTransmuter.Data.Entities;
using BtcTransmuter.Extension.Exchange.ExternalServices.Exchange;
using ExchangeSharp;

namespace BtcTransmuter.Extension.Exchange.Actions.PlaceOrder
{
    public class PlaceOrderDataActionHandler : BaseActionHandler<PlaceOrderData>
    {
        public override string ActionId => "PlaceOrder";
        public override string Name => "Place Order";

        public override string Description =>
            "Place an order on an exchange";

        public override string ViewPartial => "ViewPlaceOrderAction";
        public override string ControllerName => "PlaceOrder";


        protected override async Task<ActionHandlerResult> Execute(Dictionary<string, object> data, RecipeAction recipeAction,
            PlaceOrderData actionData)
        {
            var exchangeService = new ExchangeService(recipeAction.ExternalService);
            var client = exchangeService.ConstructClient();

            var orderRequest = new ExchangeOrderRequest()
            {
                MarketSymbol = actionData.MarketSymbol,
                OrderType = actionData.OrderType,
                Price = Convert.ToDecimal(InterpolateString(actionData.Price, data)),
                Amount = Convert.ToDecimal(InterpolateString(actionData.Amount, data)),
                StopPrice = Convert.ToDecimal(InterpolateString(actionData.StopPrice, data)),
                IsBuy = actionData.IsBuy,
                IsMargin = actionData.IsMargin,
            };

            try
            {
                var result = await client.PlaceOrderAsync(orderRequest);
                System.Threading.Thread.Sleep(500);
                result = await client.GetOrderDetailsAsync(result.OrderId);
                return new ActionHandlerResult()
                {
                    Executed = true,
                    Result =
                        $"Place order ({result.OrderId}) Status: {result.Result}",
                    Data = result
                };
            }
            catch (Exception e)
            {
                return new ActionHandlerResult()
                {
                    Executed = false,
                    Result =
                        $"Could not place order because {e.Message}"
                };
            }
        }
    }
}