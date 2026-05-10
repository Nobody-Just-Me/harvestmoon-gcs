using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services
{
    /// <summary>
    /// Interface untuk layanan alert audio
    /// </summary>
    public interface IAudioAlertService
    {
        /// <summary>
        /// Memainkan suara alert berdasarkan nama
        /// </summary>
        Task PlayAlertAsync(string alertName);

        /// <summary>
        /// Menghentikan alert yang sedang diputar
        /// </summary>
        void StopAlert();

        /// <summary>
        /// Mengatur volume alert (0.0 sampai 1.0)
        /// </summary>
        void SetVolume(double volume);

        /// <summary>
        /// Mendapatkan volume saat ini
        /// </summary>
        double GetVolume();

        /// <summary>
        /// Apakah sedang memainkan alert
        /// </summary>
        bool IsPlaying { get; }
    }
}
