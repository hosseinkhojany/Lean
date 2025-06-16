using QuantConnect.Data.Market;
using QuantConnect.Data;
using System;
using System.Globalization;

namespace QuantConnect.Algorithm.CSharp
{
        public class AAAHour3 : DynamicData
        {
            public decimal Open { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public decimal Close { get; set; }
            public decimal Volume { get; set; }

            public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
            {
                string filePath = $"..\\..\\..\\Data\\custom\\{config.Symbol.Value}_Hour3.csv";
                return new SubscriptionDataSource(filePath, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
            }

            public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Time"))
                {
                    return null;
                }

                var csv = line.Split(',');
                if (csv.Length != 6)
                {
                    return null;
                }

                var data = new AAAHour3()
                {
                    Time = DateTime.ParseExact(csv[0], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                    Open = Parse.Decimal(csv[1]),
                    High = Parse.Decimal(csv[2]),
                    Low = Parse.Decimal(csv[3]),
                    Close = Parse.Decimal(csv[4]),
                    Volume = Parse.Decimal(csv[5]),
                    Value = Parse.Decimal(csv[4]),
                    Symbol = config.Symbol
                };

                return data;
            }
            
            public TradeBar ToTradeBar()
            {
                return new TradeBar
                {
                    Time = this.Time,
                    Open = this.Open,
                    High = this.High,
                    Low = this.Low,
                    Close = this.Close,
                    Volume = this.Volume,
                    Value = this.Value,
                    Symbol = this.Symbol
                };
            }
            public TradeBar ToTradeBarWithoutSymbol()
            {
                return new TradeBar
                {
                    Time = this.Time,
                    Open = this.Open,
                    High = this.High,
                    Low = this.Low,
                    Close = this.Close,
                    Volume = this.Volume,
                    Value = this.Value,
                };
            }
        }
}
