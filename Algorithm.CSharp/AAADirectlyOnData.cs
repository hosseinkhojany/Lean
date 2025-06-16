

using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Securities;
using System;
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAADirectlyOnData : QCAlgorithm
    {
        public override void Initialize()
        {
            SetStartDate(2014, 5, 2);
            SetEndDate(2014, 5, 14);
            SetCash(10000);
            // Market.Add(Market.Alpari, 999);
            // MarketHoursDatabase.SetEntry(Market.Alpari, "BITCOIN", SecurityType.Cfd, SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc), DateTimeZone.Utc);
            // MarketHoursDatabase.AlwaysOpen.SetEntryAlwaysOpen(Market.Alpari, "BITCOIN", SecurityType.Cfd, DateTimeZone.Utc);
            // AddCfd("BITCOIN", Resolution.Tick, Market.Alpari);
            // SetBrokerageModel(new CustomBrokerageModel());
            base.OnData(new Slice(DateTime.Now, [new Tick(DateTime.Now, "BITCOIN", 35, 25)], DateTime.UtcNow));
        }

        public override void OnData(Slice slice)
        {
            Debug("OnData: ");
        }
        
        

        class CustomBrokerageModel : DefaultBrokerageModel
        {
            private static readonly IReadOnlyDictionary<SecurityType, string> _defaultMarketMap = new Dictionary<SecurityType, string>
            {
                {SecurityType.Cfd, Market.Oanda }
            }.ToReadOnlyDictionary();

            public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets => _defaultMarketMap;

            public override bool CanSubmitOrder(Security security, Order order, out BrokerageMessageEvent message)
            {
                if (security.Symbol.Value == "AIG")
                {
                    message = new BrokerageMessageEvent(BrokerageMessageType.Information, "", "Symbol AIG can not be submitted");
                    return false;
                }

                message = null;
                return true;
            }

            public override bool CanUpdateOrder(Security security, Order order, UpdateOrderRequest request, out BrokerageMessageEvent message)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Information, "", "This order can not be updated");
                return false;
            }
        }
        
        public void OnDataConsolidated1(object sender, QuoteBar quoteBar)
        {
            Log("OnDataConsolidated called");
            Log(quoteBar.ToString());
        }

    }
    
}
    
