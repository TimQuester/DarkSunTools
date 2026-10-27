using System;
using System.Collections.Generic;
using System.Text;

namespace DSunTools.Common.Gpl
{
    /// <summary>
    /// Sub-type marker that precedes a GPL string payload.
    /// </summary>
    public enum GplStringSubType : byte
    {
        Introduce = 0x01,
        Uncompressed = 0x02,
        Compressed = 0x05,
    }

    /// <summary>
    /// One string found inside a GPL/MAS chunk.
    /// <see cref="ContentStart"/> points at the first payload byte
    /// (the byte after the sub-type marker); <see cref="ContentLength"/>
    /// is the number of bytes that belong to the payload, including
    /// the terminator and any trailing padding.
    /// </summary>
    public sealed record GplStringSpan(
        int SubTypeOffset,
        int ContentStart,
        int ContentLength,
        int CharCount,
        GplStringSubType SubType,
        string Text,
        int InstructionOffset,
        byte Opcode);

    /// <summary>
    /// Fixed-length codec for the 7-bit packed GPL string format.
    ///
    /// The important invariant for translation is:
    ///   same character count =&gt; same encoded byte count,
    /// because the packed length only depends on the number of
    /// 7-bit symbols plus the 0x03 terminator.
    /// </summary>
    public static class GplStringCodec
    {
        public const byte Terminator = 0x03;
        public const int MaxChars = 300;

        /// <summary>Minimal packed length, in bytes, for N characters.</summary>
        public static int PackedLength(int charCount)
        {
            if (charCount < 0) throw new ArgumentOutOfRangeException(nameof(charCount));
            return ((charCount + 1) * 7 + 7) / 8;
        }

        /// <summary>Maximum character count that fits into a byte budget.</summary>
        public static int MaxCharsForBytes(int byteCount)
        {
            if (byteCount <= 0) return 0;
            return (8 * byteCount) / 7 - 1;
        }

        /// <summary>
        /// Decode the string whose sub-type marker is at <paramref name="subTypeOffset"/>.
        /// Returns false for an unsupported sub-type or a malformed/truncated payload.
        /// </summary>
        public static bool TryDecode(
            byte[] data,
            int subTypeOffset,
            out GplStringSpan? span,
            out string? error)
        {
            span = null;
            error = null;

            if (data == null) { error = "data is null"; return false; }
            if (subTypeOffset < 0 || subTypeOffset >= data.Length)
            {
                error = "sub-type offset outside chunk";
                return false;
            }

            byte marker = data[subTypeOffset];
            int contentStart = subTypeOffset + 1;

            if (marker == (byte)GplStringSubType.Introduce)
            {
                span = new GplStringSpan(
                    subTypeOffset,
                    contentStart,
                    0,
                    0,
                    GplStringSubType.Introduce,
                    "<active_character_name>",
                    0,
                    0);
                return true;
            }

            if (marker == (byte)GplStringSubType.Uncompressed)
            {
                // Not present in the shipped corpus; the engine's
                // uncompressed format uses a 2-bit rotation and a
                // 0xC0 terminator. Not needed for the fixed-length
                // 7-bit path yet.
                error = "uncompressed (0x02) strings are not supported yet";
                return false;
            }

            if (marker != (byte)GplStringSubType.Compressed)
            {
                error = $"unknown string sub-type 0x{marker:X2}";
                return false;
            }

            if (!TryDecodeCompressed(data, contentStart, out string text, out int consumed, out error))
                return false;

            span = new GplStringSpan(
                subTypeOffset,
                contentStart,
                consumed,
                text.Length,
                GplStringSubType.Compressed,
                text,
                0,
                0);
            return true;
        }

        /// <summary>
        /// Decode a 7-bit packed payload that starts at <paramref name="contentStart"/>.
        /// </summary>
        public static bool TryDecodeCompressed(
            byte[] data,
            int contentStart,
            out string text,
            out int consumed,
            out string? error)
        {
            text = string.Empty;
            consumed = 0;
            error = null;

            if (data == null) { error = "data is null"; return false; }
            if (contentStart < 0 || contentStart >= data.Length)
            {
                error = "string payload starts outside chunk";
                return false;
            }

            int buffer = 0;
            int shift = 1;
            int pos = contentStart;
            var sb = new StringBuilder(64);

            while (pos < data.Length && sb.Length < MaxChars)
            {
                if (shift > 0)
                {
                    buffer = ((buffer << 8) | data[pos]) & 0xFFFF;
                    pos++;
                }

                int ch = (buffer >> shift) & 0x7F;
                if (ch == Terminator)
                {
                    text = sb.ToString();
                    consumed = pos - contentStart;
                    return true;
                }

                sb.Append((char)ch);
                shift = (shift + 1) & 7;
            }

            if (sb.Length >= MaxChars)
                error = $"string exceeds {MaxChars} characters without terminator";
            else
                error = "string runs past end of chunk without terminator";

            return false;
        }

        /// <summary>
        /// Pack <paramref name="text"/> into exactly <paramref name="targetLength"/> bytes.
        /// The target must be at least <see cref="PackedLength"/>; the remaining
        /// bytes are zero padding after the terminator (the game stops at 0x03).
        /// </summary>
        public static byte[] PackCompressed(string text, int targetLength)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (targetLength <= 0) throw new ArgumentOutOfRangeException(nameof(targetLength));

            int minimal = PackedLength(text.Length);
            if (minimal > targetLength)
            {
                throw new ArgumentException(
                    $"text needs {minimal} bytes but only {targetLength} are available",
                    nameof(targetLength));
            }

            var bytes = new List<byte>(targetLength);
            int buffer = 0;
            int bits = 0;

            void Put(int value)
            {
                if (value < 0 || value > 0x7F)
                    throw new ArgumentException($"value 0x{value:X} is not a 7-bit symbol");

                buffer = (buffer << 7) | (value & 0x7F);
                bits += 7;
                while (bits >= 8)
                {
                    bits -= 8;
                    bytes.Add((byte)((buffer >> bits) & 0xFF));
                    buffer &= (1 << bits) - 1;
                }
            }

            foreach (char c in text)
            {
                if (c > 0x7F)
                    throw new ArgumentException($"character U+{(int)c:X4} is not a 7-bit symbol", nameof(text));
                if (c == Terminator)
                    throw new ArgumentException("text contains the 0x03 string terminator", nameof(text));

                Put(c);
            }

            Put(Terminator);

            if (bits > 0)
                bytes.Add((byte)((buffer << (8 - bits)) & 0xFF));

            while (bytes.Count < targetLength)
                bytes.Add(0);

            return bytes.ToArray();
        }

        /// <summary>
        /// Convenience wrapper: decode a span and assign parser context.
        /// </summary>
        public static bool TryDecodeSpan(
            byte[] data,
            int subTypeOffset,
            int instructionOffset,
            byte opcode,
            out GplStringSpan? span,
            out string? error)
        {
            if (!TryDecode(data, subTypeOffset, out span, out error))
                return false;

            span = span! with
            {
                InstructionOffset = instructionOffset,
                Opcode = opcode,
            };
            return true;
        }
    }
}
