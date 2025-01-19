using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Llama.Avalonia;

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
            var collection  = new ServiceCollection();

            collection.AddSingleton(desktop);
            collection.AddTransient<MainWindowViewModel>();
            ConfigureServices(collection);
            var services = collection.BuildServiceProvider();
            var vm = services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private static ServiceProvider ConfigureServices(ServiceCollection services)
    {
        var modelPath = System.Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ??
                     throw new ArgumentException("Model path must be provided as command line argument");
        
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
        services.AddSingleton<LlamaService>(new LlamaService(modelPath));

        return services.BuildServiceProvider();
    }
}
