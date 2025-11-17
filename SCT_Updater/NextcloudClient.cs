// NextcloudClient.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic; // NEW
using System.IO;
using System.Net.Http;
using System.Text; // NEW
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq; // NEW

namespace SCT_Updater
{
    public class NextcloudClient
    {
        private readonly HttpClient _httpClient;

        public NextcloudClient()
        {
            _httpClient = CreateNextcloudHttpClient();
        }

        private HttpClient CreateNextcloudHttpClient()
        {
            var client = new HttpClient();
            var authToken = System.Text.Encoding.UTF8.GetBytes($"{AppConfig.NC_USER}:{AppConfig.NC_PASSWORD}");
            var headerValue = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
            client.DefaultRequestHeaders.Authorization = headerValue;
            Log.Debug("HttpClient created with Basic Auth.");
            return client;
        }

        /// <summary>
        /// NEW: Lists all files (not directories) in a specific WebDAV folder.
        /// </summary>
        public async Task<List<string>> ListFilesAsync(string directoryUrl)
        {
            Log.Debug($"Listing files (PROPFIND) for: {directoryUrl}");
            var fileNames = new List<string>();

            using (var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), directoryUrl))
            {
                request.Headers.Add("Depth", "1");

                // This XML requests the 'resourcetype' property to distinguish files from folders
                const string requestBody = "<?xml version=\"1.0\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>";
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/xml");

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string xmlResponse = await response.Content.ReadAsStringAsync();
                XDocument xDoc = XDocument.Parse(xmlResponse);
                XNamespace d = "DAV:";

                var responses = xDoc.Descendants(d + "response");

                foreach (var resp in responses)
                {
                    string href = resp.Element(d + "href")?.Value;
                    if (string.IsNullOrEmpty(href)) continue;

                    string decodedHref = Uri.UnescapeDataString(href);

                    // Skip the directory itself (which is always returned)
                    if (decodedHref.EndsWith("/") || decodedHref.EndsWith(AppConfig.NC_DEVICE_CONFIGS_URL.Substring(AppConfig.NC_DEVICE_CONFIGS_URL.LastIndexOf('/'))))
                    {
                        continue;
                    }

                    // Check if it's a file (resourcetype will not have d:collection)
                    var resType = resp.Element(d + "propstat")?.Element(d + "prop")?.Element(d + "resourcetype");
                    if (resType != null && resType.Element(d + "collection") == null)
                    {
                        // It's a file. Get its name.
                        string fileName = Path.GetFileName(decodedHref);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            fileNames.Add(fileName);
                        }
                    }
                }
            }

            Log.Debug($"Found {fileNames.Count} files in {directoryUrl}");
            return fileNames;
        }


        public string BuildFullFileUrl(string baseUrl, string filePath)
        {
            string fullFileUrl;
            baseUrl = baseUrl.TrimStart('/');
            filePath = filePath.TrimStart('/');

            if (baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                fullFileUrl = $"{baseUrl.TrimEnd('/')}/{filePath}";
            }
            else
            {
                fullFileUrl = $"{AppConfig.NC_WEBDAV_BASE_URL}/{baseUrl.TrimEnd('/')}/{filePath}";
            }
            return fullFileUrl;
        }

        public async Task<SuiteManifest> GetSuiteManifestAsync()
        {
            Log.Debug($"Fetching suite manifest: {AppConfig.SUITE_MANIFEST_URL}");
            string json = await _httpClient.GetStringAsync(AppConfig.SUITE_MANIFEST_URL);
            return JsonConvert.DeserializeObject<SuiteManifest>(json);
        }

        /// <summary>
        /// CHANGED: This method now constructs the URL from productId and version
        /// based on the convention.
        /// </summary>
        public async Task<FileManifest> GetFileManifestAsync(string productId, string version)
        {
            // Build the path and filename based on the new convention
            // e.g., versions/nextcloud_cli/nextcloud_cli_1.0.2.json
            string manifestName = $"{productId}_{version}.json";
            string manifestPath = $"versions/{productId}/{manifestName}";

            // AppConfig.NC_WEBDAV_BASE_URL is ".../SCT/Updater"
            string fullManifestUrl = $"{AppConfig.NC_WEBDAV_BASE_URL}/{manifestPath}";

            Log.Debug($"Fetching file manifest: {fullManifestUrl}");
            string json = await _httpClient.GetStringAsync(fullManifestUrl);
            return JsonConvert.DeserializeObject<FileManifest>(json);
        }

        public async Task<byte[]> DownloadFileBytesAsync(string fileUrl)
        {
            Log.Debug($"Downloading bytes: {fileUrl}");
            return await _httpClient.GetByteArrayAsync(fileUrl);
        }

        public async Task DownloadFileAsync(string fileUrl, string tempPath, IProgress<int> progress)
        {
            Log.Debug($"Downloading file (stream): {fileUrl}");
            using (HttpResponseMessage response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (Stream streamToRead = await response.Content.ReadAsStreamAsync())
                using (Stream streamToWrite = File.Open(tempPath, FileMode.Create))
                {
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    long totalRead = 0;
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = await streamToRead.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await streamToWrite.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (totalBytes > 0)
                        {
                            progress.Report((int)((double)totalRead / totalBytes * 100));
                        }
                    }
                }
            }
            Log.Debug($"File download complete: {fileUrl}");
        }

    }
}