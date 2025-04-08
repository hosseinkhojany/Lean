

using QuantConnect.Brokerages;
using QuantConnect.Data;

using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;
using QuantConnect.Securities;
using System;
using System.Collections.Generic;
using NodaTime;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAAFreeToEdit : QCAlgorithm
    {
        private string symbolName = "BTCUSD";
        // private string symbolName = "XAUUSD";
        private SecurityType securityType = SecurityType.Cfd;
        
        public override void Initialize()
        {
            // SetStartDate(2014, 5, 2);
            // SetEndDate(2014, 5, 14);
            // SetCash(10000);
            
            Settings.DailyPreciseEndTime = false;
            // Market.Add(market, 999);
            // SymbolPropertiesDatabase.SetEntry(market, symbolName, securityType, SymbolProperties.GetDefault("USD"));
            // MarketHoursDatabase.SetEntry(market, symbolName, securityType, SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc), DateTimeZone.Utc);
            // MarketHoursDatabase.AlwaysOpen.SetEntryAlwaysOpen(market, symbolName, securityType, DateTimeZone.Utc);
            // SetBrokerageModel(new CustomBrokerageModel());
            
            // AddCrypto(symbolName, Resolution.Tick);
            
            // AddSecurity(symbolName, Resolution.Tick);
            AddCrypto(symbolName, Resolution.Tick);
            // AddCfd(symbolName, Resolution.Tick);

            
            // AddSecurity(
            //     QuantConnect.Symbol.Create(symbolName,  securityType, market),
            //     Resolution.Tick
            // );
            // base.OnData(new Slice(DateTime.Now, [new Tick(DateTime.Now, symbolName, 35, 25)], DateTime.UtcNow));
        }

        public override void OnData(Slice slice)
        {
            Debug("OnData:");


            if (slice.HasData)
            {
                decimal price = Securities[symbolName].Price;
                Debug("Price: " + price);

                decimal lastTradeProfit = Portfolio[symbolName].LastTradeProfit;
                Debug("LastTradeProfit: " + lastTradeProfit);
            }

        }
        
        

        
    }
    
}
    
