using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Введите путь к файлу:");
        string filePath = Console.ReadLine();

        try
        {
            // 1. Чтение и декодирование оригинального файла
            byte[] originalBytes = File.ReadAllBytes(filePath);
            List<string> decodedStrings = DecodeStrings(originalBytes);

            // 2. Дублируем массив строк для редактирования
            List<string> modifiedStrings = new List<string>(decodedStrings);

            Console.WriteLine("\nДекодированные строки:");
            for (int i = 0; i < decodedStrings.Count; i++)
            {
                Console.WriteLine($"[{i}] {decodedStrings[i]}");
            }

            // 3. Редактирование строк
            bool changesMade = false;
            for (int i = 0; i < decodedStrings.Count; i++)
            {
                Console.WriteLine($"\nРедактировать строку [{i}]? (y/n) Текущее значение: {decodedStrings[i]}");
                if (Console.ReadKey().Key == ConsoleKey.Y)
                {
                    Console.WriteLine($"\nВведите новое значение для строки [{i}]:");
                    string newValue = Console.ReadLine();

                    if (newValue != decodedStrings[i])
                    {
                        modifiedStrings[i] = newValue;
                        changesMade = true;
                    }
                }
            }

            // 4. Если были изменения - сохраняем новый файл
            if (changesMade)
            {
                byte[] modifiedBytes = ApplyStringChanges(originalBytes, decodedStrings, modifiedStrings);

                string newFilePath = Path.Combine(
                    Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(filePath) + "_modified" + Path.GetExtension(filePath));

                File.WriteAllBytes(newFilePath, modifiedBytes);
                Console.WriteLine($"\nФайл успешно сохранён: {newFilePath}");
            }
            else
            {
                Console.WriteLine("\nИзменений не было, файл не сохранён.");
            }

            File.WriteAllLines("decoded.txt", decodedStrings, Encoding.ASCII);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="originalBytes">Изначальный файл без изменений</param>
    /// <param name="originalStrings">Оригинальная строка в 8 битах</param>
    /// <param name="modifiedStrings">Изменённая строка в 8 битах</param>
    /// <returns></returns>
    static byte[] ApplyStringChanges(byte[] originalBytes, List<string> originalStrings, List<string> modifiedStrings)
    {
        List<byte> newBytes = new List<byte>(originalBytes);
        int stringIndex = 0;
        int position = 0;

        while (position < newBytes.Count && stringIndex < originalStrings.Count)
        {
            if (newBytes[position] == 0x05)
            {
                int startPos = position;
                position++;

                // Находим конец блока строки (до следующего 0x05 или конца файла)
                int blockEnd = position;
                while (blockEnd < newBytes.Count && newBytes[blockEnd] != 0x05)
                {
                    blockEnd++;
                }

                // Вычисляем оригинальный размер блока
                int originalBlockSize = blockEnd - position;

                if (originalStrings[stringIndex] != modifiedStrings[stringIndex])
                {
                    // Кодируем с учетом терминатора и оригинального размера
                    byte[] newBlock = EncodeStringWithFixedSize(
                        modifiedStrings[stringIndex],
                        originalBlockSize);

                    // Заменяем блок (сохраняя маркер 0x05)
                    newBytes.RemoveRange(position, originalBlockSize);
                    newBytes.InsertRange(position, newBlock);

                    position += newBlock.Length;
                }
                else
                {
                    position = blockEnd;
                }

                stringIndex++;
            }
            else
            {
                position++;
            }
        }

        return newBytes.ToArray();
    }
    /// <summary>
    /// Декодирует строку непрерывный поток, где каждый символ занимает 7 бит
    /// </summary>
    /// <param name="str">Строка для декодирования</param>
    /// <param name="targetSize">Требуемый размер строки</param>
    /// <returns></returns>
    static byte[] EncodeStringWithFixedSize(string str, int targetSize)
    {
        // 1. Приводим строку к нужному размеру (минус 1 байт для терминатора 0x03)
        int contentSize = targetSize - 1;
        string adjustedStr = str.Length > contentSize
            ? str.Substring(0, contentSize)
            : str.PadRight(contentSize, ' ');

        // 2. Кодируем в 7-битный формат
        List<byte> encodedBytes = new List<byte>();
        int bitBuffer = 0;
        int bitsInBuffer = 0;

        // Кодируем содержимое строки
        foreach (char c in adjustedStr)
        {
            byte charByte = (byte)c;
            if (charByte < 0x20 && charByte != 0) charByte = 0x20;

            bitBuffer |= (charByte & 0x7F) << bitsInBuffer;
            bitsInBuffer += 7;

            while (bitsInBuffer >= 8)
            {
                encodedBytes.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitsInBuffer -= 8;
            }
        }

        // Добавляем терминатор 0x03 (как в оригинале)
        bitBuffer |= (0x03 & 0x7F) << bitsInBuffer;
        bitsInBuffer += 7;
        while (bitsInBuffer >= 8)
        {
            encodedBytes.Add((byte)(bitBuffer & 0xFF));
            bitBuffer >>= 8;
            bitsInBuffer -= 8;
        }

        // Добавляем оставшиеся биты
        if (bitsInBuffer > 0)
        {
            encodedBytes.Add((byte)(bitBuffer & 0xFF));
        }

        // 3. Обрезаем/дополняем до точного размера
        if (encodedBytes.Count > targetSize)
        {
            encodedBytes = encodedBytes.Take(targetSize).ToList();
        }
        else if (encodedBytes.Count < targetSize)
        {
            // Важно: дополняем только если не хватает места для терминатора
            if (!encodedBytes.Contains(0x03))
            {
                encodedBytes.Add(0x03); // Гарантируем наличие терминатора
            }
            while (encodedBytes.Count < targetSize)
            {
                encodedBytes.Add(0); // Дополняем нулями при необходимости
            }
        }

        return encodedBytes.ToArray();
    }
    /// <summary>
    /// Декодируем строки из непрерывного потоква 7 бит в 8 бит
    /// </summary>
    /// <param name="bytes">Поток с 7-битными символами</param>
    /// <returns>Список строк</returns>
    static List<string> DecodeStrings(byte[] bytes)
    {
        const int TextBufferSize = 300;
        List<string> result = new List<string>();
        int position = 0;
        byte[] textBuffer = new byte[TextBufferSize];

        while (position < bytes.Length)
        {
            if (bytes[position] == 0x05)
            {
                position++;
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
                        result.Add(Encoding.ASCII.GetString(textBuffer, 0, strPos));
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
                    result.Add(Encoding.ASCII.GetString(textBuffer, 0, strPos));
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