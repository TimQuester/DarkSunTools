using System.Text;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Введите путь к файлу:");
        string filePath = Console.ReadLine();

        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            List<string> decodedStrings = DecodeStrings(fileBytes);

            Console.WriteLine("\nДекодированные строки:");
            foreach (string str in decodedStrings)
            {
                Console.WriteLine(str);
            }

            File.WriteAllLines("decoded.txt", decodedStrings, Encoding.ASCII);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static List<string> DecodeStrings(byte[] bytes)
    {
        List<string> result = new List<string>();
        int position = 0;
        byte[] textBuffer = new byte[300]; // Как в оригинальном коде

        while (position < bytes.Length)
        {
            if (bytes[position] == 0x05) // Начало строки
            {
                position++;
                int strPos = 0;
                int currentChar = 0;
                int currentRShift = 1;
                bool endOfString = false;

                while (position < bytes.Length && strPos < 299 && !endOfString)
                {
                    // Чтение нового байта (как в оригинале)
                    if (currentRShift > 0)
                    {
                        currentChar = (currentChar << 8) | bytes[position++];
                    }

                    // Извлечение 7-битного символа
                    int decodedChar = (currentChar >> currentRShift) & 0x7F;
                    textBuffer[strPos] = (byte)decodedChar;

                    // Обработка конца строки (0x03)
                    if (textBuffer[strPos] == 0x03)
                    {
                        textBuffer[strPos] = 0; // NULL-терминатор
                        result.Add(Encoding.ASCII.GetString(textBuffer, 0, strPos));
                        endOfString = true;
                        break;
                    }

                    // Замена непечатаемых символов (кроме \t, \n, \r)
                    if (textBuffer[strPos] < 0x20 &&
                        textBuffer[strPos] != '\t' &&
                        textBuffer[strPos] != '\n' &&
                        textBuffer[strPos] != '\r')
                    {
                        textBuffer[strPos] = 0x20; // Пробел
                    }

                    strPos++;
                    currentRShift++;

                    // Сброс сдвига (как в оригинальном коде)
                    if (currentRShift > 7)
                    {
                        currentRShift = 0;
                        //currentChar = 0;
                    }
                }

                // Если строка не закончилась 0x03
                if (!endOfString && strPos > 0)
                {
                    textBuffer[strPos] = 0; // NULL-терминатор
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