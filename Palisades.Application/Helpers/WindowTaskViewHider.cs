using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Palisades.Helpers
{
    internal static class WindowTaskViewHider
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        public static readonly DependencyProperty HideFromTaskViewProperty = DependencyProperty.RegisterAttached(
            "HideFromTaskView",
            typeof(bool),
            typeof(WindowTaskViewHider),
            new UIPropertyMetadata(false, OnHideFromTaskViewChanged));

        public static bool GetHideFromTaskView(DependencyObject obj)
        {
            return (bool)obj.GetValue(HideFromTaskViewProperty);
        }

        public static void SetHideFromTaskView(DependencyObject obj, bool value)
        {
            obj.SetValue(HideFromTaskViewProperty, value);
        }

        private static void OnHideFromTaskViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Window window)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                if (window.IsInitialized)
                {
                    ApplyToolWindowStyle(window);
                }
                else
                {
                    window.SourceInitialized += (_, _) => ApplyToolWindowStyle(window);
                }
            }
        }

        private static void ApplyToolWindowStyle(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
                exStyle |= WS_EX_TOOLWINDOW;
                exStyle &= ~WS_EX_APPWINDOW;
                SetWindowLong(handle, GWL_EXSTYLE, exStyle);
            }
            catch
            {
                // Ignore to avoid crashing if style change is blocked.
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
