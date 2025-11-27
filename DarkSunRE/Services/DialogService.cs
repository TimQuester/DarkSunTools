using DarkSunRE.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;
using DarkSunRE.Services;

namespace DarkSunRE.Services
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IUiService _uiService;
        private readonly Dictionary<Type, Type> _viewModelToViewMapping = new();

        public DialogService(IServiceProvider serviceProvider, IUiService uiService)
        {
            _serviceProvider = serviceProvider;
            _uiService = uiService;
        }

        public TViewModel CreateViewModel<TViewModel>() where TViewModel : BaseDialogViewModel
        {
            return _serviceProvider.GetService<TViewModel>();
        }

        public void RegisterDialog<TViewModel, TView>()
            where TViewModel : class
            where TView : Window
        {
            _viewModelToViewMapping[typeof(TViewModel)] = typeof(TView);
        }

        public bool? ShowDialog<TViewModel>() where TViewModel : BaseDialogViewModel
        {
            var viewModel = CreateViewModel<TViewModel>();
            return ShowDialog(viewModel);
        }

        public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : BaseDialogViewModel
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            var viewType = _viewModelToViewMapping[typeof(TViewModel)];
            var window = (Window)Activator.CreateInstance(viewType);

            window.DataContext = viewModel;
            window.Owner = Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            viewModel.RequestClose += (sender, e) =>
            {
                window.DialogResult = e.Result;
                window.Close();
            };

            return window.ShowDialog();
        }

        public async Task<bool?> ShowDialogAsync<TViewModel>() where TViewModel : BaseDialogViewModel
        {
            return await Task.Run(() => ShowDialog<TViewModel>());
        }
        public async Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : BaseDialogViewModel
        {
            return await Task.Run(() => ShowDialog(viewModel));
        }

        public string ShowFolderDialog(string description, string initialDirectory = null)
        {
            return _uiService.ShowFolderDialog(description, initialDirectory);
        }

        public string ShowOpenFileDialog(string filter, string initialDirectory = null)
        {
            return _uiService.ShowOpenFileDialog(filter, initialDirectory, "Open File");
        }

        public string ShowSaveFileDialog(string filter, string initialDirectory = null)
        {
            return _uiService.ShowSaveFileDialog(null, filter, initialDirectory, "Save File");
        }        
    }
}
