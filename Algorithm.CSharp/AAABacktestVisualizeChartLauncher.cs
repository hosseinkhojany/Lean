// using QuantConnect.Data;
// using QuantConnect.Data.Market;
// using QuantConnect.Indicators;
// using QuantConnect.Indicators.CandlestickPatterns;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Accord.IO;
// using Microsoft.FSharp.Core;
// using QuantConnect.Data.UniverseSelection;
// using QuantConnect.Interfaces;
// using QuantConnect.Orders;
// using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;
//
// namespace QuantConnect.Algorithm.CSharp
// {
//     internal class AAABacktestVisualizeChartLauncher : QCAlgorithm, IRegressionAlgorithmDefinition
//     {
//         List<string> Symbols = new ();
//         private string symbolName = "XAUUSD";
//
//         private Engulfing engulfingMinute5;
//         private Doji dojiMinute5;
//         private Engulfing engulfingDaily;
//         private RollingWindow<AAAMinute5> _tradeBars;
//         private int _lookbackPeriod = 3;
//         private decimal _minimumGapSize = 0.001m;
//         
//         
//         private Dictionary<string, string> dojiSeires = new ();
//         private Dictionary<string, string> plotSeriesEngulf = new ();
//         private Dictionary<string, string>  plotSeriesRR = new ();
//         Chart qcChart;
//
//         void GeneratePlotSeriesPerSymbol()
//         {
//             foreach (var symbol in Symbols)
//             {
//                 plotSeriesEngulf[symbol] = ChartLauncherAnnotationType.Engulfing+$"_{symbol}";
//                 dojiSeires[symbol] = ChartLauncherAnnotationType.Doji+$"_{symbol}";
//                 plotSeriesRR[symbol] = ChartLauncherAnnotationType.RR+$"_{symbol}";
//             }
//         }
//         
//         public override void Initialize()
//         {
//             SetStartDate(2025, 01, 01);
//             SetEndDate(2025, 04, 04);
//             SetCash(1000);
//             
//             Symbols.Add(AddData<AAAMinute5>(symbolName).Symbol);
//             Symbols.Add(AddData<AAADaily>(symbolName).Symbol);
//
//             // xauusdSymbolMinute5 = AddCfd("XAUUSD", Resolution.Minute).Symbol;
//             // AddData<AAAMinute5>(xauusdSymbolMinute5);
//
//             // xauusdSymbolDaily = AddCfd("XAUUSD", Resolution.Daily).Symbol;
//             // AddData<AAADaily>(xauusdSymbolDaily);
//
//             _tradeBars = new RollingWindow<AAAMinute5>(_lookbackPeriod);
//             SetWarmUp(_lookbackPeriod);
//             engulfingMinute5 = new Engulfing(Symbols[0]);
//             dojiMinute5 = new Doji(Symbols[0]);
//             engulfingDaily = new Engulfing(Symbols[1]);
//             
//             qcChart = new Chart(symbolName);
//             AddChart(qcChart);
//             GeneratePlotSeriesPerSymbol();
//         }
//
//         public override void OnData(Slice data)
//         {
//             if (data.First().Value is AAAMinute5)
//             {
//                 AAAMinute5 xauusdData = data.Get<AAAMinute5>().First().Value;
//                 var holdings = Portfolio[Symbols[0]];
//                 var quantity = holdings.Quantity;
//                 var invested = holdings.Invested;
//                 var isLong = holdings.IsLong;
//                 var isShort = holdings.IsShort;
//                 _tradeBars.Add(xauusdData);
//                 Plot(symbolName, Symbols[0], xauusdData.ToTradeBar());
//                 if (IsWarmingUp || !_tradeBars.IsReady) return;
//                 EngulfingDetection(xauusdData.Symbol, engulfingMinute5, xauusdData.ToTradeBarWithoutSymbol());
//                 DojiDetection(xauusdData.Symbol, dojiMinute5 , xauusdData.ToTradeBarWithoutSymbol());
//             }
//             else if(data.First().Value is AAADaily)
//             {
//                 AAADaily xauusdDataDaily = data.Get<AAADaily>().First().Value;
//                 var holdings = Portfolio[Symbols[1]];
//                 var quantity = holdings.Quantity;
//                 var invested = holdings.Invested;
//                 var isLong = holdings.IsLong;
//                 var isShort = holdings.IsShort;
//                 Plot(symbolName, Symbols[1], xauusdDataDaily.ToTradeBar());
//                 EngulfingDetection(xauusdDataDaily.Symbol, engulfingDaily, xauusdDataDaily.ToTradeBarWithoutSymbol());
//             }
//         }
//
//
//         private void EngulfingDetection(Symbol symbol, Engulfing engulfing, TradeBar bar)
//         {
//             engulfing.Update(bar);
//             if (engulfing.IsReady && !IsWarmingUp)
//             {
//                 if (engulfing.Current.Value == -1)
//                 {
//                     Log($"Bearish Engulfing: {engulfing.Current.Value}");
//                     Plot(symbolName, plotSeriesEngulf[symbol], bar);
//                     // decimal stopLossPrice = bar.Close * 1.01m;
//                     // decimal takeProfitPrice = bar.Close * 0.99m;
//                     // StopMarketOrder(xauusdSymbol, -1, stopLossPrice);
//                     // LimitOrder(xauusdSymbol, -1, takeProfitPrice);
//                     MarketOrder(symbol, -1);
//                 }
//                 if (engulfing.Current.Value == 1)
//                 {
//                     Log($"Bullish Engulfing: {engulfing.Current.Value}");
//                     Plot(symbolName, plotSeriesEngulf[symbol], bar);
//                     // decimal stopLossPrice = bar.Close * 1.01m;
//                     // decimal takeProfitPrice = bar.Close * 0.99m;
//                     // StopMarketOrder(xauusdSymbol, 1, stopLossPrice);
//                     // LimitOrder(xauusdSymbol, 1, takeProfitPrice);
//                     MarketOrder(symbol, 1);
//                 }
//             }
//             else
//             {
//                 Log("Engulfing indicator is not ready.");
//             }
//
//         }
//         private void DojiDetection(Symbol symbol, Doji pattern, TradeBar bar)
//         {
//             pattern.Update(bar);
//             if (pattern.IsReady && !IsWarmingUp)
//             {
//                 if (pattern.Current.Value == -1)
//                 {
//                     Log($"Doji: {pattern.Current.Value}");
//                     Plot(symbolName, dojiSeires[symbol], bar);
//                 }
//                 if (pattern.Current.Value == 1)
//                 {
//                     Log($"Doji: {pattern.Current.Value}");
//                     Plot(symbolName, dojiSeires[symbol], bar);
//                 }
//             }
//             else
//             {
//                 Log("Doji indicator is not ready.");
//             }
//
//         }
//         
//         public override void OnSecuritiesChanged(SecurityChanges changes)
//         {
//
//         }
//
//         public override void OnOrderEvent(OrderEvent orderEvent)
//         {
//             Log($"Order: {orderEvent}");
//         }
//
//         public override void OnEndOfAlgorithm()
//         {
//             AAAChartLauncher.Launch(qcChart.Series, Symbols, Statistics, false);
//         }
//
//         public bool CanRunLocally { get; } = true;
//         public List<Language> Languages { get; } = [Language.CSharp];
//
//         /// <summary>
//         /// Data Points count of all timeslices of algorithm
//         /// </summary>
//         public virtual long DataPoints => 0;
//
//         /// <summary>
//         /// Data Points count of the algorithm history
//         /// </summary>
//         public virtual int AlgorithmHistoryDataPoints => 0;
//
//         /// <summary>
//         /// Final status of the algorithm
//         /// </summary>
//         public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;
//
//         /// <summary>
//         /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
//         /// </summary>
//         public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
//         {
//             {"Total Orders", "1"},
//             {"Average Win", "0%"},
//             {"Average Loss", "0%"},
//             {"Compounding Annual Return", "30.084%"},
//             {"Drawdown", "5.400%"},
//             {"Expectancy", "0"},
//             {"Start Equity", "100000"},
//             {"End Equity", "104393.19"},
//             {"Net Profit", "4.393%"},
//             {"Sharpe Ratio", "1.543"},
//             {"Sortino Ratio", "2.111"},
//             {"Probabilistic Sharpe Ratio", "58.028%"},
//             {"Loss Rate", "0%"},
//             {"Win Rate", "0%"},
//             {"Profit-Loss Ratio", "0"},
//             {"Alpha", "0.166"},
//             {"Beta", "0.717"},
//             {"Annual Standard Deviation", "0.136"},
//             {"Annual Variance", "0.019"},
//             {"Information Ratio", "1.254"},
//             {"Tracking Error", "0.118"},
//             {"Treynor Ratio", "0.293"},
//             {"Total Fees", "$2.06"},
//             {"Estimated Strategy Capacity", "$160000000.00"},
//             {"Lowest Capacity Asset", "AAPL R735QTJ8XC9X"},
//             {"Portfolio Turnover", "0.83%"},
//             {"OrderListHash", "d38318f2dd0a38f11ef4e4fd704706a7"}
//         };
//     }
// }
