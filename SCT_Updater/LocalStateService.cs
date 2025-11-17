// LocalStateService.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SCT_Updater
{
    public class LocalStateService
    {
        private readonly string _localVersionsPath;

        public LocalStateService()
        {
            _localVersionsPath = Path.Combine(Application.StartupPath, AppConfig.LOCAL_VERSIONS_FILE);
            Log.Debug($"Local state file path set to: {_localVersionsPath}");
        }

        public LocalVersions LoadLocalVersions()
        {
            Log.Debug("Loading local versions file...");
            if (!File.Exists(_localVersionsPath))
            {
                Log.Warn("local_versions.json not found. Returning new list.");
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
            try
            {
                string json = File.ReadAllText(_localVersionsPath);
                return JsonConvert.DeserializeObject<LocalVersions>(json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse local_versions.json. Returning new list.");
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
        }

        public void SaveLocalVersion(string productId, string newVersion)
        {
            Log.Debug($"Saving local version for {productId}: {newVersion}");
            LocalVersions localData = LoadLocalVersions();
            InstalledProduct product = localData.InstalledProducts.FirstOrDefault(p => p.Id == productId);

            if (newVersion == "Not Installed")
            {
                if (product != null)
                {
                    Log.Debug($"Removing {productId} from local state.");
                    localData.InstalledProducts.Remove(product);
                }
            }
            else if (product != null)
            {
                Log.Debug($"Updating {productId} to {newVersion} in local state.");
                product.Version = newVersion;
            }
            else
            {
                Log.Debug($"Adding {productId} as {newVersion} to local state.");
                localData.InstalledProducts.Add(new InstalledProduct { Id = productId, Version = newVersion });
            }

            try
            {
                string json = JsonConvert.SerializeObject(localData, Formatting.Indented);
                File.WriteAllText(_localVersionsPath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save local_versions.json.");
                MessageBox.Show($"Failed to save local version: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}