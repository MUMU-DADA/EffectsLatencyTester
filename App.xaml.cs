using System.Windows;

namespace EffectsLatencyTester;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        I18n.UseSystemCulture();
        base.OnStartup(e);
    }
}
