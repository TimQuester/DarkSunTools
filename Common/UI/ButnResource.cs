using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace DSunTools.Common.UI
{
    /// <summary>
    /// BUTN chunk: button frame dimensions and icon/text offsets. The first
    /// 12 bytes are the shared resource header; the layout below follows the
    /// shipped UI chunks (w/h at +40/+42, icon data at +92..+107, text at +110).
    /// </summary>
    public sealed class ButnResource
    {
        private const int HeaderSize = 12;
        private const int WidthOffset = 40;
        private const int HeightOffset = 42;
        private const int IconXOffset = 92;
        private const int IconYOffset = 94;
        private const int TextXOffset = 96;
        private const int TextYOffset = 98;
        private const int IconIdOffset = 100;
        private const int HotkeyOffset = 108;
        private const int TextLengthOffset = 109;
        private const int TextOffset = 110;

        private byte[] _bytes;

        private ButnResource(byte[] bytes)
        {
            _bytes = bytes;
        }

        public string Tag => ReadTag();
        public int Number => _bytes.Length >= HeaderSize ? BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(8, 4)) : 0;
        public int Width
        {
            get => ReadInt16(WidthOffset);
            set => WriteInt16(WidthOffset, value);
        }

        public int Height
        {
            get => ReadInt16(HeightOffset);
            set => WriteInt16(HeightOffset, value);
        }

        public int IconX
        {
            get => ReadInt16(IconXOffset);
            set => WriteInt16(IconXOffset, value);
        }

        public int IconY
        {
            get => ReadInt16(IconYOffset);
            set => WriteInt16(IconYOffset, value);
        }

        public int TextX
        {
            get => ReadInt16(TextXOffset);
            set => WriteInt16(TextXOffset, value);
        }

        public int TextY
        {
            get => ReadInt16(TextYOffset);
            set => WriteInt16(TextYOffset, value);
        }

        public uint IconId
        {
            get => _bytes.Length >= IconIdOffset + 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(IconIdOffset, 4))
                : 0;
            set
            {
                EnsureLength(IconIdOffset + 4);
                BinaryPrimitives.WriteUInt32LittleEndian(_bytes.AsSpan(IconIdOffset, 4), value);
            }
        }

        public byte Hotkey
        {
            get => _bytes.Length > HotkeyOffset ? _bytes[HotkeyOffset] : (byte)0;
            set
            {
                EnsureLength(HotkeyOffset + 1);
                _bytes[HotkeyOffset] = value;
            }
        }

        public string Text
        {
            get
            {
                if (_bytes.Length <= TextLengthOffset)
                    return string.Empty;
                int length = Math.Min(_bytes[TextLengthOffset], Math.Max(0, _bytes.Length - TextOffset));
                return length <= 0 ? string.Empty : Encoding.Latin1.GetString(_bytes, TextOffset, length).TrimEnd('\0');
            }
            set
            {
                string text = value ?? string.Empty;
                if (text.Length > byte.MaxValue)
                    throw new InvalidOperationException("BUTN text is limited to 255 one-byte characters.");
                byte[] prefix = _bytes.Length >= TextOffset ? _bytes[..TextOffset] : new byte[TextOffset];
                byte[] textBytes = Encoding.Latin1.GetBytes(text);
                var result = new byte[TextOffset + textBytes.Length];
                Array.Copy(prefix, result, Math.Min(prefix.Length, result.Length));
                Array.Copy(textBytes, 0, result, TextOffset, textBytes.Length);
                result[TextLengthOffset] = (byte)textBytes.Length;
                _bytes = result;
                UpdateHeaderLength();
            }
        }

        public static ButnResource Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderSize || Encoding.ASCII.GetString(data, 0, 4) != "BUTN")
                throw new InvalidDataException("Not a BUTN resource.");
            return new ButnResource((byte[])data.Clone());
        }

        public byte[] Build()
        {
            UpdateHeaderLength();
            return (byte[])_bytes.Clone();
        }

        private void UpdateHeaderLength()
        {
            if (_bytes.Length >= 8)
                BinaryPrimitives.WriteInt32LittleEndian(_bytes.AsSpan(4, 4), _bytes.Length);
        }

        private string ReadTag() => _bytes.Length >= 4 ? Encoding.ASCII.GetString(_bytes, 0, 4) : string.Empty;

        private int ReadInt16(int offset)
        {
            return _bytes.Length >= offset + 2
                ? BinaryPrimitives.ReadInt16LittleEndian(_bytes.AsSpan(offset, 2))
                : 0;
        }

        private void WriteInt16(int offset, int value)
        {
            EnsureLength(offset + 2);
            BinaryPrimitives.WriteInt16LittleEndian(_bytes.AsSpan(offset, 2), checked((short)value));
        }

        private void EnsureLength(int length)
        {
            if (_bytes.Length >= length)
                return;
            Array.Resize(ref _bytes, length);
        }
    }
}
