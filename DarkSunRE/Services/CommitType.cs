using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public enum CommitType
    {
        Unknown,
        Feature,        // Новая функциональность
        BugFix,         // Исправление ошибки
        Refactoring,    // Рефакторинг кода
        ResourceEdit,   // Изменение ресурсов
        ProjectSetup,   // Настройка проекта
        Merge,          // Слияние веток
        Revert,         // Отмена изменений
        Documentation,  // Изменение документации
        Maintenance     // Техническое обслуживание
    }
}
