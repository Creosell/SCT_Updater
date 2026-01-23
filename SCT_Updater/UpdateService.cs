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
            Log.Debug("UpdateService initialized.");
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion)
        {
            Log.Debug("Checking for updates...");
            UpdateCheckResult result = new UpdateCheckResult();
            var productsToShow = new List<Product>();

            // --- 1. NEW: Check Device Configs First ---
            Log.Debug("Checking device configs...");
            try
            {
                var serverFiles = await _nextcloudClient.ListFilesAsync(AppConfig.NC_DEVICE_CONFIGS_URL);
                string localConfigPath = Path.Combine(Application.StartupPath, AppConfig.LOCAL_DEVICE_CONFIGS_PATH);
                bool localFolderExists = Directory.Exists(localConfigPath);
                int localFileCount = 0;

                if (localFolderExists)
                {
                    localFileCount = Directory.GetFiles(localConfigPath).Length;
                }

                var configProduct = new Product
                {
                    Id = AppConfig.DEVICE_CONFIGS_ID,
                    Name = "Device configs",
                    Description = "Update device configs",
                    InstalledVersion = "-", // Per request
                    LatestVersion = "-",    // Per request
                    IsUpdateAvailable = !localFolderExists || serverFiles.Count != localFileCount
                };
                productsToShow.Add(configProduct);
                Log.Debug($"Config check: Server={serverFiles.Count}, Local={localFileCount}, Update={configProduct.IsUpdateAvailable}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check device configs. Skipping.");
                // Optionally create a "failed" product
                // productsToShow.Add(new Product { Id = AppConfig.DEVICE_CONFIGS_ID, Name = "Device configs", Description = "Error checking configs" });
            }

            // --- 2. Check Suite Manifest (existing logic) ---
            Log.Debug("Checking suite manifest...");
            SuiteManifest suiteManifest = await _nextcloudClient.GetSuiteManifestAsync();

            Product self = suiteManifest.Products.FirstOrDefault(p => p.Id == AppConfig.UPDATER_ID);
            if (self != null && self.LatestVersion != currentVersion)
            {
                Log.Warn($"Self-update required. Current: {currentVersion}, Server: {self.LatestVersion}");
                result.SelfUpdateProduct = self;
                return result; // Self-update takes precedence
            }

            Log.Debug("Self-update not required. Checking modules.");
            LocalVersions localData = _localState.LoadLocalVersions();

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
            Log.Debug($"Found {productsToShow.Count} total items (configs + modules).");
            return result;
        }

        // --- NEW: Method to sync device configs ---
        public async Task SyncDeviceConfigsAsync(IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Info("Starting device config sync...");
            string localPath = Path.Combine(Application.StartupPath, AppConfig.LOCAL_DEVICE_CONFIGS_PATH);
            string serverUrl = AppConfig.NC_DEVICE_CONFIGS_URL;

            // 1. Get server file list
            statusProgress.Report("Fetching server file list...");
            percentProgress.Report(0);
            var serverFiles = await _nextcloudClient.ListFilesAsync(serverUrl);

            // --- CHANGED: Always clear local directory first for a clean sync ---
            statusProgress.Report("Clearing local config directory...");
            DeleteDeviceConfigs(); // This deletes the root folder

            if (!serverFiles.Any())
            {
                statusProgress.Report("No configs found on server. Local folder cleared.");
                Log.Warn("Device config sync: No files found on server. Local sync is complete (empty).");
                percentProgress.Report(100);
                return; // We are done, local folder is now empty, matching server.
            }

            // 3. Re-create directory and download
            Directory.CreateDirectory(localPath); // Re-create it
            Log.Debug($"Downloading {serverFiles.Count} config files...");

            int filesDone = 0;
            var semaphore = new SemaphoreSlim(AppConfig.MAX_PARALLEL_DOWNLOADS);
            var allTasks = new List<Task>();

            foreach (var fileName in serverFiles)
            {
                var downloadTask = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string fileUrl = $"{serverUrl}/{fileName}";
                        string localFilePath = Path.Combine(localPath, fileName);
                        byte[] data = await _nextcloudClient.DownloadFileBytesAsync(fileUrl);
                        File.WriteAllBytes(localFilePath, data); // Always overwrite

                        int done = Interlocked.Increment(ref filesDone);
                        percentProgress.Report((int)((double)done / serverFiles.Count * 100));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to download config file {fileName}");
                        // Continue with other files
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                allTasks.Add(downloadTask);
            }

            statusProgress.Report($"Downloading {serverFiles.Count} config files...");
            await Task.WhenAll(allTasks);

            percentProgress.Report(100);
            statusProgress.Report("Config sync complete.");
            Log.Info("Device config sync complete.");
        }

        // --- NEW: Method to delete all local configs ---
        public void DeleteDeviceConfigs()
        {
            Log.Info("Deleting local device configs folder...");
            string localPath = Path.Combine(Application.StartupPath, AppConfig.LOCAL_DEVICE_CONFIGS_PATH);
            try
            {
                Utility.DeletePath(localPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete device configs directory.");
                throw; // Re-throw to be caught by the UI
            }
        }


        public async Task ReinstallModuleAsync(Product product, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Info($"Starting re-install for {product.Id}...");
            statusProgress.Report($"Removing old files for {product.Name}...");

            var localData = _localState.LoadLocalVersions();
            var installedProduct = localData.InstalledProducts.FirstOrDefault(p => p.Id == product.Id);

            FileManifest oldManifest = null;
            if (installedProduct != null && installedProduct.Version != "Not Installed")
            {
                try
                {
                    // --- CHANGED: Use new GetFileManifestAsync signature ---
                    Log.Debug($"Fetching old manifest for deletion: {product.Id} v{installedProduct.Version}");
                    oldManifest = await _nextcloudClient.GetFileManifestAsync(product.Id, installedProduct.Version);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to fetch old manifest. Cannot continue re-install.");
                    throw new Exception($"Failed to fetch the *old* manifest ({installedProduct.Version}) for deletion. Cannot continue re-install. Error: {ex.Message}", ex);
                }

                DeleteModuleFilesFromManifest(oldManifest);
            }
            _localState.SaveLocalVersion(product.Id, "Not Installed");

            statusProgress.Report("Fetching latest manifest...");
            // --- CHANGED: Use new GetFileManifestAsync signature ---
            FileManifest newManifest = await _nextcloudClient.GetFileManifestAsync(product.Id, product.LatestVersion);

            statusProgress.Report($"Starting re-installation for {product.Name}...");
            if (newManifest.PackageMode == "zip")
            {
                await StartModuleUpdate_Zip(newManifest, statusProgress, percentProgress);
            }
            else
            {
                var emptyOldFiles = new Dictionary<string, string>();
                await StartModuleUpdate_Files(newManifest, emptyOldFiles, statusProgress, percentProgress);
            }
            Log.Info($"Re-install for {product.Id} complete.");
        }

        // ... (rest of UpdateService.cs remains the same) ...
        public async Task RunDeltaUpdateAsync(Product product, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Info($"Starting delta update for {product.Id}...");
            var localData = _localState.LoadLocalVersions();
            var installedProduct = localData.InstalledProducts.FirstOrDefault(p => p.Id == product.Id);

            if (installedProduct == null || installedProduct.Version == "Not Installed")
            {
                Log.Debug($"Product {product.Id} is not installed. Running fresh install.");
                // --- CHANGED: Use new GetFileManifestAsync signature ---
                FileManifest freshInstallManifest = await _nextcloudClient.GetFileManifestAsync(product.Id, product.LatestVersion);
                var emptyOldFiles = new Dictionary<string, string>();
                await StartModuleUpdate_Files(freshInstallManifest, emptyOldFiles, statusProgress, percentProgress);
                return;
            }

            Log.Debug("Fetching new and old manifests for delta comparison...");
            // --- CHANGED: Use new GetFileManifestAsync signature ---
            FileManifest newManifest = await _nextcloudClient.GetFileManifestAsync(product.Id, product.LatestVersion);

            statusProgress.Report("Fetching old manifest...");
            // --- CHANGED: Use new GetFileManifestAsync signature ---
            FileManifest oldManifest = await _nextcloudClient.GetFileManifestAsync(product.Id, installedProduct.Version);

            statusProgress.Report("Comparing versions...");
            var oldFiles = oldManifest.Files.ToDictionary(f => f.Path, f => f.Hash);
            var newFiles = newManifest.Files.ToDictionary(f => f.Path, f => f.Hash);

            var filesToDelete = oldFiles.Keys.Except(newFiles.Keys).ToList();
            if (filesToDelete.Any())
            {
                Log.Debug($"Deleting {filesToDelete.Count} obsolete files...");
                statusProgress.Report($"Deleting {filesToDelete.Count} obsolete files...");
                DeleteModuleFiles(filesToDelete);
            }

            await StartModuleUpdate_Files(newManifest, oldFiles, statusProgress, percentProgress);
            Log.Info($"Delta update for {product.Id} complete.");
        }

        private void DeleteModuleFiles(List<string> relativePaths)
        {
            string root = Application.StartupPath;
            foreach (var path in relativePaths)
            {
                try
                {
                    Utility.DeletePath(Path.Combine(root, path));
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to delete old file {path}: {ex.Message}");
                }
            }
        }

        public void DeleteModuleFilesFromManifest(FileManifest manifest)
        {
            if (manifest?.Files == null) return;
            Log.Debug($"Deleting all files from manifest for {manifest.ProductId}...");
            string root = Application.StartupPath;
            var topLevelItems = new HashSet<string>();
            foreach (var file in manifest.Files)
            {
                string topLevelItem = file.Path.Split(new[] { '/', '\\' }, 2)[0];
                topLevelItems.Add(topLevelItem);
            }

            foreach (string item in topLevelItems)
            {
                Log.Debug($"Deleting path: {item}");
                Utility.DeletePath(Path.Combine(root, item));
            }
        }

        public async Task StartModuleUpdate_Zip(FileManifest manifest, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Debug($"Starting ZIP update for {manifest.ProductId}...");
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
                    Log.Debug("Temporary ZIP has wrong hash. Deleting.");
                    File.Delete(tempZipPath);
                }
            }

            if (!File.Exists(tempZipPath))
            {
                statusProgress.Report($"Downloading {fileToDownload.Path}...");
                Log.Debug($"Downloading new ZIP: {fileToDownload.Path}");
                string fullFileUrl = _nextcloudClient.BuildFullFileUrl(manifest.BaseUrl, fileToDownload.Path);
                await _nextcloudClient.DownloadFileAsync(fullFileUrl, tempZipPath, percentProgress);
            }

            statusProgress.Report($"Extracting {fileToDownload.Path}...");
            Log.Debug("Extracting ZIP...");
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
                Log.Debug("Extraction complete. Temp ZIP deleted.");
            }
            catch (InvalidDataException ex)
            {
                Log.Error(ex, "Failed to unzip file. It may be corrupt or not a ZIP.");
                string errorMsg = $"Failed to unzip file: {ex.Message}\n" +
                                  "Ensure you uploaded a .ZIP archive, not .7z or .RAR.\n\n" +
                                  $"File for inspection: {tempZipPath}";
                throw new Exception(errorMsg, ex);
            }
        }

        public async Task StartModuleUpdate_Files(FileManifest newManifest, Dictionary<string, string> oldFiles, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Debug($"Starting FILES update for {newManifest.ProductId}...");
            int filesDone = 0;
            var semaphore = new SemaphoreSlim(AppConfig.MAX_PARALLEL_DOWNLOADS);
            var allDownloadTasks = new List<Task>();

            statusProgress.Report("Preparing to download files...");
            var filesToDownload = new List<ManifestFile>();

            // --- DELTA LOGIC ---
            foreach (var newFile in newManifest.Files)
            {
                if (oldFiles.TryGetValue(newFile.Path, out string oldHash) && oldHash == newFile.Hash)
                {
                    string localPath = Path.Combine(Application.StartupPath, newFile.Path);
                    if (File.Exists(localPath))
                    {
                        filesDone++;
                        continue;
                    }
                }
                filesToDownload.Add(newFile);
            }
            Log.Debug($"Found {filesToDownload.Count} files to download. {filesDone} files are already up-to-date.");

            if (!filesToDownload.Any())
            {
                statusProgress.Report("All files are up to date.");
                percentProgress.Report(100);
                return;
            }

            foreach (var file in filesToDownload)
            {
                var downloadTask = Task.Run(async () =>
                {
                    string localPath = Path.Combine(Application.StartupPath, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                    await semaphore.WaitAsync();
                    try
                    {
                        string fullFileUrl = _nextcloudClient.BuildFullFileUrl(newManifest.BaseUrl, file.Path);
                        byte[] fileData = await _nextcloudClient.DownloadFileBytesAsync(fullFileUrl);
                        File.WriteAllBytes(localPath, fileData);

                        int done = Interlocked.Increment(ref filesDone);
                        percentProgress.Report((int)((double)done / newManifest.Files.Count * 100));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                allDownloadTasks.Add(downloadTask);
            }

            statusProgress.Report($"Downloading {filesToDownload.Count} changed files...");
            await Task.WhenAll(allDownloadTasks);

            percentProgress.Report(100);
            statusProgress.Report("Download complete.");
        }

        public async Task StartSelfUpdateAsync(Product product, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Warn("Starting self-update...");
            // --- CHANGED: Use new GetFileManifestAsync signature ---
            FileManifest manifest = await _nextcloudClient.GetFileManifestAsync(product.Id, product.LatestVersion);
            string tempDir = Path.Combine(Path.GetTempPath(), "SCT_Updater_New");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            if (manifest.PackageMode == "zip")
                await StartSelfUpdate_Zip(manifest, tempDir, statusProgress, percentProgress);
            else
                await StartSelfUpdate_Files(manifest, tempDir, statusProgress, percentProgress);

            string exeName = Path.GetFileName(Application.ExecutablePath);
            string currentDir = Application.StartupPath;

            Log.Warn("Restarting via command line injection...");

            // Use the new fileless method
            Utility.RestartApp(currentDir, tempDir, exeName);
        }

        private async Task StartSelfUpdate_Zip(FileManifest manifest, string tempDir, IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Debug("Self-update mode: ZIP");
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
            Log.Debug("Self-update mode: FILES");
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

            statusProgress.Report("Download complete.");
        }

        /// <summary>
        /// NEW: Downloads the entire /drivers folder and executes the install.bat script.
        /// </summary>
        public async Task InstallDriversAsync(IProgress<string> statusProgress, IProgress<int> percentProgress)
        {
            Log.Info("Starting driver & framework downloading...");
            string localPath = Path.Combine(Application.StartupPath, AppConfig.LOCAL_DRIVERS_PATH);
            string serverUrl = AppConfig.NC_DRIVERS_URL;

            // 1. Clear old drivers directory
            statusProgress.Report("Clearing old driver files...");
            percentProgress.Report(0);
            try
            {
                Utility.DeletePath(localPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete old drivers directory. Aborting.");
                throw new Exception($"Failed to clear old 'drivers' directory. Close any open files and try again. Error: {ex.Message}", ex);
            }

            // 2. Get server file list (recursively)
            statusProgress.Report("Fetching server file list...");
            var serverFiles = await _nextcloudClient.ListAllFilesRecursiveAsync(serverUrl);
            if (!serverFiles.Any())
            {
                throw new Exception("No files found in the 'drivers' directory on the server.");
            }

            // 3. Ensure local directory exists
            Directory.CreateDirectory(localPath);

            // 4. Download all files (overwriting)
            Log.Debug($"Downloading {serverFiles.Count} driver files...");
            int filesDone = 0;
            var semaphore = new SemaphoreSlim(AppConfig.MAX_PARALLEL_DOWNLOADS);
            var allTasks = new List<Task>();

            foreach (var relativePath in serverFiles)
            {
                var downloadTask = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        // Note: relativePath might be "subdir/file.exe", Path.Combine handles this.
                        string fileUrl = $"{serverUrl}/{relativePath}";
                        string localFilePath = Path.Combine(localPath, relativePath);

                        // Ensure sub-directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));

                        byte[] data = await _nextcloudClient.DownloadFileBytesAsync(fileUrl);
                        File.WriteAllBytes(localFilePath, data);

                        int done = Interlocked.Increment(ref filesDone);
                        percentProgress.Report((int)((double)done / serverFiles.Count * 100));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to download driver file {relativePath}");
                        // Continue with other files
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                allTasks.Add(downloadTask);
            }

            statusProgress.Report($"Downloading {serverFiles.Count} driver files...");
            await Task.WhenAll(allTasks);

            percentProgress.Report(100);
            Log.Info("Driver download complete.");
            statusProgress.Report("Drivers are downloaded. Please run install.bat in 'drivers' folder");
        }
    }
}