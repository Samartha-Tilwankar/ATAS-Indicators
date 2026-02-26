using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Volume-Price Absorption Combiner - Combines volume and price action to detect
    /// institutional absorption using delta analysis approach
    /// </summary>
    public class VolumePriceAbsorption : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Absorption Period", DefaultValue = 10)]
        public int AbsorptionPeriod { get; set; } = 10;

        [Parameter("Volume Multiplier", DefaultValue = 2.5)]
        public double VolumeMultiplier { get; set; } = 2.5;

        public VolumePriceAbsorption()
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

            if (bar < AbsorptionPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate average volume
            decimal avgVolume = 0;
            for (int i = _volumes.Count - AbsorptionPeriod; i < _volumes.Count; i++)
                avgVolume += _volumes[i];
            avgVolume /= AbsorptionPeriod;

            bool isBigVolume = volume >= avgVolume * (decimal)VolumeMultiplier;
            
            // Determine bar direction
            bool isUpBar = close > open;
            bool isDoji = Math.Abs(close - open) < (high - low) * 0.1m;

            decimal absorptionScore = 0;

            if (isBigVolume)
            {
                // Absorption pattern 1: Close near low on up bar (selling absorption)
                if (isUpBar && !isDoji)
                {
                    decimal range = high - low;
                    if (range > 0)
                    {
                        decimal closePosition = (close - low) / range;
                        if (closePosition < 0.3m)
                        {
                            // Strong selling absorption - sellers being absorbed by buyers
                            absorptionScore = (0.3m - closePosition) * 100 * (volume / avgVolume);
                        }
                    }
                }

                // Absorption pattern 2: Close near high on down bar (buying absorption)
                if (!isUpBar && !isDoji)
                {
                    decimal range = high - low;
                    if (range > 0)
                    {
                        decimal closePosition = (close - low) / range;
                        if (closePosition > 0.7m)
                        {
                            // Strong buying absorption - buyers being absorbed by sellers
                            absorptionScore = (closePosition - 0.7m) * 100 * (volume / avgVolume);
                        }
                    }
                }

                // Absorption pattern 3: Doji with very high volume
                if (isDoji && volume >= avgVolume * 3)
                {
                    // Massive absorption - market in equilibrium
                    absorptionScore = 50;
                }
            }

            // Check for consecutive absorption at same price level
            int levelAbsorptionCount = 0;
            decimal priceLevel = isUpBar ? high : low;
            
            for (int i = bar - AbsorptionPeriod; i < bar; i++)
            {
                if (i < 0) continue;
                decimal histLevel = _closes[i] > _opens[i] ? _highs[i] : _lows[i];
                if (Math.Abs((double)(histLevel - priceLevel)) / (double)priceLevel < 0.002)
                    levelAbsorptionCount++;
            }

            if (levelAbsorptionCount >= 3)
                absorptionScore += levelAbsorptionCount * 10;

            this[bar] = Math.Min(100, absorptionScore);
        }
    }
}
