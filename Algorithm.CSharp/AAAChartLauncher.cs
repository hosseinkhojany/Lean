using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Statistics;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp;


using System;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Configuration;

class AAAChartLauncher
{
    public static void Launch(Chart chart, List<Symbol> symbols,
        Dictionary<Symbol, Dictionary<ChartLauncherAnnotationType, IndicatorHistory>> indicatorHistory,
        StatisticsResults statisticsResults, bool asFile)
    {
        string path = Config.Get("chart-launcher-path");
        List<ChartLauncherItem> chartLauncherItem = new();

        if (!string.IsNullOrEmpty(path))
        {
            if (chart != null)
            {
                foreach (var symbol in symbols)
                {
                    chartLauncherItem.Add(new ChartLauncherItem
                    {
                        Symbol = symbol.ToString(),
                        ChartData = new(),
                        AnnotationData = new(),
                        ExpectedStatistics = new Dictionary<string, string>()
                    });
                }

                foreach (var symbol in symbols)
                {
                    foreach (var series in chart.Series)
                    {
                        if (series.Key.Contains(symbol.ToString()))
                        {
                            foreach (ChartLauncherAnnotationType annotationType in Enum.GetValues(typeof(ChartLauncherAnnotationType)))
                            {
                                if (series.Key.Contains(annotationType.ToString()))
                                {
                                    foreach (var launcherItem in chartLauncherItem)
                                    {
                                        if (!launcherItem.AnnotationData.ContainsKey(annotationType))
                                        {
                                            launcherItem.AnnotationData[annotationType] = new List<TradeBar>();
                                        }

                                        foreach (var data in series.Value.Values)
                                        {
                                            if (data is Candlestick tradeBar)
                                            {
                                                if (launcherItem.Symbol == symbol)
                                                {
                                                    launcherItem.AnnotationData[annotationType].Add(new TradeBar
                                                    {
                                                        Time = tradeBar.Time,
                                                        Open = tradeBar.Open ?? 0,
                                                        High = tradeBar.High ?? 0,
                                                        Low = tradeBar.Low ?? 0,
                                                        Close = tradeBar.Close ?? 0,
                                                        Symbol = symbol
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (!asFile)
                            {
                                foreach (var data in series.Value.Values)
                                {
                                    if (data is Candlestick tradeBar)
                                    {
                                        foreach (var launcherItem in chartLauncherItem)
                                        {
                                            if (launcherItem.Symbol == symbol)
                                            {
                                                launcherItem.ChartData.Add(new TradeBar
                                                {
                                                    Time = tradeBar.Time,
                                                    Open = tradeBar.Open ?? 0,
                                                    High = tradeBar.High ?? 0,
                                                    Low = tradeBar.Low ?? 0,
                                                    Close = tradeBar.Close ?? 0,
                                                    Symbol = symbol
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string filePath = path.EndsWith(".exe")
                ? System.IO.Path.Combine(path.Split("chart_app.exe").First() + "data\\flutter_assets\\assets\\config", "config.json")
                : System.IO.Path.Combine(path.Split("index.html").First() + "data", "config.json");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            using (var file = new System.IO.StreamWriter(filePath))
            {
                if (!asFile)
                {
                    var chartLaunchResult = new ChartLaunchResult(statisticsResults, chartLauncherItem);
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(chartLaunchResult);
                    file.WriteLine(json);
                }
            }

            try
            {
                if (path.EndsWith(".exe"))
                {
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo { FileName = path };
                        process.Start();
                    }
                }
                else if (path.EndsWith(".html"))
                {
                    using (var serverProcess = new Process())
                    {
                        serverProcess.StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/C python -m http.server",
                            WorkingDirectory = path.Split("index.html").First(),
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        serverProcess.Start();
                    }

                    using (var browserProcess = new Process())
                    {
                        browserProcess.StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/C start http://localhost:8000/index.html",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        browserProcess.Start();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}

class ChartLaunchResult
{
    public StatisticsResults StatisticsResults;
    public List<ChartLauncherItem> ChartLauncherItems;

    public ChartLaunchResult(StatisticsResults statisticsResults, List<ChartLauncherItem> chartLauncherItems)
    {
        StatisticsResults = statisticsResults;
        ChartLauncherItems = chartLauncherItems;
    }

    public ChartLaunchResult() { }
}

class ChartLauncherItem
{
    public string Symbol;
    public string ChartDataFilePath;
    public List<TradeBar> ChartData;
    public Dictionary<ChartLauncherAnnotationType, List<TradeBar>> AnnotationData;
    public Dictionary<string, string> ExpectedStatistics;

    public ChartLauncherItem(string symbol, string chartDataFilePath, List<TradeBar> chartData,
        Dictionary<ChartLauncherAnnotationType, List<TradeBar>> annotationData, Dictionary<string, string> expectedStatistics)
    {
        Symbol = symbol;
        ChartDataFilePath = chartDataFilePath;
        ChartData = chartData;
        AnnotationData = annotationData;
        ExpectedStatistics = expectedStatistics;
    }

    public ChartLauncherItem() { }
}


enum ChartLauncherAnnotationType
{
    Engulfing,
    Doji,
    RR
}

