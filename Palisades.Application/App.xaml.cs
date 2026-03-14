using Palisades.Helpers;
using Sentry;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Palisades
{
    public partial class App : System.Windows.Application
    {
        private readonly Mutex _singleInstanceMutex;
        private readonly bool _isPrimaryInstance;
        private bool _isSystemSessionEnding;
        private TrayIconController? _trayIconController;

        internal static bool SuppressDesktopRestoreOnExit { get; set; }

        public App()
        {
            _singleInstanceMutex = new Mutex(true, "Global\\Palisades.SingleInstance", out bool createdNew);
            _isPrimaryInstance = createdNew;

            if (!_isPrimaryInstance)
            {
                MessageBox.Show("Palisades 已在运行。", "Palisades", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            SessionEnding += App_SessionEnding;
            SetupSentry();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SessionState previousState = SessionStateHelper.Load();
            SessionStateHelper.MarkSessionStarted();

            PalisadesManager.LoadPalisades();
            PalisadesManager.RemoveMissingShortcutsFromAllFences();

            if (!previousState.LastExitClean)
            {
                int repaired = PalisadesManager.RepairManagedStateAfterUnexpectedShutdown();
                if (repaired > 0)
                {
                    MessageBox.Show($"检测到上次未正常退出，已自动修复 {repaired} 个受管项目引用。", "Palisades", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            if (PalisadesManager.palisades.Count == 0)
            {
                PalisadesManager.CreatePalisade();
            }

            _trayIconController = new TrayIconController();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            bool shouldRestore = !_isSystemSessionEnding && !SuppressDesktopRestoreOnExit;
            try
            {
                if (shouldRestore)
                {
                    int restored = PalisadesManager.RestoreManagedItemsToDesktop();
                    if (restored > 0)
                    {
                        PalisadesManager.RemoveMissingShortcutsFromAllFences();
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
            finally
            {
                try
                {
                    SessionStateHelper.MarkSessionExited(shouldRestore);
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
            }

            if (_isPrimaryInstance)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            _singleInstanceMutex.Dispose();
            _trayIconController?.Dispose();
            base.OnExit(e);
        }

        private void App_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            _isSystemSessionEnding = true;
            SuppressDesktopRestoreOnExit = true;
        }

        private void SetupSentry()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            SentrySdk.Init(o =>
            {
                o.Dsn = "https://ffd9f3db270c4bd583ab3041d6264c38@o1336793.ingest.sentry.io/6605931";
                o.Debug = PEnv.IsDev();
                o.TracesSampleRate = 1;
            });
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.CaptureException(e.Exception);
            e.Handled = true;
        }
    }
}
