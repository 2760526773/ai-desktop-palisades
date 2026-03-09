using Palisades.Helpers;
using Palisades.Model;
using Palisades.View;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Palisades.ViewModel
{
    public class PalisadeViewModel : INotifyPropertyChanged
    {
        private readonly PalisadeModel model;
        private volatile bool shouldSave;
        private Shortcut? selectedShortcut;

        public string Identifier
        {
            get { return model.Identifier; }
            set { model.Identifier = value; OnPropertyChanged(); Save(); }
        }

        public string Name
        {
            get { return model.Name; }
            set { model.Name = value; OnPropertyChanged(); Save(); }
        }

        public int FenceX
        {
            get { return model.FenceX; }
            set { model.FenceX = value; OnPropertyChanged(); Save(); }
        }

        public int FenceY
        {
            get { return model.FenceY; }
            set { model.FenceY = value; OnPropertyChanged(); Save(); }
        }

        public int Width
        {
            get { return model.Width; }
            set { model.Width = value; OnPropertyChanged(); Save(); }
        }

        public int Height
        {
            get { return model.Height; }
            set { model.Height = value; OnPropertyChanged(); Save(); }
        }

        public Color HeaderColor
        {
            get { return model.HeaderColor; }
            set { model.HeaderColor = value; OnPropertyChanged(); Save(); }
        }

        public Color BodyColor
        {
            get { return model.BodyColor; }
            set { model.BodyColor = value; OnPropertyChanged(); Save(); }
        }

        public SolidColorBrush TitleColor
        {
            get => new(model.TitleColor);
            set { model.TitleColor = value.Color; OnPropertyChanged(); Save(); }
        }

        public SolidColorBrush LabelsColor
        {
            get => new(model.LabelsColor);
            set { model.LabelsColor = value.Color; OnPropertyChanged(); Save(); }
        }

        public ObservableCollection<Shortcut> Shortcuts
        {
            get { return model.Shortcuts; }
            set { model.Shortcuts = value; OnPropertyChanged(); Save(); }
        }

        public Shortcut? SelectedShortcut
        {
            get => selectedShortcut;
            set { selectedShortcut = value; OnPropertyChanged(); }
        }

        public PalisadeViewModel() : this(new PalisadeModel()) { }

        public PalisadeViewModel(PalisadeModel model)
        {
            this.model = model;
            OnPropertyChanged();
            Shortcuts.CollectionChanged += (_, __) => Save();

            Thread saveThread = new(SaveAsync)
            {
                IsBackground = true,
                Name = "PalisadeSaveThread"
            };
            saveThread.Start();
        }

        public void Save()
        {
            shouldSave = true;
        }

        public void Delete()
        {
            string saveDirectory = PDirectory.GetPalisadeDirectory(Identifier);
            Directory.Delete(Path.Combine(saveDirectory), true);
        }

        public ICommand NewPalisadeCommand { get; private set; } = new RelayCommand(() =>
        {
            PalisadesManager.CreatePalisade();
        });

        public ICommand DeletePalisadeCommand { get; private set; } = new RelayCommand<string>((identifier) => PalisadesManager.DeletePalisade(identifier));

        public ICommand EditPalisadeCommand { get; private set; } = new RelayCommand<PalisadeViewModel>((viewModel) =>
        {
            EditPalisade edit = new()
            {
                DataContext = viewModel,
                Owner = PalisadesManager.GetPalisade(viewModel.Identifier)
            };
            edit.ShowDialog();
        });

        public ICommand OpenAboutCommand { get; private set; } = new RelayCommand<PalisadeViewModel>((viewModel) =>
        {
            About about = new()
            {
                DataContext = new AboutViewModel(),
                Owner = PalisadesManager.GetPalisade(viewModel.Identifier)
            };
            about.ShowDialog();
        });

        public ICommand DropShortcut
        {
            get
            {
                return new RelayCommand<DragEventArgs>(DropShortcutsHandler);
            }
        }

        public void DropShortcutsHandler(DragEventArgs dragEventArgs)
        {
            dragEventArgs.Handled = true;
            if (!dragEventArgs.Data.GetDataPresent(DataFormats.FileDrop))
            {
                dragEventArgs.Handled = false;
                return;
            }

            string[] paths = (string[])dragEventArgs.Data.GetData(DataFormats.FileDrop);
            foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                Shortcut? shortcutItem = PalisadesManager.MovePathIntoFenceAndBuildShortcut(path, this);
                if (shortcutItem == null)
                {
                    continue;
                }

                Shortcuts.Add(shortcutItem);
            }

            SelectedShortcut = null;
            Save();
            PalisadesManager.RemoveMissingShortcutsFromAllFences();
        }

        public ICommand ClickShortcut
        {
            get
            {
                return new RelayCommand<Shortcut>(SelectShortcut);
            }
        }

        public void SelectShortcut(Shortcut shortcut)
        {
            if (SelectedShortcut == shortcut)
            {
                SelectedShortcut = null;
                return;
            }
            SelectedShortcut = shortcut;
        }

        public ICommand DelKeyPressed
        {
            get
            {
                return new RelayCommand(DeleteShortcut);
            }
        }

        public void DeleteShortcut()
        {
            if (SelectedShortcut == null)
            {
                return;
            }

            Shortcuts.Remove(SelectedShortcut);
            SelectedShortcut = null;
        }

        private void SaveAsync()
        {
            while (true)
            {
                if (shouldSave)
                {
                    string saveDirectory = PDirectory.GetPalisadeDirectory(Identifier);
                    PDirectory.EnsureExists(saveDirectory);
                    using StreamWriter writer = new(Path.Combine(saveDirectory, "state.xml"));
                    XmlSerializer serializer = new(typeof(PalisadeModel), new Type[] { typeof(Shortcut), typeof(LnkShortcut), typeof(UrlShortcut), typeof(FileShortcut) });
                    serializer.Serialize(writer, this.model);
                    shouldSave = false;
                }
                Thread.Sleep(1000);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
