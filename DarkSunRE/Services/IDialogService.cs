using DarkSunRE.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DarkSunRE.Services
{
    public interface IDialogService
    {
        /// <summary>
        /// Отобразить диалог
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        /// <returns></returns>
        bool? ShowDialog<TViewModel>() where TViewModel : BaseDialogViewModel;
        /// <summary>
        /// Отобразить диалог на основе модели
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        /// <param name="viewModel"></param>
        /// <returns></returns>
        bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : BaseDialogViewModel;
        /// <summary>
        /// Асинхронное отображение диалога на основе модели
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        /// <returns></returns>
        Task<bool?> ShowDialogAsync<TViewModel>() where TViewModel : BaseDialogViewModel;
        Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : BaseDialogViewModel;
        /// <summary>
        /// Отобразить диалог открытия файла
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="initialDirectory"></param>
        /// <returns></returns>
        string ShowOpenFileDialog(string filter, string initialDirectory = null);
        /// <summary>
        /// Отобразить диалог сохранения файла
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="initialDirectory"></param>
        /// <returns></returns>
        string ShowSaveFileDialog(string filter, string initialDirectory = null);
        /// <summary>
        /// Отобразить диалог выбора папки
        /// </summary>
        /// <param name="description"></param>
        /// <param name="initialDirectory"></param>
        /// <returns></returns>
        string ShowFolderDialog(string description, string initialDirectory = null);
        /// <summary>
        /// Получить ViewModel
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>        
        TViewModel CreateViewModel<TViewModel>() where TViewModel : BaseDialogViewModel;
        void RegisterDialog<TViewModel, TView>() where TViewModel : class where TView : Window;
    }
}
