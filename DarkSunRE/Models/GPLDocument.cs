using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarkSunRE.Models;

namespace DarkSunRE.Models
{
    /// <summary>
    /// Lightweight GPL document used by the editor UI.
    /// Decodes GPL resource bytes into a list of <see cref="GPLString"/>,
    /// keeps original bytes, and produces new bytes after edits.
    /// </summary>
    public class GPLDocument
    {
        public string Name { get; }
        public string Path { get; set; }
        public byte[] originalBytes { get; }
        public byte[] newBytes { get; private set; }
        public List<GPLString> decodedStrings { get; set; } = new List<GPLString>();

        public GPLDocument(byte[] resourceBytes, string name, string path = null)
        {
            Name = name;
            Path = path ?? name;
            originalBytes = resourceBytes ?? Array.Empty<byte>();
            newBytes = (byte[])originalBytes.Clone();

            decodedStrings = DecodeToGPLStrings(originalBytes);
        }

        public void ApplyStringChanges()
        {
            newBytes = ApplyStringChangesToBytes(originalBytes, decodedStrings);
        }

        public void RefreshNewBytesAfterLoadJson()
        {
            ApplyStringChanges();
        }

        // --- Decoding / Encoding implementation (ported and adapted from TextTools) ---

        private static List<GPLString> DecodeToGPLStrings(byte[] bytes)
        {
            var result = new List<GPLString>();
            if (bytes == null || bytes.Length == 0) return result;

            const int TextBufferSize = 4096;
            int position = 0;
            int stringIndex = 0;

            while (position < bytes.Length)
            {
                if (bytes[position] == 0x05)
                {
                    int markerIndex = position;
                    position++; // move to content
                    int contentStart = position;
                    var textBuffer = new byte[TextBufferSize];
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
                            endOfString = true;
                            break;
                        }

                        // replace control characters with space where appropriate
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

                    int contentEndExclusive = position;
                    var decoded = Encoding.ASCII.GetString(textBuffer, 0, strPos);

                    var gpl = new GPLString
                    {
                        StringNumber = stringIndex++,
                        StartPosition = contentStart,
                        EndPosition = Math.Max(contentStart, contentEndExclusive - 1),
                        OriginalString = decoded,
                        NewString = decoded,
                        RealString = !string.IsNullOrWhiteSpace(decoded),
                        OriginalBytes = ExtractRange(bytes, contentStart, contentEndExclusive - contentStart),
                        NewBytes = ExtractRange(bytes, contentStart, contentEndExclusive - contentStart)
                    };

                    result.Add(gpl);
                }
                else
                {
                    position++;
                }
            }

            return result;
        }

        private static byte[] ExtractRange(byte[] bytes, int offset, int count)
        {
            if (bytes == null) return Array.Empty<byte>();
            if (offset < 0) offset = 0;
            if (count <= 0) return Array.Empty<byte>();
            if (offset + count > bytes.Length) count = Math.Max(0, bytes.Length - offset);
            var outb = new byte[count];
            Array.Copy(bytes, offset, outb, 0, count);
            return outb;
        }

        private static byte[] ApplyStringChangesToBytes(byte[] originalBytes, List<GPLString> strings)
        {
            if (originalBytes == null) return Array.Empty<byte>();
            var newBytesList = new List<byte>(originalBytes);
            int stringIndex = 0;
            int position = 0;

            while (position < newBytesList.Count && stringIndex < strings.Count)
            {
                if (newBytesList[position] == 0x05)
                {
                    int markerPos = position;
                    position++;
                    int blockStart = position;

                    int blockEnd = position;
                    while (blockEnd < newBytesList.Count && newBytesList[blockEnd] != 0x05)
                        blockEnd++;

                    int originalBlockSize = blockEnd - blockStart;

                    var gpl = strings[stringIndex];
                    string originalString = gpl.OriginalString ?? string.Empty;
                    string newString = gpl.NewString ?? string.Empty;

                    if (!string.Equals(originalString, newString, StringComparison.Ordinal))
                    {
                        var encoded = EncodeStringWithFixedSize(newString, originalBlockSize);
                        // replace block preserving marker
                        newBytesList.RemoveRange(blockStart, originalBlockSize);
                        newBytesList.InsertRange(blockStart, encoded);
                        position = blockStart + encoded.Length;
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

            return newBytesList.ToArray();
        }

        private static byte[] EncodeStringWithFixedSize(string str, int targetSize)
        {
            int contentSize = Math.Max(0, targetSize - 1);
            string adjustedStr = str.Length > contentSize
                ? str.Substring(0, contentSize)
                : str.PadRight(contentSize, ' ');

            var encodedBytes = new List<byte>();
            int bitBuffer = 0;
            int bitsInBuffer = 0;

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

            // terminator 0x03
            bitBuffer |= (0x03 & 0x7F) << bitsInBuffer;
            bitsInBuffer += 7;
            while (bitsInBuffer >= 8)
            {
                encodedBytes.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitsInBuffer -= 8;
            }

            if (bitsInBuffer > 0)
            {
                encodedBytes.Add((byte)(bitBuffer & 0xFF));
            }

            if (encodedBytes.Count > targetSize)
            {
                encodedBytes = encodedBytes.Take(targetSize).ToList();
            }
            else if (encodedBytes.Count < targetSize)
            {
                if (!encodedBytes.Contains(0x03))
                {
                    encodedBytes.Add(0x03);
                }
                while (encodedBytes.Count < targetSize)
                {
                    encodedBytes.Add(0);
                }
            }

            return encodedBytes.ToArray();
        }
    }
}
