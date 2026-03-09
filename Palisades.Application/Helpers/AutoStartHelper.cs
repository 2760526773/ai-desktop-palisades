using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace Palisades.Helpers
{
    internal static class AppLaunchHelper
    {
        internal static string GetPreferredExecutablePath()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) &&
                !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(processPath))
            {
                return processPath;
            }

            string baseDirectory = AppContext.BaseDirectory;
            string exeCandidate = Path.Combine(baseDirectory, "Palisades.exe");
            if (File.Exists(exeCandidate))
            {
                return exeCandidate;
            }

            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? baseDirectory;
            exeCandidate = Path.Combine(assemblyDirectory, "Palisades.exe");
            if (File.Exists(exeCandidate))
            {
                return exeCandidate;
            }

            return processPath ?? exeCandidate;
        }
    }

    internal static class AutoStartHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AI Desktop Palisades";

        internal static bool IsEnabled()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            string? current = key?.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(current))
            {
                return false;
            }

            return string.Equals(current, BuildCommand(), StringComparison.OrdinalIgnoreCase);
        }

        internal static void SetEnabled(bool enabled)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath) ?? throw new InvalidOperationException("无法打开启动项注册表。");
            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand());
                return;
            }

            if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }

        internal static string BuildCommand()
        {
            return $"\"{AppLaunchHelper.GetPreferredExecutablePath()}\"";
        }
    }
}

