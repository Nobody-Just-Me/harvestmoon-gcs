using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace HarvestmoonGCS.Controls.Avionics
{
    /// <summary>
    /// Helper class to load avionics instrument images
    /// </summary>
    public static class AvionicsImageLoader
    {
        private static readonly string AssetsPath = "ms-appx:///Assets/avionics/";

        /// <summary>
        /// Load a bitmap from the Assets/Avionics folder
        /// </summary>
        public static async Task<SKBitmap> LoadBitmapAsync(string filename)
        {
            try
            {
                var uri = new Uri($"{AssetsPath}{filename}");
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    return SKBitmap.Decode(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load bitmap {filename}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load a bitmap synchronously (use with caution, prefer async version)
        /// </summary>
        public static SKBitmap LoadBitmap(string filename)
        {
            try
            {
                // This is a simplified synchronous version
                // In production, you should use the async version
                var task = LoadBitmapAsync(filename);
                task.Wait();
                return task.Result;
            }
            catch
            {
                return null;
            }
        }
    }
}
