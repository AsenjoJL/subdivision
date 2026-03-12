using Microsoft.AspNetCore.Http;

namespace HOMEOWNER.Services
{
    public interface IAppFileStorageService
    {
        Task<string> UploadHomeownerProfileImageAsync(IFormFile file, int homeownerId, CancellationToken cancellationToken = default);
        Task<string> UploadStaffProfileImageAsync(IFormFile file, int staffId, CancellationToken cancellationToken = default);
        Task<string> UploadFacilityImageAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadDocumentAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadForumPostMediaAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadForumPostMusicAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadForumCommentMediaAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadForumBackgroundImageAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadForumFeaturedMusicAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<string> UploadPaymentProofAsync(IFormFile file, CancellationToken cancellationToken = default);
        bool IsManagedPublicUrl(string publicUrl);
        Task DeleteManagedFileAsync(string publicUrl, CancellationToken cancellationToken = default);
    }
}
