namespace HOMEOWNER.Services
{
    public class SupabaseProfileImageStorageService : IProfileImageStorageService
    {
        private readonly IAppFileStorageService _fileStorageService;

        public SupabaseProfileImageStorageService(
            IAppFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public async Task<string> UploadHomeownerProfileImageAsync(IFormFile file, int homeownerId, CancellationToken cancellationToken = default)
        {
            return await _fileStorageService.UploadHomeownerProfileImageAsync(file, homeownerId, cancellationToken);
        }

        public async Task<string> UploadStaffProfileImageAsync(IFormFile file, int staffId, CancellationToken cancellationToken = default)
        {
            return await _fileStorageService.UploadStaffProfileImageAsync(file, staffId, cancellationToken);
        }
    }
}
