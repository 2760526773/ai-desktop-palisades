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

            SetupSentry();

            PalisadesManager.LoadPalisades();
            if (PalisadesManager.palisades.Count == 0)
            {
                PalisadesManager.CreatePalisade();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                int restored = PalisadesManager.RestoreManagedItemsToDesktop();
                if (restored > 0)
                {
                    PalisadesManager.RemoveMissingShortcutsFromAllFences();
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            if (_isPrimaryInstance)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            _singleInstanceMutex.Dispose();
            base.OnExit(e);
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
