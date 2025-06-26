
using NodaTime;

namespace QuantConnect.Algorithm.CSharp;

using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Indicators;
using System;
using NodaTime.TimeZones;

public class AAAIchimokoPeak : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;

    private IchimokuKinkoHyo _ichimoku;
    List<string> Symbols = new();

    Dictionary<string, List<TradeBar>> series = new();
    private RollingWindow<TradeBar> rollingWindows = new RollingWindow<TradeBar>(100);


    decimal previousLead1 = 0;
    decimal previousLead2 = 0;

    private decimal peakFactor = 1.0002m;
    private decimal breakoutDownFactor = 1;
    private decimal breakoutUPFactor = 1;
    private decimal pullbackDownFactor = 0.99996m;
    private decimal pullbackUPFactor = 1.00004m;
    private decimal pullbackDistanceUPFactor = 1.0050m;
    private decimal pullbackDistanceDownFactor = 0.9950m;
    
    decimal box = 0;
    decimal stoploss = 0;
    decimal takeProfit = 0;

    TradeBar currentBar;
    TradeBar peakCandle;
    TradeBar breakoutCandle;
    bool breakout = false;
    bool falseBreakout = false;
    bool pullback = false;
    int pullbackCounter = 0;
    List<decimal> threePastSpansA = new List<decimal>();
    List<decimal> threePastSpansB = new List<decimal>();

    private OrderTicket marketOrder;
    private OrderTicket stoplossOrder;
    private OrderTicket takeprofitOrder;
    

    OrderDirection orderDirection;


    public override void Initialize()
    {
        SetStartDate(2025, 06, 05);
        SetEndDate(2025, 06, 10);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute5>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(100);
        Settings.DailyPreciseEndTime = false;

        _ichimoku = new IchimokuKinkoHyo(9, 26, 26, 52, 1, 1);

        for (int i = 0; i < Symbols.Count; i++)
        {
            series[Symbols[i]] = new List<TradeBar>();
        }

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
        SetTimeZone("UTC");
    }

    private void OnMarketClose()
    {
        
    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAMinute5 daily)
        {
            currentBar = daily.ToTradeBarWithoutSymbol();

            series[Symbols[0]].Add(currentBar);
            rollingWindows.Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());

            _ichimoku.Update(currentBar);

            if (IsWarmingUp) return;

            if (_ichimoku.IsReady)
            {
                // Console.WriteLine($"Time: {currentBar.Time}, Lead 1: {_ichimoku.SenkouA}, Lead 2: {_ichimoku.SenkouB
                // }, Tenkan: {_ichimoku.Tenkan}, Kijun: {_ichimoku.Kijun}, Chikou: {_ichimoku.Chikou}, TenkanMax: {
                //     _ichimoku.TenkanMaximum}, TenkanMin: {_ichimoku.TenkanMinimum}, KijunMax: {_ichimoku.KijunMaximum
                //     }, KijunMin: {_ichimoku.KijunMinimum}, SenkouBMax: {_ichimoku.SenkouBMaximum}, SenkouBMin: {
                //         _ichimoku.SenkouBMinimum}, DelayedTenkanSenkouA: {_ichimoku.DelayedTenkanSenkouA
                //         }, DelayedKijunSenkouA: {_ichimoku.DelayedKijunSenkouA}, DelayedMaxSenkouB: {
                //             _ichimoku.DelayedMaximumSenkouB}, DelayedMinSenkouB: {_ichimoku.DelayedMinimumSenkouB}");
                decimal lead1 = _ichimoku.SenkouA;
                decimal lead2 = _ichimoku.SenkouB;
                decimal laggingSpanB = _ichimoku.Chikou;
                decimal baseLine = _ichimoku.Kijun;
                decimal conversionLine = _ichimoku.Tenkan;

                if (lead1 > 0)
                {
                    threePastSpansA.Add(lead1);
                }

                if (lead2 > 0)
                {
                    threePastSpansB.Add(lead2);
                }

                if (threePastSpansA.Count < 4) return;


                decimal firstA = threePastSpansA[threePastSpansA.Count - 3];
                decimal secondA = threePastSpansA[threePastSpansA.Count - 2];
                decimal thirdA = threePastSpansA[threePastSpansA.Count - 1];
                
                if ((secondA > thirdA * peakFactor && secondA > firstA * peakFactor) ||
                    (secondA * peakFactor < thirdA && secondA * peakFactor < firstA))
                {
                    peakCandle = currentBar;
                    box = currentBar.High - currentBar.Low;
                    pullbackCounter = 0;
                    orderDirection = OrderDirection.Buy;
                    Log(" \n\n\nLead1 (Green) has peak " + currentBar.Time+ "\n\n\n");
                }

                decimal firstB = threePastSpansB[threePastSpansB.Count - 3];
                decimal secondB = threePastSpansB[threePastSpansB.Count - 2];
                decimal thirdB = threePastSpansB[threePastSpansB.Count - 1];
                
                if ((secondB > thirdB * peakFactor && secondB > firstB * peakFactor) ||
                    (secondB * peakFactor < thirdB && secondB * peakFactor < firstB))
                {
                    peakCandle = currentBar;
                    box = currentBar.High - currentBar.Low;
                    pullbackCounter = 0;
                    orderDirection = OrderDirection.Buy;
                    Log(" \n\n\nLead2 (RED) has peak " + currentBar.Time+ "\n\n\n");
                }

                if (peakCandle != null)
                {

                    //buy
                    if (currentBar.Low > peakCandle.High)
                    {
                        //breackout from top
                        if (currentBar.Open < currentBar.Close)
                        {
                            if (currentBar.Open * breakoutDownFactor >= peakCandle.High)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        else
                        {
                            if (currentBar.Close * breakoutDownFactor >= peakCandle.High)
                            {
                                breakoutCandle = currentBar;
                            }
                        }

                        if (currentBar.Low * pullbackDownFactor <= peakCandle.High)
                        {

                            //buy
                            if (breakoutCandle != null && pullbackCounter <= 12)
                            {
                                if (IsPriceOfRangeBiggerThanX(peakCandle, currentBar, true))
                                {
                                    box = peakCandle.High - peakCandle.Low;
                                    takeProfit = Math.Abs(box + peakCandle.High);
                                    stoploss = Math.Abs(box - peakCandle.Low);
                                    MarketOrder(symbol, 1);
                                    StopMarketOrder(symbol, -1, stoploss);
                                    LimitOrder(symbol, -1, takeProfit);
                                    // client.SendOrderAsync(
                                    //     new SendOrderRq(
                                    //         OrderTypeRequest.CREATE,
                                    //         new MqlTradeRequest
                                    //         {
                                    //             Symbol = SymbolMapper.leanSymbolToMt(symbolName, MtMarketType.ORBEX, MTType.MT4),
                                    //             Volume = 0.01,
                                    //             Price = (double)Securities[symbol].Price,
                                    //             Sl = (double)stoploss,
                                    //             Tp = (double)takeProfit,
                                    //         }
                                    //     )
                                    // );
                                    Console.WriteLine(
                                        "\n\n{Open BUY " + "\n"
                                        + "peakCandle: " + peakCandle.Time + "\n"
                                        + "BreakOut: " + breakoutCandle.Time + "\n"
                                        + "PullBack:" + currentBar.Time + "\n"
                                        + "STOPLOSS: " + stoploss + "\n"
                                        + "TAKEPROFIT: " + takeProfit + "\n"
                                        + "BOXSize: " + box + "}\n\n"
                                    );
                                    peakCandle = null;
                                    breakoutCandle = null;
                                }
                            }
                        }
                    }
                    //sell
                    else if(currentBar.High < peakCandle.Low)
                    {
                        //pullback from bottom
                        if (currentBar.Open < currentBar.Close)
                        {
                            if (currentBar.Close * breakoutUPFactor <= peakCandle.Low)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        else
                        {
                            if (currentBar.Open * breakoutUPFactor <= peakCandle.Low)
                            {
                                breakoutCandle = currentBar;
                            }
                        }

                        if (currentBar.High * pullbackUPFactor >= peakCandle.Low)
                        {
                            //sell
                            if (breakoutCandle != null && pullbackCounter <= 12)
                            {
                                if (IsPriceOfRangeBiggerThanX(peakCandle, currentBar, false))
                                {
                                    box = peakCandle.High - peakCandle.Low;
                                    takeProfit = Math.Abs(box + peakCandle.Low);
                                    stoploss = Math.Abs(box + peakCandle.High);
                                    
                                    marketOrder = MarketOrder(symbol, -1);
                                    stoplossOrder = StopMarketOrder(symbol, 1, stoploss);
                                    takeprofitOrder = LimitOrder(symbol, 1, takeProfit);
                                    // client.SendOrderAsync(
                                    //     new SendOrderRq(
                                    //         OrderTypeRequest.CREATE,
                                    //         new MqlTradeRequest
                                    //         {
                                    //             Symbol = SymbolMapper.leanSymbolToMt(symbolName, MtMarketType.ORBEX, MTType.MT4),
                                    //             Volume = 0.01,
                                    //             Price = (double)Securities[symbol].Price,
                                    //             Sl = (double)stoploss,
                                    //             Tp = (double)takeProfit,
                                    //         }
                                    //     )
                                    // );
                                    Console.WriteLine(
                                        "\n\n{Open SELL " + "\n"
                                        + "peakCandle: " + peakCandle.Time + "\n"
                                        + "BreakOut: " + breakoutCandle.Time + "\n"
                                        + "PullBack:" + currentBar.Time + "\n"
                                        + "STOPLOSS: " + stoploss + "\n"
                                        + "TAKEPROFIT: " + takeProfit + "\n"
                                        + "BOXSize: " + box + "}\n\n"
                                    );
                                    peakCandle = null;
                                    breakoutCandle = null;
                                }
                            }
                        }
                    }




                }



                previousLead1 = lead1;
                previousLead2 = lead2;
                pullbackCounter += 1;
            }
        }
    }

    public bool IsPriceOfRangeBiggerThanX(TradeBar peakcandle, TradeBar pullbackCandle, bool isBuy)
    {
        int peakPosition = 0;
        int pullbackPosition = 0;
        for (var i = 0; i < rollingWindows.Count; i++)
        {
            if (rollingWindows[i].Time == peakcandle.Time)
            {
                peakPosition = i;
;            }
            if (rollingWindows[i].Time == pullbackCandle.Time)
            {
                pullbackPosition = i;
;            }
        }

        decimal highestHigh = 0;
        decimal lowestLow = 0;
        List<TradeBar> tradeBars = rollingWindows.Take(new Range(pullbackPosition, peakPosition)).ToList();
        foreach (var tradeBar in tradeBars)
        {
            if (tradeBar.High > highestHigh || highestHigh == 0)
            {
                highestHigh = tradeBar.High;
            }
            if (tradeBar.Low < lowestLow || lowestLow == 0)
            {
                lowestLow = tradeBar.Low;
            }
            
        }

        if (isBuy)
        {
            //top
            return highestHigh < peakcandle.High * pullbackDistanceUPFactor;
        }
        else
        {
            //down
             return lowestLow > peakcandle.Low * pullbackDistanceDownFactor;   
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
        AAAChartLauncher.Launch(series, Symbols, Statistics, false);
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
        { "Total Orders", "1" },
        { "Average Win", "0%" },
        { "Average Loss", "0%" },
        { "Compounding Annual Return", "30.084%" },
        { "Drawdown", "5.400%" },
        { "Expectancy", "0" },
        { "Start Equity", "100000" },
        { "End Equity", "104393.19" },
        { "Net Profit", "4.393%" },
        { "Sharpe Ratio", "1.543" },
        { "Sortino Ratio", "2.111" },
        { "Probabilistic Sharpe Ratio", "58.028%" },
        { "Loss Rate", "0%" },
        { "Win Rate", "0%" },
        { "Profit-Loss Ratio", "0" },
        { "Alpha", "0.166" },
        { "Beta", "0.717" },
        { "Annual Standard Deviation", "0.136" },
        { "Annual Variance", "0.019" },
        { "Information Ratio", "1.254" },
        { "Tracking Error", "0.118" },
        { "Treynor Ratio", "0.293" },
        { "Total Fees", "$2.06" },
        { "Estimated Strategy Capacity", "$160000000.00" },
        { "Lowest Capacity Asset", "AAPL R735QTJ8XC9X" },
        { "Portfolio Turnover", "0.83%" },
        { "OrderListHash", "d38318f2dd0a38f11ef4e4fd704706a7" }
    };
}
