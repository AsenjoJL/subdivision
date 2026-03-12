using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner,Admin,Staff")]
    public class ForumController : BaseController
    {
        private readonly IAppFileStorageService _fileStorageService;

        public ForumController(IDataService data, IAppFileStorageService fileStorageService) : base(data)
        {
            _fileStorageService = fileStorageService;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await BuildForumViewModelAsync(showBackButton: true);

            if (IsAjaxRequest())
            {
                return PartialView("_ForumPartial", viewModel);
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Embedded()
        {
            var viewModel = await BuildForumViewModelAsync(showBackButton: false);
            return PartialView("_ForumPartial", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Feed()
        {
            var posts = await _data.GetForumPostsAsync();
            return PartialView("_ForumFeedPartial", new ForumFeedViewModel
            {
                Posts = posts,
                CanParticipate = CanParticipate()
            });
        }

        [HttpGet]
        public async Task<IActionResult> PostCard(int id)
        {
            var model = await BuildForumPostCardModelAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            return PartialView("_ForumPostCardPartial", model);
        }

        [HttpGet]
        public async Task<IActionResult> FeedState()
        {
            var latestActivity = await _data.GetLatestForumActivityAsync();
            var latestActivityTicks = latestActivity?.ToUniversalTime().Ticks ?? 0;

            return Json(new
            {
                success = true,
                latestActivityTicks
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromForm] string title, [FromForm] string content, IFormFile? mediaFile, IFormFile? musicFile)
        {
            if (!CanParticipate())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Post content is required.");
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId <= 0)
            {
                return Unauthorized();
            }

            var post = new ForumPost
            {
                Title = title?.Trim() ?? string.Empty,
                Content = content.Trim(),
                HomeownerID = homeownerId,
                CreatedAt = DateTime.UtcNow
            };

            if (mediaFile != null && mediaFile.Length > 0)
            {
                const int maxMediaSize = 20 * 1024 * 1024;
                if (mediaFile.Length > maxMediaSize)
                {
                    return BadRequest("Media file exceeds 20MB limit.");
                }

                post.MediaUrl = await _fileStorageService.UploadForumPostMediaAsync(mediaFile, HttpContext.RequestAborted);
                post.MediaType = mediaFile.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase) ? "image" :
                                 mediaFile.ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase) ? "video" : null;
            }

            if (musicFile != null && musicFile.Length > 0)
            {
                const int maxMusicSize = 10 * 1024 * 1024;
                if (musicFile.Length > maxMusicSize)
                {
                    return BadRequest("Music file exceeds 10MB limit.");
                }

                post.MusicUrl = await _fileStorageService.UploadForumPostMusicAsync(musicFile, HttpContext.RequestAborted);
                post.MusicTitle = Path.GetFileNameWithoutExtension(musicFile.FileName);
            }

            try
            {
                await _data.AddForumPostAsync(post);
                return Ok(new { success = true, postId = post.ForumPostID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error creating post: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string commentText, IFormFile? mediaFile)
        {
            if (!CanParticipate())
            {
                return Unauthorized();
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId <= 0)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(commentText))
            {
                return BadRequest("Comment text is required.");
            }

            var comment = new ForumComment
            {
                ForumPostID = postId,
                HomeownerID = homeownerId,
                CommentText = commentText.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            if (mediaFile != null && mediaFile.Length > 0)
            {
                comment.MediaUrl = await _fileStorageService.UploadForumCommentMediaAsync(mediaFile, HttpContext.RequestAborted);
            }

            await _data.AddForumCommentAsync(comment);

            if (IsAjaxRequest())
            {
                var model = await BuildForumPostCardModelAsync(postId);
                if (model == null)
                {
                    return NotFound();
                }

                return PartialView("_ForumPostCardPartial", model);
            }

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AddReaction(int postId, string reactionType)
        {
            if (!CanParticipate())
            {
                return Unauthorized();
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId <= 0)
            {
                return Unauthorized();
            }

            var existingReaction = _data.Reactions
                .FirstOrDefault(r => r.ForumPostID == postId && r.HomeownerID == homeownerId);

            if (existingReaction == null)
            {
                var reaction = new Reaction
                {
                    ForumPostID = postId,
                    HomeownerID = homeownerId,
                    ReactionType = reactionType,
                    CreatedAt = DateTime.UtcNow
                };

                await _data.AddReactionAsync(reaction);
            }

            if (IsAjaxRequest())
            {
                var model = await BuildForumPostCardModelAsync(postId);
                if (model == null)
                {
                    return NotFound();
                }

                return PartialView("_ForumPostCardPartial", model);
            }

            return Ok(new { success = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBackground(IFormFile? backgroundImage, string? customCSS)
        {
            var settings = await GetOrCreateCommunitySettingsAsync();

            if (backgroundImage != null && backgroundImage.Length > 0)
            {
                if (!backgroundImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Only image files can be used as the forum background.");
                }

                const int maxBackgroundSize = 10 * 1024 * 1024;
                if (backgroundImage.Length > maxBackgroundSize)
                {
                    return BadRequest("Background image exceeds the 10MB limit.");
                }

                settings.BackgroundImageUrl = await _fileStorageService.UploadForumBackgroundImageAsync(backgroundImage, HttpContext.RequestAborted);
            }

            settings.CustomCSS = string.IsNullOrWhiteSpace(customCSS) ? null : customCSS.Trim();
            settings.LastUpdated = DateTime.UtcNow;

            await _data.AddOrUpdateCommunitySettingsAsync(settings);
            return Json(new
            {
                success = true,
                backgroundImageUrl = settings.BackgroundImageUrl,
                customCSS = settings.CustomCSS ?? string.Empty
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFeaturedMusic(IFormFile? musicFile)
        {
            if (musicFile == null || musicFile.Length == 0)
            {
                return BadRequest("Please choose an audio file to feature.");
            }

            if (!musicFile.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only audio files can be used as featured music.");
            }

            const int maxMusicSize = 15 * 1024 * 1024;
            if (musicFile.Length > maxMusicSize)
            {
                return BadRequest("Featured music exceeds the 15MB limit.");
            }

            var settings = await GetOrCreateCommunitySettingsAsync();
            settings.FeaturedMusicUrl = await _fileStorageService.UploadForumFeaturedMusicAsync(musicFile, HttpContext.RequestAborted);
            settings.LastUpdated = DateTime.UtcNow;

            await _data.AddOrUpdateCommunitySettingsAsync(settings);
            return Json(new { success = true, musicUrl = settings.FeaturedMusicUrl });
        }

        private async Task<ForumViewModel> BuildForumViewModelAsync(bool showBackButton)
        {
            var settingsTask = GetOrCreateCommunitySettingsAsync();
            var latestActivityTask = _data.GetLatestForumActivityAsync();
            var isAdmin = User.IsInRole("Admin");

            await Task.WhenAll(settingsTask, latestActivityTask);

            return new ForumViewModel
            {
                Settings = settingsTask.Result,
                CanManageSettings = isAdmin,
                CanParticipate = CanParticipate(),
                ShowBackButton = showBackButton,
                BackUrl = GetBackUrl(),
                LatestActivityTicks = latestActivityTask.Result?.ToUniversalTime().Ticks ?? 0
            };
        }

        private async Task<Tuple<ForumPost, bool>?> BuildForumPostCardModelAsync(int postId)
        {
            var post = await _data.GetForumPostByIdAsync(postId);
            if (post == null)
            {
                return null;
            }

            return Tuple.Create(post, CanParticipate());
        }

        private async Task<CommunitySettings> GetOrCreateCommunitySettingsAsync()
        {
            var settings = await _data.GetCommunitySettingsAsync();
            if (settings != null)
            {
                if (settings.CommunitySettingsID == 0)
                {
                    settings.CommunitySettingsID = 1;
                }

                return settings;
            }

            return new CommunitySettings
            {
                CommunitySettingsID = 1,
                BackgroundImageUrl = "/images/default-forum-bg.jpg",
                LastUpdated = DateTime.UtcNow
            };
        }

        private bool CanParticipate()
        {
            return User.IsInRole("Homeowner") && GetCurrentHomeownerId() > 0;
        }

        private string GetBackUrl()
        {
            if (User.IsInRole("Admin"))
            {
                return Url.Action("Dashboard", "Admin") ?? "/";
            }

            if (User.IsInRole("Staff"))
            {
                return Url.Action("Dashboard", "Staff") ?? "/";
            }

            return Url.Action("Dashboard", "Homeowner") ?? "/";
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

    }
}
