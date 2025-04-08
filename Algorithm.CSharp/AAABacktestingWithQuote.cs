using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators.CandlestickPatterns;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAABacktestingWithQuote : QCAlgorithm
    {
        private Symbol xauusdSymbol;
        private Engulfing engulfing;

        public override void Initialize()
        {
            SetStartDate(2014, 05, 01);
            SetEndDate(2025, 05, 05);
            SetCash(100000);
            xauusdSymbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            engulfing = CandlestickPatterns.Engulfing(xauusdSymbol);
        }

        public override void OnData(Slice data)
        {
            if (data.QuoteBars.Count > 0)
            {
                QuoteBar customData = data.QuoteBars.First().Value;
                if (engulfing.IsReady)
                {
                    if (engulfing.Current.Value == -1)
                    {
                        Log($"Bearish Engulfing: {engulfing.Current.Value}");
                    }
                    if (engulfing.Current.Value == 1)
                    {
                        Log($"Bullish Engulfing: {engulfing.Current.Value}");
                    }
                }
                Log($"Time: {customData.Time}, Open: {customData.Open}, High: {customData.High}, Low: {customData.Low}, Close: {customData.Close}, Price: {customData.Price}");
            }
        }
    }
}
