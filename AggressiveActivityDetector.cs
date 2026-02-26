using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Aggressive Activity Detector - Identifies aggressive buying and selling based on
    /// price action relative to the bar range and volume
    /// </summary>
    public class AggressiveActivityDetector : Indicator
    {
        private readonly List<decimal> _closePrices = new();
        private readonly List<decimal> _highPrices = new();
        private readonly List<decimal> _lowPrices = new();
        private readonly List<decimal> _openPrices = new();
        private readonly List<decimal> _volumes = new();
        
        [Parameter("Volume Period", DefaultValue = 20)]
        public int VolumePeriod { get; set; } = 20;

        [Parameter("Strength Threshold", DefaultValue = 0.7)]
        public double StrengthThreshold { get; set; } = 0.7;

        public enum AggressiveType
        {
            None = 0,
            AggressiveBuy = 1,
            AggressiveSell = -1
        }

        public AggressiveActivityDetector()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var close = GetSeriesValue(bar, PriceField.Close);
            var high = GetSeriesValue(bar, PriceField.High);
            var low = GetSeriesValue(bar, PriceField.Low);
            var open = GetSeriesValue(bar, PriceField.Open);
            var volume = GetSeriesValue(bar, VolumeField);

            _closePrices.Add(close);
            _highPrices.Add(high);
            _lowPrices.Add(low);
            _openPrices.Add(open);
            _volumes.Add(volume);

            if (bar < VolumePeriod)
            {
                this[bar] = 0;
                return;
            }

            decimal range = high - low;
            if (range == 0)
            {
                this[bar] = 0;
                return;
            }

            // Calculate position of close within the range (0 = low, 1 = high)
            decimal closePosition = (close - low) / range;

            // Calculate average volume
            decimal avgVolume = 0;
            for (int i = _volumes.Count - VolumePeriod; i < _volumes.Count; i++)
                avgVolume += _volumes[i];
            avgVolume /= VolumePeriod;

            // Determine if volume is significantly higher than average
            bool isHighVolume = volume > avgVolume * 1.5m;

            AggressiveType type = AggressiveType.None;
            decimal strength = 0;

            // Aggressive Buying: Price closed in top portion of range with high volume
            if (closePosition >= (decimal)StrengthThreshold && isHighVolume)
            {
                type = AggressiveType.AggressiveBuy;
                strength = closePosition * (volume / avgVolume);
            }
            // Aggressive Selling: Price closed in bottom portion of range with high volume
            else if (closePosition <= (decimal)(1 - StrengthThreshold) && isHighVolume)
            {
                type = AggressiveType.AggressiveSell;
                strength = (1 - closePosition) * (volume / avgVolume);
            }

            // Output: positive for aggressive buying, negative for aggressive selling
            if (type == AggressiveType.AggressiveBuy)
                this[bar] = Math.Min(100, strength * 50);
            else if (type == AggressiveType.AggressiveSell)
                this[bar] = -Math.Min(100, strength * 50);
            else
                this[bar] = 0;
        }
    }
}
