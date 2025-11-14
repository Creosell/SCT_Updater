// ManifestClasses.cs
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SCT_Updater
{
    /// <summary>
    /// Cтруктура для 'suite_manifest.json'.
    /// </summary>
    public class SuiteManifest
    {
        [JsonProperty("products")]
        public List<Product> Products { get; set; }
    }

    /// <summary>
    /// Представляет один продукт в 'suite_manifest.json'.
    /// </summary>
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

        [JsonProperty("manifest_url")]
        public string ManifestUrl { get; set; }

        // --- Локальные поля, не из JSON ---

        public string InstalledVersion { get; set; } = "Not Installed";
        public bool IsUpdateAvailable { get; set; } = false;
    }

    /// <summary>
    /// Cтруктура для 'local_versions.json' (локально).
    /// </summary>
    public class LocalVersions
    {
        [JsonProperty("installed_products")]
        public List<InstalledProduct> InstalledProducts { get; set; }
    }

    public class InstalledProduct
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }


    /// <summary>
    /// Cтруктура для файловых манифестов (напр. 'uploader_1.0.0.json').
    /// </summary>
    public class FileManifest
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }

        [JsonProperty("package_mode")]
        public string PackageMode { get; set; } // "zip" или "files"

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
}