using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SaveHere.Models;

namespace SaveHere.Helpers
{
    /// <summary>
    /// Helper class to apply authentication settings to HTTP requests.
    /// </summary>
    public static class HttpRequestAuthenticator
    {
        /// <summary>
        /// Applies the authentication settings from a queue item to an HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request to modify.</param>
        /// <param name="queueItem">The queue item containing authentication settings.</param>
        public static void ApplyAuthentication(HttpRequestMessage request, FileDownloadQueueItem queueItem)
        {
            if (request == null || queueItem == null)
                return;

            switch (queueItem.AuthType)
            {
                case AuthenticationType.BasicAuth:
                    if (!string.IsNullOrEmpty(queueItem.AuthUsername))
                    {
                        var credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes($"{queueItem.AuthUsername}:{queueItem.AuthPassword ?? string.Empty}"));
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    }
                    break;

                case AuthenticationType.BearerToken:
                    if (!string.IsNullOrEmpty(queueItem.AuthBearerToken))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", queueItem.AuthBearerToken);
                    }
                    break;

                case AuthenticationType.Cookie:
                    if (!string.IsNullOrEmpty(queueItem.AuthCookies))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", queueItem.AuthCookies);
                    }
                    break;

                case AuthenticationType.CustomHeaders:
                    if (!string.IsNullOrEmpty(queueItem.AuthCustomHeaders))
                    {
                        try
                        {
                            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(queueItem.AuthCustomHeaders);
                            if (headers != null)
                            {
                                foreach (var (key, value) in headers)
                                {
                                    request.Headers.TryAddWithoutValidation(key, value);
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Invalid JSON, silently ignore
                        }
                    }
                    break;

                case AuthenticationType.None:
                default:
                    // No authentication needed
                    break;
            }
        }
    }
}
