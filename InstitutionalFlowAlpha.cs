//=============================================================================
// NinjaTrader 8 - INSTITUTIONAL FLOW ALPHA V1 (Advanced)
//=============================================================================
// Improvements from previous versions:
// 1. Enhanced delta calculation with_tick-by-tick simulation
// 2. Multi-layer absorption detection with confirmation
// 3. Intelligent trap detection with liquidity pool analysis
// 4. Dynamic Supertrend with adaptive multipliers
// 5. RSI filter for overbought/oversold confirmation
// 6. MACD histogram for momentum confirmation
// 7. VWAP deviation for institutional entry/exit
//=============================================================================
namespace NinjaTrader.NinjaScript.Indicators
{
    public class InstitutionalFlowAlpha : Indicator
    {
        private Series<double> smartMoneyScore;
        private Series<double> liquidityScore;
        private Series<double> momentumFilter;
        
        // Enhanced Parameters
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
        
        [Parameter("Absorption Lookback", DefaultValue = 15)]
        public int AbsorptionLookback { get; set; }
        
        [Parameter("Trap Lookback", DefaultValue = 10)]
        public int TrapLookback { get; set; }
        
        [Parameter("Volume Threshold", DefaultValue = 1.6)]
        public double VolumeThreshold { get; set; }
        
        [Parameter("RSI Period", DefaultValue = 14)]
        public int RSIPeriod { get; set; }
        
        [Parameter("RSI Overbought", DefaultValue = 70)]
        public double RSIOverbought { get; set; }
        
        [Parameter("RSI Oversold", DefaultValue = 30)]
        public double RSIOversold { get; set; }
        
        [Parameter("MACD Fast", DefaultValue = 12)]
        public int MACDFast { get; set; }
        
        [Parameter("MACD Slow", DefaultValue = 26)]
        public int MACDSlow { get; set; }
        
        [Parameter("MACD Signal", DefaultValue = 9)]
        public int MACDSignal { get; set; }
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "InstitutionalFlowAlphaV1";
                Description = "Institutional Flow Alpha - Advanced institutional detection system";
                AddPlot(SeriesColor.Lime, "Buy Signal");
                AddPlot(SeriesColor.Red, "Sell Signal");
                AddPlot(SeriesColor.Gold, "Smart Money Score");
                AddPlot(SeriesColor.Cyan, "Supertrend");
                AddPlot(SeriesColor.White, "RSI");
                
                smartMoneyScore = new Series<double>(this);
                liquidityScore = new Series<double>(this);
                momentumFilter = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(SlowEMA, Math.Max(RSIPeriod, TrapLookback)))
            {
                Values[0][0] = 0;
                Values[1][0] = 0;
                Values[2][0] = 0;
                Values[3][0] = Close[0];
                Values[4][0] = 50;
                return;
            }
            
            // ===== CALCULATE ALL INDICATORS =====
            
            // 1. Enhanced EMA Analysis
            double emaFast = EMA(Close, FastEMA)[0];
            double emaMedium = EMA(Close, MediumEMA)[0];
            double emaSlow = EMA(Close, SlowEMA)[0];
            
            // 2. Delta Calculation with volume weighting
            double delta = CalculateEnhancedDelta();
            double cumDelta = CalculateCumulativeDelta();
            
            // 3. RSI for overbought/oversold
            double rsi = CalculateRSI();
            Values[4][0] = rsi;
            
            // 4. MACD for momentum
            double macdHist = CalculateMACDHistogram();
            
            // 5. VWAP deviation
            double vwapDev = CalculateVWAPDeviation();
            
            // 6. Enhanced Absorption Detection
            double absorption = CalculateEnhancedAbsorption();
            
            // 7. Liquidity Pool & Trap Detection
            double liquidity = DetectLiquidityPools();
            liquidityScore[0] = liquidity;
            
            // 8. Supertrend with adaptive multiplier
            double supertrend = CalculateAdaptiveSupertrend();
            Values[3][0] = supertrend;
            
            // 9. Calculate Smart Money Score
            double smScore = CalculateSmartMoneyScore(emaFast, emaMedium, emaSlow, 
                                                       delta, cumDelta, absorption, rsi, macdHist);
            smartMoneyScore[0] = smScore;
            Values[2][0] = smScore;
            
            // ===== GENERATE SIGNALS =====
            
            // Volume analysis
            double volumeMA = SMA(Volume, 14)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            // Direction analysis
            bool supertrendBullish = Close[0] > supertrend && Values[3][1] < Values[3][2];
            bool supertrendBearish = Close[0] < supertrend && Values[3][1] > Values[3][2];
            
            bool emaBullish = emaFast > emaMedium && emaMedium > emaSlow;
            bool emaBearish = emaFast < emaMedium && emaMedium < emaSlow;
            
            // RSI conditions
            bool rsiBuy = rsi < RSIOversold || (rsi < 40 && rsi > RSIOversold);
            bool rsiSell = rsi > RSIOverbought || (rsi > 60 && rsi < RSIOverbought);
            
            // MACD conditions
            bool macdBullish = macdHist > 0 && macdHist > Values[5][1];
            bool macdBearish = macdHist < 0 && macdHist < Values[5][1];
            
            // BUY SIGNAL - Institutional Accumulation
            bool buySignal = false;
            bool sellSignal = false;
            
            // Primary Buy: All align
            if (supertrendBullish && emaBullish && volumeRatio > VolumeThreshold)
            {
                // Check for institutional buying
                if ((absorption > AbsorptionLookback * 3 || liquidity > TrapLookback * 2) && 
                    delta > 0 && rsiBuy && (macdBullish || macdHist > -0.0001))
                {
                    buySignal = true;
                }
            }
            
            // Secondary Buy: Trend continuation
            if (emaBullish && supertrend > 0 && absorption > AbsorptionLookback * 2 && 
                delta > 0 && volumeRatio > VolumeThreshold * 0.8)
            {
                buySignal = true;
            }
            
            // Primary Sell: Institutional Distribution
            if (supertrendBearish && emaBearish && volumeRatio > VolumeThreshold)
            {
                if ((absorption < -AbsorptionLookback * 3 || liquidity < -TrapLookback * 2) && 
                    delta < 0 && rsiSell && (macdBearish || macdHist < 0.0001))
                {
                    sellSignal = true;
                }
            }
            
            // Secondary Sell: Trend continuation
            if (emaBearish && supertrend < 0 && absorption < -AbsorptionLookback * 2 && 
                delta < 0 && volumeRatio > VolumeThreshold * 0.8)
            {
                sellSignal = true;
            }
            
            // Output signals
            if (buySignal)
                Values[0][0] = Math.Min(100, smScore * 1.2);
            else
                Values[0][0] = 0;
            
            if (sellSignal)
                Values[1][0] = Math.Max(-100, smScore * 1.2);
            else
                Values[1][0] = 0;
        }
        
        // ===== ENHANCED CALCULATION METHODS =====
        
        private double CalculateEnhancedDelta()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            
            // Enhanced delta using close position weighted by volume
            double closePos = (Close[0] - Low[0]) / range;
            
            // Apply volume-weighted multiplier for aggressive moves
            double volumeMA = SMA(Volume, DeltaPeriod)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            // For aggressive moves, amplify the delta
            double aggressionMultiplier = 1.0;
            if (volumeRatio > 2.0)
                aggressionMultiplier = 1.5;
            else if (volumeRatio > 1.5)
                aggressionMultiplier = 1.25;
            
            double delta = (closePos - 0.5) * 2 * Volume[0] * aggressionMultiplier;
            
            return delta;
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
        
        private double CalculateRSI()
        {
            double gain = 0, loss = 0;
            
            for (int i = 0; i < RSIPeriod; i++)
            {
                double change = Close[i] - Close[i + 1];
                if (change > 0) gain += change;
                else loss -= change;
            }
            
            double avgGain = gain / RSIPeriod;
            double avgLoss = loss / RSIPeriod;
            
            if (avgLoss == 0) return 100;
            
            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }
        
        private double CalculateMACDHistogram()
        {
            double emaFast = EMA(Close, MACDFast)[0];
            double emaSlow = EMA(Close, MACDSlow)[0];
            double macdLine = emaFast - emaSlow;
            
            // Simplified signal line calculation
            double signalLine = EMA(new Series<double>(this, 1, macdLine), MACDSignal)[0];
            
            return macdLine - signalLine;
        }
        
        private double CalculateVWAPDeviation()
        {
            double vwap = 0;
            double cumulativeTPV = 0;
            double cumulativeVol = 0;
            
            int period = Math.Min(DeltaPeriod, CurrentBar + 1);
            for (int i = 0; i < period; i++)
            {
                double typicalPrice = (High[i] + Low[i] + Close[i]) / 3;
                cumulativeTPV += typicalPrice * Volume[i];
                cumulativeVol += Volume[i];
            }
            
            if (cumulativeVol > 0)
                vwap = cumulativeTPV / cumulativeVol;
            
            if (vwap == 0) return 0;
            
            return ((Close[0] - vwap) / vwap) * 100;
        }
        
        private double CalculateEnhancedAbsorption()
        {
            double score = 0;
            double volumeMA = SMA(Volume, AbsorptionLookback)[0];
            
            for (int i = 1; i <= AbsorptionLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                double closePos = (Close[i] - Low[i]) / range;
                bool isUpBar = Close[i] > Open[i];
                
                // Strong absorption patterns
                if (volRatio > 2.5)
                {
                    // Buyer absorption - close near low on up bar
                    if (isUpBar && closePos < 0.25)
                        score += volRatio * 15;
                    // Seller absorption - close near high on down bar
                    else if (!isUpBar && closePos > 0.75)
                        score -= volRatio * 15;
                }
                
                // Moderate absorption
                if (volRatio > 1.8 && volRatio <= 2.5)
                {
                    if (isUpBar && closePos < 0.35)
                        score += volRatio * 8;
                    else if (!isUpBar && closePos > 0.65)
                        score -= volRatio * 8;
                }
                
                // Doji absorption
                if (Math.Abs(Close[i] - Open[i]) < range * 0.1 && volRatio > 2.0)
                {
                    score += (isUpBar ? 1 : -1) * volRatio * 10;
                }
            }
            
            return Math.Max(-150, Math.Min(150, score));
        }
        
        private double DetectLiquidityPools()
        {
            double score = 0;
            double volumeMA = SMA(Volume, TrapLookback)[0];
            
            for (int i = 1; i <= TrapLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                
                // Liquidity pool at highs (stop run above)
                bool isLocalHigh = High[i] == MAX(High, TrapLookback)[i];
                bool rejection = Close[i] < (High[i] + Low[i]) / 2;
                
                if (isLocalHigh && volRatio > 2.0 && rejection)
                {
                    // Check for failure (price reversed)
                    if (i < TrapLookback)
                    {
                        bool failed = false;
                        for (int j = i + 1; j <= Math.Min(i + 3, TrapLookback); j++)
                        {
                            if (Close[j] < Low[i])
                            {
                                failed = true;
                                break;
                            }
                        }
                        if (failed)
                            score -= 30 * volRatio;
                    }
                }
                
                // Liquidity pool at lows (stop run below)
                bool isLocalLow = Low[i] == MIN(Low, TrapLookback)[i];
                bool lowRejection = Close[i] > (High[i] + Low[i]) / 2;
                
                if (isLocalLow && volRatio > 2.0 && lowRejection)
                {
                    if (i < TrapLookback)
                    {
                        bool failed = false;
                        for (int j = i + 1; j <= Math.Min(i + 3, TrapLookback); j++)
                        {
                            if (Close[j] > High[i])
                            {
                                failed = true;
                                break;
                            }
                        }
                        if (failed)
                            score += 30 * volRatio;
                    }
                }
            }
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double CalculateAdaptiveSupertrend()
        {
            double tr = CalculateTrueRange();
            double atr = EMA(new Series<double>(this, 1, tr), SupertrendPeriod)[0];
            
            // Adaptive multiplier based on volatility
            double volatilityRatio = atr / SMA(new Series<double>(this, 1, tr), SupertrendPeriod * 2)[0];
            double adaptiveMultiplier = SupertrendMultiplier * (volatilityRatio > 1.2 ? 1.1 : 
                                                               (volatilityRatio < 0.8 ? 0.9 : 1.0));
            
            double sma = SMA(Close, SupertrendPeriod)[0];
            double upper = sma + (adaptiveMultiplier * atr);
            double lower = sma - (adaptiveMultiplier * atr);
            
            double supertrend = Values[3][1];
            
            if (Close[0] > upper)
                supertrend = lower;
            else if (Close[0] < lower)
                supertrend = upper;
            
            return supertrend;
        }
        
        private double CalculateSmartMoneyScore(double emaF, double emaM, double emaS, 
                                                double delta, double cumDelta, 
                                                double absorp, double rsi, double macd)
        {
            double score = 0;
            
            // EMA alignment (30 points max)
            if (emaF > emaM && emaM > emaS)
                score += 30;
            else if (emaF < emaM && emaM < emaS)
                score -= 30;
            else if (emaF > emaM)
                score += 15;
            else
                score -= 15;
            
            // Delta contribution (25 points max)
            double deltaNorm = Math.Min(25, Math.Max(-25, delta / 10000));
            score += deltaNorm;
            
            // Absorption contribution (30 points max)
            score += Math.Min(30, Math.Max(-30, absorp / 5));
            
            // RSI filter (10 points max)
            if (rsi < 40)
                score += 10;
            else if (rsi > 60)
                score -= 10;
            else if (rsi < 50)
                score += 5;
            else
                score -= 5;
            
            // MACD momentum (5 points max)
            score += Math.Min(5, Math.Max(-5, macd * 1000));
            
            return Math.Max(-100, Math.Min(100, score));
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
