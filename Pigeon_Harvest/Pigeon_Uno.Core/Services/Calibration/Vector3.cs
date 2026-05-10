namespace Pigeon_Uno.Core.Services.Calibration
{
    public struct Vector3
    {
        public float X, Y, Z;
        
        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public float Length()
        {
            return MathF.Sqrt(X * X + Y * Y + Z * Z);
        }
        
        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }
        
        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }
        
        public static Vector3 operator -(Vector3 a)
        {
            return new Vector3(-a.X, -a.Y, -a.Z);
        }
        
        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Z:F2})";
        }
    }
    
    public class CalibrationResult
    {
        public bool Success { get; set; }
        public Vector3 Offsets { get; set; }
        public int SampleCount { get; set; }
        public string? Error { get; set; }
    }
}
