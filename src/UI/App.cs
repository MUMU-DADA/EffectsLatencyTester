using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace EffectsLatencyTester.UI;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "EffectsLatencyTester";
    private Mutex? singleInstanceMutex;
    private bool ownsSingleInstanceMutex;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        I18n.Initialize(Environment.GetCommandLineArgs().Skip(1).ToArray());
        ThemeManager.Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            ownsSingleInstanceMutex = createdNew;
            if (!createdNew)
            {
                desktop.Shutdown(2);
                return;
            }

            desktop.MainWindow = new MainWindow
            {
                Title = I18n.AppTitle,
            };
            desktop.Exit += (_, _) => ReleaseSingleInstance();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ReleaseSingleInstance()
    {
        if (ownsSingleInstanceMutex)
        {
            try
            {
                singleInstanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            ownsSingleInstanceMutex = false;
        }

        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
    }
}