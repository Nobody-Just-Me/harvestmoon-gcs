using OpenCvSharp;

namespace HarvestmoonGCS.Core.Models
{
    /// <summary>
    /// Represents a single object detection result from YOLO
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// The class ID of the detected object
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>
        /// The class name of the detected object (e.g., "person", "car")
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score of the detection (0.0 to 1.0)
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Bounding box coordinates of the detected object
        /// </summary>
        public Rect BoundingBox { get; set; }
    }
}
