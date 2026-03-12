using Microsoft.AspNetCore.Http;

namespace HOMEOWNER.Services
{
    public interface ISupabaseObjectStorageService
    {
        Task<string> UploadFileAsync(
            IFormFile file,
            string objectPrefix,
            string fileNamePrefix,
            CancellationToken cancellationToken = default);

        Task DeleteFileByPublicUrlAsync(string publicUrl, CancellationToken cancellationToken = default);
    }
}
