using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ViewMd;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // CLI contract: `view-md <path>` where <path> is a file or a directory —
            // this is what the desktop file association invokes on double-click.
            var initialPath = desktop.Args?.FirstOrDefault(a => !a.StartsWith('-'));
            desktop.MainWindow = new MainWindow(initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}