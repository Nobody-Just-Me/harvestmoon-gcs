using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Core.Helpers
{
    /// <summary>
    /// YOLOv8n-compatible object detector using ONNX Runtime with optional CUDA support.
    /// </summary>
    public class YoloDetector : IDisposable
    {
        private InferenceSession _session;
        private string[] _classNames;
        private int _inputWidth = 640;
        private int _inputHeight = 640;
        private float _confThreshold = 0.25f; // Lowered from 0.35 so more detections surface on the dashboard overlay
        private float _nmsThreshold = 0.5f;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;
        public string[] ClassNames => _classNames;
        public InferenceSession Session => _session;

        /// <summary>
        /// Initialize YOLO detector with model and class names
        /// </summary>
        /// <param name="modelPath">Path to .onnx model file</param>
        /// <param name="classNamesPath">Path to class names file</param>
        /// <param name="useCuda">Use CUDA GPU acceleration</param>
        public bool Initialize(string modelPath, string classNamesPath, bool useCuda = true)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    Serilog.Log.Error("[YoloDetector] Model file not found: {Path}", modelPath);
                    return false;
                }

                if (!File.Exists(classNamesPath))
                {
                    Serilog.Log.Error("[YoloDetector] Class names file not found: {Path}", classNamesPath);
                    return false;
                }

                // Load class names
                _classNames = File.ReadAllLines(classNamesPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();

                Serilog.Log.Information("[YoloDetector] Loaded {Count} class names", _classNames.Length);

                // Create session options with multi-threading
                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                sessionOptions.IntraOpNumThreads = Environment.ProcessorCount; // Use all CPU cores
                sessionOptions.InterOpNumThreads = Environment.ProcessorCount;

                // Try CUDA (GPU) first, but only when the user explicitly opts in via the
                // PIGEON_YOLO_USE_CUDA environment variable. AppendExecutionProvider_CUDA on a
                // CPU-only Linux box throws and sometimes leaves the session options in a state
                // that prevents InferenceSession from loading at all, which is why the dashboard
                // was falling back to OpenCV ("Yolo Fallback" pill).
                bool cudaRequested = useCuda && string.Equals(
                    Environment.GetEnvironmentVariable("PIGEON_YOLO_USE_CUDA"), "1", StringComparison.Ordinal);
                if (cudaRequested)
                {
                    try
                    {
                        sessionOptions.AppendExecutionProvider_CUDA(0);
                        System.Diagnostics.Debug.WriteLine("[YoloDetector] CUDA execution provider enabled - using GPU");
                    }
                    catch (Exception cudaEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YoloDetector] CUDA requested but unavailable: {cudaEx.Message}. Falling back to CPU.");
                        // Recreate options to discard any partial CUDA state that might block CPU execution.
                        sessionOptions.Dispose();
                        sessionOptions = new SessionOptions
                        {
                            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                            IntraOpNumThreads = Environment.ProcessorCount,
                            InterOpNumThreads = Environment.ProcessorCount
                        };
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[YoloDetector] Using CPU execution provider (default)");
                }

                // Create inference session
                Serilog.Log.Information("[YoloDetector] Creating InferenceSession for: {Path}", modelPath);
                _session = new InferenceSession(modelPath, sessionOptions);
                _isInitialized = true;

                Serilog.Log.Information("[YoloDetector] Initialized successfully with {Count} classes", _classNames.Length);
                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[YoloDetector] Initialization failed");
                return false;
            }
        }

        /// <summary>
        /// Set confidence threshold
        /// </summary>
        public void SetConfidenceThreshold(float threshold)
        {
            _confThreshold = Math.Max(0.1f, Math.Min(1.0f, threshold));
        }

        /// <summary>
        /// Detect objects in frame
        /// </summary>
        public List<DetectionResult> Detect(Mat frame)
        {
            var results = new List<DetectionResult>();

            if (!_isInitialized || frame == null || frame.Empty())
                return results;

            try
            {
                // Preprocess image
                var inputTensor = PreprocessImage(frame);

                // Run inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                using (var outputs = _session.Run(inputs))
                {
                    // Process outputs
                    var output = outputs.First().AsTensor<float>();
                    results = PostProcess(output, frame.Width, frame.Height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YoloDetector] Detection error: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Draw detection results on frame
        /// </summary>
        public Mat DrawDetections(Mat frame, List<DetectionResult> detections)
        {
            // Clone frame to prevent issues with frame reuse
            var output = frame.Clone();

            foreach (var det in detections)
            {
                // Get color based on class ID
                var color = GetClassColor(det.ClassId);

                // Draw bounding box
                Cv2.Rectangle(output, det.BoundingBox, color, 2);

                // Draw label background
                string label = $"{det.ClassName} {det.Confidence:P0}";
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 2, out int baseline);
                var labelRect = new Rect(
                    det.BoundingBox.X,
                    det.BoundingBox.Y - textSize.Height - 10,
                    textSize.Width + 10,
                    textSize.Height + 10
                );
                Cv2.Rectangle(output, labelRect, color, -1);

                // Draw label text
                Cv2.PutText(output, label,
                    new Point(det.BoundingBox.X + 5, det.BoundingBox.Y - 5),
                    HersheyFonts.HersheySimplex, 0.6, Scalar.White, 2);
            }

            return output;
        }

        private DenseTensor<float> PreprocessImage(Mat frame)
        {
            // Resize to input size
            var resized = new Mat();
            Cv2.Resize(frame, resized, new Size(_inputWidth, _inputHeight));

            // Convert BGR to RGB
            var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            // Create tensor
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

            // OPTIMIZED: Use GetArray for direct memory access (10-20x faster than At<T>)
            rgb.GetArray(out Vec3b[] pixels);
            
            // Use parallel processing for faster conversion
            int width = _inputWidth;
            System.Threading.Tasks.Parallel.For(0, _inputHeight, y =>
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    var pixel = pixels[rowStart + x];
                    tensor[0, 0, y, x] = pixel.Item0 * 0.00392156862f; // /255.0f optimized
                    tensor[0, 1, y, x] = pixel.Item1 * 0.00392156862f;
                    tensor[0, 2, y, x] = pixel.Item2 * 0.00392156862f;
                }
            });

            resized.Dispose();
            rgb.Dispose();

            return tensor;
        }

        /// <summary>
        /// Public wrapper for PostProcess so external callers (e.g. SkiaSharp-based pipeline)
        /// can run post-processing on raw ONNX output without going through the OpenCvSharp Detect path.
        /// </summary>
        public List<DetectionResult> PostProcessPublic(Tensor<float> output, int originalWidth, int originalHeight)
            => PostProcess(output, originalWidth, originalHeight);

        private List<DetectionResult> PostProcess(Tensor<float> output, int originalWidth, int originalHeight)
        {
            var results = new List<DetectionResult>();
            
            // YOLOv8n output shape: [1, 84, 8400] for COCO (84 = 4 bbox + 80 classes).
            int numClasses = _classNames.Length;
            int numDetections = output.Dimensions[2];

            float xScale = (float)originalWidth / _inputWidth;
            float yScale = (float)originalHeight / _inputHeight;

            // OPTIMIZED: Convert tensor to array for faster access
            float[] outputArray = output.ToArray();
            int stride = numDetections; // Each row has numDetections elements

            // Pre-filter candidates in parallel
            var candidates = new System.Collections.Concurrent.ConcurrentBag<(int idx, float score, int classId)>();
            
            System.Threading.Tasks.Parallel.For(0, numDetections, i =>
            {
                // Quick check: find max class score
                float maxScore = 0;
                int maxClassId = 0;

                int baseIdx = (4 * stride) + i; // Start at class scores
                for (int c = 0; c < numClasses; c++)
                {
                    float score = outputArray[baseIdx + (c * stride)];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxClassId = c;
                    }
                }

                if (maxScore > _confThreshold)
                {
                    candidates.Add((i, maxScore, maxClassId));
                }
            });

            // Extract bounding boxes only for candidates
            var boxes = new List<Rect>();
            var confidences = new List<float>();
            var classIds = new List<int>();

            foreach (var (i, maxScore, maxClassId) in candidates)
            {
                // Get bounding box (center x, center y, width, height)
                float cx = outputArray[i];
                float cy = outputArray[stride + i];
                float w = outputArray[(2 * stride) + i];
                float h = outputArray[(3 * stride) + i];

                // Convert to top-left corner
                int x = (int)((cx - w / 2) * xScale);
                int y = (int)((cy - h / 2) * yScale);
                int boxWidth = (int)(w * xScale);
                int boxHeight = (int)(h * yScale);

                // Clamp to image bounds
                x = Math.Max(0, Math.Min(x, originalWidth - 1));
                y = Math.Max(0, Math.Min(y, originalHeight - 1));
                boxWidth = Math.Min(boxWidth, originalWidth - x);
                boxHeight = Math.Min(boxHeight, originalHeight - y);

                if (boxWidth > 0 && boxHeight > 0)
                {
                    boxes.Add(new Rect(x, y, boxWidth, boxHeight));
                    confidences.Add(maxScore);
                    classIds.Add(maxClassId);
                }
            }

            // Apply Non-Maximum Suppression
            if (boxes.Count > 0)
            {
                var selectedIndices = ApplyNMS(boxes, confidences, _nmsThreshold);

                foreach (int idx in selectedIndices)
                {
                    results.Add(new DetectionResult
                    {
                        ClassId = classIds[idx],
                        ClassName = classIds[idx] < _classNames.Length ? _classNames[classIds[idx]] : "unknown",
                        Confidence = confidences[idx],
                        BoundingBox = boxes[idx]
                    });
                }
            }

            return results;
        }

        private List<int> ApplyNMS(List<Rect> boxes, List<float> confidences, float nmsThreshold)
        {
            var indices = new List<int>();
            var sortedIndices = confidences
                .Select((conf, idx) => new { Confidence = conf, Index = idx })
                .OrderByDescending(x => x.Confidence)
                .Select(x => x.Index)
                .ToList();

            var suppressed = new bool[boxes.Count];

            foreach (int i in sortedIndices)
            {
                if (suppressed[i]) continue;

                indices.Add(i);
                var boxA = boxes[i];

                for (int j = 0; j < boxes.Count; j++)
                {
                    if (i == j || suppressed[j]) continue;

                    var boxB = boxes[j];
                    float iou = ComputeIoU(boxA, boxB);

                    if (iou > nmsThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }

            return indices;
        }

        private float ComputeIoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int areaA = a.Width * a.Height;
            int areaB = b.Width * b.Height;
            int unionArea = areaA + areaB - intersectionArea;

            return unionArea > 0 ? (float)intersectionArea / unionArea : 0;
        }

        private Scalar GetClassColor(int classId)
        {
            // Generate consistent color for each class
            var colors = new Scalar[]
            {
                new Scalar(0, 255, 0),    // Green
                new Scalar(255, 0, 0),    // Blue
                new Scalar(0, 0, 255),    // Red
                new Scalar(255, 255, 0),  // Cyan
                new Scalar(255, 0, 255),  // Magenta
                new Scalar(0, 255, 255),  // Yellow
                new Scalar(128, 255, 0),  // Lime
                new Scalar(255, 128, 0),  // Orange
                new Scalar(128, 0, 255),  // Purple
                new Scalar(0, 128, 255),  // Light Blue
            };
            return colors[classId % colors.Length];
        }

        public void Dispose()
        {
            _session?.Dispose();
            _isInitialized = false;
        }
    }
}
