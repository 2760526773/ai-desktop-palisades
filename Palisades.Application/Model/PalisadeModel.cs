using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Palisades.Model
{
    [XmlRoot(Namespace = "io.stouder", ElementName = "PalisadeModel")]
    public class PalisadeModel
    {
        private string identifier;
        private string name;
        private int fenceX;
        private int fenceY;
        private int width;
        private int height;
        private ObservableCollection<Shortcut> shortcuts;
        private Color headerColor;
        private Color bodyColor;
        private Color titleColor;
        private Color labelsColor;

        public PalisadeModel()
        {
            identifier = Guid.NewGuid().ToString();
            name = "新建栅栏";

            // 默认毛玻璃风格，避免纯黑块遮挡桌面。
            headerColor = Color.FromArgb(190, 255, 255, 255);
            bodyColor = Color.FromArgb(96, 230, 240, 255);
            titleColor = Color.FromArgb(255, 33, 37, 41);
            labelsColor = Color.FromArgb(255, 33, 37, 41);

            width = 520;
            height = 340;
            shortcuts = new();
        }

        public string Identifier { get { return identifier; } set { identifier = value; } }
        public string Name { get { return name; } set { name = value; } }

        public int FenceX { get { return fenceX; } set { fenceX = value; } }
        public int FenceY { get { return fenceY; } set { fenceY = value; } }

        public int Width { get { return width; } set { width = value; } }
        public int Height { get { return height; } set { height = value; } }

        public Color HeaderColor { get { return headerColor; } set { headerColor = value; } }
        public Color BodyColor { get { return bodyColor; } set { bodyColor = value; } }
        public Color TitleColor { get { return titleColor; } set { titleColor = value; } }
        public Color LabelsColor { get { return labelsColor; } set { labelsColor = value; } }

        [XmlArrayItem(typeof(LnkShortcut))]
        [XmlArrayItem(typeof(UrlShortcut))]
        [XmlArrayItem(typeof(FileShortcut))]
        public ObservableCollection<Shortcut> Shortcuts { get { return shortcuts; } set { shortcuts = value; } }
    }
}
