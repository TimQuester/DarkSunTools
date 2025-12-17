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
        /// Основное имя (например, "Staff Sling")
        /// </summary>
        public string MainName { get; set; }
        
        /// <summary>
        /// Путь или идентификатор (например, "te\NAMEIX\*.oda" или "EIX\*.oda")
        /// </summary>
        public string Path { get; set; }
        
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
            Path = string.Empty;
        }
        
        public override string ToString()
        {
            return $"{MainName} [{Path}] ({Index}, {Flag})";
        }
    }
}