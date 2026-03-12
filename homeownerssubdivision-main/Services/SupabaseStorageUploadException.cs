namespace HOMEOWNER.Services
{
    public class SupabaseStorageUploadException : Exception
    {
        public SupabaseStorageUploadException(
            string userMessage,
            string storageMessage,
            string rawResponse,
            int statusCode,
            string errorCategory) : base(userMessage)
        {
            UserMessage = userMessage;
            StorageMessage = storageMessage;
            RawResponse = rawResponse;
            StatusCode = statusCode;
            ErrorCategory = errorCategory;
        }

        public string UserMessage { get; }
        public string StorageMessage { get; }
        public string RawResponse { get; }
        public int StatusCode { get; }
        public string ErrorCategory { get; }
    }
}
