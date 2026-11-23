using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DSunTools.Common.Gpl;

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
        private const int RecordSize = 25;
        private const int StringAreaSize = 22;

        public void LoadFromBytes(byte[] data)
        {
            Entries.Clear();
            if (data == null)
                return;

            // DS1 NAME records are fixed 25-byte slots: two NUL-terminated
            // strings in bytes 0..21, u16 index at 22, flag at 24. A bare
            // C-string walk misparses the pool because the path fragments
            // are not aligned to record boundaries.
            for (int offset = 0; offset + RecordSize <= data.Length; offset += RecordSize)
            {
                string mainName = ReadString(data, offset, StringAreaSize);
                int pathStart = offset + mainName.Length + 1;
                string path = pathStart < offset + StringAreaSize
                    ? ReadString(data, pathStart, offset + StringAreaSize - pathStart)
                    : string.Empty;

                int indexOffset = offset + 22;
                ushort index = BitConverter.ToUInt16(data, indexOffset);
                byte flag = data[offset + 24];

                Entries.Add(new NameEntry
                {
                    MainName = mainName,
                    OriginalMainName = mainName,
                    Path = path,
                    OriginalPath = path,
                    Index = index,
                    Flag = flag
                });
            }
        }

        /// <summary>
        /// Reads a NUL-terminated string from a bounded string area.
        /// </summary>
        private static string ReadString(byte[] data, int start, int maximumLength)
        {
            int length = 0;
            while (length < maximumLength && data[start + length] != 0)
                length++;

            return length == 0 ? string.Empty : Encoding.Latin1.GetString(data, start, length);
        }

        /// <summary>
        /// Сохраняет данные NAME ресурса в байтовый массив
        /// </summary>
        /// <returns>Байтовый массив данных NAME ресурса</returns>
        public byte[] SaveToBytes(IEnumerable<GplTransliterationRule>? rules = null)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var entry in Entries)
                {
                    byte[] mainNameBytes = EncodeName(entry.MainName, rules);
                    byte[] pathBytes = EncodeName(entry.Path, rules);
                    int stringBytes = mainNameBytes.Length + 1 + pathBytes.Length + 1;
                    if (stringBytes > StringAreaSize)
                    {
                        throw new InvalidOperationException(
                            $"NAME entry '{entry.MainName}' needs {stringBytes} bytes in the 22-byte string area.");
                    }

                    ms.Write(mainNameBytes, 0, mainNameBytes.Length);
                    ms.WriteByte(0);
                    ms.Write(pathBytes, 0, pathBytes.Length);
                    ms.WriteByte(0);

                    for (int i = stringBytes; i < StringAreaSize; i++)
                        ms.WriteByte(0);

                    ms.Write(BitConverter.GetBytes(entry.Index), 0, 2);
                    ms.WriteByte(entry.Flag);
                }
                return ms.ToArray();
            }
        }

        private static byte[] EncodeName(string value, IEnumerable<GplTransliterationRule>? rules)
        {
            string gameText = GplTransliteration.EncodeToGameCharset(value, rules);
            if (gameText.Any(character => character > byte.MaxValue || character == '\0'))
                throw new InvalidOperationException("NAME text contains a character that cannot be encoded as a one-byte game glyph.");

            return Encoding.Latin1.GetBytes(gameText);
        }

        /// <summary>
        /// Количество записей в ресурсе
        /// </summary>
        public int Count => Entries.Count;
    }
}