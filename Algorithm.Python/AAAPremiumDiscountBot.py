from AlgorithmImports import *
from datetime import time
import pytz

class AAAPremiumDiscountStrategy(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2023, 1, 1)  # Set the start date for backtesting
        self.SetEndDate(2024, 1, 1)    # Set the end date for backtesting
        self.SetCash(100000)           # Set the initial cash amount

        # Add EUR/USD forex pair with minute resolution
        self.forex_minute = self.AddForex("EURUSD", Resolution.Minute)
        self.forex_minute.SetDataNormalizationMode(DataNormalizationMode.Raw)  # Use raw prices

        # Define the symbols for different resolutions
        self.hourly = self.forex_minute.Symbol
        self.m15 = self.forex_minute.Symbol
        self.m5 = self.forex_minute.Symbol

        # Initialize dictionaries to store premium/discount and MSS information
        self.pd_array = {}
        self.mss = {}

        # Add RSI indicators for different resolutions
        self.rsi_hourly = self.RSI(self.hourly, 14, MovingAverageType.Wilders, Resolution.Hour)

        # Create consolidators for 15-minute and 5-minute resolutions
        self.consolidator_m15 = QuoteBarConsolidator(timedelta(minutes=15))
        self.consolidator_m15.DataConsolidated += self.OnDataConsolidatedM15
        self.SubscriptionManager.AddConsolidator(self.m15, self.consolidator_m15)
        self.rsi_m15 = self.RSI(self.m15, 14, MovingAverageType.Wilders)
        self.RegisterIndicator(self.m15, self.rsi_m15, self.consolidator_m15)

        self.consolidator_m5 = QuoteBarConsolidator(timedelta(minutes=5))
        self.consolidator_m5.DataConsolidated += self.OnDataConsolidatedM5
        self.SubscriptionManager.AddConsolidator(self.m5, self.consolidator_m5)
        self.rsi_m5 = self.RSI(self.m5, 14, MovingAverageType.Wilders)
        self.RegisterIndicator(self.m5, self.rsi_m5, self.consolidator_m5)

    def check_premium_discount(self, history):
        high = history['high'].max()
        low = history['low'].min()
        current_price = history.iloc[-1]['close']

        premium_level = high - (high - low) * 0.25
        discount_level = low + (high - low) * 0.25

        if current_price >= premium_level:
            return 'premium'
        elif current_price <= discount_level:
            return 'discount'
        else:
            return None

    def check_mss(self, history):
        recent_high = history['high'][-5:].max()
        recent_low = history['low'][-5:].min()
        previous_high = history['high'][-10:-5].max()
        previous_low = history['low'][-10:-5].min()

        if recent_high > previous_high and recent_low > previous_low:
            return 'bullish'
        elif recent_high < previous_high and recent_low < previous_low:
            return 'bearish'
        else:
            return None

    def OnData(self, data):
        history = self.History(self.hourly, 100, Resolution.Hour)
        if history.empty:
            self.Debug("No historical hourly data available.")
            return

        # Update premium/discount and MSS arrays for the hourly timeframe
        self.pd_array[self.hourly] = self.check_premium_discount(history)
        self.mss[self.hourly] = self.check_mss(history)

        # Align higher timeframes with the 1-hour timeframe
        for timeframe in [self.hourly]:
            try:
                history_htf = self.History(timeframe, 100, Resolution.Daily)
            except Exception as e:
                self.Debug(f"Error fetching {timeframe} data: {str(e)}")
                continue

            if history_htf.empty:
                self.Debug(f"No historical data available for {timeframe} timeframe.")
                continue

            self.pd_array[timeframe] = self.check_premium_discount(history_htf)
            self.mss[timeframe] = self.check_mss(history_htf)

        # Execute trades based on the conditions
        self.ExecuteTrade(data)

    def OnDataConsolidatedM15(self, sender, bar):
        self.rsi_m15.Update(IndicatorDataPoint(bar.EndTime, bar.Close))

    def OnDataConsolidatedM5(self, sender, bar):
        self.rsi_m5.Update(IndicatorDataPoint(bar.EndTime, bar.Close))

    def ExecuteTrade(self, data):
        # Get the current UTC time and convert it to New York time
        utc_time = self.UtcTime
        ny_time = utc_time.astimezone(pytz.timezone("America/New_York")).time()

        # Check if the current time is within the specified trading windows
        if not ((ny_time >= time(7, 0) and ny_time <= time(11, 0)) or (ny_time >= time(2, 0) and ny_time <= time(5, 0))):
            self.Debug(f"Current time {ny_time} is outside the trading windows. No trades will be executed.")
            return

        # Check RSI levels
        if not self.rsi_hourly.IsReady:
            self.Debug("RSI not ready.")
            return

        rsi_hourly = self.rsi_hourly.Current.Value

        # Check if the portfolio is already invested
        self.Debug(f"Current Portfolio Position: {self.Portfolio.Invested}")

        if self.Portfolio.Invested:
            self.Debug("Portfolio is already invested. Checking position adjustment.")
            # Check conditions for position adjustment
            if self.pd_array.get(self.hourly) == 'discount' and self.mss.get(self.hourly) == 'bullish' and rsi_hourly < 40:
                self.Debug(f"Conditions met for buying: Premium/Discount={self.pd_array.get(self.hourly)}, MSS={self.mss.get(self.hourly)}, RSI={rsi_hourly}")
                entry_confirmation = self.ConfirmEntry(data)
                if entry_confirmation == 'bullish':
                    self.AdjustPosition(data[self.hourly].Price, is_long=True)
            elif self.pd_array.get(self.hourly) == 'premium' and self.mss.get(self.hourly) == 'bearish' and rsi_hourly > 60:
                self.Debug(f"Conditions met for selling: Premium/Discount={self.pd_array.get(self.hourly)}, MSS={self.mss.get(self.hourly)}, RSI={rsi_hourly}")
                entry_confirmation = self.ConfirmEntry(data)
                if entry_confirmation == 'bearish':
                    self.AdjustPosition(data[self.hourly].Price, is_long=False)
        else:
            self.Debug("Portfolio is not invested. Checking for new trades.")
            # Check conditions for new trades
            if self.pd_array.get(self.hourly) == 'discount' and self.mss.get(self.hourly) == 'bullish' and rsi_hourly < 40:
                self.Debug(f"Conditions met for buying: Premium/Discount={self.pd_array.get(self.hourly)}, MSS={self.mss.get(self.hourly)}, RSI={rsi_hourly}")
                entry_confirmation = self.ConfirmEntry(data)
                if entry_confirmation == 'bullish':
                    self.AdjustPosition(data[self.hourly].Price, is_long=True)
            elif self.pd_array.get(self.hourly) == 'premium' and self.mss.get(self.hourly) == 'bearish' and rsi_hourly > 60:
                self.Debug(f"Conditions met for selling: Premium/Discount={self.pd_array.get(self.hourly)}, MSS={self.mss.get(self.hourly)}, RSI={rsi_hourly}")
                entry_confirmation = self.ConfirmEntry(data)
                if entry_confirmation == 'bearish':
                    self.AdjustPosition(data[self.hourly].Price, is_long=False)

    def AdjustPosition(self, current_price, is_long):
        risk_per_trade = 0.01  # Risk 1% of the portfolio
        risk_to_reward_ratio = 2  # 1:2 Risk to Reward ratio

        # Calculate the amount to risk and the stop loss / take profit levels
        portfolio_value = self.Portfolio.TotalPortfolioValue
        risk_amount = portfolio_value * risk_per_trade
        stop_loss = 0.0
        take_profit = 0.0

        # Fetch historical data for calculating stop loss and take profit levels
        history = self.History(self.hourly, 100, Resolution.Hour)
        if history.empty:
            self.Debug("No historical hourly data available for position adjustment.")
            return

        if is_long:
            stop_loss = history['low'].min()  # Use the minimum low as stop loss for long trades
            take_profit = current_price + (current_price - stop_loss) * risk_to_reward_ratio
        else:
            stop_loss = history['high'].max()  # Use the maximum high as stop loss for short trades
            take_profit = current_price - (stop_loss - current_price) * risk_to_reward_ratio

        # Calculate the stop loss distance
        stop_loss_distance = abs(current_price - stop_loss)

        # Avoid division by zero or very small distances
        if stop_loss_distance <= 0:
            self.Debug("Stop loss distance is zero or negative. Cannot calculate position size.")
            return

        # Calculate position size based on the risk amount and stop loss distance
        position_size = risk_amount / stop_loss_distance

        # Ensure position size is positive and does not exceed cash available
        if position_size <= 0:
            self.Debug("Calculated position size is non-positive. Aborting trade.")
            return

        # Check if the position size exceeds available buying power
        available_cash = self.Portfolio.Cash
        if position_size * current_price > available_cash:
            self.Debug(f"Position size {position_size} exceeds available cash {available_cash}. Adjusting position size.")
            position_size = available_cash / current_price

        # Check if we need to close any existing position
        if self.Portfolio.Invested:
            current_position = self.Portfolio[self.forex_minute.Symbol]
            if current_position:
                if (is_long and current_position.Quantity > 0) or (not is_long and current_position.Quantity < 0):
                    self.Debug(f"Existing position already matches the intended trade direction. No adjustment needed.")
                    return
                else:
                    self.Debug(f"Closing existing position in {self.forex_minute.Symbol}.")
                    self.Liquidate(self.forex_minute.Symbol)  # Close existing position

        # Execute trade with stop loss and take profit
        if is_long:
            self.MarketOrder(self.forex_minute.Symbol, position_size)  # Enter long position
            self.StopMarketOrder(self.forex_minute.Symbol, -position_size, stop_loss)  # Set stop loss
            self.LimitOrder(self.forex_minute.Symbol, -position_size, take_profit)  # Set take profit
            self.Debug(f"Entered long position with stop loss at {stop_loss} and take profit at {take_profit}")
        else:
            self.MarketOrder(self.forex_minute.Symbol, -position_size)  # Enter short position
            self.StopMarketOrder(self.forex_minute.Symbol, position_size, stop_loss)  # Set stop loss
            self.LimitOrder(self.forex_minute.Symbol, position_size, take_profit)  # Set take profit
            self.Debug(f"Entered short position with stop loss at {stop_loss} and take profit at {take_profit}")

    def ConfirmEntry(self, data):
        try:
            m15_history = self.History(self.m15, 50, Resolution.Minute)
            m5_history = self.History(self.m5, 50, Resolution.Minute)
        except Exception as e:
            self.Debug(f"Error fetching M15 or M5 data: {str(e)}")
            return None

        if m15_history.empty or m5_history.empty:
            self.Debug("15-minute or 5-minute historical data is empty.")
            return None

        m15_mss = self.check_mss(m15_history)
        m5_mss = self.check_mss(m5_history)

        # Check RSI levels for confirmation
        rsi_m15 = self.rsi_m15.Current.Value
        rsi_m5 = self.rsi_m5.Current.Value

        if m15_mss == 'bullish' and m5_mss == 'bullish' and rsi_m15 < 40 and rsi_m5 < 40:
            self.Debug("Entry confirmation: bullish.")
            return 'bullish'
        elif m15_mss == 'bearish' and m5_mss == 'bearish' and rsi_m15 > 60 and rsi_m5 > 60:
            self.Debug("Entry confirmation: bearish.")
            return 'bearish'
        else:
            self.Debug("Entry confirmation: none.")
            return None

    def FetchHourlyData(self):
        # Attempt to fetch hourly data directly
        try:
            history = self.History(self.hourly, 100, Resolution.Hour)
            if not history.empty:
                self.Debug(f"Successfully fetched hourly data: {history.tail()}")
            else:
                self.Debug("Hourly data fetched but is empty.")
        except Exception as e:
            self.Debug(f"Error fetching hourly data directly: {str(e)}")
