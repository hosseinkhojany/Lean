using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    public class TrendAlertDeepSeek : QCAlgorithm
    {
        private Symbol _symbol;
        // Heikin-Ashi state trackers
        private HeikinAshi _lastHaLt;
        private HeikinAshi _lastHaMt;
        // Indicators
        private ExponentialMovingAverage _ema20Mt;
        private RollingWindow<decimal> _ema20Window;
        // Resolution settings
        private TimeSpan _ltResolution = TimeSpan.FromDays(1);
        private TimeSpan _mtResolution = TimeSpan.FromMinutes(240);
        // Consolidators
        private TradeBarConsolidator _ltConsolidator;
        private TradeBarConsolidator _mtConsolidator;
        // Current trend value
        private int _trend;

        public override void Initialize()
        {
            SetStartDate(2020, 1, 1);
            SetCash(100000);
            _symbol = AddEquity("SPY", Resolution.Minute).Symbol;

            // Initialize EMA20 and rolling window
            _ema20Mt = new ExponentialMovingAverage(20);
            _ema20Window = new RollingWindow<decimal>(2);

            // LT (Daily) consolidator
            _ltConsolidator = new TradeBarConsolidator(_ltResolution);
            _ltConsolidator.DataConsolidated += (sender, bar) => 
            {
                _lastHaLt = HeikinAshi.Calculate(bar, _lastHaLt);
                ComputeTrend();
            };
            SubscriptionManager.AddConsolidator(_symbol, _ltConsolidator);

            // MT (4-hour) consolidator
            _mtConsolidator = new TradeBarConsolidator(_mtResolution);
            _mtConsolidator.DataConsolidated += (sender, bar) => 
            {
                _lastHaMt = HeikinAshi.Calculate(bar, _lastHaMt);
                _ema20Mt.Update(bar.EndTime, _lastHaMt.Close);
                if (_ema20Mt.IsReady)
                {
                    _ema20Window.Add(_ema20Mt);
                }
                ComputeTrend();
            };
            SubscriptionManager.AddConsolidator(_symbol, _mtConsolidator);
        }

        private void ComputeTrend()
        {
            // Ensure all data is available
            if (_lastHaLt == null || _lastHaMt == null || !_ema20Window.IsReady)
            {
                _trend = 0;
                return;
            }

            // LT Direction (Step 1)
            bool ltLong = _lastHaLt.Close > _lastHaLt.Open;
            bool ltShort = _lastHaLt.Close < _lastHaLt.Open;

            // MT Conditions (Step 2)
            decimal currentEma = _ema20Window[0];
            decimal previousEma = _ema20Window[1];
            decimal emaDelta = currentEma - previousEma;

            bool mtLong = 
                _lastHaMt.Close > _lastHaMt.Open && 
                _lastHaMt.Close > currentEma && 
                emaDelta > 0;
                
            bool mtShort = 
                _lastHaMt.Close < _lastHaMt.Open && 
                _lastHaMt.Close < currentEma && 
                emaDelta < 0;

            // Determine final trend
            _trend = (mtLong && ltLong) ? 1 : 
                     (mtShort && ltShort) ? -1 : 0;

            // Plot the trend (optional)
            Plot("Trend", "Value", _trend);
        }

        // Heikin-Ashi helper class
        public class HeikinAshi
        {
            public decimal Open { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public decimal Close { get; set; }

            public static HeikinAshi Calculate(TradeBar bar, HeikinAshi previous)
            {
                if (previous == null)
                {
                    // First bar calculation
                    return new HeikinAshi
                    {
                        Open = (bar.Open + bar.Close) / 2,
                        Close = (bar.Open + bar.High + bar.Low + bar.Close) / 4,
                        High = bar.High,
                        Low = bar.Low
                    };
                }

                // Subsequent bars
                decimal haClose = (bar.Open + bar.High + bar.Low + bar.Close) / 4;
                decimal haOpen = (previous.Open + previous.Close) / 2;
                decimal haHigh = Math.Max(bar.High, Math.Max(haOpen, haClose));
                decimal haLow = Math.Min(bar.Low, Math.Min(haOpen, haClose));

                return new HeikinAshi
                {
                    Open = haOpen,
                    High = haHigh,
                    Low = haLow,
                    Close = haClose
                };
            }
        }

        // Optional: Implement trading logic based on _trend
        public override void OnData(Slice data)
        {
            // Example trading logic (customize as needed)
            if (_trend == 1 && !Portfolio.Invested)
            {
                SetHoldings(_symbol, 1.0);
            }
            else if (_trend == -1 && Portfolio.Invested)
            {
                Liquidate(_symbol);
            }
        }
    }
}
