using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;

namespace Pigeon_Uno.Core.Helpers
{
    /// <summary>
    /// Hybrid Vegetation Analyzer: HSV color analysis + Grid classification + GPS tagging
    /// For drought/irrigation detection in agricultural fields
    /// </summary>
    public class VegetationAnalyzer : IDisposable
    {
        #region Enums and Classes

        public enum VegetationClass
        {
            Healthy,        // Green, normal vegetation
            Stressed,       // Yellow/brown, water deficit
            BareSoil,       // No vegetation
            Unknown
        }

        public class ZoneResult
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public VegetationClass Classification { get; set; }
            public double HealthyPercentage { get; set; }
            public double StressedPercentage { get; set; }
            public double BareSoilPercentage { get; set; }
            public Rect BoundingBox { get; set; }
        }

        public class VegetationStatistics
        {
            public double TotalHealthyPercentage { get; set; }
            public double TotalStressedPercentage { get; set; }
            public double TotalBareSoilPercentage { get; set; }
            public int HealthyZoneCount { get; set; }
            public int StressedZoneCount { get; set; }
            public int BareSoilZoneCount { get; set; }
            public int TotalZones { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class StressedZoneWaypoint
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Altitude { get; set; }
            public DateTime Timestamp { get; set; }
            public double StressPercentage { get; set; }
            public int ZoneRow { get; set; }
            public int ZoneCol { get; set; }
            public string ImagePath { get; set; }
            public VegetationClass Classification { get; set; }
        }

        #endregion

        #region Configuration

        // HSV ranges - will be adjusted based on illumination
        private Scalar _healthyLower = new Scalar(35, 40, 40);
        private Scalar _healthyUpper = new Scalar(85, 255, 255);
        private Scalar _stressedLower = new Scalar(15, 30, 40);
        private Scalar _stressedUpper = new Scalar(35, 255, 255);
        private Scalar _soilLower = new Scalar(0, 10, 30);
        private Scalar _soilUpper = new Scalar(20, 100, 200);

        // Grid configuration
        private int _gridRows = 4;
        private int _gridCols = 4;

        // Stress threshold for waypoint tagging (percentage)
        private double _stressThreshold = 30.0;

        // Heatmap colors (BGR format)
        private readonly Scalar _colorHealthy = new Scalar(0, 255, 0);    // Green
        private readonly Scalar _colorStressed = new Scalar(0, 165, 255); // Orange
        private readonly Scalar _colorBareSoil = new Scalar(0, 0, 255);   // Red
        private readonly Scalar _colorUnknown = new Scalar(128, 128, 128); // Gray

        // Waypoint storage
        private List<StressedZoneWaypoint> _waypoints = new List<StressedZoneWaypoint>();

        // Frame skip for performance
        private int _frameSkipCount = 0;
        private int _frameSkipInterval = 3; // Analyze every 3rd frame

        #endregion

        #region Properties

        public int GridRows
        {
            get => _gridRows;
            set => _gridRows = Math.Max(2, Math.Min(12, value));
        }

        public int GridCols
        {
            get => _gridCols;
            set => _gridCols = Math.Max(2, Math.Min(12, value));
        }

        public double StressThreshold
        {
            get => _stressThreshold;
            set => _stressThreshold = Math.Max(0, Math.Min(100, value));
        }

        public List<StressedZoneWaypoint> Waypoints => _waypoints;
        
        public int WaypointCount => _waypoints.Count;

        #endregion

        #region Public Methods

        /// <summary>
        /// Analyze frame and classify vegetation zones (with frame skipping for performance)
        /// </summary>
        public List<ZoneResult> AnalyzeFrame(Mat frame, bool forceAnalyze = false)
        {
            _frameSkipCount++;
            
            // Skip frames for performance unless forced
            if (!forceAnalyze && _frameSkipCount % _frameSkipInterval != 0)
                return null;

            return PerformAnalysis(frame);
        }

        /// <summary>
        /// Perform full analysis on frame
        /// </summary>
        private List<ZoneResult> PerformAnalysis(Mat frame)
        {
            var results = new List<ZoneResult>();

            if (frame == null || frame.Empty())
                return results;

            // Adjust HSV ranges based on illumination
            AdjustForIllumination(frame);

            int zoneWidth = frame.Width / _gridCols;
            int zoneHeight = frame.Height / _gridRows;

            for (int row = 0; row < _gridRows; row++)
            {
                for (int col = 0; col < _gridCols; col++)
                {
                    int x = col * zoneWidth;
                    int y = row * zoneHeight;

                    int w = (col == _gridCols - 1) ? frame.Width - x : zoneWidth;
                    int h = (row == _gridRows - 1) ? frame.Height - y : zoneHeight;

                    var roi = new Rect(x, y, w, h);
                    
                    using (var zoneFrame = new Mat(frame, roi))
                    {
                        var zoneResult = AnalyzeZone(zoneFrame, row, col, roi);
                        results.Add(zoneResult);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Generate heatmap overlay based on zone classifications
        /// </summary>
        public Mat GenerateHeatmapOverlay(Mat frame, List<ZoneResult> zones, double opacity = 0.35)
        {
            if (frame == null || frame.Empty() || zones == null || zones.Count == 0)
                return frame?.Clone();

            var overlay = frame.Clone();
            
            using (var heatmap = new Mat(frame.Size(), frame.Type(), Scalar.All(0)))
            {
                foreach (var zone in zones)
                {
                    Scalar color;
                    switch (zone.Classification)
                    {
                        case VegetationClass.Healthy: color = _colorHealthy; break;
                        case VegetationClass.Stressed: color = _colorStressed; break;
                        case VegetationClass.BareSoil: color = _colorBareSoil; break;
                        default: color = _colorUnknown; break;
                    }

                    Cv2.Rectangle(heatmap, zone.BoundingBox, color, -1);
                }

                Cv2.AddWeighted(frame, 1 - opacity, heatmap, opacity, 0, overlay);
            }

            // Draw grid lines and labels
            foreach (var zone in zones)
            {
                // White border
                Cv2.Rectangle(overlay, zone.BoundingBox, new Scalar(255, 255, 255), 1);

                // Zone label
                string label;
                switch (zone.Classification)
                {
                    case VegetationClass.Healthy: label = $"H:{zone.HealthyPercentage:F0}%"; break;
                    case VegetationClass.Stressed: label = $"S:{zone.StressedPercentage:F0}%"; break;
                    case VegetationClass.BareSoil: label = $"B:{zone.BareSoilPercentage:F0}%"; break;
                    default: label = "?"; break;
                }

                var textPos = new Point(zone.BoundingBox.X + 3, zone.BoundingBox.Y + 15);
                
                // Text shadow for readability
                Cv2.PutText(overlay, label, new Point(textPos.X + 1, textPos.Y + 1), 
                    HersheyFonts.HersheySimplex, 0.35, new Scalar(0, 0, 0), 1);
                Cv2.PutText(overlay, label, textPos, 
                    HersheyFonts.HersheySimplex, 0.35, new Scalar(255, 255, 255), 1);
            }

            return overlay;
        }

        /// <summary>
        /// Calculate overall statistics from zone results
        /// </summary>
        public VegetationStatistics CalculateStatistics(List<ZoneResult> zones)
        {
            var stats = new VegetationStatistics { Timestamp = DateTime.Now };

            if (zones == null || zones.Count == 0)
                return stats;

            stats.TotalZones = zones.Count;
            double totalHealthy = 0, totalStressed = 0, totalBareSoil = 0;

            foreach (var zone in zones)
            {
                totalHealthy += zone.HealthyPercentage;
                totalStressed += zone.StressedPercentage;
                totalBareSoil += zone.BareSoilPercentage;

                switch (zone.Classification)
                {
                    case VegetationClass.Healthy: stats.HealthyZoneCount++; break;
                    case VegetationClass.Stressed: stats.StressedZoneCount++; break;
                    case VegetationClass.BareSoil: stats.BareSoilZoneCount++; break;
                }
            }

            stats.TotalHealthyPercentage = totalHealthy / zones.Count;
            stats.TotalStressedPercentage = totalStressed / zones.Count;
            stats.TotalBareSoilPercentage = totalBareSoil / zones.Count;

            return stats;
        }

        /// <summary>
        /// Tag stressed zones with GPS coordinates
        /// </summary>
        public int TagStressedZones(List<ZoneResult> zones, double latitude, double longitude, double altitude)
        {
            if (zones == null) return 0;

            int taggedCount = 0;

            foreach (var zone in zones)
            {
                bool shouldTag = zone.StressedPercentage >= _stressThreshold || 
                                zone.Classification == VegetationClass.Stressed ||
                                zone.Classification == VegetationClass.BareSoil;

                if (shouldTag && latitude != 0 && longitude != 0) // Valid GPS
                {
                    // Check if similar waypoint already exists (within same zone)
                    bool exists = _waypoints.Any(w => 
                        w.ZoneRow == zone.Row && 
                        w.ZoneCol == zone.Col &&
                        Math.Abs(w.Latitude - latitude) < 0.00001 &&
                        Math.Abs(w.Longitude - longitude) < 0.00001);

                    if (!exists)
                    {
                        _waypoints.Add(new StressedZoneWaypoint
                        {
                            Latitude = latitude,
                            Longitude = longitude,
                            Altitude = altitude,
                            Timestamp = DateTime.Now,
                            StressPercentage = zone.StressedPercentage,
                            ZoneRow = zone.Row,
                            ZoneCol = zone.Col,
                            Classification = zone.Classification
                        });
                        taggedCount++;
                    }
                }
            }

            return taggedCount;
        }

        /// <summary>
        /// Clear all stored waypoints
        /// </summary>
        public void ClearWaypoints()
        {
            _waypoints.Clear();
        }

        /// <summary>
        /// Export waypoints to CSV file
        /// </summary>
        public bool ExportWaypointsToCSV(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Timestamp,Latitude,Longitude,Altitude,StressPercentage,ZoneRow,ZoneCol,Classification,Priority");

                    // Sort by stress percentage (highest first)
                    var sortedWaypoints = _waypoints.OrderByDescending(w => w.StressPercentage);

                    int priority = 1;
                    foreach (var wp in sortedWaypoints)
                    {
                        string priorityLevel = wp.StressPercentage > 60 ? "HIGH" : 
                                               wp.StressPercentage > 40 ? "MEDIUM" : "LOW";
                        
                        writer.WriteLine($"{wp.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                            $"{wp.Latitude:F8},{wp.Longitude:F8},{wp.Altitude:F2}," +
                            $"{wp.StressPercentage:F1},{wp.ZoneRow},{wp.ZoneCol}," +
                            $"{wp.Classification},{priorityLevel}");
                        priority++;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[VegetationAnalyzer] Exported {_waypoints.Count} waypoints to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VegetationAnalyzer] Export error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get irrigation recommendations based on analysis
        /// </summary>
        public List<string> GetIrrigationRecommendations(VegetationStatistics stats)
        {
            var recommendations = new List<string>();

            if (stats.TotalStressedPercentage > 50)
                recommendations.Add("⚠️ KRITIS: >50% area stress. Irigasi segera!");
            else if (stats.TotalStressedPercentage > 30)
                recommendations.Add("⚡ PRIORITAS: 30-50% stress. Jadwalkan irigasi 1-2 hari.");
            else if (stats.TotalStressedPercentage > 15)
                recommendations.Add("📋 MONITOR: 15-30% stress. Persiapkan irigasi.");
            else
                recommendations.Add("✅ NORMAL: Vegetasi mayoritas sehat.");

            if (_waypoints.Count > 0)
                recommendations.Add($"📍 {_waypoints.Count} titik stress terdeteksi dan di-tag GPS.");

            return recommendations;
        }

        #endregion

        #region Private Methods

        private ZoneResult AnalyzeZone(Mat zoneFrame, int row, int col, Rect boundingBox)
        {
            var result = new ZoneResult
            {
                Row = row,
                Col = col,
                BoundingBox = boundingBox,
                Classification = VegetationClass.Unknown
            };

            if (zoneFrame == null || zoneFrame.Empty())
                return result;

            try
            {
                using (var hsv = new Mat())
                using (var healthyMask = new Mat())
                using (var stressedMask = new Mat())
                using (var soilMask = new Mat())
                {
                    Cv2.CvtColor(zoneFrame, hsv, ColorConversionCodes.BGR2HSV);

                    Cv2.InRange(hsv, _healthyLower, _healthyUpper, healthyMask);
                    Cv2.InRange(hsv, _stressedLower, _stressedUpper, stressedMask);
                    Cv2.InRange(hsv, _soilLower, _soilUpper, soilMask);

                    int totalPixels = zoneFrame.Width * zoneFrame.Height;
                    int healthyPixels = Cv2.CountNonZero(healthyMask);
                    int stressedPixels = Cv2.CountNonZero(stressedMask);
                    int soilPixels = Cv2.CountNonZero(soilMask);

                    result.HealthyPercentage = (healthyPixels * 100.0) / totalPixels;
                    result.StressedPercentage = (stressedPixels * 100.0) / totalPixels;
                    result.BareSoilPercentage = (soilPixels * 100.0) / totalPixels;

                    // Apply ExG boost for better vegetation detection
                    double exgIndex = CalculateExGIndex(zoneFrame);
                    if (exgIndex > 25)
                        result.HealthyPercentage = Math.Min(100, result.HealthyPercentage * 1.15);
                    else if (exgIndex < 5)
                        result.HealthyPercentage *= 0.85;

                    // Classify based on dominant class
                    if (result.HealthyPercentage >= result.StressedPercentage && 
                        result.HealthyPercentage >= result.BareSoilPercentage &&
                        result.HealthyPercentage > 20)
                    {
                        result.Classification = VegetationClass.Healthy;
                    }
                    else if (result.StressedPercentage >= result.BareSoilPercentage &&
                             result.StressedPercentage > 15)
                    {
                        result.Classification = VegetationClass.Stressed;
                    }
                    else if (result.BareSoilPercentage > 20)
                    {
                        result.Classification = VegetationClass.BareSoil;
                    }
                    else
                    {
                        result.Classification = VegetationClass.Unknown;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VegetationAnalyzer] Zone error: {ex.Message}");
            }

            return result;
        }

        private void AdjustForIllumination(Mat frame)
        {
            try
            {
                using (var hsv = new Mat())
                {
                    Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);
                    var channels = Cv2.Split(hsv);
                    double vMean = Cv2.Mean(channels[2]).Val0;
                    foreach (var ch in channels) ch.Dispose();

                    if (vMean < 60) // Dark
                    {
                        _healthyLower = new Scalar(30, 50, 30);
                        _healthyUpper = new Scalar(90, 255, 200);
                        _stressedLower = new Scalar(10, 30, 30);
                        _stressedUpper = new Scalar(35, 255, 200);
                    }
                    else if (vMean > 200) // Bright
                    {
                        _healthyLower = new Scalar(35, 30, 100);
                        _healthyUpper = new Scalar(85, 255, 255);
                        _stressedLower = new Scalar(15, 20, 100);
                        _stressedUpper = new Scalar(40, 255, 255);
                    }
                    else // Normal
                    {
                        _healthyLower = new Scalar(35, 40, 40);
                        _healthyUpper = new Scalar(85, 255, 255);
                        _stressedLower = new Scalar(15, 30, 40);
                        _stressedUpper = new Scalar(35, 255, 255);
                    }
                }
            }
            catch { /* Use default values */ }
        }

        private double CalculateExGIndex(Mat frame)
        {
            try
            {
                var channels = Cv2.Split(frame);
                if (channels.Length < 3) return 0;

                using (var exg = new Mat())
                using (var exgFinal = new Mat())
                {
                    // ExG = 2*G - R - B
                    Cv2.AddWeighted(channels[1], 2.0, channels[2], -1.0, 0, exg, MatType.CV_32F);
                    Cv2.AddWeighted(exg, 1.0, channels[0], -1.0, 0, exgFinal, MatType.CV_32F);
                    double mean = Cv2.Mean(exgFinal).Val0;
                    foreach (var ch in channels) ch.Dispose();
                    return mean;
                }
            }
            catch { return 0; }
        }

        #endregion

        public void Dispose()
        {
            _waypoints.Clear();
        }
    }
}
