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
        }

        public LocalVersions LoadLocalVersions()
        {
            if (!File.Exists(_localVersionsPath))
            {
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
            try
            {
                string json = File.ReadAllText(_localVersionsPath);
                return JsonConvert.DeserializeObject<LocalVersions>(json);
            }
            catch (Exception)
            {
                return new LocalVersions { InstalledProducts = new List<InstalledProduct>() };
            }
        }

        public void SaveLocalVersion(string productId, string newVersion)
        {
            LocalVersions localData = LoadLocalVersions();
            InstalledProduct product = localData.InstalledProducts.FirstOrDefault(p => p.Id == productId);

            if (newVersion == "Not Installed")
            {
                if (product != null)
                {
                    localData.InstalledProducts.Remove(product);
                }
            }
            else if (product != null)
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
                File.WriteAllText(_localVersionsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local version: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}