using System.Collections.Generic;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect;
using QuantConnect.Data;

public class PivotPointBreakoutAlgorithm : QCAlgorithm
{
    private string symbolName = "XAUUSD";
    private Symbol _symbol;
    private PivotPointsHighLow _pivotHigh;
    private PivotPointsHighLow _pivotLow;
    List<Symbol> Symbols = new ();

    private int _leftBars = 5;
    private int _rightBars = 5;

    private decimal _stopLossPercent = 0.015m;
    private decimal _takeProfitPercent = 0.03m;

    private decimal _entryPrice;
    private bool _longPosition, _shortPosition;

    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 05, 05);
        SetCash(10000);

        _symbol = AddCfd(symbolName).Symbol;

        _pivotHigh = new PivotPointsHighLow(_leftBars, _rightBars);
        _pivotLow = new PivotPointsHighLow(_leftBars, _rightBars);

        RegisterIndicator(_symbol, _pivotHigh, Resolution.Daily);
        RegisterIndicator(_symbol, _pivotLow, Resolution.Daily);
    }

    public override void OnData(Slice data)
    {
        if (!data.Bars.ContainsKey(_symbol)) return;

        var price = data[_symbol].Close;

        if (!_pivotHigh.IsReady || !_pivotLow.IsReady)
            return;

        var pivotHigh = _pivotHigh.Current.Value;
        var pivotLow = _pivotLow.Current.Value;

        if (!Portfolio.Invested)
        {
            if (price > pivotHigh)
            {
                var quantity = CalculateOrderQuantity(_symbol, 0.9);
                MarketOrder(_symbol, quantity);
                _entryPrice = price;
                _longPosition = true;
                Debug($"LONG ENTRY at {price}");
            }
            else if (price < pivotLow)
            {
                var quantity = CalculateOrderQuantity(_symbol, -0.9);
                MarketOrder(_symbol, quantity);
                _entryPrice = price;
                _shortPosition = true;
                Debug($"SHORT ENTRY at {price}");
            }
        }
        else
        {
            if (_longPosition)
            {
                if (price <= _entryPrice * (1 - _stopLossPercent) ||
                    price >= _entryPrice * (1 + _takeProfitPercent))
                {
                    Liquidate(_symbol);
                    _longPosition = false;
                    Debug($"LONG EXIT at {price}");
                }
            }
            else if (_shortPosition)
            {
                if (price >= _entryPrice * (1 + _stopLossPercent) ||
                    price <= _entryPrice * (1 - _takeProfitPercent))
                {
                    Liquidate(_symbol);
                    _shortPosition = false;
                    Debug($"SHORT EXIT at {price}");
                }
            }
        }
    }
}
