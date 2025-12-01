using DarkSunRE.Services;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Threading.Tasks;

namespace DarkSunRE
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ServiceProvider = ConfigureServices(serviceCollection);

            RegisterDialogs(ServiceProvider);

            // Global exception handlers — логируем необработанные ошибки
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            Application.Current.DispatcherUnhandledException += (s, args) =>
            {
                logger.LogError(args.Exception, "Unhandled UI exception");
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                logger.LogError(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    logger.LogCritical(ex, "Unhandled domain exception");
            };

            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();
        }

        private IServiceProvider ConfigureServices(IServiceCollection services)
        {
            // Register logging providers first
            services.AddLogging(builder =>
            {
                // avoid AddDebug if package not referenced
                builder.AddConsole();
            });

            services.AddSingleton<IStatusService, StatusService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IResourceService, ResourceService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IGameDefinitionService, GameDefinitionService>();

            // register UI service (dialogs) separately
            services.AddSingleton<IUiService, UiService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<CreateProjectViewModel>();
            services.AddTransient<StatusBarViewModel>();
            services.AddTransient<ProjectTreeViewModel>();

            // register view types if you want to resolve them via DI
            services.AddTransient<MainWindow>();
            services.AddTransient<CreateProjectDialog>();

            // Enable validations to catch common registration errors early
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        }

        private void RegisterDialogs(IServiceProvider serviceProvider)
        {
            var dialogService = serviceProvider.GetRequiredService<IDialogService>();

            dialogService.RegisterDialog<CreateProjectViewModel, CreateProjectDialog>();
            dialogService.RegisterDialog<ProjectSettingsViewModel, ProjectSettingsView>();
            // other dialog registrations...
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnExit(e);
        }
    }
}
