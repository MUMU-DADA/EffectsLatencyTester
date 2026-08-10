using System.Threading;
using System.Windows;

namespace EffectsLatencyTester;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\EffectsLatencyTester";
    private static Mutex? singleInstanceMutex;
    private static bool ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        I18n.Initialize(e.Args);
        ThemeManager.Initialize();
        singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show(I18n.AlreadyRunning, I18n.AppTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsSingleInstanceMutex)
        {
            singleInstanceMutex?.ReleaseMutex();
            ownsSingleInstanceMutex = false;
        }

        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
        base.OnExit(e);
    }
}
