// UpdateService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SCT_Updater
{
    public class UpdateService
    {
        private readonly NextcloudClient _nextcloudClient;
        private readonly LocalStateService _localState;

        public UpdateService(NextcloudClient nextcloudClient, LocalStateService localState)
        {
            _nextcloudClient = nextcloudClient;
            _localState = localState;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion)
        {
            SuiteManifest suiteManifest = await _nextcloudClient.GetSuiteManifestAsync();
            UpdateCheckResult result = new UpdateCheckResult();

            // Check for self-update first
            Product self = suiteManifest.Products.FirstOrDefault(p => p.Id == AppConfig.UPDATER_ID);
            if (self != null && self.LatestVersion != currentVersion)
            {
                result.SelfUpdateProduct = self;
                return result; // Stop here. We must update self first.
            }

            // Check for module updates
            LocalVersions localData = _localState.LoadLocalVersions();
            var productsToShow = new List<Product>();
            foreach (var serverProduct in suiteManifest.Products.Where(p => p.Id != AppConfig.UPDATER_ID))
            {
                InstalledProduct localProduct = localData.InstalledProducts.FirstOrDefault(p => p.Id == serverProduct.Id);
                if (localProduct != null)
                {
                    serverProduct.InstalledVersion = localProduct.Version;
                    serverProduct.IsUpdateAvailable = serverProduct.LatestVersion != localProduct.Version;
                }
                else
                {
                    serverProduct.InstalledVersion = "Not Installed";
                    serverProduct.IsUpdateAvailable = true;
                }
                productsToShow.Add(serverProduct);
            }
            result.ProductsToShow = productsToShow;
            return result;
        }

        public async Task StartModuleUpdate_Zip(FileManifest manifest, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            var fileToDownload = manifest.Files.FirstOrDefault();
            if (fileToDownload == null) throw new Exception("File manifest is empty.");

            string localDir = Application.StartupPath;
            string tempZipPath = Path.Combine(Path.GetTempPath(), fileToDownload.Path);

            if (File.Exists(tempZipPath))
            {
                string localHash = await Utility.GetFileHashAsync(tempZipPath);
                if (localHash.Equals(fileToDownload.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    statusProgress.Report("ZIP file already downloaded. Skipping...");
                }
                else
                {
                    File.Delete(tempZipPath);
                }
            }

            if (!File.Exists(tempZipPath))
            {
                statusProgress.Report($"Downloading {fileToDownload.Path}...");
                string fullFileUrl = _nextcloudClient.BuildFullFileUrl(manifest.BaseUrl, fileToDownload.Path);
                await _nextcloudClient.DownloadFileAsync(fullFileUrl, tempZipPath, percentProgress);
            }

            statusProgress.Report($"Extracting {fileToDownload.Path}...");

            try
            {
                await Task.Run(() =>
                {
                    using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(localDir, entry.FullName));
                            if (!destinationPath.StartsWith(localDir, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new IOException("Attempted to extract file outside of the target directory.");
                            }
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destinationPath);
                                continue;
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                });
                File.Delete(tempZipPath);
            }
            catch (InvalidDataException ex)
            {
                string errorMsg = $"Failed to unzip file: {ex.Message}\n" +
                                  "Ensure you uploaded a .ZIP archive, not .7z or .RAR.\n\n" +
                                  $"File for inspection: {tempZipPath}";
                throw new Exception(errorMsg, ex);
            }
        }

        public async Task StartModuleUpdate_Files(FileManifest manifest, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            int filesDone = 0;
            var semaphore = new SemaphoreSlim(AppConfig.MAX_PARALLEL_DOWNLOADS);
            var allDownloadTasks = new List<Task>();

            statusProgress.Report("Preparing to download files...");

            foreach (var file in manifest.Files)
            {
                var downloadTask = Task.Run(async () =>
                {
                    string localPath = Path.Combine(Application.StartupPath, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                    if (File.Exists(localPath))
                    {
                        string localHash = await Utility.GetFileHashAsync(localPath);
                        if (localHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            int done = Interlocked.Increment(ref filesDone);
                            percentProgress.Report((int)((double)done / manifest.Files.Count * 100));
                            return;
                        }
                    }

                    await semaphore.WaitAsync();
                    try
                    {
                        string fullFileUrl = _nextcloudClient.BuildFullFileUrl(manifest.BaseUrl, file.Path);
                        byte[] fileData = await _nextcloudClient.DownloadFileBytesAsync(fullFileUrl);
                        File.WriteAllBytes(localPath, fileData);

                        int done = Interlocked.Increment(ref filesDone);
                        percentProgress.Report((int)((double)done / manifest.Files.Count * 100));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                allDownloadTasks.Add(downloadTask);
            }

            statusProgress.Report($"Downloading {manifest.Files.Count} files...");
            await Task.WhenAll(allDownloadTasks);
        }

        public async Task StartSelfUpdateAsync(Product product, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            FileManifest manifest = await _nextcloudClient.GetFileManifestAsync(product.ManifestUrl);
            string tempDir = Path.Combine(Path.GetTempPath(), "SCT_Updater_New");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            if (manifest.PackageMode == "zip")
            {
                await StartSelfUpdate_Zip(manifest, tempDir, statusProgress, percentProgress);
            }
            else
            {
                await StartSelfUpdate_Files(manifest, tempDir, statusProgress, percentProgress);
            }

            string exeName = Path.GetFileName(Application.ExecutablePath);
            string currentDir = Application.StartupPath;
            Utility.CreateUpdateStub(currentDir, tempDir, exeName);

            // The service is done. It returns control to Form1 to launch the stub.
        }

        private async Task StartSelfUpdate_Zip(FileManifest manifest, string tempDir, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            var fileToDownload = manifest.Files.FirstOrDefault();
            if (fileToDownload == null) throw new Exception("Self-update manifest is empty.");

            statusProgress.Report($"Downloading {fileToDownload.Path}...");
            string fullFileUrl = _nextcloudClient.BuildFullFileUrl(manifest.BaseUrl, fileToDownload.Path);
            string tempZipPath = Path.Combine(tempDir, fileToDownload.Path);

            await _nextcloudClient.DownloadFileAsync(fullFileUrl, tempZipPath, percentProgress);

            statusProgress.Report("Extracting update...");
            await Task.Run(() =>
            {
                using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                        if (!destinationPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException("Attempted to extract file outside of the target directory.");
                        }
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true); // Overwrite
                    }
                }
            });
            File.Delete(tempZipPath);
            statusProgress.Report("Update downloaded and extracted.");
        }

        private async Task StartSelfUpdate_Files(FileManifest manifest, string tempDir, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            int filesDone = 0;
            const int MAX_PARALLEL = 10;
            var semaphore = new SemaphoreSlim(MAX_PARALLEL);
            var allDownloadTasks = new List<Task>();

            foreach (var file in manifest.Files)
            {
                var downloadTask = Task.Run(async () => {
                    string localPath = Path.Combine(tempDir, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                    await semaphore.WaitAsync();
                    try
                    {
                        string fullFileUrl = _nextcloudClient.BuildFullFileUrl(manifest.BaseUrl, file.Path);
                        byte[] fileData = await _nextcloudClient.DownloadFileBytesAsync(fullFileUrl);
                        File.WriteAllBytes(localPath, fileData);

                        int done = Interlocked.Increment(ref filesDone);
                        percentProgress.Report((int)((double)done / manifest.Files.Count * 100));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                allDownloadTasks.Add(downloadTask);
            }

            statusProgress.Report($"Downloading {manifest.Files.Count} files...");
            await Task.WhenAll(allDownloadTasks);
            statusProgress.Report("Update files downloaded.");
        }
    }
}