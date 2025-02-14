using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Data;
using System.Windows;
using TwitchLib.EventSub;
using TwitchLib.EventSub.Websockets;
using Microsoft.Extensions.DependencyInjection;
using TwitchLib.EventSub.Websockets.Extensions;

namespace TwitchBot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<MainWindow>();

                    services.AddLogging();
                    services.AddTwitchLibEventSubWebsockets();
                    services.AddHostedService<WebsocketHostedService>();
                })
                .Build();
        }

        protected async void OnStartup(object sender, StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            startupForm.Show();

            //base.OnStartup(e);    //triggers the App.xaml listed OnStartup event (aka infinite loop of this method)
        }

        protected async void OnExit(object sender, ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            AppHost.Dispose();

            //base.OnExit(e);   //triggers the App.xaml listed OnExit event (aka infinite loop of this method)
        }
    }

}
