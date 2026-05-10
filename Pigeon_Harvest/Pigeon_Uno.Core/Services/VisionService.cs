using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;
using System;

namespace Pigeon_Uno.Services;

public class VisionService
{
    public struct VegetationStats
    {
        public double HealthyPercentage;
        public double StressedPercentage;
        public double SoilPercentage;
    }

    public VegetationStats AnalyzeVegetation(string imagePath)
    {
        using var src = new Mat(imagePath);
        if (src.Empty()) return new VegetationStats();

        using var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

        // Define thresholds (Based on typical NDVI-like color ranges in HSV)
        // Green (Healthy): H=35-85
        // Yellow/Orange (Stressed): H=15-35
        // Brown (Soil): H=0-15 or H=160-180 (Reddish)

        // Healthy Vegetation
        using var maskHealthy = new Mat();
        Cv2.InRange(hsv, new Scalar(35, 50, 50), new Scalar(85, 255, 255), maskHealthy);
        
        // Stressed Vegetation
        using var maskStressed = new Mat();
        Cv2.InRange(hsv, new Scalar(15, 50, 50), new Scalar(35, 255, 255), maskStressed);

        // Soil
        using var maskSoil1 = new Mat();
        using var maskSoil2 = new Mat();
        using var maskSoil = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 30, 30), new Scalar(15, 255, 255), maskSoil1);
        Cv2.InRange(hsv, new Scalar(160, 30, 30), new Scalar(180, 255, 255), maskSoil2);
        Cv2.BitwiseOr(maskSoil1, maskSoil2, maskSoil);

        double totalPixels = src.Rows * src.Cols;
        double healthyCount = Cv2.CountNonZero(maskHealthy);
        double stressedCount = Cv2.CountNonZero(maskStressed);
        double soilCount = Cv2.CountNonZero(maskSoil);

        return new VegetationStats
        {
            HealthyPercentage = (healthyCount / totalPixels) * 100.0,
            StressedPercentage = (stressedCount / totalPixels) * 100.0,
            SoilPercentage = (soilCount / totalPixels) * 100.0
        };
    }
}
