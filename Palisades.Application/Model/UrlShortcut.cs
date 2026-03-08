using System.IO;

namespace Palisades.Model
{
    public class UrlShortcut : Shortcut
    {
        public UrlShortcut() : base()
        {
        }

        public UrlShortcut(string name, string iconPath, string uriOrFileAction) : base(name, iconPath, uriOrFileAction)
        {
        }

        public static UrlShortcut? BuildFrom(string shortcut, string palisadeIdentifier)
        {
            if (!File.Exists(shortcut))
            {
                return null;
            }

            string name = Shortcut.GetName(shortcut);
            string iconPath = Shortcut.GetIcon(shortcut, palisadeIdentifier);

            // 保留 .url 文件本体路径，便于右键/移动/属性等系统操作。
            return new UrlShortcut(name, iconPath, shortcut);
        }
    }
}
