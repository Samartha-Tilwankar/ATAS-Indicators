using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Order Flow Imbalance - Detects aggressive buying and selling by analyzing
    /// order flow dynamics and price movement within each bar
    /// </summary>
    public class OrderFlowImbalance : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Trend Period", DefaultValue = 10)]
        public int TrendPeriod { get; set; } = 10;

        [Parameter("Imbalance Threshold", DefaultValue = 0.65)]
        public double ImbalanceThreshold { get; set; } = 0.65;

        [Parameter("Volume Weight", DefaultValue = 2.0)]
        public double VolumeWeight { get; set; } = 2.0;

        public OrderFlowImbalance()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var close = GetSeriesValue(bar, PriceField.Close);
            var open = GetSeriesValue(bar, PriceField.Open);
            var high = GetSeriesValue(bar, PriceField.High);
            var low = GetSeriesValue(bar, PriceField.Low);
            var volume = GetSeriesValue(bar, VolumeField);

            _closes.Add(close);
            _opens.Add(open);
            _highs.Add(high);
            _lows.Add(low);
            _volumes.Add(volume);

            if (bar < TrendPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate average volume
            decimal avgVolume = GetAverageVolume(bar, TrendPeriod);
            decimal volumeRatio = volume / (avgVolume > 0 ? avgVolume : 1);

            // Calculate order flow imbalance for current bar
            decimal range = high - low;
            decimal imbalance = 0;

            if (range > 0)
            {
                // Calculate where price closed relative to the range
                decimal closePosition = (close - low) / range;
                
                // Calculate where price opened relative to the range
                decimal openPosition = (open - low) / range;

                // Intraday momentum: where did price go during the bar?
                decimal momentum = closePosition - openPosition;

                // Order flow imbalance calculation
                // Positive = buying pressure, Negative = selling pressure
                imbalance = momentum * 2; // Scale to -1 to 1 range approximately

                // Aggressive buying: strong upward momentum with high volume
                if (momentum > (decimal)(ImbalanceThreshold - 0.5) && volumeRatio >= (decimal)VolumeWeight)
                {
                    imbalance *= volumeRatio;
                }
                // Aggressive selling: strong downward momentum with high volume
                else if (momentum < -(decimal)(ImbalanceThreshold - 0.5) && volumeRatio >= (decimal)VolumeWeight)
                {
                    imbalance *= volumeRatio;
                }
            }

            // Confirm with trend: check if imbalance aligns with recent price action
            decimal trendStrength = CalculateTrendStrength(bar);
            
            // If imbalance aligns with trend, it's more significant
            decimal finalSignal = imbalance;
            if ((imbalance > 0 && trendStrength > 0) || (imbalance < 0 && trendStrength < 0))
            {
                finalSignal *= (1 + Math.Abs(trendStrength) * 0.5m);
            }

            this[bar] = Math.Max(-100, Math.Min(100, finalSignal * 50));
        }

        private decimal GetAverageVolume(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
                sum += _volumes[i];

            return sum / period;
        }

        private decimal CalculateTrendStrength(int bar)
        {
            if (bar < TrendPeriod) return 0;

            // Calculate price change over the period
            decimal priceChange = (_closes[bar] - _closes[bar - TrendPeriod]) / _closes[bar - TrendPeriod];
            
            // Calculate volume-weighted price change
            decimal vwChange = 0;
            decimal totalVolume = 0;

            for (int i = bar - TrendPeriod; i < bar; i++)
            {
                decimal barChange = (_closes[i] - _opens[i]) / _opens[i];
                vwChange += barChange * _volumes[i];
                totalVolume += _volumes[i];
            }

            if (totalVolume > 0)
                vwChange /= totalVolume;

            // Return trend strength normalized
            return vwChange * 100;
        }
    }
}
