using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services
{
    /// <summary>
    /// Interface untuk layanan platform-specific
    /// </summary>
    public interface IPlatformService
    {
        /// <summary>
        /// Memilih file menggunakan dialog
        /// </summary>
        Task<string?> SelectFileAsync(FileDialogOptions options);

        /// <summary>
        /// Memilih folder menggunakan dialog
        /// </summary>
        Task<string?> SelectFolderAsync();

        /// <summary>
        /// Membuka file dengan aplikasi default
        /// </summary>
        Task<bool> OpenFileWithDefaultAppAsync(string filePath);

        /// <summary>
        /// Menampilkan file di file manager
        /// </summary>
        Task<bool> RevealInFileManagerAsync(string filePath);

        /// <summary>
        /// Mendapatkan path data aplikasi
        /// </summary>
        string GetApplicationDataPath();

        /// <summary>
        /// Mendapatkan path temporary
        /// </summary>
        string GetTempPath();

        /// <summary>
        /// Mendapatkan nama platform
        /// </summary>
        string GetPlatformName();
    }

    /// <summary>
    /// Opsi untuk dialog pemilihan file
    /// </summary>
    public class FileDialogOptions
    {
        public string Title { get; set; } = string.Empty;
        public string? InitialDirectory { get; set; }
        public List<FileDialogFilter> Filters { get; set; } = new List<FileDialogFilter>();
        public bool AllowMultiple { get; set; }
    }

    /// <summary>
    /// Filter untuk dialog file
    /// </summary>
    public class FileDialogFilter
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Extensions { get; set; } = new List<string>();
    }
}
