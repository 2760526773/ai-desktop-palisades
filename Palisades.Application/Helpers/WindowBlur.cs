using Palisades.Helpers.Native;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Palisades.Helpers
{
    internal class WindowBlur
    {
        internal static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(WindowBlur),
            new PropertyMetadata(false, OnIsEnabledChanged));

        internal static void SetIsEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsEnabledProperty, value);
        }

        internal static bool GetIsEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsEnabledProperty);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window)
            {
                if (true.Equals(e.OldValue))
                {
                    GetWindowBlur(window)?.Detach();
                    window.ClearValue(WindowBlurProperty);
                }
                if (true.Equals(e.NewValue))
                {
                    var blur = new WindowBlur();
                    blur.Attach(window);
                    window.SetValue(WindowBlurProperty, blur);
                }
            }
        }

        internal static readonly DependencyProperty WindowBlurProperty = DependencyProperty.RegisterAttached(
            "WindowBlur", typeof(WindowBlur), typeof(WindowBlur),
            new PropertyMetadata(null, OnWindowBlurChanged));

        internal static void SetWindowBlur(DependencyObject element, WindowBlur value)
        {
            element.SetValue(WindowBlurProperty, value);
        }

        internal static WindowBlur GetWindowBlur(DependencyObject element)
        {
            return (WindowBlur)element.GetValue(WindowBlurProperty);
        }

        private static void OnWindowBlurChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window)
            {
                (e.OldValue as WindowBlur)?.Detach();
                (e.NewValue as WindowBlur)?.Attach(window);
            }
        }

        private Window? _window;

        private void Attach(Window window)
        {
            _window = window;
            var source = (HwndSource)PresentationSource.FromVisual(window);
            if (source == null)
            {
                window.SourceInitialized += OnSourceInitialized;
            }
            else
            {
                AttachCore();
            }
        }

        private void Detach()
        {
            try
            {
                DetachCore();
            }
            finally
            {
                _window = null;
            }
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.SourceInitialized -= OnSourceInitialized;
                AttachCore();
            }
        }

        private void AttachCore()
        {
            if (_window == null)
            {
                return;
            }

            EnableBlur(_window);
        }

        private void DetachCore()
        {
            if (_window == null)
            {
                return;
            }

            _window.SourceInitialized += OnSourceInitialized;
        }

        private static void EnableBlur(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);

            // Windows 10/11: acrylic first; fallback to blur behind.
            if (!TryApplyAccent(windowHelper.Handle, AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, 0x7FD9EEF5))
            {
                _ = TryApplyAccent(windowHelper.Handle, AccentState.ACCENT_ENABLE_BLURBEHIND, 0);
            }
        }

        private static bool TryApplyAccent(IntPtr hwnd, AccentState state, int gradientColor)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = state,
                    AccentFlags = 2,
                    GradientColor = gradientColor,
                    AnimationId = 0
                };

                int accentStructSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                try
                {
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                        SizeOfData = accentStructSize,
                        Data = accentPtr
                    };

                    int result = SetWindowCompositionAttribute(hwnd, ref data);
                    return result != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    }

    namespace Native
    {
        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19,
        }
    }
}
