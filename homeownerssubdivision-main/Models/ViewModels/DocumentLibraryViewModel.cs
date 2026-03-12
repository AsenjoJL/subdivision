using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class DocumentLibraryViewModel
    {
        public IReadOnlyList<Document> Documents { get; set; } = Array.Empty<Document>();

        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

        public string SelectedCategory { get; set; } = "All";

        public int TotalDownloads => Documents.Sum(document => document.DownloadCount);
    }
}
