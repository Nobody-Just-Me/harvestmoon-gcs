using System;
using System.IO;
using System.Threading.Tasks;

#if HAS_UNO_WINUI || WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
#endif

namespace HarvestmoonGCS.Core.Helpers;

/// <summary>
/// Screenshot helper for capturing UI elements.
/// Provides cross-platform screenshot functionality for Uno Platform.
/// </summary>
public static class Screenshot
{
#if HAS_UNO_WINUI || WINDOWS
    /// <summary>
    /// Captures a screenshot of the specified UIElement as a JPEG image
    /// </summary>
    /// <param name="source">UIElement to capture</param>
    /// <param name="scale">Scale factor for rendering (1.0 = original size)</param>
    /// <param name="quality">JPEG quality (0-100)</param>
    /// <returns>Byte array containing JPEG image data</returns>
    public static async Task<byte[]> GetJpgImageAsync(UIElement source, double scale = 1.0, int quality = 90)
    {
        try
        {
            // Get the actual size of the element
            var actualHeight = source.ActualSize.Y;
            var actualWidth = source.ActualSize.X;

            if (actualHeight <= 0 || actualWidth <= 0)
            {
                throw new InvalidOperationException("Element has no size to capture");
            }

            var renderWidth = (int)(actualWidth * scale);
            var renderHeight = (int)(actualHeight * scale);

            // Create a RenderTargetBitmap to capture the element
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(source, renderWidth, renderHeight);

            // Get the pixel buffer
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();

            // Create a temporary file to save the image
            var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                $"screenshot_{Guid.NewGuid()}.jpg",
                CreationCollisionOption.ReplaceExisting);

            using (var stream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Create encoder
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                
                // Set the pixel data
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    96.0,
                    96.0,
                    pixelBuffer.ToArray());

                // Set JPEG quality
                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(
                    quality / 100.0,
                    Windows.Foundation.PropertyType.Single);
                propertySet.Add("ImageQuality", qualityValue);
                await encoder.BitmapProperties.SetPropertiesAsync(propertySet);

                // Flush the encoder
                await encoder.FlushAsync();
            }

            // Read the file back as byte array
            using (var stream = await tempFile.OpenReadAsync())
            {
                var bytes = new byte[stream.Size];
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(bytes);
                }
                return bytes;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Screenshot] Error capturing screenshot: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Saves a screenshot of the specified UIElement to a file
    /// </summary>
    /// <param name="source">UIElement to capture</param>
    /// <param name="filePath">Path where the screenshot will be saved</param>
    /// <param name="scale">Scale factor for rendering (1.0 = original size)</param>
    /// <param name="quality">JPEG quality (0-100)</param>
    public static async Task SaveJpgImageAsync(UIElement source, string filePath, double scale = 1.0, int quality = 90)
    {
        var imageData = await GetJpgImageAsync(source, scale, quality);
        await File.WriteAllBytesAsync(filePath, imageData);
    }
#else
    /// <summary>
    /// Captures a screenshot of the specified UIElement as a JPEG image
    /// Note: Screenshot functionality is not available on this platform
    /// </summary>
    public static Task<byte[]> GetJpgImageAsync(object source, double scale = 1.0, int quality = 90)
    {
        System.Diagnostics.Debug.WriteLine("[Screenshot] Screenshot functionality not available on this platform");
        return Task.FromResult(Array.Empty<byte>());
    }

    /// <summary>
    /// Saves a screenshot of the specified UIElement to a file
    /// Note: Screenshot functionality is not available on this platform
    /// </summary>
    public static Task SaveJpgImageAsync(object source, string filePath, double scale = 1.0, int quality = 90)
    {
        System.Diagnostics.Debug.WriteLine("[Screenshot] Screenshot functionality not available on this platform");
        return Task.CompletedTask;
    }
#endif
}
