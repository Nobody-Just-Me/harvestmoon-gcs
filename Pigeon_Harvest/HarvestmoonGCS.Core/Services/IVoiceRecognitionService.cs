using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services
{
    /// <summary>
    /// Interface untuk layanan pengenalan suara
    /// </summary>
    public interface IVoiceRecognitionService
    {
        /// <summary>
        /// Apakah layanan siap digunakan
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Alasan ketersediaan voice engine saat ini (untuk UI diagnostik).
        /// </summary>
        string AvailabilityReason { get; }

        /// <summary>
        /// Error terakhir dari engine pengenalan suara.
        /// </summary>
        string? LastError { get; }

        /// <summary>
        /// Bahasa STT aktif (contoh: id-ID, en-US).
        /// </summary>
        string Language { get; set; }

        /// <summary>
        /// Apakah sedang mendengarkan
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// Mulai mendengarkan perintah suara
        /// </summary>
        Task StartListeningAsync();

        /// <summary>
        /// Berhenti mendengarkan
        /// </summary>
        void StopListening();

        /// <summary>
        /// Event ketika perintah suara terdeteksi
        /// </summary>
        event VoiceCommandEventHandler? CommandRecognized;

        /// <summary>
        /// Event saat terjadi error pada engine voice recognition.
        /// </summary>
        event VoiceRecognitionErrorEventHandler? RecognitionError;
    }

    /// <summary>
    /// Delegate untuk event voice command
    /// </summary>
    public delegate void VoiceCommandEventHandler(object sender, VoiceCommandEventArgs e);
    public delegate void VoiceRecognitionErrorEventHandler(object sender, string error);

    /// <summary>
    /// Argument untuk event voice command
    /// </summary>
    public class VoiceCommandEventArgs : System.EventArgs
    {
        public string Command { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public float Confidence { get; set; }
    }
}
