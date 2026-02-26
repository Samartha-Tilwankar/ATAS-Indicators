using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Absorption Zone Detector - Identifies price levels where large players absorb market orders
    /// Detects when price repeatedly rejects from the same level without breaking through
    /// </summary>
    public class AbsorptionZoneDetector : Indicator
    {
        private readonly List<decimal> _highPrices = new();
        private readonly List<decimal> _lowPrices = new();
        private readonly List<decimal> _volumes = new();
        
        [Parameter("Rejection Count", DefaultValue = 3)]
        public int RejectionCount { get; set; } = 3;

        [Parameter("Lookback Period", DefaultValue = 20)]
        public int LookbackPeriod { get; set; } = 20;

        [Parameter("Volume Threshold", DefaultValue = 1.5)]
        public double VolumeThreshold { get; set; } = 1.5;

        public AbsorptionZoneDetector()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var high = GetSeriesValue(bar, PriceField.High);
            var low = GetSeriesValue(bar, PriceField.Low);
            var volume = GetSeriesValue(bar, VolumeField);

            _highPrices.Add(high);
            _lowPrices.Add(low);
            _volumes.Add(volume);

            if (bar < LookbackPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate average volume for recent period
            decimal avgVolume = 0;
            for (int i = _volumes.Count - LookbackPeriod; i < _volumes.Count; i++)
                avgVolume += _volumes[i];
            avgVolume /= LookbackPeriod;

            int buyerAbsorption = DetectAbsorption(bar, true, avgVolume);
            int sellerAbsorption = DetectAbsorption(bar, false, avgVolume);

            // Return combined signal: positive for buyer absorption, negative for seller absorption
            if (buyerAbsorption >= RejectionCount)
                this[bar] = buyerAbsorption * 10;  // Strong buyer absorption
            else if (sellerAbsorption >= RejectionCount)
                this[bar] = -sellerAbsorption * 10; // Strong seller absorption
            else
                this[bar] = 0;
        }

        private int DetectAbsorption(int bar, bool isBuyer, decimal avgVolume)
        {
            var currentHigh = _highPrices[bar];
            var currentLow = _lowPrices[bar];
            int rejectionCount = 0;

            // Look for rejections at similar levels
            for (int i = bar - LookbackPeriod; i < bar; i++)
            {
                if (i < 0) continue;
                
                var histHigh = _highPrices[i];
                var histLow = _lowPrices[i];
                var histVol = _volumes[i];

                // Check if volume is above threshold (significant activity)
                if (histVol < avgVolume * (decimal)VolumeThreshold)
                    continue;

                if (isBuyer)
                {
                    // Buyer absorption: price rejected from highs (failed to break higher)
                    if (Math.Abs((double)(histHigh - currentHigh)) / (double)currentHigh < 0.001)
                        rejectionCount++;
                }
                else
                {
                    // Seller absorption: price rejected from lows (failed to break lower)
                    if (Math.Abs((double)(histLow - currentLow)) / (double)currentLow < 0.001)
                        rejectionCount++;
                }
            }

            return rejectionCount;
        }
    }
}
