using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class QCPaperTradingBrokerageExampleAlgorithm : QCAlgorithm
    {
        private Symbol _symbol;

        public override void Initialize()
        {
            SetStartDate(2025, 1, 1);
            SetEndDate(2025, 4, 4);
            SetCash(100000);

            SetBrokerageModel(BrokerageName.QuantConnectBrokerage, AccountType.Margin);

            _symbol = AddEquity("XAUUSD", Resolution.Minute).Symbol;

            // Set default order properties
            DefaultOrderProperties.TimeInForce = TimeInForce.Day;
        }

        public override void OnData(Slice data)
        {
            if (Portfolio.Invested)
            {
                return;
            }

            // Place an order with the default order properties
            MarketOrder(_symbol, 1);

            // Place an order with new order properties
            var orderProperties = new OrderProperties
            {
                TimeInForce = TimeInForce.GoodTilCanceled
            };
            var ticket = LimitOrder(_symbol, 1, data[_symbol].Price * 0.9m, orderProperties: orderProperties);

            // Update the order
            var updateFields = new UpdateOrderFields
            {
                Quantity = 2,
                LimitPrice = data[_symbol].Price * 1.05m,
                Tag = "Informative order tag"
            };
            var response = ticket.Update(updateFields);
            if (!LiveMode && response.IsSuccess)
            {
                Debug("Order updated successfully");
            }
        }
    }
}
