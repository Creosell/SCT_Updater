// Utility.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SCT_Updater
{
    public static class Utility
    {
        public static async Task<string> GetFileHashAsync(string filePath)
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

        public static void CreateUpdateStub(string currentDir, string tempDir, string exeName)
        {
            string stubPath = Path.Combine(currentDir, AppConfig.UPDATE_STUB_FILENAME);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("echo Updating application...");
            sb.AppendLine("echo Waiting for application to close...");
            sb.AppendLine("timeout /t 2 /nobreak > nul");
            sb.AppendLine($"echo Copying new files from {tempDir}...");
            sb.AppendLine($"xcopy \"{tempDir}\" \"{currentDir}\" /E /I /Y /Q");
            sb.AppendLine("echo Cleaning up temporary files...");
            sb.AppendLine($"rmdir /s /q \"{tempDir}\"");
            sb.AppendLine($"echo Relaunching {exeName}...");
            sb.AppendLine($"start \"\" \"{Path.Combine(currentDir, exeName)}\"");
            sb.AppendLine("echo Self-deleting stub...");
            sb.AppendLine($"(goto) 2>nul & del \"{stubPath}\"");
            File.WriteAllText(stubPath, sb.ToString());
        }
    }
}