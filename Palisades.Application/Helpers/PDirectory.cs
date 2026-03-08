using System;
using System.IO;

namespace Palisades.Helpers
{
    internal static class PDirectory
    {
        internal static string GetAppDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PEnv.IsDev() ? "PalisadesDev" : "Palisades");
        }

        internal static string GetPalisadesDirectory()
        {
            return Path.Combine(GetAppDirectory(), "saved");
        }

        internal static string GetPalisadeDirectory(string identifier)
        {
            return Path.Combine(GetPalisadesDirectory(), identifier);
        }

        internal static string GetPalisadeIconsDirectory(string identifier)
        {
            return Path.Combine(GetPalisadeDirectory(identifier), "icons");
        }

        internal static string GetManagedItemsRootDirectory()
        {
            return Path.Combine(GetAppDirectory(), "managed-items");
        }

        internal static string GetManagedCategoryDirectory(string categoryName)
        {
            return Path.Combine(GetManagedItemsRootDirectory(), categoryName);
        }

        internal static void EnsureExists(string directory)
        {
            DirectoryInfo infos = new(directory);
            if (!infos.Exists)
            {
                infos.Create();
            }
        }
    }
}
