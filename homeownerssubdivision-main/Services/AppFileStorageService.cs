using HOMEOWNER.Configuration;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class AppFileStorageService : IAppFileStorageService
    {
        private readonly ISupabaseObjectStorageService _objectStorageService;
        private readonly AppFileStorageOptions _fileOptions;
        private readonly SupabaseStorageOptions _storageOptions;

        public AppFileStorageService(
            ISupabaseObjectStorageService objectStorageService,
            IOptions<AppFileStorageOptions> fileOptions,
            IOptions<SupabaseStorageOptions> storageOptions)
        {
            _objectStorageService = objectStorageService;
            _fileOptions = fileOptions.Value;
            _storageOptions = storageOptions.Value;
        }

        public Task<string> UploadHomeownerProfileImageAsync(IFormFile file, int homeownerId, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolveProfileImagesPrefix(),
                $"homeowner_{homeownerId}",
                cancellationToken);
        }

        public Task<string> UploadStaffProfileImageAsync(IFormFile file, int staffId, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolveStaffProfileImagesPrefix(),
                $"staff_{staffId}",
                cancellationToken);
        }

        public Task<string> UploadFacilityImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.FacilitiesPrefix, "facilities"),
                "facility",
                cancellationToken);
        }

        public Task<string> UploadDocumentAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.DocumentsPrefix, "documents"),
                "document",
                cancellationToken);
        }

        public Task<string> UploadForumPostMediaAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.ForumPostMediaPrefix, "forum/posts/media"),
                "forum-post-media",
                cancellationToken);
        }

        public Task<string> UploadForumPostMusicAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.ForumPostMusicPrefix, "forum/posts/music"),
                "forum-post-music",
                cancellationToken);
        }

        public Task<string> UploadForumCommentMediaAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.ForumCommentMediaPrefix, "forum/comments/media"),
                "forum-comment-media",
                cancellationToken);
        }

        public Task<string> UploadForumBackgroundImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.ForumBackgroundsPrefix, "forum/backgrounds"),
                "forum-background",
                cancellationToken);
        }

        public Task<string> UploadForumFeaturedMusicAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.ForumFeaturedMusicPrefix, "forum/featured-music"),
                "forum-featured-music",
                cancellationToken);
        }

        public Task<string> UploadPaymentProofAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.UploadFileAsync(
                file,
                ResolvePrefix(_fileOptions.PaymentProofPrefix, "payments/proof"),
                "payment-proof",
                cancellationToken);
        }

        public bool IsManagedPublicUrl(string publicUrl)
        {
            if (string.IsNullOrWhiteSpace(publicUrl))
            {
                return false;
            }

            var projectUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? _storageOptions.ProjectUrl;
            var bucketName = string.IsNullOrWhiteSpace(_storageOptions.BucketName) ? "profile-images" : _storageOptions.BucketName.Trim();
            if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri) ||
                !Uri.TryCreate(projectUrl?.Trim().TrimEnd('/'), UriKind.Absolute, out var projectUri))
            {
                return false;
            }

            var managedPrefix = $"/storage/v1/object/public/{Uri.EscapeDataString(bucketName)}/";
            return string.Equals(publicUri.Host, projectUri.Host, StringComparison.OrdinalIgnoreCase)
                && publicUri.AbsolutePath.StartsWith(managedPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public Task DeleteManagedFileAsync(string publicUrl, CancellationToken cancellationToken = default)
        {
            return _objectStorageService.DeleteFileByPublicUrlAsync(publicUrl, cancellationToken);
        }

        private string ResolveProfileImagesPrefix()
        {
            var configured = ResolvePrefix(_fileOptions.ProfileImagesPrefix, null);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return ResolvePrefix(_storageOptions.ObjectPrefix, "homeowners");
        }

        private string ResolveStaffProfileImagesPrefix()
        {
            var configured = ResolvePrefix(_fileOptions.StaffProfileImagesPrefix, null);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return ResolvePrefix(_storageOptions.ObjectPrefix, "staff");
        }

        private static string ResolvePrefix(string? configuredPrefix, string? fallbackPrefix)
        {
            var selected = string.IsNullOrWhiteSpace(configuredPrefix) ? fallbackPrefix : configuredPrefix;
            return string.IsNullOrWhiteSpace(selected)
                ? string.Empty
                : selected.Trim().Trim('/').Replace("\\", "/");
        }
    }
}
