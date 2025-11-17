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
using System.Net; // For ServicePointManager
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading; // For SemaphoreSlim
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SCT_Updater
{
    public partial class Form1 : Form
    {
        // --- Services ---
        private readonly NextcloudClient _nextcloudClient;
        private readonly LocalStateService _localState;
        private readonly UpdateService _updateService;

        // --- State ---
        private bool _isUpdating = false;
        private Product _selectedProduct; // For context menu

        public Form1()
        {
            InitializeComponent();

            ServicePointManager.DefaultConnectionLimit = AppConfig.MAX_PARALLEL_DOWNLOADS + 4;

            try
            {
                AppConfig.LoadEnvConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical error: {ex.Message}", ".env Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // Initialize services
            _nextcloudClient = new NextcloudClient();
            _localState = new LocalStateService();
            _updateService = new UpdateService(_nextcloudClient, _localState);
        }

        #region Form Load and UI Setup

        private async void Form1_Load(object sender, EventArgs e)
        {
            lblStatus.Text = "Configuring UI...";
            SetupDataGridView();
            dgvModules.DataSource = null;

            lblStatus.Text = "Checking for updates...";
            await RunUpdateCheck();
        }

        private void SetupDataGridView()
        {
            dgvModules.AutoGenerateColumns = false;
            dgvModules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvModules.RowHeadersVisible = false;
            dgvModules.AllowUserToAddRows = false;
            dgvModules.AllowUserToDeleteRows = false;
            dgvModules.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn { Name = "ModuleName", HeaderText = "Module", DataPropertyName = "Name", ReadOnly = true, FillWeight = 150 });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn { Name = "Installed", HeaderText = "Installed", DataPropertyName = "InstalledVersion", ReadOnly = true, FillWeight = 70 });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn { Name = "Available", HeaderText = "Available", DataPropertyName = "LatestVersion", ReadOnly = true, FillWeight = 70 });
            dgvModules.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", DataPropertyName = "Description", ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }, FillWeight = 200 });
            dgvModules.Columns.Add(new DataGridViewButtonColumn { Name = "UpdateAction", HeaderText = "", Text = "Update", UseColumnTextForButtonValue = false, FillWeight = 60 });
        }

        #endregion

        #region Update Check and UI State

        /// <summary>
        /// Main wrapper for checking updates and handling self-update.
        /// </summary>
        private async Task RunUpdateCheck()
        {
            SetUiState(isChecking: true);

            try
            {
                var result = await _updateService.CheckForUpdatesAsync(GetCurrentVersion());

                if (result.SelfUpdateProduct != null)
                {
                    // A self-update is required.
                    lblStatus.Text = "Updating the updater...";
                    MessageBox.Show($"A new version of the updater ({result.SelfUpdateProduct.LatestVersion}) is required. The application will now restart.", "Update Required", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    var statusProgress = new Progress<string>(status => lblStatus.Text = status);
                    var percentProgress = new Progress<int>(percent => progressBar.Value = percent);

                    await _updateService.StartSelfUpdateAsync(result.SelfUpdateProduct, statusProgress, percentProgress);

                    // Launch the stub and exit
                    LaunchStubAndExit();
                    return;
                }

                // No self-update, just refresh the module list
                BindingSource bs = new BindingSource { DataSource = result.ProductsToShow };
                dgvModules.DataSource = bs;
                UpdateRowButtons();
                lblStatus.Text = "Update check complete.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error checking updates.";
                MessageBox.Show($"Could not check for updates: {ex.Message}\n\nURL: {AppConfig.SUITE_MANIFEST_URL}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiState(isChecking: false);
            }
        }

        private string GetCurrentVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private void SetUiState(bool isChecking)
        {
            _isUpdating = isChecking;
            btnCheckUpdates.Enabled = !isChecking;
            dgvModules.Enabled = !isChecking;
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

        #region Button Click Handlers

        private async void btnCheckUpdates_Click(object sender, EventArgs e)
        {
            lblStatus.Text = "Checking for updates...";
            await RunUpdateCheck();
        }

        private async void dgvModules_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dgvModules.Columns["UpdateAction"].Index) return;
            if (_isUpdating)
            {
                MessageBox.Show("Another update is already in progress.", "Please Wait", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Product productToUpdate = dgvModules.Rows[e.RowIndex].DataBoundItem as Product;
            if (productToUpdate == null || !productToUpdate.IsUpdateAvailable) return;

            SetUiState(isChecking: true);
            lblStatus.Text = $"Starting update for {productToUpdate.Name}...";
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = 0;

            // Setup progress reporting
            var statusProgress = new Progress<string>(status => lblStatus.Text = status);
            var percentProgress = new Progress<int>(percent => progressBar.Value = percent);

            try
            {
                FileManifest manifest = await _nextcloudClient.GetFileManifestAsync(productToUpdate.ManifestUrl);

                if (manifest.PackageMode == "zip")
                {
                    await _updateService.StartModuleUpdate_Zip(manifest, statusProgress, percentProgress);
                }
                else
                {
                    await _updateService.StartModuleUpdate_Files(manifest, statusProgress, percentProgress);
                }

                // Update local state
                _localState.SaveLocalVersion(productToUpdate.Id, productToUpdate.LatestVersion);

                // Refresh grid
                productToUpdate.InstalledVersion = productToUpdate.LatestVersion;
                productToUpdate.IsUpdateAvailable = false;
                (dgvModules.DataSource as BindingSource).ResetCurrentItem();
                UpdateRowButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update {productToUpdate.Name}: {ex.Message}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiState(isChecking: false);
                lblStatus.Text = "Ready.";
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = 0;
            }
        }

        #endregion

        #region Context Menu Handlers

        private void cmsRowMenu_Opening(object sender, CancelEventArgs e)
        {
            var mousePos = dgvModules.PointToClient(Cursor.Position);
            var hitTest = dgvModules.HitTest(mousePos.X, mousePos.Y);

            if (hitTest.Type == DataGridViewHitTestType.Cell && hitTest.RowIndex >= 0)
            {
                _selectedProduct = dgvModules.Rows[hitTest.RowIndex].DataBoundItem as Product;
                tsmiReinstall.Enabled = (_selectedProduct != null && _selectedProduct.InstalledVersion != "Not Installed");
            }
            else
            {
                _selectedProduct = null;
                e.Cancel = true;
            }
        }

        private async void tsmiReinstall_Click(object sender, EventArgs e)
        {
            if (_selectedProduct == null || _isUpdating) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to re-install '{_selectedProduct.Name}'?\n" +
                "This will mark the module as uninstalled, allowing you to download it again.",
                "Confirm Re-install",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.No) return;

            SetUiState(isChecking: true);
            lblStatus.Text = $"Preparing to re-install {_selectedProduct.Name}...";

            try
            {
                // Simple way: Remove from local_versions.json
                _localState.SaveLocalVersion(_selectedProduct.Id, "Not Installed");

                // Force a full re-check
                lblStatus.Text = "Refreshing product list...";
                await RunUpdateCheck();

                lblStatus.Text = $"Ready to re-install '{_selectedProduct.Name}'. Click 'Install'.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during re-install prep: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiState(isChecking: false);
            }
        }

        #endregion

        private void LaunchStubAndExit()
        {
            string exeName = Path.GetFileName(Application.ExecutablePath);
            string currentDir = Application.StartupPath;

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(currentDir, AppConfig.UPDATE_STUB_FILENAME),
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = currentDir
            });

            Application.Exit();
        }
    }
}