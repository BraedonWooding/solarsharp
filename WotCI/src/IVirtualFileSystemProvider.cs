using System.IO;
using System.Threading.Tasks;

namespace WotCI
{
    /// <summary>
    /// Interface for virtual filesystem providers
    /// </summary>
    public interface IVirtualFileSystemProvider
    {
        Task<byte[]> ReadFileAsync(string relativePath);
        Task WriteFileAsync(string relativePath, byte[] content);
        Task<bool> ExistsAsync(string relativePath);
        Task<bool> IsDirectoryAsync(string relativePath);
        Task<string[]> ListDirectoryAsync(string relativePath);
        Task DeleteFileAsync(string relativePath);
        Task CreateDirectoryAsync(string relativePath);
        Task<Stream> OpenReadAsync(string relativePath);
        Task<Stream> OpenWriteAsync(string relativePath);
        Task<FileSystemInfo> GetFileInfoAsync(string relativePath);
    }
}