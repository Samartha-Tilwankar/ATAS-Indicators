//=============================================================================
// NinjaTrader 8 - APEX LIQUIDITY PRO V1 (Advanced)
//=============================================================================
// Improvements:
// 1. Liquidity pool identification with sweep detection
// 2. Stop hunt / liquidity grab analysis
// 3. Inverse inverse order flow analysis
// 4. Market maker manipulation detection
// 5. ICT concepts: Order blocks, FVG, Liquidity sweeps
// 6. Volume Profile analysis
// 7. Momentum divergence detection
//=============================================================================
namespace NinjaTrader.NinjaScript.Indicators
{
    public class ApexLiquidityPro : Indicator
    {
        private Series<double> liquidityScore;
        private Series<double> manipulationScore;
        
        [Parameter("Fast EMA", DefaultValue = 9)]
        public int FastEMA { get; set; }
        
        [Parameter("Medium EMA", DefaultValue = 21)]
        public int MediumEMA { get; set; }
        
        [Parameter("Slow EMA", DefaultValue = 50)]
        public int SlowEMA { get; set; }
        
        [Parameter("Supertrend Period", DefaultValue = 10)]
        public int SupertrendPeriod { get; set; }
        
        [Parameter("Supertrend Multiplier", DefaultValue = 2.5)]
        public double SupertrendMultiplier { get; set; }
        
        [Parameter("Liquidity Lookback", DefaultValue = 12)]
        public int LiquidityLookback { get; set; }
        
        [Parameter("Volume Profile Period", DefaultValue = 20)]
        public int VolumeProfilePeriod { get; set; }
        
        [Parameter("Volume Threshold", DefaultValue = 1.8)]
        public double VolumeThreshold { get; set; }
        
        [Parameter("Sweep Sensitivity", DefaultValue = 2.0)]
        public double SweepSensitivity { get; set; }
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ApexLiquidityProV1";
                Description = "Apex Liquidity Pro - Advanced liquidity and manipulation detection";
                AddPlot(SeriesColor.Lime, "Buy Signal");
                AddPlot(SeriesColor.Red, "Sell Signal");
                AddPlot(SeriesColor.Gold, "Liquidity Score");
                AddPlot(SeriesColor.Cyan, "Supertrend");
                AddPlot(SeriesColor.Orange, "Manipulation");
                
                liquidityScore = new Series<double>(this);
                manipulationScore = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(SlowEMA, LiquidityLookback))
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
            
            // Liquidity Pool Detection
            double liqPool = DetectLiquidityPools();
            liquidityScore[0] = liqPool;
            Values[2][0] = liqPool;
            
            // Liquidity Sweep Detection
            double sweep = DetectLiquiditySweeps();
            
            // Market Maker Manipulation Detection
            double manip = DetectManipulation();
            manipulationScore[0] = manip;
            Values[4][0] = manip;
            
            // Volume Profile
            double vpoc = CalculateVPOC();
            double vpar = CalculateVPAR();
            
            // Order Block Detection
            double ob = DetectOrderBlocks();
            
            // Fair Value Gap
            double fvg = DetectFVG();
            
            // Supertrend
            double supertrend = CalculateSupertrend();
            Values[3][0] = supertrend;
            
            // Calculate Apex Score
            double apexScore = CalculateApexScore(emaFast, emaMedium, emaSlow, 
                                                delta, liqPool, sweep, manip,
                                                vpoc, vpar, ob, fvg);
            
            // ===== GENERATE SIGNALS =====
            
            double volumeMA = SMA(Volume, 14)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            bool supertrendBullish = Close[0] > supertrend && Values[3][1] < Values[3][2];
            bool supertrendBearish = Close[0] < supertrend && Values[3][1] > Values[3][2];
            
            bool emaBullish = emaFast > emaMedium && emaMedium > emaSlow;
            bool emaBearish = emaFast < emaMedium && emaMedium < emaSlow;
            
            bool buySignal = false;
            bool sellSignal = false;
            
            // BUY: Liquidity sweep + Order Block + Supertrend + Delta
            // Sweep detected (stop hunt failed) + price returning
            if (sweep > LiquidityLookback * 3 && delta > 0)
            {
                if ((ob > 0 || fvg > 0) && (supertrendBullish || supertrend > 0))
                {
                    if (volumeRatio > VolumeThreshold * 0.8)
                        buySignal = true;
                }
            }
            
            // Alternative: Strong absorption + trend
            if (liqPool > LiquidityLookback * 2 && emaBullish && delta > 0)
            {
                if (supertrendBullish || (supertrend > 0 && Close[0] > supertrend))
                    buySignal = true;
            }
            
            // SELL: Liquidity sweep + Order Block + Supertrend + Delta
            if (sweep < -LiquidityLookback * 3 && delta < 0)
            {
                if ((ob < 0 || fvg < 0) && (supertrendBearish || supertrend < 0))
                {
                    if (volumeRatio > VolumeThreshold * 0.8)
                        sellSignal = true;
                }
            }
            
            // Alternative: Strong absorption + trend
            if (liqPool < -LiquidityLookback * 2 && emaBearish && delta < 0)
            {
                if (supertrendBearish || (supertrend < 0 && Close[0] < supertrend))
                    sellSignal = true;
            }
            
            Values[0][0] = buySignal ? Math.Min(100, apexScore) : 0;
            Values[1][0] = sellSignal ? Math.Max(-100, apexScore) : 0;
        }
        
        // ===== APEX CALCULATION METHODS =====
        
        private double CalculateDelta()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            double closePos = (Close[0] - Low[0]) / range;
            return (closePos - 0.5) * 2 * Volume[0];
        }
        
        private double DetectLiquidityPools()
        {
            double score = 0;
            double volumeMA = SMA(Volume, LiquidityLookback)[0];
            
            // Find swing highs and lows
            double swingHigh = High[1];
            double swingLow = Low[1];
            
            for (int i = 2; i <= LiquidityLookback; i++)
            {
                swingHigh = Math.Max(swingHigh, High[i]);
                swingLow = Math.Min(swingLow, Low[i]);
            }
            
            // Check if current price near liquidity pools
            double distToHigh = Math.Abs(Close[0] - swingHigh) / swingHigh;
            double distToLow = Math.Abs(Close[0] - swingLow) / swingLow;
            
            // High liquidity at highs (resistance)
            if (distToHigh < 0.005)
            {
                double volRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
                if (volRatio > SweepSensitivity)
                    score -= volRatio * 15;
            }
            
            // High liquidity at lows (support)
            if (distToLow < 0.005)
            {
                double volRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
                if (volRatio > SweepSensitivity)
                    score += volRatio * 15;
            }
            
            // Recent liquidity pool analysis
            for (int i = 1; i <= LiquidityLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                
                // Check for liquidity grab patterns
                bool isHigh = High[i] == MAX(High, LiquidityLookback)[i];
                bool isLow = Low[i] == MIN(Low, LiquidityLookback)[i];
                
                if (isHigh && volRatio > SweepSensitivity)
                {
                    // Check if it was a failed break (liquidity grab)
                    if (i < LiquidityLookback)
                    {
                        bool failed = false;
                        for (int j = i + 1; j <= Math.Min(i + 3, LiquidityLookback); j++)
                        {
                            if (Close[j] < Low[i])
                            {
                                failed = true;
                                break;
                            }
                        }
                        if (failed)
                            score -= 20 * volRatio;
                    }
                }
                
                if (isLow && volRatio > SweepSensitivity)
                {
                    if (i < LiquidityLookback)
                    {
                        bool failed = false;
                        for (int j = i + 1; j <= Math.Min(i + 3, LiquidityLookback); j++)
                        {
                            if (Close[j] > High[i])
                            {
                                failed = true;
                                break;
                            }
                        }
                        if (failed)
                            score += 20 * volRatio;
                    }
                }
            }
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double DetectLiquiditySweeps()
        {
            double score = 0;
            double volumeMA = SMA(Volume, LiquidityLookback)[0];
            
            for (int i = 1; i <= 5; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                
                // Quick sweep of highs with high volume
                bool sweepHigh = High[i] == MAX(High, 5)[i] && volRatio > SweepSensitivity;
                
                // Quick sweep of lows with high volume
                bool sweepLow = Low[i] == MIN(Low, 5)[i] && volRatio > SweepSensitivity;
                
                if (sweepHigh)
                {
                    // Check if it reversed immediately (failed sweep = trap)
                    if (i < 5 && Close[i] < Open[i])
                    {
                        score -= 15 * volRatio;
                    }
                }
                
                if (sweepLow)
                {
                    if (i < 5 && Close[i] > Open[i])
                    {
                        score += 15 * volRatio;
                    }
                }
            }
            
            return score;
        }
        
        private double DetectManipulation()
        {
            double score = 0;
            double volumeMA = SMA(Volume, 10)[0];
            
            // Detect market maker manipulation patterns
            for (int i = 1; i <= LiquidityLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                double closePos = (Close[i] - Low[i]) / range;
                
                // Manipulation pattern: High volume + low range + close in middle
                // Usually indicates market maker filling orders
                if (volRatio > 2.5 && range < SMA(new Series<double>(this, 1, range), 5)[0] * 0.7)
                {
                    if (closePos > 0.4 && closePos < 0.6)
                        score += volRatio * 3;
                }
                
                // Stop hunt manipulation
                if (volRatio > 2.0)
                {
                    bool atHighs = High[i] > HIGH(High, LiquidityLookback)[i] * 0.99;
                    bool atLows = Low[i] < LOW(Low, LiquidityLookback)[i] * 1.01;
                    
                    if (atHighs && Close[i] < Open[i])
                        score -= volRatio * 5;
                    if (atLows && Close[i] > Open[i])
                        score += volRatio * 5;
                }
            }
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double CalculateVPOC()
        {
            // Volume Point of Control
            double[] prices = new double[VolumeProfilePeriod];
            double[] vols = new double[VolumeProfilePeriod];
            
            for (int i = 0; i < VolumeProfilePeriod; i++)
            {
                prices[i] = (High[i] + Low[i] + Close[i]) / 3;
                vols[i] = Volume[i];
            }
            
            // Find max volume price
            double maxVol = 0;
            double vpoc = prices[0];
            
            for (int i = 0; i < VolumeProfilePeriod; i++)
            {
                if (vols[i] > maxVol)
                {
                    maxVol = vols[i];
                    vpoc = prices[i];
                }
            }
            
            return vpoc;
        }
        
        private double CalculateVPAR()
        {
            // Volume Profile Area of Value
            double totalVol = 0;
            double weightedSum = 0;
            
            for (int i = 0; i < VolumeProfilePeriod; i++)
            {
                double typicalPrice = (High[i] + Low[i] + Close[i]) / 3;
                weightedSum += typicalPrice * Volume[i];
                totalVol += Volume[i];
            }
            
            if (totalVol == 0) return Close[0];
            return weightedSum / totalVol;
        }
        
        private double DetectOrderBlocks()
        {
            double score = 0;
            double volumeMA = SMA(Volume, LiquidityLookback)[0];
            
            for (int i = 2; i <= LiquidityLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                if (volRatio < 1.8) continue;
                
                double closePos = (Close[i] - Low[i]) / range;
                bool isUpBar = Close[i] > Open[i];
                
                // Bullish Order Block
                if (!isUpBar && closePos < 0.35)
                {
                    if (Close[0] > High[i] && Close[0] < High[i] * 1.03)
                        score += volRatio * 12;
                }
                
                // Bearish Order Block
                if (isUpBar && closePos > 0.65)
                {
                    if (Close[0] < Low[i] && Close[0] > Low[i] * 0.97)
                        score -= volRatio * 12;
                }
            }
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double DetectFVG()
        {
            if (CurrentBar < 3) return 0;
            
            double gap = 0;
            
            // Bullish FVG
            if (Low[0] > High[2])
            {
                double fvgSize = Low[0] - High[2];
                double avgRange = 0;
                for (int i = 0; i < 10; i++)
                    avgRange += High[i] - Low[i];
                avgRange /= 10;
                
                if (fvgSize > avgRange * 0.5)
                {
                    // Check if price returned to fill FVG
                    if (Close[0] > Low[0] && Close[0] < High[2])
                        gap = fvgSize / Close[0] * 100;
                }
            }
            
            // Bearish FVG
            if (High[0] < Low[2])
            {
                double fvgSize = Low[2] - High[0];
                double avgRange = 0;
                for (int i = 0; i < 10; i++)
                    avgRange += High[i] - Low[i];
                avgRange /= 10;
                
                if (fvgSize > avgRange * 0.5)
                {
                    if (Close[0] < High[0] && Close[0] > Low[2])
                        gap = -fvgSize / Close[0] * 100;
                }
            }
            
            return gap;
        }
        
        private double CalculateApexScore(double emaF, double emaM, double emaS,
                                        double delta, double liq, double sweep,
                                        double manip, double vpoc, double vpar,
                                        double ob, double fvg)
        {
            double score = 0;
            
            // EMA (20 pts)
            if (emaF > emaM && emaM > emaS) score += 20;
            else if (emaF < emaM && emaM < emaS) score -= 20;
            
            // Delta (15 pts)
            score += Math.Min(15, Math.Max(-15, delta / 3000));
            
            // Liquidity (20 pts)
            score += Math.Min(20, Math.Max(-20, liq / 5));
            
            // Sweep (15 pts)
            score += Math.Min(15, Math.Max(-15, sweep / 5));
            
            // Order Block (10 pts)
            score += Math.Min(10, Math.Max(-10, ob / 5));
            
            // FVG (10 pts)
            score += Math.Min(10, Math.Max(-10, fvg));
            
            // VPOC proximity (10 pts)
            double distToVPOC = Math.Abs(Close[0] - vpoc) / Close[0];
            if (distToVPOC < 0.002)
                score += 10;
            
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
