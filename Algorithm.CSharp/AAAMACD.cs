using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Accord.IO;
using Microsoft.FSharp.Core;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NodaTime;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAAMACD : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        List<Symbol> Symbols = new ();
        private string symbolName = "XAUUSD";
        private Symbol symbol;

        private MovingAverageConvergenceDivergence macd15m;
        private StochasticRelativeStrengthIndex  srsi15m;
        private MovingAverageConvergenceDivergence macd4h;
        private StochasticRelativeStrengthIndex  srsi4h;
        
        private int _lookbackPeriod = 3;
        private decimal _minimumGapSize = 0.001m;
        private readonly RollingWindow<TradeBar> rollingWindowsCandle15m = new RollingWindow<TradeBar>(200);
        
        private decimal? previousHistogram15m = null;
        private decimal? previousK15m = null;
        private decimal? previousD15m = null;
        private decimal? previousHistogram4h = null;
        private decimal? previousK4h = null;
        private decimal? previousD4h = null;
        Chart qcChart;
        private OrderTicket orderBuy;
        private OrderTicket orderSell;
        private TradeBar openPositionBuy15m;
        private TradeBar openPositionSell15m;
        
        private MarketHoursDatabase _marketHoursDatabase;
        private ConcurrentDictionary<Symbol, TimeZoneOffsetProvider> _symbolExchangeTimeZones = new();
        protected virtual ITimeProvider TimeProvider { get; } = RealTimeProvider.Instance;
        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 05, 05);
            SetCash(1000);
            
            Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
            Symbols.Add(AddData<AAAHour4>(symbolName).Symbol);
            // Symbols.Add(AddData<AAADaily>(symbolName).Symbol);

            symbol = AddCfd(symbolName).Symbol;
            // AddData<AAAMinute15>(xauusdSymbolMinute5);

            // xauusdSymbolDaily = AddCfd("XAUUSD", Resolution.Daily).Symbol;
            // AddData<AAADaily>(xauusdSymbolDaily);
            
            srsi15m = new StochasticRelativeStrengthIndex(symbolName,  14, 14, 3, 3);
            srsi4h = new StochasticRelativeStrengthIndex(symbolName,  14, 14, 3, 3);
            
            macd15m = new MovingAverageConvergenceDivergence(symbolName, 12, 26, 9);
            macd15m.Window.Size = 200;
            macd4h = new MovingAverageConvergenceDivergence(symbolName, 12, 26, 9);
            
            // macd15m = new MovingAverageConvergenceDivergence(symbolName, 8, 17, 5);
            // macd4h = new MovingAverageConvergenceDivergence(symbolName, 10, 21, 6);
            
            SetWarmUp(5);
            
            qcChart = new Chart(symbolName);
            AddChart(qcChart);
            Settings.DailyPreciseEndTime = false;
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            Schedule.On(DateRules.EveryDay(), TimeRules.BeforeMarketClose("XAUUSD", 10), LiquidatePortfolio);
        }

        private void LiquidatePortfolio()
        {
            Liquidate();
        }

        public override void OnData(Slice data)
        {
            if (data.First().Value is AAAMinute15 || data.First().Value is AAAHour4)
            {
                TradeBar xauusdData = new TradeBar();
                if (data.First().Value is AAAMinute15 minute)
                {
                    // var offsetProvider = GetTimeZoneOffsetProvider(symbol);
                    // var now = TimeProvider.GetUtcNow();
                    // var exchangeTime = offsetProvider.ConvertFromUtc(now);
                    // minute.Time = exchangeTime;
                    xauusdData = minute.ToTradeBarWithoutSymbol();
                    macd15m.Update(xauusdData);
                    srsi15m.Update(xauusdData);
                    Plot(symbolName, Symbols[0], xauusdData);
                    // Securities[symbol].SetMarketPrice(xauusdData);
                    Securities[symbol].Update(new List<BaseData> { minute.ToTradeBar() }, xauusdData.GetType());
                }
                else if(data.First().Value is AAAHour4 hour)
                {
                    xauusdData = hour.ToTradeBarWithoutSymbol();
                    macd4h.Update(xauusdData);
                    srsi4h.Update(xauusdData);
                    Plot(symbolName, Symbols[1], xauusdData);
                    Securities[symbol].Update(new List<BaseData> { hour.ToTradeBar() }, xauusdData.GetType());
                }

                openPositionBuy15m ??= xauusdData;
                openPositionSell15m ??= xauusdData;


                if (IsWarmingUp) return;
                
                if (macd15m.IsReady && srsi15m.IsReady && macd4h.IsReady && srsi4h.IsReady)
                {
                    decimal currentHistogram15m = macd15m.Histogram.Current.Value;
                    decimal currentK15m = srsi15m.K.Current.Value;
                    decimal currentD15m = srsi15m.D.Current.Value;
                    
                    decimal currentHistogram4h = macd4h.Histogram.Current.Value;
                    decimal currentK4h = srsi4h.K.Current.Value;
                    decimal currentD4h = srsi4h.D.Current.Value;
                    
                    // decimal currentHistogram = macd15m.Current.Value;
                    // decimal currentHistogram = macd15m.Signal.Current.Value;

                    if (previousHistogram15m.HasValue && previousHistogram4h.HasValue)
                    {
                        
                        DateTime targetDate = DateTime.Parse("2022-07-21 11:30:00", DateTimeFormatInfo.CurrentInfo);
                        if (
                            xauusdData.Time.Year == targetDate.Year &&
                            xauusdData.Time.Month == targetDate.Month &&
                            xauusdData.Time.Day == targetDate.Day &&
                            xauusdData.Time.Hour == targetDate.Hour 
                            // && xauusdData.Time.Minute == targetDate.Minute
                        )
                        {
                            Console.WriteLine("");
                        }
                        
                        //---------------------------------------------BUY
                        // var filteredWindowMacd15m = macd15m.Window.Where(data => data.Time > openPositionBuy15m.Time).ToList();
                        // decimal biggestD15mFromOpenPosition = 0;
                        // var filteredWindowD15m = srsi15m.D.Window.Where(data => data.Time > openPositionBuy15m.Time).ToList();
                        // if (filteredWindowD15m.Count > 0)
                        // {
                        //     biggestD15mFromOpenPosition = filteredWindowD15m.Max(data => data.Value);
                        // }
                        //
                        // decimal biggestK15mFromOpenPosition = 0;
                        // var filteredWindowK15m = srsi15m.K.Window.Where(data => data.Time > openPositionBuy15m.Time).ToList();
                        // if (filteredWindowK15m.Count > 0)
                        // {
                        //     biggestK15mFromOpenPosition = filteredWindowK15m.Max(data => data.Value);
                        // }
                        //
                        // if (
                        //     currentHistogram4h > 0 &&
                        //     currentHistogram15m > 0 &&
                        //     currentHistogram15m - previousHistogram15m > 0 &&
                        //     macd15m.Current.Value > macd15m.Signal.Current.Value &&
                        //     currentK15m > 51 && 
                        //     currentD15m > 51 && 
                        //     currentK15m > currentD15m
                        //     )
                        // {
                        //     if (!Portfolio.Invested)
                        //     {
                        //         // printWhenEntry();
                        //         Log($"Attempting to place order for {symbol} with quantity 1. Cash: {Portfolio.Cash}");
                        //         orderBuy = MarketOrder(symbol, 1); // Open a new sell order
                        //         openPositionBuy15m = xauusdData;
                        //         Console.WriteLine("");
                        //     }
                        // }
                        // else if (
                        //     currentHistogram15m < 0 
                        //     || (currentHistogram15m * 1.25m) - previousHistogram15m < 0 
                        //     || macd15m.Current.Value < macd15m.Signal.Current.Value 
                        //     || (currentK15m <= 50 && biggestK15mFromOpenPosition is > 53 and < 80)
                        //     || (currentD15m <= 50 && biggestD15mFromOpenPosition is > 53 and < 80)
                        //     || (currentK15m <= 80 && biggestK15mFromOpenPosition is >= 80 and <= 100)
                        //     || (currentD15m <= 80 && biggestD15mFromOpenPosition is >= 80 and <= 100)
                        //     || currentK15m + 5 < currentD15m
                        //     )
                        // {
                        //     // Liquidate(symbolName); // Exit position   
                        //     if (orderBuy != null)
                        //     {
                        //         Transactions.CancelOrder(orderBuy.OrderId);
                        //     }
                        // }
                        //---------------------------------------------BUY
                        
                        
                        
                        
                        // //---------------------------------------------SELL
                        
                        decimal smallestD15mFromOpenPosition = 0;
                        var filteredWindowD15mSell = srsi15m.D.Window.Where(data => data.Time > openPositionSell15m.Time).ToList();
                        if (filteredWindowD15mSell.Count > 0)
                        {
                            smallestD15mFromOpenPosition = filteredWindowD15mSell.Min(data => data.Value);
                        }
                        
                        decimal smallestK15mFromOpenPosition = 0;
                        var filteredWindowK15mSell = srsi15m.K.Window.Where(data => data.Time > openPositionSell15m.Time).ToList();
                        if (filteredWindowK15mSell.Count > 0)
                        {
                            smallestK15mFromOpenPosition = filteredWindowK15mSell.Min(data => data.Value);
                        }
                        
                        if (
                            currentHistogram4h < 0 &&
                            currentHistogram15m < 0 &&
                            currentHistogram15m - previousHistogram15m < 0 &&
                            macd15m.Current.Value < macd15m.Signal.Current.Value &&
                            currentK15m < 49 &&
                            currentD15m < 49 && 
                            currentK15m < currentD15m
                        )
                        {
                            if (!Portfolio.Invested)
                            {
                                Log($"Attempting to place order for {symbol} with quantity 1. Cash: {Portfolio.Cash}");
                                orderSell = MarketOrder(symbol, -1);
                                openPositionSell15m = xauusdData;
                                Console.WriteLine("");
                            }
                        }
                        else if (
                            currentHistogram15m > 0 ||
                            (currentHistogram15m * 1.25m) - previousHistogram15m > 0 ||
                            macd15m.Current.Value > macd15m.Signal.Current.Value ||
                            (currentK15m >= 50 && smallestK15mFromOpenPosition is < 47 and > 20) ||
                            (currentD15m >= 50 && smallestD15mFromOpenPosition is < 47 and > 20) ||
                            (currentK15m >= 20 && smallestK15mFromOpenPosition is >= 0 and < 20) ||
                            (currentD15m >= 20 && smallestD15mFromOpenPosition is >= 0 and < 20) ||
                            currentK15m - 5 > currentD15m
                        )
                        {
                            // Liquidate(symbolName);
                            if (orderSell != null)
                            {
                                Transactions.CancelOrder(orderSell.OrderId);
                            }
                        }
                        // //---------------------------------------------SELL
                    }

                    previousHistogram15m = currentHistogram15m; // Update previous value
                    previousK15m = currentK15m; // Update previous value
                    previousD15m = currentD15m; // Update previous value
                    previousHistogram4h = currentHistogram4h; // Update previous value
                    previousK4h = currentK4h; // Update previous value
                    previousD4h = currentD4h; // Update previous value
                    
                }

            }
        }

        public void printWhenEntry()
        {
            Console.WriteLine(
                "MACD 15m: " + macd15m.Current.Value +
                ", Signal 15m: " + macd15m.Signal.Current.Value +
                ", Histogram 15m: " + macd15m.Histogram.Current.Value +
                ", Fast 15m: " + macd15m.Fast.Current.Value +
                ", Slow 15m: " + macd15m.Slow.Current.Value
            );
            Console.WriteLine(
                "MACD 4h: " + macd4h.Current.Value +
                ", Signal 4h: " + macd4h.Signal.Current.Value +
                ", Histogram 4h: " + macd4h.Histogram.Current.Value +
                ", Fast 4h: " + macd4h.Fast.Current.Value +
                ", Slow 4h: " + macd4h.Slow.Current.Value
            );
            Console.WriteLine(
                "SRSI 15m: " + srsi15m.Current.Value +
                ", K 15m: " + srsi15m.K.Current.Value +
                ", D 15m: " + srsi15m.D.Current.Value 
            );
            Console.WriteLine(
                "SRSI 4h: " + srsi4h.Current.Value +
                ", K 4h: " + srsi4h.K.Current.Value +
                ", D 4h: " + srsi4h.D.Current.Value 
            );
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {

        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log($"Order: {orderEvent}");
        }

        public override void OnEndOfAlgorithm()
        {
            Dictionary<Symbol, Dictionary<ChartLauncherAnnotationType, IndicatorHistory>> indicatorHistories = new();
            foreach (var symbol in Symbols)
            {
                indicatorHistories[symbol] = new Dictionary<ChartLauncherAnnotationType, IndicatorHistory>();
                var timePeriodIndicatorHistory = IndicatorHistory(macd15m, symbolName, 
                    new DateTime(2025, 1, 1),
                    new DateTime(2025, 4, 4), Resolution.Minute);
            }
            
            AAAChartLauncher.Launch(qcChart, Symbols,indicatorHistories, Statistics,false);
        }

        public bool CanRunLocally { get; } = true;
        public List<Language> Languages { get; } = [Language.CSharp];

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public virtual long DataPoints => 0;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public virtual int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// Final status of the algorithm
        /// </summary>
        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Orders", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "30.084%"},
            {"Drawdown", "5.400%"},
            {"Expectancy", "0"},
            {"Start Equity", "100000"},
            {"End Equity", "104393.19"},
            {"Net Profit", "4.393%"},
            {"Sharpe Ratio", "1.543"},
            {"Sortino Ratio", "2.111"},
            {"Probabilistic Sharpe Ratio", "58.028%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.166"},
            {"Beta", "0.717"},
            {"Annual Standard Deviation", "0.136"},
            {"Annual Variance", "0.019"},
            {"Information Ratio", "1.254"},
            {"Tracking Error", "0.118"},
            {"Treynor Ratio", "0.293"},
            {"Total Fees", "$2.06"},
            {"Estimated Strategy Capacity", "$160000000.00"},
            {"Lowest Capacity Asset", "AAPL R735QTJ8XC9X"},
            {"Portfolio Turnover", "0.83%"},
            {"OrderListHash", "d38318f2dd0a38f11ef4e4fd704706a7"}
        };
        
        private TimeZoneOffsetProvider GetTimeZoneOffsetProvider(Symbol symbol)
        {
            return _symbolExchangeTimeZones.GetOrAdd(symbol, s =>
            {
                var exchangeTimeZone = _marketHoursDatabase.GetExchangeHours(s.ID.Market, s, s.SecurityType).TimeZone;
                return new TimeZoneOffsetProvider(exchangeTimeZone, TimeProvider.GetUtcNow(), QuantConnect.Time.EndOfTime);
            });
        }
    }
}
