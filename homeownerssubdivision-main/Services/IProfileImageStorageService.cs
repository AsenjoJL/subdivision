using Microsoft.AspNetCore.Http;

namespace HOMEOWNER.Services
{
    public interface IProfileImageStorageService
    {
        Task<string> UploadHomeownerProfileImageAsync(IFormFile file, int homeownerId, CancellationToken cancellationToken = default);
        Task<string> UploadStaffProfileImageAsync(IFormFile file, int staffId, CancellationToken cancellationToken = default);
    }
}
