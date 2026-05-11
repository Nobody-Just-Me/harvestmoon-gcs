using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Lightweight Vegetation Analyzer for mobile/tablet devices
/// Uses HSV color analysis for real-time vegetation health monitoring
/// Optimized for low-end tablets (< 2GB RAM, budget processors)
/// </summary>
public class VegetationAnalyzerMobile
{
    #region Configuration
    
    // HSV ranges for vegetation classification
    private readonly (int hMin, int sMin, int vMin, int hMax, int sMax, int vMax) _healthyRange = 
        (35, 40, 40, 85, 255, 255);  // Green vegetation
    
    private readonly (int hMin, int sMin, int vMin, int hMax, int sMax, int vMax) _stressedRange = 
        (15, 30, 40, 35, 255, 255);  // Yellow/brown stressed vegetation
    
    private readonly (int hMin, int sMin, int vMin, int hMax, int sMax, int vMax) _soilRange = 
        (0, 10, 30, 20, 100, 200);   // Bare soil
    
    // Grid configuration for zone analysis
    private int _gridRows = 4;
    private int _gridCols = 4;
    
    // Performance optimization
    private int _frameSkipInterval = 3; // Process every 3rd frame
    private int _frameCounter = 0;
    
    // Stress threshold for alerts (percentage)
    private double _stressThreshold = 30.0;
    
    #endregion
    
    #region Data Classes
    
    public class VegetationResult
    {
        public double HealthyPercentage { get; set; }
        public double StressedPercentage { get; set; }
        public double BareSoilPercentage { get; set; }
        public VegetationClass OverallClassification { get; set; }
        public List<ZoneResult> Zones { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public bool RequiresIrrigation { get; set; }
    }
    
    public class ZoneResult
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public VegetationClass Classification { get; set; }
        public double HealthyPercentage { get; set; }
        public double StressedPercentage { get; set; }
        public double BareSoilPercentage { get; set; }
        public (int x, int y, int width, int height) BoundingBox { get; set; }
    }
    
    public enum VegetationClass
    {
        Healthy,        // Green, well-watered
        Stressed,       // Yellow/brown, needs water
        BareSoil,       // No vegetation
        Unknown
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Analyze frame for vegetation health (HSV-based, lightweight)
    /// </summary>
    public async Task<VegetationResult> AnalyzeFrameAsync(byte[] frameData, int width, int height)
    {
        // Frame skipping for performance
        _frameCounter++;
        if (_frameCounter % _frameSkipInterval != 0)
        {
            return null; // Skip this frame
        }
        
        return await Task.Run(() => AnalyzeFrameInternal(frameData, width, height));
    }
    
    /// <summary>
    /// Configure grid size for zone analysis
    /// </summary>
    public void SetGridSize(int rows, int cols)
    {
        _gridRows = Math.Max(2, Math.Min(8, rows));
        _gridCols = Math.Max(2, Math.Min(8, cols));
    }
    
    /// <summary>
    /// Set frame skip interval (higher = better performance, lower accuracy)
    /// </summary>
    public void SetFrameSkipInterval(int interval)
    {
        _frameSkipInterval = Math.Max(1, Math.Min(10, interval));
    }
    
    /// <summary>
    /// Set stress threshold for irrigation alerts
    /// </summary>
    public void SetStressThreshold(double threshold)
    {
        _stressThreshold = Math.Max(0, Math.Min(100, threshold));
    }
    
    #endregion
    
    #region Private Methods
    
    private VegetationResult AnalyzeFrameInternal(byte[] frameData, int width, int height)
    {
        var result = new VegetationResult
        {
            Timestamp = DateTime.Now
        };
        
        try
        {
            // Calculate zone dimensions
            int zoneWidth = width / _gridCols;
            int zoneHeight = height / _gridRows;
            
            int totalHealthyPixels = 0;
            int totalStressedPixels = 0;
            int totalSoilPixels = 0;
            int totalPixels = width * height;
            
            // Analyze each grid zone
            for (int row = 0; row < _gridRows; row++)
            {
                for (int col = 0; col < _gridCols; col++)
                {
                    var zoneResult = AnalyzeZone(
                        frameData, width, height,
                        col * zoneWidth, row * zoneHeight,
                        zoneWidth, zoneHeight
                    );
                    
                    zoneResult.Row = row;
                    zoneResult.Col = col;
                    result.Zones.Add(zoneResult);
                    
                    // Accumulate totals
                    int zonePixels = zoneWidth * zoneHeight;
                    totalHealthyPixels += (int)(zoneResult.HealthyPercentage * zonePixels / 100);
                    totalStressedPixels += (int)(zoneResult.StressedPercentage * zonePixels / 100);
                    totalSoilPixels += (int)(zoneResult.BareSoilPercentage * zonePixels / 100);
                }
            }
            
            // Calculate overall percentages
            result.HealthyPercentage = (totalHealthyPixels * 100.0) / totalPixels;
            result.StressedPercentage = (totalStressedPixels * 100.0) / totalPixels;
            result.BareSoilPercentage = (totalSoilPixels * 100.0) / totalPixels;
            
            // Determine overall classification
            if (result.HealthyPercentage > 60)
                result.OverallClassification = VegetationClass.Healthy;
            else if (result.StressedPercentage > 40)
                result.OverallClassification = VegetationClass.Stressed;
            else if (result.BareSoilPercentage > 50)
                result.OverallClassification = VegetationClass.BareSoil;
            else
                result.OverallClassification = VegetationClass.Unknown;
            
            // Check if irrigation is needed
            result.RequiresIrrigation = result.StressedPercentage > _stressThreshold;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Vegetation analysis error: {ex.Message}");
        }
        
        return result;
    }
    
    private ZoneResult AnalyzeZone(byte[] frameData, int frameWidth, int frameHeight,
                                   int zoneX, int zoneY, int zoneWidth, int zoneHeight)
    {
        var result = new ZoneResult
        {
            BoundingBox = (zoneX, zoneY, zoneWidth, zoneHeight)
        };
        
        int healthyPixels = 0;
        int stressedPixels = 0;
        int soilPixels = 0;
        int totalPixels = 0;
        
        // Analyze pixels in this zone (RGB to HSV conversion)
        for (int y = zoneY; y < zoneY + zoneHeight && y < frameHeight; y++)
        {
            for (int x = zoneX; x < zoneX + zoneWidth && x < frameWidth; x++)
            {
                int pixelIndex = (y * frameWidth + x) * 3; // RGB format
                
                if (pixelIndex + 2 >= frameData.Length)
                    continue;
                
                byte r = frameData[pixelIndex];
                byte g = frameData[pixelIndex + 1];
                byte b = frameData[pixelIndex + 2];
                
                // Convert RGB to HSV
                var (h, s, v) = RgbToHsv(r, g, b);
                
                // Classify pixel
                if (IsInRange(h, s, v, _healthyRange))
                    healthyPixels++;
                else if (IsInRange(h, s, v, _stressedRange))
                    stressedPixels++;
                else if (IsInRange(h, s, v, _soilRange))
                    soilPixels++;
                
                totalPixels++;
            }
        }
        
        // Calculate percentages
        if (totalPixels > 0)
        {
            result.HealthyPercentage = (healthyPixels * 100.0) / totalPixels;
            result.StressedPercentage = (stressedPixels * 100.0) / totalPixels;
            result.BareSoilPercentage = (soilPixels * 100.0) / totalPixels;
        }
        
        // Classify zone
        if (result.HealthyPercentage > 50)
            result.Classification = VegetationClass.Healthy;
        else if (result.StressedPercentage > 30)
            result.Classification = VegetationClass.Stressed;
        else if (result.BareSoilPercentage > 40)
            result.Classification = VegetationClass.BareSoil;
        else
            result.Classification = VegetationClass.Unknown;
        
        return result;
    }
    
    /// <summary>
    /// Convert RGB to HSV color space
    /// </summary>
    private (int h, int s, int v) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;
        
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;
        
        // Hue calculation
        double h = 0;
        if (delta != 0)
        {
            if (max == rd)
                h = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd)
                h = 60 * (((bd - rd) / delta) + 2);
            else
                h = 60 * (((rd - gd) / delta) + 4);
        }
        if (h < 0) h += 360;
        
        // Saturation calculation
        double s = (max == 0) ? 0 : (delta / max);
        
        // Value calculation
        double v = max;
        
        return ((int)(h / 2), (int)(s * 255), (int)(v * 255)); // OpenCV HSV format
    }
    
    /// <summary>
    /// Check if HSV values are within range
    /// </summary>
    private bool IsInRange(int h, int s, int v, 
                          (int hMin, int sMin, int vMin, int hMax, int sMax, int vMax) range)
    {
        return h >= range.hMin && h <= range.hMax &&
               s >= range.sMin && s <= range.sMax &&
               v >= range.vMin && v <= range.vMax;
    }
    
    #endregion
}
