using DarkSunRE.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DarkSunRE.Views
{
    /// <summary>
    /// Interaction logic for StatusBarView.xaml
    /// </summary>
    public partial class StatusBarView : UserControl
    {
        public StatusBarView()
        {
            InitializeComponent();

            // Подключаем сервис статуса как DataContext для представления.
            // Используем ServiceProvider, который инициализируется в App.OnStartup.
            try
            {
                if (Application.Current is App app && app.ServiceProvider != null)
                {
                    var svc = app.ServiceProvider.GetService<IStatusService>();
                    if (svc != null)
                    {
                        // DataContext будет указывать на конкретную реализацию StatusService,
                        // которая реализует INotifyPropertyChanged и содержит команды.
                        DataContext = svc;
                    }
                }
            }
            catch (Exception)
            {
                // Ничего не делаем — оставляем DataContext пустым, чтобы не ломать загрузку дизайнера.
            }
        }
    }
}
