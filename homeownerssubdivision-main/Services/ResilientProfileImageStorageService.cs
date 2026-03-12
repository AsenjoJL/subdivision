namespace HOMEOWNER.Services
{
    public class ResilientProfileImageStorageService : IProfileImageStorageService
    {
        private readonly SupabaseProfileImageStorageService _supabaseStorageService;
        private readonly ILogger<ResilientProfileImageStorageService> _logger;

        public ResilientProfileImageStorageService(
            SupabaseProfileImageStorageService supabaseStorageService,
            ILogger<ResilientProfileImageStorageService> logger)
        {
            _supabaseStorageService = supabaseStorageService;
            _logger = logger;
        }

        public async Task<string> UploadHomeownerProfileImageAsync(IFormFile file, int homeownerId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _supabaseStorageService.UploadHomeownerProfileImageAsync(file, homeownerId, cancellationToken);
            }
            catch (SupabaseStorageUploadException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supabase Storage upload failed for homeowner {HomeownerId}.",
                    homeownerId);

                throw;
            }
        }

        public async Task<string> UploadStaffProfileImageAsync(IFormFile file, int staffId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _supabaseStorageService.UploadStaffProfileImageAsync(file, staffId, cancellationToken);
            }
            catch (SupabaseStorageUploadException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supabase Storage upload failed for staff {StaffId}.",
                    staffId);

                throw;
            }
        }
    }
}
