using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkSunRE.Models
{
    public class NameResource
    {
        public List<NameEntry> Entries { get; private set; }

        public NameResource()
        {
            Entries = new List<NameEntry>();
        }

        /// <summary>
        /// Загружает данные NAME ресурса из байтового массива
        /// </summary>
        /// <param name="data">Байтовый массив данных NAME ресурса</param>
        public void LoadFromBytes(byte[] data)
        {
            Entries.Clear();
            
            int offset = 0;
            while (offset < data.Length - 3)
            {
                // Читаем первую строку (основное имя)
                string mainName = ReadNullTerminatedString(data, ref offset);
                if (string.IsNullOrEmpty(mainName) && offset >= data.Length - 3)
                    break;
                    
                // Читаем вторую строку (путь/идентификатор)
                string path = ReadNullTerminatedString(data, ref offset);
                
                // Читаем 2 байта индекса/смещения
                if (offset + 1 < data.Length)
                {
                    ushort index = BitConverter.ToUInt16(data, offset);
                    offset += 2;
                    
                    // Читаем 1 байт флага
                    if (offset < data.Length)
                    {
                        byte flag = data[offset];
                        offset++;
                        
                        Entries.Add(new NameEntry
                        {
                            MainName = mainName,
                            Path = path,
                            Index = index,
                            Flag = flag
                        });
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Читает строку до нулевого терминатора
        /// </summary>
        private string ReadNullTerminatedString(byte[] data, ref int offset)
        {
            int start = offset;
            while (offset < data.Length && data[offset] != 0)
            {
                offset++;
            }
            
            string result = string.Empty;
            if (offset > start)
            {
                result = Encoding.ASCII.GetString(data, start, offset - start);
            }
            
            // Пропускаем нулевой байт
            if (offset < data.Length && data[offset] == 0)
            {
                offset++;
            }
            
            return result;
        }

        /// <summary>
        /// Сохраняет данные NAME ресурса в байтовый массив
        /// </summary>
        /// <returns>Байтовый массив данных NAME ресурса</returns>
        public byte[] SaveToBytes()
        {
            using (var ms = new MemoryStream())
            {
                foreach (var entry in Entries)
                {
                    // Записываем основное имя
                    byte[] mainNameBytes = Encoding.ASCII.GetBytes(entry.MainName);
                    ms.Write(mainNameBytes, 0, mainNameBytes.Length);
                    ms.WriteByte(0); // нулевой терминатор
                    
                    // Записываем путь
                    byte[] pathBytes = Encoding.ASCII.GetBytes(entry.Path);
                    ms.Write(pathBytes, 0, pathBytes.Length);
                    ms.WriteByte(0); // нулевой терминатор
                    
                    // Записываем индекс
                    ms.Write(BitConverter.GetBytes(entry.Index), 0, 2);
                    
                    // Записываем флаг
                    ms.WriteByte(entry.Flag);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Количество записей в ресурсе
        /// </summary>
        public int Count => Entries.Count;
    }
}