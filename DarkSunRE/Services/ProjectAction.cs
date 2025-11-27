using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public enum ProjectAction
    {
        // Основные действия
        Created,
        Opened,
        Saved,
        Closed,

        // Действия с ресурсами
        ResourceAdded,
        ResourceModified,
        ResourceDeleted,
        ResourceReplaced,
        ResourceOpened,

        // Действия с GFF файлами
        GffFileLoaded,
        GffFileModified,
        GffFileSaved,

        // Состояния проекта
        Modified,       // Проект был изменен
        Clean,          // Все изменения сохранены
        ValidationError,

        // Git операции
        GitInitialized,
        GitCommitted,
        GitReverted
    }
}
