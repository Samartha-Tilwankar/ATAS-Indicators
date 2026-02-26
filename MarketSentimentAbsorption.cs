using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Market Sentiment Absorption - Combines multiple signals to detect when
    /// market sentiment is being absorbed by institutional players
    /// </summary>
    public class MarketSentimentAbsorption : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Fast Period", DefaultValue = 5)]
        public int FastPeriod { get; set; } = 5;

        [Parameter("Slow Period", DefaultValue = 20)]
        public int SlowPeriod { get; set; } = 20;

        [Parameter("Volume Spike Multiplier", DefaultValue = 2.0)]
        public double VolumeSpikeMultiplier { get; set; } = 2.0;

        [Parameter("Sentiment Threshold", DefaultValue = 0.7)]
        public double SentimentThreshold { get; set; } = 0.7;

        public MarketSentimentAbsorption()
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

            if (bar < SlowPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate volume metrics
            decimal avgVolumeFast = GetAverageVolume(bar, FastPeriod);
            decimal avgVolumeSlow = GetAverageVolume(bar, SlowPeriod);
            
            // Volume analysis
            bool isVolumeSpike = volume > avgVolumeSlow * (decimal)VolumeSpikeMultiplier;
            decimal volumeMomentum = avgVolumeFast / (avgVolumeSlow > 0 ? avgVolumeSlow : 1);

            // Price momentum analysis
            decimal fastMomentum = CalculateMomentum(bar, FastPeriod);
            decimal slowMomentum = CalculateMomentum(bar, SlowPeriod);

            // Sentiment calculation: combining momentum and volume
            decimal sentiment = CalculateSentiment(bar);
            
            // Absorption detection
            decimal absorptionSignal = 0;

            // Pattern 1: Extreme sentiment with volume spike (reversal signal)
            if (isVolumeSpike)
            {
                if (sentiment > (decimal)SentimentThreshold && fastMomentum < slowMomentum * 0.5m)
                {
                    // Bullish sentiment but momentum weakening = selling absorption
                    absorptionSignal = -sentiment * 100;
                }
                else if (sentiment < -(decimal)SentimentThreshold && fastMomentum > slowMomentum * 0.5m)
                {
                    // Bearish sentiment but momentum weakening = buying absorption
                    absorptionSignal = Math.Abs(sentiment) * 100;
                }
            }

            // Pattern 2: High volume + low price movement (absorption/consolidation)
            if (isVolumeSpike)
            {
                decimal priceRange = high - low;
                decimal avgRange = GetAverageRange(bar, SlowPeriod);
                
                if (priceRange < avgRange * 0.5m && volumeMomentum > 1.5m)
                {
                    // Massive volume with minimal price movement = absorption
                    absorptionSignal += 30 * volumeMomentum * (sentiment >= 0 ? 1 : -1);
                }
            }

            // Pattern 3: Momentum divergence with volume confirmation
            decimal priceChange = _closes[bar] - _closes[bar - SlowPeriod];
            decimal volumeWeightedChange = GetVolumeWeightedChange(bar, SlowPeriod);
            
            if (Math.Sign(priceChange) != Math.Sign(volumeWeightedChange) && isVolumeSpike)
            {
                // Price and volume-weighted momentum diverge = absorption
                absorptionSignal += 40;
            }

            this[bar] = Math.Max(-100, Math.Min(100, absorptionSignal));
        }

        private decimal CalculateMomentum(int bar, int period)
        {
            if (bar < period) return 0;

            decimal momentum = 0;
            for (int i = bar - period; i < bar; i++)
            {
                if (i < 0) continue;
                decimal barMomentum = (_closes[i] - _opens[i]) / (_opens[i] != 0 ? _opens[i] : 1);
                momentum += barMomentum * _volumes[i];
            }

            return momentum;
        }

        private decimal CalculateSentiment(int bar)
        {
            if (bar < SlowPeriod) return 0;

            // Calculate sentiment based on recent bar characteristics
            decimal totalSentiment = 0;
            decimal totalWeight = 0;

            for (int i = bar - FastPeriod; i < bar; i++)
            {
                decimal range = _highs[i] - _lows[i];
                if (range == 0) continue;

                // Position of close within range
                decimal position = (_closes[i] - _lows[i]) / range;
                
                // Volume weight
                decimal avgVol = GetAverageVolume(i, SlowPeriod);
                decimal volWeight = _volumes[i] / (avgVol > 0 ? avgVol : 1);

                // Sentiment contribution
                decimal barSentiment = (position - 0.5m) * 2 * volWeight;
                totalSentiment += barSentiment;
                totalWeight += volWeight;
            }

            if (totalWeight == 0) return 0;
            return (totalSentiment / totalWeight);
        }

        private decimal GetAverageVolume(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
            {
                if (i >= 0) sum += _volumes[i];
            }

            return sum / period;
        }

        private decimal GetAverageRange(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
            {
                if (i >= 0) sum += (_highs[i] - _lows[i]);
            }

            return sum / period;
        }

        private decimal GetVolumeWeightedChange(int bar, int period)
        {
            if (bar < period) return 0;

            decimal totalChange = 0;
            decimal totalVolume = 0;

            for (int i = bar - period; i < bar; i++)
            {
                if (i < 1) continue;
                decimal change = (_closes[i] - _closes[i - 1]) / (_closes[i - 1] != 0 ? _closes[i - 1] : 1);
                totalChange += change * _volumes[i];
                totalVolume += _volumes[i];
            }

            if (totalVolume == 0) return 0;
            return totalChange / totalVolume * 100;
        }
    }
}
