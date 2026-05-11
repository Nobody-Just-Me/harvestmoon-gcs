using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services.Calibration
{
    public class CompassCalibrator
    {
        private List<Vector3> _dataPoints = new();
        private HashSet<string> _gridFilter = new();
        private const int GridDiv = 20; // Same as MagCalib.cs
        private const int MaxPerCell = 3;
        
        public bool IsCalibrating { get; private set; }
        public int SampleCount => _dataPoints.Count;
        
        // Events for UI updates
        public event EventHandler<int>? OnProgress; // 0-100%
        public event EventHandler<Vector3>? OnResult;
        
        /// <summary>
        /// Process incoming magnetometer data from MAVLink RAW_IMU message
        /// </summary>
        public void OnRawIMUReceived(float magX, float magY, float magZ)
        {
            if (!IsCalibrating) return;
            
            // Grid-based filtering (same as MagCalib.cs)
            string cell = $"{(int)(magX / GridDiv)},{(int)(magY / GridDiv)},{(int)(magZ / GridDiv)}";
            int count = _gridFilter.Count(g => g == cell);
            if (count >= MaxPerCell) return;
            
            _gridFilter.Add(cell);
            _dataPoints.Add(new Vector3(magX, magY, magZ));
            
            // Update progress (100 samples = 100%)
            int progress = Math.Min(100, _dataPoints.Count);
            OnProgress?.Invoke(this, progress);
        }
        
        /// <summary>
        /// Start calibration - reset data and compass offsets
        /// </summary>
        public void StartCalibration()
        {
            _dataPoints.Clear();
            _gridFilter.Clear();
            IsCalibrating = true;
            
            // TODO: Reset compass offsets to 0
            // MavLinkService.SetParam("COMPASS_OFS_X", 0);
            // MavLinkService.SetParam("COMPASS_OFS_Y", 0);
            // MavLinkService.SetParam("COMPASS_OFS_Z", 0);
        }
        
        /// <summary>
        /// Stop calibration and save results
        /// </summary>
        public async Task<CalibrationResult> StopCalibrationAsync()
        {
            IsCalibrating = false;
            
            if (_dataPoints.Count < 30)
                return new CalibrationResult { Success = false, Error = "Too few samples (min 30)" };
            
            // Calculate offsets using simple averaging (good enough for MVP)
            // Full implementation would use Least Squares
            float sumX = 0, sumY = 0, sumZ = 0;
            foreach (var p in _dataPoints)
            {
                sumX += p.X;
                sumY += p.Y;
                sumZ += p.Z;
            }
            int n = _dataPoints.Count;
            var center = new Vector3(sumX / n, sumY / n, sumZ / n);
            
            // TODO: Save to autopilot (offset = -center)
            // await MavLinkService.SetParamAsync("COMPASS_OFS_X", -center.X);
            // await MavLinkService.SetParamAsync("COMPASS_OFS_Y", -center.Y);
            // await MavLinkService.SetParamAsync("COMPASS_OFS_Z", -center.Z);
            
            OnResult?.Invoke(this, center);
            
            return new CalibrationResult
            {
                Success = true,
                Offsets = center,
                SampleCount = _dataPoints.Count
            };
        }
        
        /// <summary>
        /// Cancel calibration without saving
        /// </summary>
        public void CancelCalibration()
        {
            IsCalibrating = false;
            _dataPoints.Clear();
            _gridFilter.Clear();
        }
    }
}
