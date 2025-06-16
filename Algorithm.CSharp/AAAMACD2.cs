using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Generic;
using System.Linq;
using Accord.IO;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAAMACD2 : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        List<string> Symbols = new();
        private string symbolName = "XAUUSD";

        private MovingAverageConvergenceDivergence macd;
        private StochasticRelativeStrengthIndex  srsi;
        private RollingWindow<AAAMinute30> _tradeBars;
        private int _lookbackPeriod = 3;
        private decimal _minimumGapSize = 0.001m;
        
        private decimal? previousHistogram = null;
        private decimal? previousK = null;
        private decimal? previousD = null;
        Chart qcChart;
        private OrderTicket orderBuy;
        private OrderTicket orderSell;
        
        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 04, 04);
            SetCash(10000);
            
            Symbols.Add(AddData<AAAMinute30>(symbolName).Symbol);
            // Symbols.Add(AddData<AAADaily>(symbolName).Symbol);

            // xauusdSymbolMinute5 = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            // AddData<AAAMinute30>(xauusdSymbolMinute5);

            // xauusdSymbolDaily = AddCfd("XAUUSD", Resolution.Daily).Symbol;
            // AddData<AAADaily>(xauusdSymbolDaily);
            macd = new MovingAverageConvergenceDivergence(symbolName, 12, 26, 9, MovingAverageType.Exponential);
            srsi = new StochasticRelativeStrengthIndex(symbolName,  14, 14, 3, 3);

            _tradeBars = new RollingWindow<AAAMinute30>(_lookbackPeriod);
            SetWarmUp(_lookbackPeriod);
            
            qcChart = new Chart(symbolName);
            AddChart(qcChart);
        }

        public override void OnData(Slice data)
        {
            if (data.First().Value is AAAMinute30)
            {
                AAAMinute30 xauusdData = data.Get<AAAMinute30>().First().Value;
                _tradeBars.Add(xauusdData);
                macd.Update(xauusdData.ToTradeBarWithoutSymbol());
                srsi.Update(xauusdData.ToTradeBarWithoutSymbol());
                Plot(symbolName, Symbols[0], xauusdData.ToTradeBar());
        
                if (IsWarmingUp) return;
                if (!macd.IsReady) return;
                
                if (macd.IsReady && srsi.IsReady)
                {
                    
                    Console.WriteLine(
                        "MACD: " + macd.Current.Value +
                        ", Signal: " + macd.Signal.Current.Value +
                        ", Histogram: " + macd.Histogram.Current.Value +
                        ", Fast: " + macd.Fast.Current.Value +
                        ", Slow: " + macd.Slow.Current.Value
                    );
                    Console.WriteLine(
                        "SRSI: " + srsi.Current.Value +
                        ", K: " + srsi.K.Current.Value +
                        ", D: " + srsi.D.Current.Value 
                    );
                    

                    decimal currentHistogram = macd.Histogram.Current.Value;
                    decimal currentK = srsi.K.Current.Value;
                    decimal currentD = srsi.D.Current.Value;
                    // decimal currentHistogram = macd.Current.Value;
                    // decimal currentHistogram = macd.Signal.Current.Value;

                    if (previousHistogram.HasValue)
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
                            currentHistogram > 0 &&
                            currentHistogram - previousHistogram > 0 &&
                            macd.Current.Value > macd.Signal.Current.Value &&
                            currentK > 80 &&
                            currentD > 80 && 
                            currentK > currentD
                            )
                        {
                            if (!Portfolio.Invested && Securities[symbol: symbolName].Price > 0)
                            {
                                orderSell = MarketOrder(symbolName, 1); // Open a new sell order
                            }
                        }
                        else if (
                            currentHistogram < 0 &&
                            currentHistogram - previousHistogram < 0 &&
                            macd.Current.Value < macd.Signal.Current.Value &
                            currentK < 80 &&
                            currentD < 80 &&
                            currentK + 5 < currentD
                            )
                        {
                            Liquidate(symbolName); // Exit position   
                        }
                        //---------------------------------------------BUY
                        
                        
                        // //---------------------------------------------SELL
                        // if (
                        //     currentHistogram < 0 &&
                        //     currentHistogram - previousHistogram < 0 &&
                        //     macd.Current.Value < macd.Signal.Current.Value &&
                        //     currentK < 20 &&
                        //     currentD < 20
                        //     )
                        // {
                        //     orderBuy = MarketOrder(symbolName, -1); // Open a new buy order
                        // }
                        // else if (
                        //     currentHistogram > 0 ||
                        //          currentHistogram - previousHistogram > 0 ||
                        //          macd.Current.Value > macd.Signal.Current.Value || 
                        //          currentK > 20 ||
                        //          currentD > 20)
                        // {
                        //     Liquidate(symbolName); 
                        // }
                        // //---------------------------------------------SELL

                    }

                    previousHistogram = currentHistogram; // Update previous value
                    previousK = currentK; // Update previous value
                    previousD = currentD; // Update previous value
                    
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
                var timePeriodIndicatorHistory = IndicatorHistory(macd, symbolName, 
                    new DateTime(2025, 1, 1),
                    new DateTime(2025, 4, 4), Resolution.Minute);
            }
            AAAChartLauncher.Launch(qcChart.Series, Symbols, Statistics,false);
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
