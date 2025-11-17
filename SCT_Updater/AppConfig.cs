// AppConfig.cs
using System;
using System.IO;
using System.Windows.Forms;

namespace SCT_Updater
{
    public static class AppConfig
    {
        // --- App Constants ---
        public const string LOCAL_VERSIONS_FILE = "local_versions.json";
        public const string UPDATER_ID = "suite_updater";
        public const string UPDATE_STUB_FILENAME = "update_stub.bat";
        public const int MAX_PARALLEL_DOWNLOADS = 20;

        // --- Nextcloud Path ---
        private const string NC_FILES_PATH = "/SCT/Updater";

        // --- Loaded from .env ---
        public static string NC_USER { get; private set; }
        public static string NC_PASSWORD { get; private set; }
        public static string NC_SERVER_URL { get; private set; }

        // --- Derived properties ---
        public static string NC_WEBDAV_BASE_URL { get; private set; }
        public static string SUITE_MANIFEST_URL { get; private set; }

        public static void LoadEnvConfiguration()
        {
            var envPath = Path.Combine(Application.StartupPath, ".env");
            if (!File.Exists(envPath)) throw new FileNotFoundException(".env file not found!");

            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');
                switch (key)
                {
                    case "NC_USER": NC_USER = value; break;
                    case "NC_PASSWORD": NC_PASSWORD = value; break;
                    case "NC_SERVER_URL": NC_SERVER_URL = value; break;
                }
            }

            if (string.IsNullOrEmpty(NC_USER) || string.IsNullOrEmpty(NC_PASSWORD) || string.IsNullOrEmpty(NC_SERVER_URL))
            {
                throw new Exception("Failed to load NC_USER, NC_PASSWORD, or NC_SERVER_URL from .env.");
            }

            // Set derived properties
            NC_WEBDAV_BASE_URL = $"{NC_SERVER_URL}/remote.php/dav/files/{NC_USER}{NC_FILES_PATH}";
            SUITE_MANIFEST_URL = $"{NC_WEBDAV_BASE_URL}/suite_manifest.json";
        }
    }
}