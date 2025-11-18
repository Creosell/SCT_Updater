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
            // ... (code remains the same)
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

        /// <summary>
        /// NEW: Public helper to safely delete a file or directory.
        /// </summary>
        public static void DeletePath(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                else if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }
            }
            catch (Exception ex)
            {
                // Throw a specific error that the UI can catch
                throw new IOException($"Failed to delete '{fullPath}'. Close the module if it is running. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// NEW: Executes a batch file, requesting Admin privileges.
        /// </summary>
        public static void ExecuteBatchFile(string batchFilePath)
        {
            Log.Debug($"Executing batch file: {batchFilePath}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"{batchFilePath}\"", // /C carries out the command and then terminates
                    Verb = "runas", // Request admin elevation
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(batchFilePath)
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit(); // Wait for the installer to finish
                    Log.Debug($"Batch file execution finished with exit code {process.ExitCode}.");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223: The operation was canceled by the user (UAC prompt denied).
                if (ex.NativeErrorCode == 1223)
                {
                    Log.Warn("Driver installation was canceled by the user (UAC prompt).");
                    MessageBox.Show("Installation was canceled by the user.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    Log.Error(ex, "Failed to execute batch file.");
                    throw new Exception($"Failed to run installer: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to execute batch file.");
                throw new Exception($"Failed to run installer: {ex.Message}", ex);
            }
        }
    }
}