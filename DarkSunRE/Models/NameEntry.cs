using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    /// <summary>
    /// Представляет запись в NAME ресурсе
    /// </summary>
    public class NameEntry
    {
        /// <summary>
        /// Основное имя в отображаемом виде (перевод, с таблицей
        /// транслитерации).
        /// </summary>
        public string MainName { get; set; }

        /// <summary>
        /// Исходное основное имя из игры, без применения таблицы.
        /// </summary>
        public string OriginalMainName { get; set; }

        /// <summary>
        /// Путь или идентификатор (например, "te\NAMEIX\*.oda" или "EIX\*.oda")
        /// </summary>
        public string Path { get; set; }

        /// <summary>Исходный путь из игры (для Reload).</summary>
        public string OriginalPath { get; set; }
        
        /// <summary>
        /// Индекс или смещение
        /// </summary>
        public ushort Index { get; set; }
        
        /// <summary>
        /// Флаг
        /// </summary>
        public byte Flag { get; set; }
        
        public NameEntry()
        {
            MainName = string.Empty;
            OriginalMainName = string.Empty;
            Path = string.Empty;
            OriginalPath = string.Empty;
        }
        
        public override string ToString()
        {
            return $"{MainName} [{Path}] ({Index}, {Flag})";
        }
    }
}