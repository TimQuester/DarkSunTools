using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    internal class GPLDocumentModel
    {
        public byte[] originalBytes;
        public byte[] newBytes;
        public GPLDocumentModel(string path, string name)
        {
            Name = name;
            originalBytes = File.ReadAllBytes(path);
            decodedStrings = DecodeStrings(originalBytes);
        }
        public string Name { get; set; }
        public List<GPLString> decodedStrings { get; set; } = new List<GPLString>();

        /// <summary>
        /// Декодируем строки из непрерывного потоква 7 бит в 8 бит
        /// </summary>
        /// <param name="bytes">Поток с 7-битными символами</param>
        /// <returns>Список строк</returns>
        List<GPLString> DecodeStrings(byte[] bytes)
        {
            const int TextBufferSize = 300;
            int strNumber = 0;
            List<GPLString> result = new List<GPLString>();
            int position = 0;
            byte[] textBuffer = new byte[TextBufferSize];

            while (position < bytes.Length)
            {
                if (bytes[position] == 0x05)
                {
                    position++;

                    GPLString nGPLString = new GPLString();
                    nGPLString.StartPosition = position;

                    int strPos = 0;
                    int currentChar = 0;
                    int currentRShift = 1;
                    bool endOfString = false;

                    while (position < bytes.Length && strPos < TextBufferSize - 1 && !endOfString)
                    {
                        if (currentRShift > 0)
                        {
                            currentChar = (currentChar << 8) | bytes[position++];
                        }

                        int decodedChar = (currentChar >> currentRShift) & 0x7F;
                        textBuffer[strPos] = (byte)decodedChar;

                        if (textBuffer[strPos] == 0x03)
                        {
                            textBuffer[strPos] = 0;

                            nGPLString.EndPosition = position - 1;
                            nGPLString.OriginalBytes = textBuffer;
                            nGPLString.OriginalString = Encoding.ASCII.GetString(textBuffer, 0, strPos);
                            nGPLString.StringNumber = ++strNumber;
                            result.Add(nGPLString);
                            endOfString = true;
                            break;
                        }

                        // Все символы с кодом меньше 0x20, кроме управляющих, заменяем на пробелы
                        if (textBuffer[strPos] < 0x20 &&
                            textBuffer[strPos] != '\t' &&
                            textBuffer[strPos] != '\n' &&
                            textBuffer[strPos] != '\r')
                        {
                            textBuffer[strPos] = 0x20;
                        }

                        strPos++;
                        currentRShift++;

                        if (currentRShift > 7)
                        {
                            currentRShift = 0;
                        }
                    }

                    if (!endOfString && strPos > 0)
                    {
                        textBuffer[strPos] = 0;

                        nGPLString.EndPosition = position - 1;
                        nGPLString.OriginalBytes = textBuffer;
                        nGPLString.OriginalString = Encoding.ASCII.GetString(textBuffer, 0, strPos);
                        nGPLString.StringNumber = ++strNumber;
                        result.Add(nGPLString);
                    }
                }
                else
                {
                    position++;
                }
            }

            return result;
        }
    }
}
