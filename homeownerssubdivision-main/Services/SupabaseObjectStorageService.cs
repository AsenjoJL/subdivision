using System.Net;
using System.Text.Json;
using HOMEOWNER.Configuration;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class SupabaseObjectStorageService : ISupabaseObjectStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseStorageOptions _options;
        private readonly ILogger<SupabaseObjectStorageService> _logger;

        public SupabaseObjectStorageService(
            HttpClient httpClient,
            IOptions<SupabaseStorageOptions> options,
            ILogger<SupabaseObjectStorageService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> UploadFileAsync(
            IFormFile file,
            string objectPrefix,
            string fileNamePrefix,
            CancellationToken cancellationToken = default)
        {
            var settings = ResolveSettings();
            if (string.IsNullOrWhiteSpace(settings.ProjectUrl) || string.IsNullOrWhiteSpace(settings.ServiceRoleKey))
            {
                throw new InvalidOperationException("Supabase Storage is not configured for file uploads.");
            }

            var bucketName = string.IsNullOrWhiteSpace(settings.BucketName)
                ? "profile-images"
                : settings.BucketName.Trim();
            var prefix = NormalizePath(objectPrefix);
            var sanitizedPrefix = SanitizeFileNamePrefix(fileNamePrefix);
            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{sanitizedPrefix}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{extension}";
            var objectPath = string.IsNullOrWhiteSpace(prefix)
                ? uniqueFileName
                : $"{prefix}/{uniqueFileName}";

            var uploadUrl = BuildObjectUrl(settings.ProjectUrl, bucketName, objectPath);
            var publicUrl = BuildPublicUrl(settings.ProjectUrl, bucketName, objectPath);
            var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey)
                ? settings.ServiceRoleKey
                : settings.ApiKey;

            using var stream = file.OpenReadStream();
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
            {
                Content = content
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ServiceRoleKey);
            request.Headers.Add("apikey", apiKey);
            request.Headers.Add("x-upsert", settings.UseUpsert ? "true" : "false");

            _logger.LogInformation(
                "Uploading file to Supabase Storage. Bucket={BucketName}, ObjectPath={ObjectPath}, Upsert={UseUpsert}",
                bucketName,
                objectPath,
                settings.UseUpsert);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildUploadException(response, responseBody);
            }

            return publicUrl;
        }

        public async Task DeleteFileByPublicUrlAsync(string publicUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(publicUrl))
            {
                return;
            }

            var settings = ResolveSettings();
            if (string.IsNullOrWhiteSpace(settings.ProjectUrl) || string.IsNullOrWhiteSpace(settings.ServiceRoleKey))
            {
                throw new InvalidOperationException("Supabase Storage is not configured for file deletion.");
            }

            if (!TryExtractObjectLocation(publicUrl, settings.ProjectUrl, out var bucketName, out var objectPath))
            {
                _logger.LogWarning("Skipping Supabase Storage delete because the public URL does not match the configured project. Url={PublicUrl}", publicUrl);
                return;
            }

            var deleteUrl = BuildObjectUrl(settings.ProjectUrl, bucketName, objectPath);
            var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey)
                ? settings.ServiceRoleKey
                : settings.ApiKey;

            using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ServiceRoleKey);
            request.Headers.Add("apikey", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Supabase Storage object already absent. Bucket={BucketName}, ObjectPath={ObjectPath}", bucketName, objectPath);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw BuildUploadException(response, responseBody);
            }
        }

        private static string BuildObjectUrl(string projectUrl, string bucketName, string objectPath)
        {
            var normalizedProjectUrl = projectUrl.Trim().TrimEnd('/');
            var encodedBucketName = Uri.EscapeDataString(bucketName);
            var encodedObjectPath = string.Join("/",
                objectPath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            return $"{normalizedProjectUrl}/storage/v1/object/{encodedBucketName}/{encodedObjectPath}";
        }

        private static string BuildPublicUrl(string projectUrl, string bucketName, string objectPath)
        {
            var normalizedProjectUrl = projectUrl.Trim().TrimEnd('/');
            var encodedBucketName = Uri.EscapeDataString(bucketName);
            var encodedObjectPath = string.Join("/",
                objectPath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            return $"{normalizedProjectUrl}/storage/v1/object/public/{encodedBucketName}/{encodedObjectPath}";
        }

        private static bool TryExtractObjectLocation(string publicUrl, string projectUrl, out string bucketName, out string objectPath)
        {
            bucketName = string.Empty;
            objectPath = string.Empty;

            if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri) ||
                !Uri.TryCreate(projectUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var projectUri))
            {
                return false;
            }

            if (!string.Equals(publicUri.Host, projectUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var marker = "/storage/v1/object/public/";
            var absolutePath = publicUri.AbsolutePath;
            var markerIndex = absolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            var relativePath = absolutePath[(markerIndex + marker.Length)..];
            var firstSlashIndex = relativePath.IndexOf('/');
            if (firstSlashIndex <= 0 || firstSlashIndex == relativePath.Length - 1)
            {
                return false;
            }

            bucketName = Uri.UnescapeDataString(relativePath[..firstSlashIndex]);
            objectPath = Uri.UnescapeDataString(relativePath[(firstSlashIndex + 1)..]);
            return true;
        }

        private SupabaseStorageUploadException BuildUploadException(HttpResponseMessage response, string responseBody)
        {
            var storageMessage = ExtractStorageErrorMessage(responseBody);
            var category = CategorizeStorageError(response.StatusCode, storageMessage);
            var userMessage = category switch
            {
                "invalid_credentials" => $"Supabase Storage error: {storageMessage}. Check your project URL and service role key.",
                "bucket_config" => $"Supabase Storage error: {storageMessage}. Check your bucket name or bucket permissions.",
                _ => $"Supabase Storage error: {storageMessage}"
            };

            _logger.LogError(
                "Supabase Storage request failed. StatusCode={StatusCode}, Category={Category}, Message={StorageMessage}, RawResponse={RawResponse}",
                (int)response.StatusCode,
                category,
                storageMessage,
                responseBody);

            return new SupabaseStorageUploadException(
                userMessage,
                storageMessage,
                responseBody,
                (int)response.StatusCode,
                category);
        }

        private static string ExtractStorageErrorMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return "Unknown Supabase Storage upload error.";
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                foreach (var propertyName in new[] { "message", "error", "msg" })
                {
                    if (document.RootElement.TryGetProperty(propertyName, out var element))
                    {
                        var message = element.GetString();
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            return message;
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }

            return responseBody;
        }

        private static string CategorizeStorageError(HttpStatusCode statusCode, string message)
        {
            if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
            {
                return "invalid_credentials";
            }

            if (statusCode == HttpStatusCode.NotFound ||
                message.Contains("bucket", StringComparison.OrdinalIgnoreCase))
            {
                return "bucket_config";
            }

            return "unknown";
        }

        private static string NormalizePath(string? path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Trim('/').Replace("\\", "/");
        }

        private static string SanitizeFileNamePrefix(string? fileNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(fileNamePrefix))
            {
                return "file";
            }

            var sanitized = new string(fileNamePrefix
                .Trim()
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-')
                .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
        }

        private SupabaseStorageOptions ResolveSettings()
        {
            return new SupabaseStorageOptions
            {
                ProjectUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? _options.ProjectUrl,
                ServiceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? _options.ServiceRoleKey,
                ApiKey = Environment.GetEnvironmentVariable("SUPABASE_API_KEY") ?? _options.ApiKey,
                BucketName = _options.BucketName,
                ObjectPrefix = _options.ObjectPrefix,
                UseUpsert = _options.UseUpsert
            };
        }
    }
}
