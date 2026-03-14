using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace Palisades.Helpers
{
    internal sealed class TrayIconController : IDisposable
    {
        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem showAllItem;
        private readonly ToolStripMenuItem hideAllItem;
        private readonly ToolStripMenuItem aiClassifyItem;
        private readonly ToolStripMenuItem autoStartItem;
        private readonly ToolStripMenuItem exitItem;

        public TrayIconController()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Palisades",
                Visible = true,
            };

            showAllItem = new ToolStripMenuItem("显示全部栅栏");
            hideAllItem = new ToolStripMenuItem("隐藏全部栅栏");
            aiClassifyItem = new ToolStripMenuItem("AI分类");
            autoStartItem = new ToolStripMenuItem("开机自启动");
            exitItem = new ToolStripMenuItem("退出");

            showAllItem.Click += (_, _) => ExecuteOnUiThread(PalisadesManager.ShowAllFences);
            hideAllItem.Click += (_, _) => ExecuteOnUiThread(PalisadesManager.HideAllFences);
            aiClassifyItem.Click += async (_, _) => await RunAiClassifyAsync();
            autoStartItem.Click += (_, _) => ToggleAutoStart();
            exitItem.Click += (_, _) => ExecuteOnUiThread(() =>
            {
                App.SuppressDesktopRestoreOnExit = false;
                System.Windows.Application.Current.Shutdown();
            });

            ContextMenuStrip menu = new();
            menu.Items.Add(showAllItem);
            menu.Items.Add(hideAllItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(aiClassifyItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            notifyIcon.ContextMenuStrip = menu;

            notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                {
                    RefreshAutoStartMenuState();
                    notifyIcon.ContextMenuStrip?.Show(Cursor.Position);
                }
            };

            RefreshAutoStartMenuState();
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }

        private static void ExecuteOnUiThread(Action action)
        {
            if (System.Windows.Application.Current == null)
            {
                return;
            }

            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string exePath = AppLaunchHelper.GetPreferredExecutablePath();
                if (!string.IsNullOrWhiteSpace(exePath) && System.IO.File.Exists(exePath))
                {
                    Icon? icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        return icon;
                    }
                }
            }
            catch
            {
            }

            return SystemIcons.Application;
        }

        private async Task RunAiClassifyAsync()
        {
            AutoOrganizePlan plan = await Task.Run(PalisadesManager.PrepareAutoOrganizeDesktopByAi);
            ExecuteOnUiThread(() =>
            {
                AutoOrganizeResult result = PalisadesManager.ApplyAutoOrganizePlan(plan);
                System.Windows.MessageBox.Show(result.StatusMessage, "AI 分类结果", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void ToggleAutoStart()
        {
            try
            {
                bool enabled = AutoStartHelper.IsEnabled();
                AutoStartHelper.SetEnabled(!enabled);
                RefreshAutoStartMenuState();
            }
            catch (Exception ex)
            {
                RefreshAutoStartMenuState();
                ExecuteOnUiThread(() =>
                {
                    System.Windows.MessageBox.Show($"设置开机自启动失败：{ex.Message}", "栅栏", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        private void RefreshAutoStartMenuState()
        {
            bool enabled = AutoStartHelper.IsEnabled();
            autoStartItem.Checked = enabled;
            autoStartItem.Text = enabled ? "开机自启动（已开启）" : "开机自启动（已关闭）";
        }
    }
}
