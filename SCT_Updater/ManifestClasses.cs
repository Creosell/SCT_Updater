// ManifestClasses.cs
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SCT_Updater
{
    public class SuiteManifest
    {
        [JsonProperty("products")]
        public List<Product> Products { get; set; }
    }

    public class Product
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }

        // Local-only fields
        public string InstalledVersion { get; set; } = "Not Installed";
        public bool IsUpdateAvailable { get; set; } = false;
    }

    public class LocalVersions
    {
        [JsonProperty("installed_products")]
        public List<InstalledProduct> InstalledProducts { get; set; } = new List<InstalledProduct>();
    }

    public class InstalledProduct
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public class FileManifest
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }
        [JsonProperty("package_mode")]
        public string PackageMode { get; set; } // "zip" or "files"
        [JsonProperty("files")]
        public List<ManifestFile> Files { get; set; }
    }

    public class ManifestFile
    {
        [JsonProperty("path")]
        public string Path { get; set; }
        [JsonProperty("hash")]
        public string Hash { get; set; } // SHA256
    }

    // A helper class to return results from CheckForUpdatesAsync
    public class UpdateCheckResult
    {
        public List<Product> ProductsToShow { get; set; }
        public Product SelfUpdateProduct { get; set; }
    }
}