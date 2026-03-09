using Microsoft.VisualBasic.FileIO;
using Palisades.Helpers;
using Palisades.Model;
using Palisades.ViewModel;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;

namespace Palisades.View
{
    public partial class Palisade : Window
    {
        private const double ExpandedMinHeight = 220;
        private readonly PalisadeViewModel viewModel;
        private bool isLocked;
        private bool isCollapsed;
        private double expandedHeight;
        private Point dragStartPoint;
        private Shortcut? pendingDragShortcut;

        public Palisade(PalisadeViewModel defaultModel)
        {
            InitializeComponent();
            DataContext = defaultModel;
            viewModel = defaultModel;

            expandedHeight = Math.Max(Height, ExpandedMinHeight);
            ApplyLockState(false);
            ApplyCollapsedState(false);
            RefreshAutoStartMenuState();

            Show();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isLocked)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source)
            {
                if (FindAncestor<Button>(source) != null)
                {
                    return;
                }
            }

            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        private void ToggleCollapse_Click(object sender, RoutedEventArgs e)
        {
            isCollapsed = !isCollapsed;
            ApplyCollapsedState(isCollapsed);
            e.Handled = true;
        }

        private void ToggleLock_Click(object sender, RoutedEventArgs e)
        {
            isLocked = !isLocked;
            ApplyLockState(isLocked);
            e.Handled = true;
        }

        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            RefreshAutoStartMenuState();
            if (Header.ContextMenu != null)
            {
                Header.ContextMenu.PlacementTarget = MenuButton;
                Header.ContextMenu.Placement = PlacementMode.Bottom;
                Header.ContextMenu.IsOpen = true;
            }
            e.Handled = true;
        }

        private void RestartApplication_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = AppLaunchHelper.GetPreferredExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    throw new InvalidOperationException("未找到可执行文件路径。");
                }

                Process.Start(new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                });

                App.SuppressDesktopRestoreOnExit = true;
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重启失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            App.SuppressDesktopRestoreOnExit = false;
            Application.Current.Shutdown();
        }

        private void UnarchiveFenceToDesktop_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                $"确定要取消栅栏“{viewModel.Name}”中的分类，并将其中的受管项目移回桌面吗？",
                "确认取消分类",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                int restored = PalisadesManager.UnarchiveFenceToDesktop(viewModel);
                MessageBox.Show(restored > 0 ? $"已取消分类并移回桌面 {restored} 项。" : "当前栅栏没有可移回桌面的受管项目。", "栅栏", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消分类失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ToggleAutoStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem)
                {
                    AutoStartHelper.SetEnabled(menuItem.IsChecked);
                    RefreshAutoStartMenuState();
                }
            }
            catch (Exception ex)
            {
                RefreshAutoStartMenuState();
                MessageBox.Show($"设置开机自启动失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshAutoStartMenuState()
        {
            if (AutoStartMenuItem != null)
            {
                bool enabled = AutoStartHelper.IsEnabled();
                AutoStartMenuItem.IsChecked = enabled;
                AutoStartMenuItem.Header = enabled ? "开机自启动（已开启）" : "开机自启动（已关闭）";
            }
        }

        private void ApplyCollapsedState(bool collapsed)
        {
            double collapsedHeight = Header.ActualHeight > 0 ? Header.ActualHeight + 2 : 32;

            if (collapsed)
            {
                expandedHeight = Math.Max(ActualHeight > 0 ? ActualHeight : Height, ExpandedMinHeight);
                drag.Visibility = Visibility.Collapsed;
                Height = collapsedHeight;
                CollapseButton.Content = "v";
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                drag.Visibility = Visibility.Visible;
                Height = Math.Max(expandedHeight, ExpandedMinHeight);
                CollapseButton.Content = "^";
                if (!isLocked)
                {
                    ResizeMode = ResizeMode.CanResize;
                }
            }
        }

        private void ApplyLockState(bool locked)
        {
            LockButton.Content = locked ? "🔒" : "🔓";

            WindowChrome chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.ResizeBorderThickness = locked ? new Thickness(0) : new Thickness(4);
            }

            if (!isCollapsed)
            {
                ResizeMode = locked ? ResizeMode.NoResize : ResizeMode.CanResize;
            }
        }

        private void Shortcut_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(this);
            pendingDragShortcut = (sender as FrameworkElement)?.Tag as Shortcut;
        }

        private void Shortcut_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || pendingDragShortcut == null)
            {
                return;
            }

            Point current = e.GetPosition(this);
            if (Math.Abs(current.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            Shortcut shortcut = pendingDragShortcut;
            pendingDragShortcut = null;

            if (!File.Exists(shortcut.UriOrFileAction) && !Directory.Exists(shortcut.UriOrFileAction))
            {
                PalisadesManager.RemoveMissingShortcutsFromFence(viewModel);
                return;
            }

            DataObject dataObject = new();
            dataObject.SetData(DataFormats.FileDrop, new[] { shortcut.UriOrFileAction });
            dataObject.SetData("Preferred DropEffect", new MemoryStream(new byte[] { 2, 0, 0, 0 }));

            DragDropEffects effect = DragDrop.DoDragDrop((DependencyObject)sender, dataObject, DragDropEffects.Move | DragDropEffects.Copy);
            if (effect != DragDropEffects.None)
            {
                PalisadesManager.RemoveMissingShortcutsFromAllFences();
            }
        }

        private void Shortcut_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is Shortcut shortcut)
            {
                viewModel.SelectedShortcut = shortcut;
            }
        }

        private void Shortcut_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.ContextMenu == null)
            {
                return;
            }

            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.Placement = PlacementMode.MousePoint;
            element.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private async void OpenAiSettings_Click(object sender, RoutedEventArgs e)
        {
            AiSettings aiSettings = new()
            {
                Owner = this,
            };

            bool? saved = aiSettings.ShowDialog();
            if (saved == true)
            {
                await RunAiOrganizeInBackgroundAsync();
            }
        }

        private async void RunAiClassifyNow_Click(object sender, RoutedEventArgs e)
        {
            await RunAiOrganizeInBackgroundAsync();
        }

        private async Task RunAiOrganizeInBackgroundAsync()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                AutoOrganizePlan plan = await Task.Run(PalisadesManager.PrepareAutoOrganizeDesktopByAi);
                AutoOrganizeResult result = PalisadesManager.ApplyAutoOrganizePlan(plan);
                MessageBox.Show(result.StatusMessage, "AI 分类结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void OpenShortcut_Click(object sender, RoutedEventArgs e)
        {
            Shortcut? shortcut = GetShortcutFromCommandParameter(sender);
            if (shortcut == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(shortcut.UriOrFileAction) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenShortcutLocation_Click(object sender, RoutedEventArgs e)
        {
            Shortcut? shortcut = GetShortcutFromCommandParameter(sender);
            if (shortcut == null)
            {
                return;
            }

            string path = shortcut.UriOrFileAction;
            try
            {
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return;
                }

                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                    return;
                }

                MessageBox.Show("目标不存在。", "栅栏", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开位置失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MoveShortcutToDesktop_Click(object sender, RoutedEventArgs e)
        {
            Shortcut? shortcut = GetShortcutFromCommandParameter(sender);
            if (shortcut == null)
            {
                return;
            }

            if (!PalisadesManager.TryMoveShortcutToDesktop(shortcut, out _, out string message))
            {
                MessageBox.Show(message, "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteShortcut_Click(object sender, RoutedEventArgs e)
        {
            Shortcut? shortcut = GetShortcutFromCommandParameter(sender);
            if (shortcut == null)
            {
                return;
            }

            string path = shortcut.UriOrFileAction;
            try
            {
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else if (Directory.Exists(path))
                {
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }

                PalisadesManager.RemoveShortcutFromAllFences(path);
                PalisadesManager.RemoveMissingShortcutsFromAllFences();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenShortcutProperties_Click(object sender, RoutedEventArgs e)
        {
            Shortcut? shortcut = GetShortcutFromCommandParameter(sender);
            if (shortcut == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(shortcut.UriOrFileAction)
                {
                    UseShellExecute = true,
                    Verb = "properties"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开属性失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Shortcut? GetShortcutFromCommandParameter(object sender)
        {
            if (sender is MenuItem menuItem)
            {
                return menuItem.CommandParameter as Shortcut;
            }

            return null;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            DependencyObject? p = current;
            while (p != null)
            {
                if (p is T typed)
                {
                    return typed;
                }
                p = System.Windows.Media.VisualTreeHelper.GetParent(p);
            }
            return null;
        }
    }
}

