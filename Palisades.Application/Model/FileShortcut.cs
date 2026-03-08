namespace Palisades.Model
{
    public class FileShortcut : Shortcut
    {
        public FileShortcut() : base()
        {
        }

        public FileShortcut(string name, string iconPath, string uriOrFileAction) : base(name, iconPath, uriOrFileAction)
        {
        }

        public static FileShortcut BuildFrom(string filepath, string palisadeIdentifier)
        {
            string name = Shortcut.GetName(filepath);
            string iconPath = Shortcut.GetIcon(filepath, palisadeIdentifier);
            return new FileShortcut(name, iconPath, filepath);
        }
    }
}
