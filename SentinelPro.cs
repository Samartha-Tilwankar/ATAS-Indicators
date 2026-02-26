//=============================================================================
// NinjaTrader 8 - SENTINEL PRO V1 (Advanced)
//=============================================================================
// Improvements:
// 1. Multi-c timeframe confirmation (using multiple EMAs as proxy)
// 2. Cumulative delta with divergence detection
// 3. Absorption zone identification with strength scoring
// 4. Break of structure (BOS) detection
// 5. Change of character (CHoCH) detection
// 6. Liquidity void analysis
// 7. Composite signal with weighted scoring
//=============================================================================
namespace NinjaTrader.NinjaScript.Indicators
{
    public class SentinelPro : Indicator
    {
        private Series<double> compositeScore;
        private Series<double> bosSignal;
        
        [Parameter("Fast EMA", DefaultValue = 8)]
        public int FastEMA { get; set; }
        
        [Parameter("Medium EMA", DefaultValue = 21)]
        public int MediumEMA { get; set; }
        
        [Parameter("Slow EMA", DefaultValue = 55)]
        public int SlowEMA { get; set; }
        
        [Parameter("Supertrend Period", DefaultValue = 10)]
        public int SupertrendPeriod { get; set; }
        
        [Parameter("Supertrend Multiplier", DefaultValue = 2.0)]
        public double SupertrendMultiplier { get; set; }
        
        [Parameter("Delta Period", DefaultValue = 20)]
        public int DeltaPeriod { get; set; }
        
        [Parameter("Structure Period", DefaultValue = 8)]
        public int StructurePeriod { get; set; }
        
        [Parameter("Volume Threshold", DefaultValue = 1.7)]
        public double VolumeThreshold { get; set; }
        
        [Parameter("Absorption Sensitivity", DefaultValue = 2.0)]
        public double AbsorptionSensitivity { get; set; }
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "SentinelProV1";
                Description = "Sentinel Pro - Advanced structure and order flow sentinel";
                AddPlot(SeriesColor.Lime, "Buy Signal");
                AddPlot(SeriesColor.Red, "Sell Signal");
                AddPlot(SeriesColor.Gold, "Sentinel Score");
                AddPlot(SeriesColor.Cyan, "Supertrend");
                AddPlot(SeriesColor.Magenta, "BOS Signal");
                
                compositeScore = new Series<double>(this);
                bosSignal = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(SlowEMA, StructurePeriod))
            {
                Values[0][0] = 0;
                Values[1][0] = 0;
                Values[2][0] = 0;
                Values[3][0] = Close[0];
                Values[4][0] = 0;
                return;
            }
            
            // ===== CORE CALCULATIONS =====
            
            // EMAs
            double emaFast = EMA(Close, FastEMA)[0];
            double emaMedium = EMA(Close, MediumEMA)[0];
            double emaSlow = EMA(Close, SlowEMA)[0];
            
            // Delta
            double delta = CalculateDelta();
            double cumDelta = CalculateCumulativeDelta();
            
            // Absorption
            double absorption = CalculateAbsorption();
            
            // Break of Structure
            double bos = DetectBOS();
            bosSignal[0] = bos;
            Values[4][0] = bos;
            
            // Change of Character
            bool choch = DetectCHoCH();
            
            // Liquidity Void
            double voidScore = DetectLiquidityVoid();
            
            // Delta Divergence
            double div = DetectDeltaDivergence();
            
            // Supertrend
            double supertrend = CalculateSupertrend();
            Values[3][0] = supertrend;
            
            // Composite Score
            double sentinelScore = CalculateSentinelScore(emaFast, emaMedium, emaSlow,
                                                        delta, cumDelta, absorption,
                                                        bos, choch, voidScore, div);
            compositeScore[0] = sentinelScore;
            Values[2][0] = sentinelScore;
            
            // ===== GENERATE SIGNALS =====
            
            double volumeMA = SMA(Volume, 14)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            bool supertrendBullish = Close[0] > supertrend && Values[3][1] < Values[3][2];
            bool supertrendBearish = Close[0] < supertrend && Values[3][1] > Values[3][2];
            
            bool emaBullish = emaFast > emaMedium && emaMedium > emaSlow;
            bool emaBearish = emaFast < emaMedium && emaMedium < emaSlow;
            
            bool buySignal = false;
            bool sellSignal = false;
            
            // BUY: BOS + Absorption + Delta + EMA + Supertrend
            if ((bos > 0 || choch) && emaBullish && delta > 0)
            {
                if (supertrendBullish || (supertrend > 0 && Close[0] > supertrend))
                {
                    if (absorption > StructurePeriod * 2 || voidScore > 0)
                    {
                        if (volumeRatio > VolumeThreshold)
                            buySignal = true;
                    }
                }
            }
            
            // Alternative: Strong structure with absorption
            if (emaBullish && absorption > StructurePeriod * 3 && delta > 0)
            {
                if (supertrendBullish || volumeRatio > VolumeThreshold * 1.2)
                    buySignal = true;
            }
            
            // SELL: BOS + Absorption + Delta + EMA + Supertrend
            if ((bos < 0 || choch) && emaBearish && delta < 0)
            {
                if (supertrendBearish || (supertrend < 0 && Close[0] < supertrend))
                {
                    if (absorption < -StructurePeriod * 2 || voidScore < 0)
                    {
                        if (volumeRatio > VolumeThreshold)
                            sellSignal = true;
                    }
                }
            }
            
            // Alternative: Strong structure with absorption
            if (emaBearish && absorption < -StructurePeriod * 3 && delta < 0)
            {
                if (supertrendBearish || volumeRatio > VolumeThreshold * 1.2)
                    sellSignal = true;
            }
            
            Values[0][0] = buySignal ? Math.Min(100, sentinelScore) : 0;
            Values[1][0] = sellSignal ? Math.Max(-100, sentinelScore) : 0;
        }
        
        // ===== SENTINEL CALCULATION METHODS =====
        
        private double CalculateDelta()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            double closePos = (Close[0] - Low[0]) / range;
            
            double volumeMA = SMA(Volume, DeltaPeriod)[0];
            double volRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            double amp = volRatio > 2.0 ? 1.5 : (volRatio > 1.5 ? 1.25 : 1.0);
            
            return (closePos - 0.5) * 2 * Volume[0] * amp;
        }
        
        private double CalculateCumulativeDelta()
        {
            double cumDelta = 0;
            for (int i = 0; i < DeltaPeriod; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                double closePos = (Close[i] - Low[i]) / range;
                cumDelta += (closePos - 0.5) * 2 * Volume[i];
            }
            return cumDelta;
        }
        
        private double CalculateAbsorption()
        {
            double score = 0;
            double volumeMA = SMA(Volume, StructurePeriod)[0];
            
            for (int i = 1; i <= StructurePeriod; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                double closePos = (Close[i] - Low[i]) / range;
                bool isUpBar = Close[i] > Open[i];
                
                // Strong absorption
                if (volRatio > AbsorptionSensitivity)
                {
                    if (isUpBar && closePos < 0.3)
                        score += volRatio * 12;
                    else if (!isUpBar && closePos > 0.7)
                        score -= volRatio * 12;
                }
                
                // Doji absorption
                if (Math.Abs(Close[i] - Open[i]) < range * 0.1 && volRatio > 2.0)
                {
                    score += (isUpBar ? 1 : -1) * volRatio * 8;
                }
            }
            
            return Math.Max(-150, Math.Min(150, score));
        }
        
        private double DetectBOS()
        {
            if (CurrentBar < StructurePeriod + 1) return 0;
            
            double score = 0;
            
            // Find recent swing points
            double swingHigh = High[1];
            double swingLow = Low[1];
            
            for (int i = 2; i <= StructurePeriod; i++)
            {
                swingHigh = Math.Max(swingHigh, High[i]);
                swingLow = Math.Min(swingLow, Low[i]);
            }
            
            // Bullish BOS: Price breaks above swing high with momentum
            if (Close[0] > swingHigh && Volume[0] > SMA(Volume, 10)[0])
            {
                double prevSwingHigh = High[StructurePeriod + 1];
                for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
                {
                    if (i > CurrentBar) break;
                    prevSwingHigh = Math.Max(prevSwingHigh, High[i]);
                }
                
                if (swingHigh > prevSwingHigh)
                    score = 50;
            }
            
            // Bearish BOS: Price breaks below swing low
            if (Close[0] < swingLow && Volume[0] > SMA(Volume, 10)[0])
            {
                double prevSwingLow = Low[StructurePeriod + 1];
                for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
                {
                    if (i > CurrentBar) break;
                    prevSwingLow = Math.Min(prevSwingLow, Low[i]);
                }
                
                if (swingLow < prevSwingLow)
                    score = -50;
            }
            
            return score;
        }
        
        private bool DetectCHoCH()
        {
            if (CurrentBar < StructurePeriod + 2) return false;
            
            // Bullish CHoCH: Previous downtrend breaks up
            double prevLow = Low[StructurePeriod + 1];
            for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
            {
                if (i > CurrentBar) break;
                prevLow = Math.Min(prevLow, Low[i]);
            }
            
            if (Close[0] > High[StructurePeriod] && Low[0] > prevLow)
                return true;
            
            // Bearish CHoCH
            double prevHigh = High[StructurePeriod + 1];
            for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
            {
                if (i > CurrentBar) break;
                prevHigh = Math.Max(prevHigh, High[i]);
            }
            
            if (Close[0] < Low[StructurePeriod] && High[0] < prevHigh)
                return true;
            
            return false;
        }
        
        private double DetectLiquidityVoid()
        {
            double score = 0;
            
            // Calculate average range
            double avgRange = 0;
            for (int i = 0; i < 10; i++)
                avgRange += High[i] - Low[i];
            avgRange /= 10;
            
            // Check for voids (gaps)
            if (Low[0] > High[1])
            {
                double voidSize = Low[0] - High[1];
                if (voidSize > avgRange * 0.5)
                    score = voidSize / Close[0] * 100;
            }
            
            if (High[0] < Low[1])
            {
                double voidSize = Low[1] - High[0];
                if (voidSize > avgRange * 0.5)
                    score = -voidSize / Close[0] * 100;
            }
            
            return score;
        }
        
        private double DetectDeltaDivergence()
        {
            if (CurrentBar < DeltaPeriod * 2) return 0;
            
            // Price divergence from delta
            double priceChange = Close[0] - Close[DeltaPeriod];
            double deltaChange = CalculateCumulativeDelta() - GetPastCumulativeDelta(DeltaPeriod);
            
            double priceChangePct = priceChange / (Close[DeltaPeriod] > 0 ? Close[DeltaPeriod] : 1);
            double deltaChangeNorm = deltaChange / (Math.Abs(GetPastCumulativeDelta(DeltaPeriod)) + 1);
            
            // Bullish divergence
            if (priceChange < -0.01 && deltaChangeNorm > 0.1)
                return 25;
            
            // Bearish divergence
            if (priceChange > 0.01 && deltaChangeNorm < -0.1)
                return -25;
            
            return 0;
        }
        
        private double GetPastCumulativeDelta(int barsAgo)
        {
            double cumDelta = 0;
            for (int i = barsAgo + DeltaPeriod; i < barsAgo + DeltaPeriod * 2; i++)
            {
                if (i > CurrentBar) break;
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                double closePos = (Close[i] - Low[i]) / range;
                cumDelta += (closePos - 0.5) * 2 * Volume[i];
            }
            return cumDelta;
        }
        
        private double CalculateSentinelScore(double emaF, double emaM, double emaS,
                                           double delta, double cumDelta,
                                           double absorp, double bos, bool choch,
                                           double voidScore, double div)
        {
            double score = 0;
            
            // EMA (20 pts)
            if (emaF > emaM && emaM > emaS) score += 20;
            else if (emaF < emaM && emaM < emaS) score -= 20;
            
            // Delta (15 pts)
            score += Math.Min(15, Math.Max(-15, delta / 4000));
            
            // Absorption (20 pts)
            score += Math.Min(20, Math.Max(-20, absorp / 6));
            
            // BOS (15 pts)
            score += Math.Min(15, Math.Max(-15, bos / 3));
            
            // CHoCH (5 pts)
            if (choch) score += 5;
            
            // Void (10 pts)
            score += Math.Min(10, Math.Max(-10, voidScore));
            
            // Divergence (5 pts)
            score += Math.Min(5, Math.Max(-5, div / 5));
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double CalculateSupertrend()
        {
            double tr = CalculateTrueRange();
            double atr = EMA(new Series<double>(this, 1, tr), SupertrendPeriod)[0];
            
            double sma = SMA(Close, SupertrendPeriod)[0];
            double upper = sma + (SupertrendMultiplier * atr);
            double lower = sma - (SupertrendMultiplier * atr);
            
            double supertrend = Values[3][1];
            
            if (Close[0] > upper)
                supertrend = lower;
            else if (Close[0] < lower)
                supertrend = upper;
            
            return supertrend;
        }
        
        private double CalculateTrueRange()
        {
            return Math.Max(High[0] - Low[0],
                   Math.Max(Math.Abs(High[0] - Close[1]),
                           Math.Abs(Low[0] - Close[1])));
        }
        
        private double EMA(Series<double> series, int period)
        {
            if (CurrentBar < period) return series[0];
            double multiplier = 2.0 / (period + 1);
            double ema = SMA(series, period)[0];
            for (int i = CurrentBar - period + 1; i <= CurrentBar; i++)
            {
                if (i < 0) continue;
                ema = (series[i] - ema) * multiplier + ema;
            }
            return ema;
        }
        
        private double SMA(Series<double> series, int period)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < period && i <= CurrentBar; i++)
            {
                sum += series[i];
                count++;
            }
            return count > 0 ? sum / count : 0;
        }
    }
}
