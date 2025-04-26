using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAABacktestingWithTradeBar : QCAlgorithm
    {
        private Symbol xauusdSymbol;
        private Engulfing engulfing;

        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 04, 04);
            SetCash(100000);
            xauusdSymbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            AddData<AAAMinute5>(xauusdSymbol);
            engulfing = CandlestickPatterns.Engulfing(xauusdSymbol);
        }

        public override void OnData(Slice data)
        {
            if (data.Bars.Count > 0)
            {
                TradeBar customData = data.Bars.First().Value;
                engulfing.Update(customData);
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
                else
                {
                    Log("Engulfing indicator is not ready.");
                }

                Log($"Time: {customData.Time}, Open: {customData.Open}, High: {customData.High}, Low: {customData.Low}, Close: {customData.Close}, Volume: {customData.Volume}");
            }
        }
    }
}
