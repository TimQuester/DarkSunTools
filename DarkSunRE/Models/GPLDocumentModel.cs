using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfHexaEditor.Core;

namespace DarkSunRE.Models
{
    internal class GPLDocumentModel
    {
        public byte[] originalBytes;
        public byte[] newBytes;
        public GPLDocumentModel(string path, string name)
        {
            Name = name;
            Path = path;
            originalBytes = File.ReadAllBytes(path);
            newBytes = new byte[originalBytes.Length];
            originalBytes.CopyTo(newBytes);
            decodedStrings = DecodeStrings(originalBytes);
        }
        public GPLDocumentModel()
        {

        }
        public string Name { get; set; }
        public string Path;// { get; set; }
        public List<GPLString> decodedStrings { get; set; } = new List<GPLString>();

        /// <summary>
        /// Декодируем строки из непрерывного потоква 7 бит в 8 бит
        /// </summary>
        /// <param name="bytes">Поток с 7-битными символами</param>
        /// <returns>Список строк</returns>
        List<GPLString> DecodeStrings(byte[] bytes)
        {
            const int TextBufferSize = 3000;
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

                    int tempPosition = position;
                    while (position < bytes.Length && strPos < TextBufferSize - 1 && !endOfString)
                    {
                        if (currentRShift > 0)
                        {
                            currentChar = (currentChar << 8) | bytes[tempPosition++];
                        }

                        int decodedChar = (currentChar >> currentRShift) & 0x7F;
                        textBuffer[strPos] = (byte)decodedChar;

                        if (textBuffer[strPos] == 0x03)
                        {
                            textBuffer[strPos] = 0;

                            nGPLString.EndPosition = tempPosition - 1;
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

                        nGPLString.EndPosition = tempPosition - 1;
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
 
        public void ApplyStringChanges()
        {
            foreach (var str in decodedStrings)
            {

                int originalBlockSize = str.EndPosition - str.StartPosition;

                if (str.NewString != null && str.OriginalString != str.NewString)
                {
                    byte[] newBlock =
                        EncodeString(str.NewString, originalBlockSize);
                    Array.Copy(newBlock, 0, newBytes, str.StartPosition, originalBlockSize);
                }
            }
        }
        byte[] EncodeString(string str, int size)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
        
                    byte[] asciiBytes = Encoding.ASCII.GetBytes(str);
                    int currentShift = 1;
                    int buffer = 0;
                    int bitsInBuffer = 0;

                    foreach (byte b in asciiBytes)
                    {
                        // Добавляем 7 бит символа в буфер
                        buffer = (buffer << 7) | (b & 0x7F);
                        bitsInBuffer += 7;

                        // Когда в буфере достаточно бит, извлекаем байты
                        while (bitsInBuffer >= 8)
                        {
                            int shiftAmount = bitsInBuffer - 8;
                            byte outputByte = (byte)((buffer >> shiftAmount) & 0xFF);
                            outputStream.WriteByte(outputByte);
                    
                            // Убираем обработанные биты
                            buffer &= (1 << shiftAmount) - 1;
                            bitsInBuffer -= 8;
                        }

                        currentShift++;
                        if (currentShift > 7)
                        {
                            currentShift = 1;
                        }
                    }

                    // Добавляем терминатор строки (0x03)
                    buffer = (buffer << 7) | 0x03;
                    bitsInBuffer += 7;

                    // Обрабатываем оставшиеся биты
                    while (bitsInBuffer > 0)
                    {
                        int shiftAmount = Math.Max(bitsInBuffer - 8, 0);
                        byte outputByte = (byte)((buffer >> shiftAmount) & 0xFF);
                        outputStream.WriteByte(outputByte);
                
                        if (bitsInBuffer < 8)
                        {
                            break;
                        }
                
                        buffer &= (1 << shiftAmount) - 1;
                        bitsInBuffer -= 8;
                    }

                    // Проверяем, совпадает ли размер с исходным
                    int encodedLength = (int)outputStream.Length - ((int)outputStream.Position - str.Length);                    

                    if (encodedLength != size)
                    {
                        // Если размер не совпадает, корректируем (может потребоваться дописать нули или обрезать)
                        while (outputStream.Length < size)
                        {
                            outputStream.WriteByte(0); // Дополняем нулями (если не хватает)
                        }
                        if (outputStream.Length > size)
                        {
                            byte[] data = outputStream.ToArray();
                            Array.Resize(ref data, size);
                            outputStream.SetLength(0);
                            outputStream.Write(data, 0, data.Length); // Обрезаем лишнее
                        }
                    }

                return outputStream.ToArray();
            }
        }
        /// <summary>
        /// Обновление данных в байтах при изменении новых строк
        /// </summary>
        public void RefreshNewBytesAfterLoadJson()
        {
            originalBytes.CopyTo(newBytes);
            ApplyStringChanges();
        }
    }
}
