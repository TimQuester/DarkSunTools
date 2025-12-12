using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public enum ResourceType
    {
        Unknown,
        Image,          // BMP, TGA изображения
        Palette,        // PAL палитры
        Font,           // Шрифты
        Text,           // Текстовые ресурсы
        Script,         // Скрипты
        Sound,          // Звуковые файлы
        Music,          // Музыка
        Dialog,         // Диалоги
        Item,           // Предметы
        Character,      // Персонажи
        Map,            // Карты        
        Animation,      // Анимации
        RawData,         // Бинарные данные
        AnimationControl
    }
}
