// Form1.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression; // For ZipFile
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SCT_Updater
{
    public partial class Form1 : Form
    {
        // --- Configuration (Loaded from .env) ---
        private static string NC_USER;
        private static string NC_PASSWORD;
        private static string NC_SERVER_URL;

        // !!! YOU MUST DEFINE THIS PATH !!!
        // This is the path to your files on Nextcloud *after* /remote.php/dav/files/Robot/
        // Example: "/SCT_Updater_Files" (must start with a slash)
        private const string NC_FILES_PATH = "/SCT/Updater"; // <--- Set your path here

        // --- Application Constants (Initialized in Constructor) ---
        private static string NC_WEBDAV_BASE_URL;
        private static string SUITE_MANIFEST_URL;
        private const string LOCAL_VERSIONS_FILE = "local_versions.json";
        private const string UPDATER_ID = "suite_updater";
        private const string UPDATE_STUB_FILENAME = "update_stub.bat";

        // --- State ---
        private static HttpClient httpClient;
        private List<Product> allProducts = new List<Product>();
        private bool isUpdating = false;

        public Form1()
        {
            InitializeComponent();

            // 1. Load .env config first
            try
            {
                LoadEnvConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical error: {ex.Message}", ".env Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // 2. Set dynamic URLs based on .env
            NC_WEBDAV_BASE_URL = $"{NC_SERVER_URL}/remote.php/dav/files/{NC_USER}{NC_FILES_PATH}";
            SUITE_MANIFEST_URL = $"{NC_WEBDAV_BASE_URL}/suite_manifest.json";

            // 3. Create the HttpClient with authentication
            httpClient = CreateNextcloudHttpClient();
        }

        /// <summary>
        /// Loads credentials from the .env file in the root directory.
        /// </summary>
        private void LoadEnvConfiguration()
        {
            var envPath = Path.Combine(Application.StartupPath, ".env");
            if (!File.Exists(envPath))
            {
                throw new FileNotFoundException(".env file not found!");
            }

            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');

                switch (key)
                {
                    case "NC_USER":
                        NC_USER = value;
                        break;
                    case "NC_PASSWORD":
                        NC_PASSWORD = value;
                        break;
                    case "NC_SERVER_URL":
                        NC_SERVER_URL = value;
                        break;
                }
            }

            if (string.IsNullOrEmpty(NC_USER) || string.IsNullOrEmpty(NC_PASSWORD) || string.IsNullOrEmpty(NC_SERVER_URL))
            {
                throw new Exception("Failed to load NC_USER, NC_PASSWORD, or NC_SERVER_URL from .env.");
            }
        }

        /// <summary>
        /// Creates the HttpClient with Basic Auth headers.
        /// </summary>
        private static HttpClient CreateNextcloudHttpClient()
        {
            var client = new HttpClient();
            var authToken = System.Text.Encoding.UTF8.GetBytes($"{NC_USER}:{NC_PASSWORD}");
            var headerValue = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(authToken));
            client.DefaultRequestHeaders.Authorization = headerValue;
            return client;
        }

        #region Form Load and UI Setup

        private async void Form1_Load(object sender, EventArgs e)
        {
            lblStatus.Text = "Configuring UI...";
            SetupDataGridView();
            dgvModules.DataSource = null;

            lblStatus.Text = "Checking for updates...";
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // 1. Download the main manifest
                string suiteManifestJson = await httpClient.GetStringAsync(SUITE_MANIFEST_URL);
                SuiteManifest suiteManifest = JsonConvert.DeserializeObject<SuiteManifest>(suiteManifestJson);

                // --- Forced Self-Update Check ---
                Product self = suiteManifest.Products.FirstOrDefault(p => p.Id == UPDATER_ID);
                if (self != null)
                {
                    string currentVersion = GetCurrentVersion();
                    if (self.LatestVersion != currentVersion)
                    {
                        lblStatus.Text = "Updating the updater...";
                        MessageBox.Show($"A new version of the updater ({self.LatestVersion}) is required. The application will now restart.", "Update Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await StartSelfUpdateAsync(self);
                        return;
                    }
                }

                // 2. Load local versions
                LocalVersions localData = LoadLocalVersions();

                // 3. Compare versions
                allProducts.Clear();
                foreach (var serverProduct in suiteManifest.Products)
                {
                    if (serverProduct.Id == UPDATER_ID) continue; // Don't show the updater itself in the list

                    InstalledProduct localProduct = localData.InstalledProducts.FirstOrDefault(p => p.Id == serverProduct.Id);

                    if (localProduct != null)
                    {
                        serverProduct.InstalledVersion = localProduct.Version;
                        if (serverProduct.LatestVersion != localProduct.Version)
                        {
                            serverProduct.IsUpdateAvailable = true;
                        }
                    }
                    else
                    {
                        serverProduct.InstalledVersion = "Not Installed";
                        serverProduct.IsUpdateAvailable = true;
                    }
                    allProducts.Add(serverProduct);
                }

                // 4. Bind to DataGridView
                BindingSource bs = new BindingSource();
                bs.DataSource = allProducts;
                dgvModules.DataSource = bs;

                // 5. Update buttons
                UpdateRowButtons();
                lblStatus.Text = "Update check complete.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error checking updates.";
                MessageBox.Show($"Could not check for updates: {ex.Message}\n\nURL: {SUITE_MANIFEST_URL}\n\n(This might be a 404 error for 'suite_manifest.json' or a file manifest, check your Nextcloud paths)", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetCurrentVersion()
        {
            // Reads the version from AssemblyInfo
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private void SetupDataGridView()
        {
            dgvModules.AutoGenerateColumns = false;
            dgvModules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvModules.RowHeadersVisible = false;
            dgvModules.AllowUserToAddRows = false;
            dgvModules.AllowUserToDeleteRows = false;
            dgvModules.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            dgvModules.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ModuleName",
                HeaderText = "Module",
                DataPropertyName = "Name",
                ReadOnly = true,
                FillWeight = 150
            });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Installed",
                HeaderText = "Installed",
                DataPropertyName = "InstalledVersion",
                ReadOnly = true,
                FillWeight = 70
            });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Available",
                HeaderText = "Available",
                DataPropertyName = "LatestVersion",
                ReadOnly = true,
                FillWeight = 70
            });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                DataPropertyName = "Description",
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
                FillWeight = 200
            });
            dgvModules.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "UpdateAction",
                HeaderText = "",
                Text = "Update",
                UseColumnTextForButtonValue = false,
                FillWeight = 60
            });
        }

        private void UpdateRowButtons()
        {
            foreach (DataGridViewRow row in dgvModules.Rows)
            {
                Product product = row.DataBoundItem as Product;
                if (product != null)
                {
                    var buttonCell = row.Cells["UpdateAction"] as DataGridViewButtonCell;
                    if (product.IsUpdateAvailable)
                    {
                        buttonCell.Value = (product.InstalledVersion == "Not Installed") ? "Install" : "Update";
                        buttonCell.Style.BackColor = Color.LightGreen;
                        buttonCell.Style.ForeColor = Color.Black;
                    }
                    else
                    {
                        buttonCell.Value = "✓";
                        buttonCell.Style.BackColor = Color.LightGray;
                        buttonCell.Style.ForeColor = Color.DarkGray;
                    }
                }
            }
        }

        #endregion

        #region Button Click and Hybrid Update Logic

        private async void dgvModules_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dgvModules.Columns["UpdateAction"].Index) return;
            if (isUpdating)
            {
                MessageBox.Show("Another update is already in progress.", "Please Wait", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Product productToUpdate = dgvModules.Rows[e.RowIndex].DataBoundItem as Product;
            if (productToUpdate == null || !productToUpdate.IsUpdateAvailable) return;

            isUpdating = true;
            dgvModules.Enabled = false;
            lblStatus.Text = $"Starting update for {productToUpdate.Name}...";
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = 0;

            try
            {
                // --- HYBRID LOGIC ---
                lblStatus.Text = $"Downloading file list for {productToUpdate.Name}...";
                string fullManifestUrl = $"{NC_WEBDAV_BASE_URL}/{productToUpdate.ManifestUrl}";
                string fileManifestJson = await httpClient.GetStringAsync(fullManifestUrl);
                FileManifest manifest = JsonConvert.DeserializeObject<FileManifest>(fileManifestJson);

                if (manifest.PackageMode == "zip")
                {
                    await StartModuleUpdate_Zip(productToUpdate, manifest);
                }
                else // "files" or null (default)
                {
                    await StartModuleUpdate_Files(productToUpdate, manifest);
                }
                // --- END HYBRID LOGIC ---

                // Update UI
                productToUpdate.InstalledVersion = productToUpdate.LatestVersion;
                productToUpdate.IsUpdateAvailable = false;
                SaveLocalVersion(productToUpdate.Id, productToUpdate.LatestVersion);

                (dgvModules.DataSource as BindingSource).ResetCurrentItem();
                UpdateRowButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update {productToUpdate.Name}: {ex.Message}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isUpdating = false;
                dgvModules.Enabled = true;
                lblStatus.Text = "Ready.";
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = 0;
            }
        }

        /// <summary>
        /// Update logic for "zip" package_mode.
        /// </summary>
        private async Task StartModuleUpdate_Zip(Product product, FileManifest manifest)
        {
            var fileToDownload = manifest.Files.FirstOrDefault();
            if (fileToDownload == null) throw new Exception("File manifest is empty.");

            string localDir = Path.Combine(Application.StartupPath, "modules", product.Id);
            string tempZipPath = Path.Combine(Path.GetTempPath(), fileToDownload.Path);

            if (File.Exists(tempZipPath))
            {
                string localHash = await GetFileHashAsync(tempZipPath);
                if (localHash.Equals(fileToDownload.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    lblStatus.Text = "ZIP file already downloaded. Skipping...";
                }
                else
                {
                    File.Delete(tempZipPath);
                }
            }

            if (!File.Exists(tempZipPath))
            {
                lblStatus.Text = $"Downloading {fileToDownload.Path}...";
                string fullFileUrl = BuildFullFileUrl(manifest.BaseUrl, fileToDownload.Path);

                using (HttpResponseMessage response = await httpClient.GetAsync(fullFileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode(); // Check for 404 etc.

                    using (Stream streamToRead = await response.Content.ReadAsStreamAsync())
                    using (Stream streamToWrite = File.Open(tempZipPath, FileMode.Create))
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
                                progressBar.Value = (int)((double)totalRead / totalBytes * 100);
                            }
                        }
                    }
                }
            }

            lblStatus.Text = $"Extracting {fileToDownload.Path}...";
            progressBar.Style = ProgressBarStyle.Marquee;

            if (Directory.Exists(localDir)) Directory.Delete(localDir, true);
            Directory.CreateDirectory(localDir);

            try
            {
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(tempZipPath, localDir);
                });

                File.Delete(tempZipPath);
                lblStatus.Text = $"Update complete for {product.Name}.";
            }
            catch (InvalidDataException ex)
            {
                string errorMsg = $"Failed to unzip file: {ex.Message}\n" +
                                  "Ensure you uploaded a .ZIP archive, not .7z or .RAR.\n\n" +
                                  $"File for inspection: {tempZipPath}";
                MessageBox.Show(errorMsg, "Unzip Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw new Exception("Unzip failed.", ex);
            }
        }

        /// <summary>
        /// Update logic for "files" package_mode.
        /// </summary>
        private async Task StartModuleUpdate_Files(Product product, FileManifest manifest)
        {
            int filesDone = 0;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Blocks;

            foreach (var file in manifest.Files)
            {
                string localDir = Path.Combine(Application.StartupPath, "modules", product.Id);
                string localPath = Path.Combine(localDir, file.Path);

                Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                if (File.Exists(localPath))
                {
                    string localHash = await GetFileHashAsync(localPath);
                    if (localHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        filesDone++;
                        progressBar.Value = (int)((double)filesDone / manifest.Files.Count * 100);
                        continue; // File matches, skip
                    }
                }

                lblStatus.Text = $"Downloading {file.Path}...";
                string fullFileUrl = BuildFullFileUrl(manifest.BaseUrl, file.Path);

                byte[] fileData = await httpClient.GetByteArrayAsync(fullFileUrl);
                File.WriteAllBytes(localPath, fileData);

                filesDone++;
                progressBar.Value = (int)((double)filesDone / manifest.Files.Count * 100);
            }

            lblStatus.Text = $"Update complete for {product.Name}.";
        }


        /// <summary>
        /// Self-update logic (always uses ZIP).
        /// </summary>
        private async Task StartSelfUpdateAsync(Product product)
        {
            lblStatus.Text = "Downloading updater manifest...";
            string fullManifestUrl = $"{NC_WEBDAV_BASE_URL}/{product.ManifestUrl}";
            string fileManifestJson = await httpClient.GetStringAsync(fullManifestUrl);
            FileManifest manifest = JsonConvert.DeserializeObject<FileManifest>(fileManifestJson);

            // We expect self-update to always be a ZIP
            var fileToDownload = manifest.Files.FirstOrDefault();
            if (fileToDownload == null || manifest.PackageMode != "zip")
            {
                throw new Exception("Self-update manifest is misconfigured (must be 'zip').");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "SCT_Updater_New");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            // 1. Download ZIP to temp folder
            lblStatus.Text = $"Downloading {fileToDownload.Path}...";
            string fullFileUrl = BuildFullFileUrl(manifest.BaseUrl, fileToDownload.Path);
            string tempZipPath = Path.Combine(tempDir, fileToDownload.Path);

            using (HttpResponseMessage response = await httpClient.GetAsync(fullFileUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (Stream streamToRead = await response.Content.ReadAsStreamAsync())
                {
                    using (Stream streamToWrite = File.Open(tempZipPath, FileMode.Create))
                    {
                        await streamToRead.CopyToAsync(streamToWrite);
                    }
                }
            }

            // 2. Extract ZIP in temp folder
            lblStatus.Text = "Extracting update...";
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(tempZipPath, tempDir);
            });
            File.Delete(tempZipPath); // Delete .zip, keep files

            // 3. Create .bat stub
            string exeName = Path.GetFileName(Application.ExecutablePath);
            string currentDir = Application.StartupPath;
            CreateUpdateStub(currentDir, tempDir, exeName);

            // 4. Run stub (with admin rights) and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(currentDir, UPDATE_STUB_FILENAME),
                UseShellExecute = true,
                Verb = "runas", // Request admin rights to overwrite .exe
                WorkingDirectory = currentDir
            });

            Application.Exit();
        }

        #endregion

        #region Helper Methods (File I/O, Hashing, Stub)

        private string BuildFullFileUrl(string baseUrl, string filePath)
        {
            string fullFileUrl;
            // Trim leading slashes for robust path joining
            baseUrl = baseUrl.TrimStart('/');
            filePath = filePath.TrimStart('/');

            if (baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // BaseUrl is absolute
                fullFileUrl = $"{baseUrl.TrimEnd('/')}/{filePath}";
            }
            else
            {
                // BaseUrl is relative (e.g., "files/uploader/1.0.0")
                fullFileUrl = $"{NC_WEBDAV_BASE_URL}/{baseUrl.TrimEnd('/')}/{filePath}";
            }
            return fullFileUrl;
        }

        private void CreateUpdateStub(string currentDir, string tempDir, string exeName)
        {
            string stubPath = Path.Combine(currentDir, UPDATE_STUB_FILENAME);
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("@echo off");
            sb.AppendLine("echo Updating application...");
            sb.AppendLine("echo Waiting for application to close...");
            sb.AppendLine("timeout /t 2 /nobreak > nul");

            sb.AppendLine($"echo Copying new files from {tempDir}...");
            // xcopy /E (all subdirs) /I (create dir) /Y (overwrite) /Q (quiet)
            sb.AppendLine($"xcopy \"{tempDir}\" \"{currentDir}\" /E /I /Y /Q");

            sb.AppendLine("echo Cleaning up temporary files...");
            sb.AppendLine($"rmdir /s /q \"{tempDir}\"");

            sb.AppendLine($"echo Relaunching {exeName}...");
            sb.AppendLine($"start \"\" \"{Path.Combine(currentDir, exeName)}\"");

            sb.AppendLine("echo Self-deleting stub...");
            sb.AppendLine($"(goto) 2>nul & del \"{stubPath}\"");

            File.WriteAllText(stubPath, sb.ToString());
        }

        private LocalVersions LoadLocalVersions()
        {
            string localPath = Path.Combine(Application.StartupPath, LOCAL_VERSIONS_FILE);
            if (!File.Exists(localPath))
            {
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
            try
            {
                string json = File.ReadAllText(localPath);
                return JsonConvert.DeserializeObject<LocalVersions>(json);
            }
            catch (Exception)
            {
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
        }

        private void SaveLocalVersion(string productId, string newVersion)
        {
            LocalVersions localData = LoadLocalVersions();
            InstalledProduct product = localData.InstalledProducts.FirstOrDefault(p => p.Id == productId);

            if (product != null)
            {
                product.Version = newVersion;
            }
            else
            {
                localData.InstalledProducts.Add(new InstalledProduct { Id = productId, Version = newVersion });
            }

            try
            {
                string json = JsonConvert.SerializeObject(localData, Formatting.Indented);
                File.WriteAllText(Path.Combine(Application.StartupPath, LOCAL_VERSIONS_FILE), json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local version: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task<string> GetFileHashAsync(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = await Task.Run(() => sha256.ComputeHash(stream));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        #endregion
    }
}