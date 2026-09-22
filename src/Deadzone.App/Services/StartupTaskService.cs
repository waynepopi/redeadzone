using System;
using System.Diagnostics;
using System.IO;

namespace Deadzone.App.Services;

public static class StartupTaskService
{
    private const string TaskName = "Deadzone";

    public static bool IsStartupTaskEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartupTask(bool enable, out string? errorMessage)
    {
        errorMessage = null;

        if (enable)
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                errorMessage = "Could not determine current executable path.";
                return false;
            }

            try
            {
                // Create or update elevated logon task with --startup argument
                string trArgument = $"\\\"{exePath}\\\" --startup";
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN \"{TaskName}\" /TR \"{trArgument}\" /SC ONLOGON /RL HIGHEST /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    errorMessage = "Failed to launch schtasks.exe.";
                    return false;
                }

                process.WaitForExit(5000);
                string stderr = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    errorMessage = string.IsNullOrWhiteSpace(stderr)
                        ? $"schtasks failed with exit code {process.ExitCode}"
                        : stderr.Trim();
                    return false;
                }

                // Verify creation succeeded (§54)
                if (!IsStartupTaskEnabled())
                {
                    errorMessage = "Task creation succeeded but could not be verified in Task Scheduler.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        else
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(5000);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
