using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using System;
using System.Collections.Generic;
using System.Linq;
using Accord.IO;
using Plotly.NET;
using Plotly.NET.LayoutObjects;
using Plotly.NET.ImageExport;
using Microsoft.FSharp.Core;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using GenericChartExtensions = Plotly.NET.CSharp.GenericChartExtensions;

namespace QuantConnect.Algorithm.CSharp
{
    internal class AAABacktestVisualize : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol xauusdSymbol;
        private int _lookbackPeriod = 3;
        private decimal _minimumGapSize = 0.001m;
        private Engulfing engulfing;

        private RollingWindow<AAAMinute5> _tradeBars;
        private string chartName = "XAUUSD Chart";
        private string plotSeriesBearish = "Bearish Engulfing";
        private string plotSeriesBullish = "Bullish Engulfing";
        private string plotSeriesRR = "Risk/Reward";
        Chart qcChart;
        
        public override void Initialize()
        {
            SetStartDate(2025, 01, 01);
            SetEndDate(2025, 04, 04);
            SetCash(100000);

            xauusdSymbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            AddData<AAAMinute5>(xauusdSymbol);

            _tradeBars = new RollingWindow<AAAMinute5>(_lookbackPeriod);
            SetWarmUp(_lookbackPeriod);
            
            engulfing = CandlestickPatterns.Engulfing(xauusdSymbol);
            
            qcChart = new Chart(chartName, xauusdSymbol);
            AddChart(qcChart);
        }

        public override void OnData(Slice data)
        {
            AAAMinute5 xauusdData = data.Get<AAAMinute5>().First().Value;
            if (xauusdData.Price > 0)
            {
                var holdings = Portfolio[xauusdSymbol];
                var quantity = holdings.Quantity;
                var invested = holdings.Invested;
                var isLong = holdings.IsLong;
                var isShort = holdings.IsShort;
                _tradeBars.Add(xauusdData);
                Plot(chartName, xauusdSymbol.Value, xauusdData.ToTradeBar());
                if (IsWarmingUp || !_tradeBars.IsReady) return;
                EngulfingDetection(xauusdData);
            }
        }


        private void EngulfingDetection(AAAMinute5 bar)
        {
            engulfing.Update(bar.ToTradeBar());
            if (engulfing.IsReady && !IsWarmingUp)
            {
                if (engulfing.Current.Value == -1)
                {
                    SetHoldings(xauusdSymbol, -1);
                    Log($"Bearish Engulfing: {engulfing.Current.Value}");
                    Plot(chartName, plotSeriesBearish, bar.ToTradeBar());
                    // decimal stopLossPrice = bar.Close * 1.01m;
                    // decimal takeProfitPrice = bar.Close * 0.99m;
                    // StopMarketOrder(xauusdSymbol, -1, stopLossPrice);
                    // LimitOrder(xauusdSymbol, -1, takeProfitPrice);
                    MarketOrder(xauusdSymbol, -1);
                }
                if (engulfing.Current.Value == 1)
                {
                    SetHoldings(xauusdSymbol, -1);
                    Log($"Bullish Engulfing: {engulfing.Current.Value}");
                    Plot(chartName, plotSeriesBullish, bar.ToTradeBar());
                    // decimal stopLossPrice = bar.Close * 1.01m;
                    // decimal takeProfitPrice = bar.Close * 0.99m;
                    // StopMarketOrder(xauusdSymbol, 1, stopLossPrice);
                    // LimitOrder(xauusdSymbol, 1, takeProfitPrice);
                    MarketOrder(xauusdSymbol, 1);
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

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log($"Order: {orderEvent}");
        }

        public override void OnEndOfAlgorithm()
        {
            List<Candlestick> bars = qcChart.Series[xauusdSymbol.Value].Values.OfType<Candlestick>().ToList().Take(51).ToList();
            if (bars.Count == 0)
            {
                Log("No data to plot.");
                return;
            }
            GenericChart chart = Chart2D.Chart.Candlestick<decimal, decimal, decimal, decimal, DateTime, string>(
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
            chart.WithTitle(xauusdSymbol.Value);
            
            chart.WithXAxis(xAxis);
            chart.WithYAxis(yAxis);
            
            List<Shape> shapes = new List<Shape>();
            foreach (var point in qcChart.Series[plotSeriesBullish].Values.Take(10))
            {
                if (point is Candlestick candlestick)
                {
                    chart.WithAnnotation(Annotation.init<DateTime, decimal, int, int, int, int, double, int, int, int>(
                            X: new FSharpOption<DateTime>(candlestick.Time),
                            Y: new FSharpOption<decimal>(candlestick.Close ?? 0),
                            Text: new FSharpOption<string>(plotSeriesBullish),
                            BGColor: new FSharpOption<Color>(Color.fromString("white"))
                        )
                    );


                    // Helper function to convert DateTime to milliseconds since epoch
                    double DateTimeToMilliseconds(DateTime dateTime)
                    {
                        return (dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                    }

                    // var tpShape = Shape.init(
                    //     ShapeType: new FSharpOption<StyleParam.ShapeType>(StyleParam.ShapeType.Rectangle),
                    //     X0: new FSharpOption<double>(DateTimeToMilliseconds(candlestick.Time)),
                    //     X1: new FSharpOption<double>(DateTimeToMilliseconds(candlestick.Time.AddHours(1))),
                    //     Y0: new FSharpOption<decimal>(candlestick.Close ?? 0),
                    //     Y1: new FSharpOption<decimal>((candlestick.Close ?? 0) + 20),
                    //     FillColor: new FSharpOption<Color>(Color.fromString("rgba(0, 255, 0, 0.2)")),
                    //     Layer: new FSharpOption<StyleParam.Layer>(StyleParam.Layer.Below),
                    //     Line: Line.init(Width: new FSharpOption<double>(1))
                    // );
                    // shapes.Add(tpShape);

                }
            }
            chart.WithShapes(shapes);
            // chart.SaveJPG("C:\\Users\\PSG\\Desktop\\chart.jpg", EngineType: new FSharpOption<ExportEngine>(ExportEngine.PuppeteerSharp), 800, 100);
            HTML.CreateChartHTML(GenericChart.toChartHTML(chart), GenericChartExtensions.GetLayout(chart).ToString(), null, PlotlyJSReference.Full);
            chart.Show();
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
