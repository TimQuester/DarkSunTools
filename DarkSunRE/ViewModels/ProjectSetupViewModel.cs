using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class ProjectSetupViewModel : INotifyPropertyChanged
    {
        private readonly IProjectService _projectService;
        private readonly IStatusService _statusService;

        public bool IsScanning { get => _isScanning; private set => _isScanning = value; }

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isScanning;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand ScanGameCommand { get; }
        public ICommand CancelScanCommand { get; }

        public ProjectSetupViewModel(IProjectService projectService, IStatusService statusService)
        {
            _projectService = projectService;
            _statusService = statusService;

            ScanGameCommand = new AsyncRelayCommand(ScanGameAsync, () => !IsScanning);
            CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);

            //_projectService.ScanProgress += OnScanProgress;            
        }

        private async Task ScanGameAsync()
        {
            IsScanning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                //var result = await _projectService.ScanGameResourcesAsync(
                //    GamePath, SelectedGame.Type, _cancellationTokenSource.Token);

                //if (result.Success)
                //{
                //    GffFiles = new ObservableCollection<GffFileInfo>(result.GffFiles);
                //    _statusService.ShowMessage($"Found {result.GffFiles.Count} GFF files with {result.TotalResourcesFound} resources");
                //}
                //else
                //{
                //    // Обработка ошибок
                //}
            }
            catch (OperationCanceledException)
            {
                _statusService.ShowMessage("Scan cancelled");
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void CancelScan()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void OnScanProgress(object sender, ScanProgressEventArgs e)
        {
            var ProgressValue = e.ProgressPercentage;
            var ProgressMessage = $"{e.CurrentOperation} ({e.FilesProcessed}/{e.TotalFiles})";
            _statusService.ShowProgress(ProgressMessage, ProgressValue);
        }
    }
}
