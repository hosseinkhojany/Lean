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
        List<ChartLauncherItem> chartLauncherItem = new ();
        if (!String.IsNullOrEmpty(path))
        {
            if (chart != null)
            {
                foreach (var symbol in symbols)
                {
                    chartLauncherItem.Add(new ChartLauncherItem
                    {
                        Symbol = symbol.ToString(),
                        ChartData = new (),
                        AnnotationData = new (),
                        ExpectedStatistics = new Dictionary<string, string>()
                    });
                }
                foreach (var symbol in symbols)
                {
                    foreach (var series in chart.Series)
                    {
                        if (series.Key.Contains(symbol.ToString()))
                        {
                            foreach (ChartLauncherAnnotationType annotationType in Enum.GetValues(
                                         typeof(ChartLauncherAnnotationType)))
                            {
                                if (series.Key.Contains(annotationType.ToString()))
                                {
                                    foreach (ChartLauncherItem launcherItem in chartLauncherItem)
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
                                        foreach (ChartLauncherItem launcherItem in chartLauncherItem)
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
                    foreach (var data in indicatorHistory[symbol])
                    {
                        if (data.Key == ChartLauncherAnnotationType.MACD)
                        {
                                var macd = data.Value.Select(x => ((dynamic)x).Current).ToList();
                                var fast = data.Value.Select(x => ((dynamic)x).Fast).ToList();
                                var slow = data.Value.Select(x => ((dynamic)x).Slow).ToList();
                                var signal = data.Value.Select(x => ((dynamic)x).Signal).ToList();
                                var histogram = data.Value.Select(x => ((dynamic)x).Histogram).ToList();
                                
                                foreach (ChartLauncherItem launcherItem in chartLauncherItem)
                                {
                                    if (launcherItem.Symbol == symbol)
                                    {
                                        launcherItem.MACDData = new List<MACDData>();
                                        for (int i = 0; i < fast.Count; i++)
                                        {
                                            launcherItem.MACDData.Add(new MACDData(symbol.ToString(), macd[i], signal[i], histogram[i], slow[i], fast[i]));
                                        }
                                    }
                                }
                        }

                    }
                }
            }

            string filePath =
                System.IO.Path.Combine(path.Split("chart_app.exe").First() + "data\\flutter_assets\\assets\\config",
                    "config.json");
            using (var file = new System.IO.StreamWriter(filePath))
            {
                if (!asFile)
                {
                    ChartLaunchResult chartLaunchResult = new ChartLaunchResult(statisticsResults, chartLauncherItem);
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(chartLaunchResult);
                    file.WriteLine(json);
                }
            }

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo { FileName = path };
                    process.Start();
                }
            }
            catch (System.ComponentModel.Win32Exception e)
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
    
    public ChartLaunchResult()
    {
    }
}

class ChartLauncherItem
{
    public string Symbol;
    public string ChartDataFilePath;
    public List<TradeBar> ChartData;
    public Dictionary<ChartLauncherAnnotationType, List<TradeBar>> AnnotationData;
    public Dictionary<string, string> ExpectedStatistics;
    public List<MACDData> MACDData;
    
    public ChartLauncherItem(
        string symbol, 
        string chartDataFilePath, 
        List<TradeBar> chartData, 
        Dictionary<ChartLauncherAnnotationType, List<TradeBar>> annotationData,
        Dictionary<string, string> expectedStatistics,
        List<MACDData> mACDData
        )
    {
        Symbol = symbol;
        ChartDataFilePath = chartDataFilePath;
        ChartData = chartData;
        AnnotationData = annotationData;
        ExpectedStatistics = expectedStatistics;
        MACDData = mACDData;
    }
    
    public ChartLauncherItem()
    {
    }   
}

class MACDData 
{
    public string Symbol;
    public decimal MACD;
    public decimal Signal;
    public decimal Histogram;
    public decimal Slow;
    public decimal Fast;
    
    public MACDData(string symbol, decimal macd, decimal signal, decimal histogram, decimal slow, decimal fast)
    {
        Symbol = symbol;
        MACD = macd;
        Signal = signal;
        Histogram = histogram;
        Slow = slow;
        Fast = fast;
    }
    public MACDData()
    {
    }
}

enum ChartLauncherAnnotationType
{
    Engulfing,
    Doji,
    RR,
    MACD
}

