using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp;

public class AAASP2L : QCAlgorithm, IRegressionAlgorithmDefinition
{

    /*
     Strategy Description:
        1. detect FVG (Fair Value Gap) in the last 3 bars the 3 last bar should be the same color (all green or all red)
        2. if the gap between the first bar and the current bar is greater than the body of the current bar, then it is a valid FVG
        3. if the FVG is valid, then open two StopLimit orders (1. at the low of the current bar with 1% risk, 2. in the Midle of FVG price with 2% risk) 
        4. set stoploss at the first candle low and we have 3 take profits: 
            - TP1 at the entry price + stoploss to entry price
            - TP2 at the entry price + (stoploss to entry price * 2)
            - TP3 at the entry price + (stoploss to entry price * 3)
     */


    private int _lookbackPeriod = 3;
    private decimal _minimumGapSize = 0.001m;

    private RollingWindow<TradeBar> _tradeBars;
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    List<string> Symbols = new();
    Dictionary<string, List<TradeBar>> series = new();
    private decimal tp1, tp2, tp3;
    private TradeBar fvgBar;

    public override void Initialize()
    {
        SetStartDate(2025, 05, 20);
        SetEndDate(2025, 06, 10);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(15);

        Settings.DailyPreciseEndTime = false;
        _tradeBars = new RollingWindow<TradeBar>(_lookbackPeriod);
        for (int i = 0; i < Symbols.Count; i++)
        {
            series[Symbols[i]] = new List<TradeBar>();
        }

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {

    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAMinute minute)
        {
            TradeBar customData = minute.ToTradeBarWithoutSymbol();
            Securities[symbol].Update(new List<BaseData> { minute.ToTradeBar() }, customData.GetType());
            _tradeBars.Add(customData);
            series[Symbols[0]].Add(customData);
            if (IsWarmingUp || !_tradeBars.IsReady) return;
            FVG();
        }
    }
    
    private void FVG()
    {
        var barFirst = _tradeBars[2];
        var barFVG = _tradeBars[1];
        var barCurrent = _tradeBars[0];

        bool isAllGreen = barCurrent.Close > barCurrent.Open && barFVG.Close > barFVG.Open && barFirst.Close > barFirst.Open;
        bool isAllRed = barCurrent.Close < barCurrent.Open && barFVG.Close < barFVG.Open && barFirst.Close < barFirst.Open;
        decimal gap = 0;
        gap = isAllGreen ? Math.Abs(barCurrent.Low - barFirst.High) : Math.Abs(barCurrent.High - barFirst.Low);

        decimal fvgMidPrice = Math.Abs(barFVG.Close + barFVG.Open) / 2;
        decimal bodyCurrentBar = Math.Abs(barCurrent.Close - barCurrent.Open);
        decimal bodyFirstBar = Math.Abs(barFirst.Close - barFirst.Open);

        if ((double)gap > ((double)bodyCurrentBar*1.5) && (isAllGreen || isAllRed))
        {
            // Log((isAllGreen ? "Green " : "Red ")+"FVG Detect at " + barFVG.Time + " Balance: "+Portfolio.Cash);

            decimal stoplossDistance = Math.Abs(barFirst.Low - barCurrent.Low);

            tp1 = isAllGreen ? Securities[symbol].Close + stoplossDistance : Securities[symbol].Close - stoplossDistance;
            tp2 = isAllGreen ? Securities[symbol].Close + (stoplossDistance * 2) : Securities[symbol].Close - (stoplossDistance * 2);
            tp3 = isAllGreen ? Securities[symbol].Close + (stoplossDistance * 3) : Securities[symbol].Close - (stoplossDistance * 3);

            fvgBar = barCurrent;

            if (isAllGreen)
            {
                //stoploss
                StopMarketOrder(symbol, -3, barFirst.Low);
                //entry
                LimitOrder(symbol, 2, fvgMidPrice);
                LimitOrder(symbol, 1, barCurrent.Low);
                //exit
                LimitOrder(symbol, -2, tp1);
                //LimitOrder(symbol, -1, tp2);
                TrailingStopOrder(
                    symbol: symbol,
                    quantity: -1,
                    trailingAmount: 1,
                    trailingAsPercentage: false);
            }
            if (isAllRed)
            {
                //stoploss
                StopMarketOrder(symbol, 3, barFirst.Low);
                //entry
                LimitOrder(symbol, -2, fvgMidPrice);
                LimitOrder(symbol, -1, barCurrent.Low);
                //exit
                LimitOrder(symbol, 2, tp1);
                //LimitOrder(symbol, 1, tp2);
                TrailingStopOrder(
                    symbol: symbol,
                    quantity: 1,
                    trailingAmount: 1,
                    trailingAsPercentage: false);
            }

            Log(" Balance: " + Portfolio.Cash + " Current Bar: "+ barCurrent.Time);

        }
    }
    
    
    public override void OnSecuritiesChanged(SecurityChanges changes)
    {

    }

    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        //if (orderEvent.Status == OrderStatus.Invalid && orderEvent.Message.Contains("Insufficient buying power"))
        //{
        //    Quit("Critical Margin Issue - Force Quitting Algorithm.");
        //}
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
