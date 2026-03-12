using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class AdminDocumentManagementViewModel
    {
        public IReadOnlyList<Document> Documents { get; set; } = Array.Empty<Document>();

        public int TotalDownloads => Documents.Sum(document => document.DownloadCount);

        public int TotalDocuments => Documents.Count;
    }
}
