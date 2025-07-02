using QuantConnect.Indicators;

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
using System;

public class AAAThreeRSI : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    List<string> Symbols = new();

    Dictionary<string, List<TradeBar>> series = new();
    
    private RelativeStrengthIndex longTermRsi;
    private RelativeStrengthIndex midTermRsi;
    private RelativeStrengthIndex shortTermRsi;
        
    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAHour2>(symbolName).Symbol);
        Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
        Symbols.Add(AddData<AAAMinute3>(symbolName).Symbol);

        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(TimeSpan.FromHours(15));
        Settings.DailyPreciseEndTime = false;
        
        longTermRsi = new RelativeStrengthIndex(symbol, 12, MovingAverageType.Simple);
        midTermRsi = new RelativeStrengthIndex(symbol, 28, MovingAverageType.Simple);
        shortTermRsi = new RelativeStrengthIndex(symbol, 68, MovingAverageType.Simple);

        foreach (var t in Symbols)
        {
            series[t] = new List<TradeBar>();
        }

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {

    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAHour4 aaaHour4)
        {
            TradeBar currentBar = aaaHour4.ToTradeBarWithoutSymbol();
            series[Symbols[0]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { aaaHour4.ToTradeBar() }, currentBar.GetType());
            longTermRsi.Update(currentBar);
            if (IsWarmingUp) return;

        }
        else if (slice.First().Value is AAAMinute30 AAAMinute30)
        {
            TradeBar currentBar = AAAMinute30.ToTradeBarWithoutSymbol();
            series[Symbols[1]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { AAAMinute30.ToTradeBar() }, currentBar.GetType());
            midTermRsi.Update(currentBar);
            if (IsWarmingUp) return;

        }
        else if (slice.First().Value is AAAMinute5 AAAMinute5)
        {
            TradeBar currentBar = AAAMinute5.ToTradeBarWithoutSymbol();
            series[Symbols[0]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { AAAMinute5.ToTradeBar() }, currentBar.GetType());
            shortTermRsi.Update(currentBar);
            if (IsWarmingUp) return;

        }
        else if (slice.First().Value is AAAMinute3 AAAMinute3)
        {
            TradeBar currentBar = AAAMinute3.ToTradeBarWithoutSymbol();
            series[Symbols[0]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { AAAMinute3.ToTradeBar() }, currentBar.GetType());
            shortTermRsi.Update(currentBar);
            if (IsWarmingUp) return;

        }

        if (!longTermRsi.IsReady || !midTermRsi.IsReady || !shortTermRsi.IsReady) return;

        if (longTermRsi > midTermRsi && longTermRsi > shortTermRsi && midTermRsi > shortTermRsi)
        {
            SetHoldings(symbol, 1.0);
            Log("Enter bye: " + Time);
        }
        else if (longTermRsi < midTermRsi && longTermRsi < shortTermRsi && midTermRsi < shortTermRsi)
        {
            SetHoldings(symbol, -1.0);
            Log("Enter sell: " + Time);
        }
        else
        {
            if (longTermRsi < midTermRsi || midTermRsi < shortTermRsi)
            {
                Liquidate(symbol);
                Log("Liquate: " + Time);
            }
            else if (longTermRsi > midTermRsi || midTermRsi > shortTermRsi)
            {
                Liquidate(symbol);
                Log("Liquate: " + Time);
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

