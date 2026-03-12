namespace HOMEOWNER.Configuration
{
    public class AppFileStorageOptions
    {
        public string ProfileImagesPrefix { get; set; } = "homeowners";
        public string StaffProfileImagesPrefix { get; set; } = "staff";
        public string FacilitiesPrefix { get; set; } = "facilities";
        public string DocumentsPrefix { get; set; } = "documents";
        public string ForumPostMediaPrefix { get; set; } = "forum/posts/media";
        public string ForumPostMusicPrefix { get; set; } = "forum/posts/music";
        public string ForumCommentMediaPrefix { get; set; } = "forum/comments/media";
        public string ForumBackgroundsPrefix { get; set; } = "forum/backgrounds";
        public string ForumFeaturedMusicPrefix { get; set; } = "forum/featured-music";
        public string PaymentProofPrefix { get; set; } = "payments/proof";
    }
}
