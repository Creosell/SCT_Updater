// Utility.cs
using System;
using System.Diagnostics;
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
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = await Task.Run(() => sha256.ComputeHash(stream));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static void RestartApp(string currentDir, string tempDir, string exeName)
        {
            // Chain commands directly in arguments to avoid creating a .bat file on disk (Dropper behavior)
            // 1. Wait 2 seconds (ping hack)
            // 2. Robocopy moves files (more robust than xcopy/move)
            // 3. Remove temp dir
            // 4. Start app
            string cmdArgs = $"/C ping 127.0.0.1 -n 2 > nul & " +
                             $"robocopy \"{tempDir}\" \"{currentDir}\" /E /IS /MOVE > nul & " +
                             $"rmdir /s /q \"{tempDir}\" & " +
                             $"start \"\" \"{Path.Combine(currentDir, exeName)}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdArgs,
                WindowStyle = ProcessWindowStyle.Hidden, // Hide the black window
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Application.Exit();
        }

        public static void DeletePath(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
                else if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to delete '{fullPath}'. Error: {ex.Message}", ex);
            }
        }
    }
}