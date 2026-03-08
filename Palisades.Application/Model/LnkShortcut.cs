using System;

namespace Palisades.Model
{
    public class LnkShortcut : Shortcut
    {
        public LnkShortcut() : base()
        {
        }

        public LnkShortcut(string name, string iconPath, string uriOrFileAction) : base(name, iconPath, uriOrFileAction)
        {
        }

        public static string? TryResolveTargetPath(string shortcut)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return null;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic link = shell.CreateShortcut(shortcut);
                string targetPath = link.TargetPath as string ?? string.Empty;
                return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
            }
            catch
            {
                return null;
            }
        }

        public static LnkShortcut? BuildFrom(string shortcut, string palisadeIdentifier)
        {
            string name = Shortcut.GetName(shortcut);
            string iconPath = Shortcut.GetIcon(shortcut, palisadeIdentifier);

            // 直接执行 .lnk 本体，确保来源唯一且不因目标相同被误去重。
            return new LnkShortcut(name, iconPath, shortcut);
        }
    }
}
