using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class PollController : BaseController
    {
        public PollController(IDataService data) : base(data)
        {
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await BuildHomeownerPollsViewModelAsync();
            return PartialView("Index", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> LoadActivePolls()
        {
            var viewModel = await BuildHomeownerPollsViewModelAsync();
            return PartialView("_HomeownerPollCards", viewModel);
        }

        [Authorize(Roles = "Homeowner")]
        [HttpPost]
        public async Task<IActionResult> Vote(int pollId, int optionId)
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "Homeowner not found." });
            }

            var poll = await _data.GetPollByIdAsync(pollId);
            if (poll == null)
            {
                return Json(new { success = false, message = "Poll not found." });
            }

            if (!string.Equals(poll.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "This poll is not active." });
            }

            if (await _data.HasHomeownerVotedAsync(pollId, homeownerId))
            {
                return Json(new { success = false, message = "You have already voted on this poll." });
            }

            var option = poll.Options.FirstOrDefault(o => o.OptionID == optionId);
            if (option == null)
            {
                return Json(new { success = false, message = "Invalid option selected." });
            }

            var vote = new PollVote
            {
                PollID = pollId,
                OptionID = optionId,
                HomeownerID = homeownerId,
                VotedAt = DateTime.UtcNow
            };

            await _data.AddPollVoteAsync(vote);
            return Json(new { success = true, message = "Vote submitted successfully!" });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var viewModel = await BuildAdminPollManagementViewModelAsync();
            return PartialView("Manage", viewModel);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> LoadPollCards()
        {
            var viewModel = await BuildAdminPollManagementViewModelAsync();
            return PartialView("_AdminPollCards", viewModel.Polls);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePollViewModel model)
        {
            if (!TryValidatePollModel(model))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid poll data provided.",
                    errors = ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
                });
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            var admin = await _data.GetAdminByEmailAsync(email ?? string.Empty);

            var poll = new Poll
            {
                Question = model.Question.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                CreatedByAdminID = admin?.AdminID ?? 1,
                CreatedAt = DateTime.UtcNow,
                StartDate = EnsureUtc(model.StartDate),
                EndDate = EnsureUtc(model.EndDate),
                Status = model.Status,
                IsAnonymous = model.IsAnonymous,
                AllowMultipleChoices = model.AllowMultipleChoices,
                TotalVotes = 0,
                Options = model.Options
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Select((option, index) => new PollOption
                    {
                        OptionText = option.Trim(),
                        VoteCount = 0,
                        DisplayOrder = index
                    })
                    .ToList()
            };

            await _data.AddPollAsync(poll);
            return Json(new { success = true, message = "Poll created successfully!", pollId = poll.PollID });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetPoll(int id)
        {
            var poll = await _data.GetPollByIdAsync(id);
            if (poll == null)
            {
                return NotFound(new { success = false, message = "Poll not found." });
            }

            return Json(new
            {
                success = true,
                poll = new
                {
                    poll.PollID,
                    poll.Question,
                    poll.Description,
                    startDate = poll.StartDate?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm"),
                    endDate = poll.EndDate?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm"),
                    poll.Status,
                    poll.IsAnonymous,
                    poll.AllowMultipleChoices,
                    poll.TotalVotes,
                    options = poll.Options
                        .OrderBy(option => option.DisplayOrder)
                        .Select(option => new
                        {
                            option.OptionID,
                            option.OptionText,
                            option.VoteCount
                        })
                }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var poll = await _data.GetPollByIdAsync(id);
            if (poll == null)
            {
                return Json(new { success = false, message = "Poll not found." });
            }

            poll.Status = status;
            await _data.UpdatePollAsync(poll);
            return Json(new { success = true, message = $"Poll status updated to {status} successfully!" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var poll = await _data.GetPollByIdAsync(id);
            if (poll == null)
            {
                return Json(new { success = false, message = "Poll not found." });
            }

            await _data.DeletePollAsync(id);
            return Json(new { success = true, message = "Poll deleted successfully!" });
        }

        private async Task<HomeownerPollsViewModel> BuildHomeownerPollsViewModelAsync()
        {
            var activePolls = await _data.GetActivePollsAsync();
            var homeownerId = GetCurrentHomeownerId();
            var votedPolls = homeownerId > 0
                ? await _data.GetVotedPollIdsByHomeownerAsync(homeownerId)
                : new HashSet<int>();

            var now = DateTime.UtcNow;
            var orderedPolls = activePolls
                .OrderBy(p => p.EndDate ?? DateTime.MaxValue)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();

            return new HomeownerPollsViewModel
            {
                Polls = orderedPolls,
                VotedPollIds = votedPolls.ToList(),
                ActivePolls = orderedPolls.Count,
                TotalVotes = orderedPolls.Sum(p => p.TotalVotes),
                ClosingSoon = orderedPolls.Count(p => p.EndDate.HasValue && p.EndDate.Value <= now.AddDays(3))
            };
        }

        private async Task<AdminPollManagementViewModel> BuildAdminPollManagementViewModelAsync()
        {
            var polls = (await _data.GetPollsAsync())
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return new AdminPollManagementViewModel
            {
                Polls = polls,
                TotalPolls = polls.Count,
                ActivePolls = polls.Count(p => string.Equals(p.Status, "Active", StringComparison.OrdinalIgnoreCase)),
                TotalVotes = polls.Sum(p => p.TotalVotes)
            };
        }

        private bool TryValidatePollModel(CreatePollViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return false;
            }

            if (model.Options == null || model.Options.Count(option => !string.IsNullOrWhiteSpace(option)) < 2)
            {
                ModelState.AddModelError(nameof(model.Options), "At least 2 options are required.");
            }

            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after the start date.");
            }

            if (string.IsNullOrWhiteSpace(model.Status))
            {
                ModelState.AddModelError(nameof(model.Status), "Status is required.");
            }

            return ModelState.IsValid;
        }

        private static DateTime? EnsureUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
            };
        }
    }
}
