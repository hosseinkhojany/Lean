using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Generic;
using System.Linq;
using Accord.IO;
using Microsoft.FSharp.Core;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;

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
        
        private decimal? previousHistogram15m = null;
        private decimal? previousK15m = null;
        private decimal? previousD15m = null;
        private decimal? previousHistogram4h = null;
        private decimal? previousK4h = null;
        private decimal? previousD4h = null;
        Chart qcChart;
        private OrderTicket orderBuy;
        private OrderTicket orderSell;
        
        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 04, 04);
            SetCash(10000);
            
            Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
            Symbols.Add(AddData<AAAHour4>(symbolName).Symbol);
            // Symbols.Add(AddData<AAADaily>(symbolName).Symbol);

            symbol = AddCfd(symbolName).Symbol;
            // AddData<AAAMinute15>(xauusdSymbolMinute5);

            // xauusdSymbolDaily = AddCfd("XAUUSD", Resolution.Daily).Symbol;
            // AddData<AAADaily>(xauusdSymbolDaily);
            macd15m = new MovingAverageConvergenceDivergence(symbolName, 12, 26, 9);
            srsi15m = new StochasticRelativeStrengthIndex(symbolName,  14, 14, 3, 3);
            macd4h = new MovingAverageConvergenceDivergence(symbolName, 12, 26, 9);
            srsi4h = new StochasticRelativeStrengthIndex(symbolName,  14, 14, 3, 3);

            SetWarmUp(15);
            
            qcChart = new Chart(symbolName);
            AddChart(qcChart);
            Settings.DailyPreciseEndTime = false;
        }

        public override void OnData(Slice data)
        {
            if (data.First().Value is AAAMinute15 || data.First().Value is AAAHour4)
            {
                TradeBar xauusdData;
                if (data.First().Value is AAAMinute15 minute)
                {
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

        
                if (IsWarmingUp) return;
                
                if (macd15m.IsReady && srsi15m.IsReady && macd4h.IsReady && srsi4h.IsReady)
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
                        // Bullish crossover: Histogram crosses from negative to positive
                        // if (currentHistogram > 0 && previousHistogram <= 0)
                        // {
                        //     if (!Portfolio.Invested)
                        //     {
                        //         MarketOrder(symbolName, 1); // Enter long position
                        //     }
                        // } 
                        // // Bearish crossover: Histogram crosses from positive to negative
                        // else if (currentHistogram < 0 && previousHistogram >= 0)
                        // {
                        //     if (Portfolio.Invested)
                        //     {
                        //         Liquidate(symbolName); // Exit position
                        //     }
                        // }

                        //---------------------------------------------BUY
                        if (
                            currentHistogram4h > 0 &&
                            currentHistogram15m > 0 &&
                            currentHistogram15m - previousHistogram15m > 0 &&
                            macd15m.Current.Value > macd15m.Signal.Current.Value &&
                            currentK15m > 80 &&
                            currentD15m > 80 && 
                            currentK15m > currentD15m
                            )
                        {
                            if (!Portfolio.Invested && Securities[symbol].Price > 0)
                            {
                                Log($"Attempting to place order for {symbol} with quantity 1. Cash: {Portfolio.Cash}");
                                orderBuy = MarketOrder(symbol, 1); // Open a new sell order
                                Console.WriteLine("");
                            }
                        }
                        else if (
                            currentHistogram15m < 0 &&
                           (currentHistogram15m * 1.2m) - previousHistogram15m < 0 &&
                            macd15m.Current.Value < macd15m.Signal.Current.Value &
                            currentK15m < 80 &&
                            currentD15m < 80 &&
                            currentK15m + 5 < currentD15m
                            )
                        {
                            Liquidate(symbolName); // Exit position   
                        }
                        //---------------------------------------------BUY
                        
                        
                        // //---------------------------------------------SELL
                        // if (
                        //     currentHistogram < 0 &&
                        //     currentHistogram - previousHistogram < 0 &&
                        //     macd15m.Current.Value < macd15m.Signal.Current.Value &&
                        //     currentK < 20 &&
                        //     currentD < 20
                        //     )
                        // {
                        //     orderBuy = MarketOrder(symbolName, -1); // Open a new buy order
                        // }
                        // else if (
                        //     currentHistogram > 0 ||
                        //          currentHistogram - previousHistogram > 0 ||
                        //          macd15m.Current.Value > macd15m.Signal.Current.Value || 
                        //          currentK > 20 ||
                        //          currentD > 20)
                        // {
                        //     Liquidate(symbolName); 
                        // }
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
            
                indicatorHistories[symbol][ChartLauncherAnnotationType.MACD] = timePeriodIndicatorHistory;
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
    }
}
