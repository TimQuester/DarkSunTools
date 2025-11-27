using DarkSunRE.Services;
using DarkSunRE.ViewModels;
using DarkSunRE.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

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

            // Resolve VM and create main window via DI (preserves constructor injection)
            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();

            // Resolve MainWindow via DI (will throw early if registration/problem exists)
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();
        }

        private IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IStatusService, StatusService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IResourceService, ResourceService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IGameDefinitionService, GameDefinitionService>();

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
