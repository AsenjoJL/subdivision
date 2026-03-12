using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class DocumentController : BaseController
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IAppFileStorageService _fileStorageService;

        public DocumentController(
            IDataService data,
            IWebHostEnvironment hostingEnvironment,
            IAppFileStorageService fileStorageService) : base(data)
        {
            _hostingEnvironment = hostingEnvironment;
            _fileStorageService = fileStorageService;
        }

        // Admin: Upload Document
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Upload()
        {
            return PartialView("Upload");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string title, string description, string category)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Please select a file." });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Title is required." });
            }

            try
            {
                var documentUrl = await _fileStorageService.UploadDocumentAsync(file, HttpContext.RequestAborted);

                // Get admin ID
                var email = User.FindFirstValue(ClaimTypes.Email);
                var admin = await _data.GetAdminByEmailAsync(email ?? "");
                var adminId = admin?.AdminID ?? 1;

                // Create document record
                var document = new Document
                {
                    Title = title,
                    Description = description,
                    Category = category,
                    FilePath = documentUrl,
                    FileType = Path.GetExtension(file.FileName).TrimStart('.'),
                    FileSize = file.Length,
                    UploadedByAdminID = adminId,
                    UploadedAt = DateTime.UtcNow,
                    IsPublic = true,
                    DownloadCount = 0
                };

                await _data.AddDocumentAsync(document);

                return Json(new { success = true, message = "Document uploaded successfully!", documentId = document.DocumentID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error uploading file: {ex.Message}" });
            }
        }

        // View all documents (Homeowner & Admin)
        public async Task<IActionResult> Index(string category = "All")
        {
            var categories = new[] { "All", "Forms", "Guidelines", "Financial Reports", "Meeting Minutes" };
            var documents = (await _data.GetDocumentsAsync())
                .Where(d => d.IsPublic)
                .OrderByDescending(d => d.UploadedAt)
                .ToList();

            if (category != "All")
            {
                documents = documents.Where(d => d.Category == category).ToList();
            }

            var viewModel = new DocumentLibraryViewModel
            {
                Documents = documents,
                Categories = categories,
                SelectedCategory = category
            };

            return PartialView("Index", viewModel);
        }

        // Download document
        public async Task<IActionResult> Download(int id)
        {
            var document = await _data.GetDocumentByIdAsync(id);
            if (document == null || !document.IsPublic)
            {
                return NotFound();
            }

            // Increment download count
            await _data.IncrementDownloadCountAsync(id);

            if (Uri.TryCreate(document.FilePath, UriKind.Absolute, out _))
            {
                return Redirect(document.FilePath);
            }

            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", $"{document.Title}{Path.GetExtension(document.FilePath)}");
        }

        // Admin: Manage Documents
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var documents = await GetOrderedDocumentsAsync();
            var viewModel = new AdminDocumentManagementViewModel
            {
                Documents = documents
            };

            return PartialView("Manage", viewModel);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LoadDocumentCards()
        {
            var documents = await GetOrderedDocumentsAsync();
            return PartialView("_AdminDocumentCards", documents);
        }

        // Admin: Delete Document
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _data.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return Json(new { success = false, message = "Document not found." });
            }

            try
            {
                if (_fileStorageService.IsManagedPublicUrl(document.FilePath))
                {
                    await _fileStorageService.DeleteManagedFileAsync(document.FilePath, HttpContext.RequestAborted);
                }
                else
                {
                    var filePath = Path.Combine(_hostingEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Delete database record
                await _data.DeleteDocumentAsync(id);

                return Json(new { success = true, message = "Document deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting document: {ex.Message}" });
            }
        }

        private async Task<List<Document>> GetOrderedDocumentsAsync()
        {
            return (await _data.GetDocumentsAsync())
                .OrderByDescending(d => d.UploadedAt)
                .ToList();
        }
    }
}
