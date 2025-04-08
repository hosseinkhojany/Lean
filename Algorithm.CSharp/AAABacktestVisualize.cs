using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Generic;
using System.Linq;
using Plotly.NET;
using Plotly.NET.LayoutObjects;
using Microsoft.FSharp.Core;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAABacktestVisualize : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol xauusdSymbol;
        private int _lookbackPeriod = 3;
        private decimal _minimumGapSize = 0.001m;
        private Engulfing engulfing;

        private RollingWindow<TradeBar> _tradeBars;
        private string chartName = "XAUUSD Chart";
        Chart qcChart;
        
        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 04, 04);
            SetCash(100000);

            xauusdSymbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            AddData<AAACustomData>(xauusdSymbol, Resolution.Minute);

            _tradeBars = new RollingWindow<TradeBar>(_lookbackPeriod);
            SetWarmUp(_lookbackPeriod);
            
            engulfing = CandlestickPatterns.Engulfing(xauusdSymbol);
            
            qcChart = new Chart(chartName, xauusdSymbol);
            qcChart.AddSeries(new CandlestickSeries(xauusdSymbol.Value));
            AddChart(qcChart);
        }

        public override void OnData(Slice data)
        {
            if (data.Bars.Count > 0)
            {
                TradeBar tradeBar = data.Bars.First().Value;
                _tradeBars.Add(tradeBar);
                Plot(chartName, xauusdSymbol.Value, tradeBar);
                if (IsWarmingUp || !_tradeBars.IsReady) return;
                EngulfingDetection(tradeBar);
            }
        }


        private void EngulfingDetection(TradeBar customData)
        {
            engulfing.Update(customData);
            if (engulfing.IsReady)
            {
                if (engulfing.Current.Value == -1)
                {
                    Log($"Bearish Engulfing: {engulfing.Current.Value}");
                    Plot(chartName, "Bearish Engulfing", customData);
                }
                if (engulfing.Current.Value == 1)
                {
                    Log($"Bullish Engulfing: {engulfing.Current.Value}");
                    Plot(chartName, "Bullish Engulfing", customData);
                }
            }
            else
            {
                Log("Engulfing indicator is not ready.");
            }

        }
        
        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
        }

        public override void OnEndOfAlgorithm()
        {
            
            GenericChart chart;
            
            List<Candlestick> bars = qcChart.Series[xauusdSymbol.Value].Values.OfType<Candlestick>().ToList();
            chart = Chart2D.Chart.Candlestick<decimal, decimal, decimal, decimal, DateTime, string>(
                bars.Select(x => x.Open ?? 0),
                bars.Select(x => x.High ?? 0),
                bars.Select(x => x.Low ?? 0),
                bars.Select(x => x.Close ?? 0),
                bars.Select(x => x.Time),
                ShowXAxisRangeSlider: new FSharpOption<bool>(false)
                );
            
            
            LinearAxis xAxis = new LinearAxis();
            xAxis.SetValue("title", "Time");
            xAxis.SetValue("resizable", true);
            
            LinearAxis yAxis = new LinearAxis();
            yAxis.SetValue("title", "Price");
            yAxis.SetValue("resizable", true);
            if (bars.Count > 100)
            {
                xAxis.SetValue("range", new object[] { bars[^50].Time, bars[^1].Time });
                yAxis.SetValue("range", new object[] { bars[^100].Low, bars[^1].High });
            }
            else
            {
                yAxis.SetValue("range", new object[] { bars[^(bars.Count - 1)].Low, bars[^1].High });
                xAxis.SetValue("range", new object[] { bars[^50].Time, bars[^1].Time });
            }

            chart.WithTemplate(ChartTemplates.plotly);
            chart.WithSize(1000, 800);
            chart.WithTitle($"{xauusdSymbol} OHLC");
            
            chart.WithXAxis(xAxis);
            chart.WithYAxis(yAxis);
            
            foreach (var point in qcChart.Series["Bullish Engulfing"].Values)
            {
                if (point is Candlestick candlestick)
                {
                    chart.WithAnnotation(Annotation.init<DateTime, decimal, int, int, int, int, double, int, int, int>(
                            X: new FSharpOption<DateTime>(candlestick.Time),
                            Y: new FSharpOption<decimal>(candlestick.Close ?? 0),
                            Text: new FSharpOption<string>("Bullish Engulfing"),
                            BGColor: new FSharpOption<Color>(Color.fromString("white"))
                        )
                    );
                }
            }
            
            HTML.CreateChartHTML(GenericChart.toChartHTML(chart), GenericChartExtensions.GetLayout(chart).ToString(), null, PlotlyJSReference.Full);
            chart.Show();
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public List<Language> Languages { get; } = new() { Language.CSharp };

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
        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Initializing;

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
