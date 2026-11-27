using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSunTools.Common.UI
{
    public sealed class WindItem
    {
        private readonly byte[] _bytes;

        internal WindItem(byte[] bytes)
        {
            _bytes = bytes;
        }

        public string Tag => _bytes.Length >= 8 ? Encoding.ASCII.GetString(_bytes, 4, 4) : string.Empty;
        public uint Id => _bytes.Length >= 12 ? BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(8, 4)) : 0;

        public short X
        {
            get => ReadInt16(12);
            set => WriteInt16(12, value);
        }

        public short Y
        {
            get => ReadInt16(14);
            set => WriteInt16(14, value);
        }

        public bool IsButton => Tag == "BUTN";
        public byte[] Build() => (byte[])_bytes.Clone();

        private short ReadInt16(int offset) => _bytes.Length >= offset + 2
            ? BinaryPrimitives.ReadInt16LittleEndian(_bytes.AsSpan(offset, 2))
            : (short)0;

        private void WriteInt16(int offset, short value)
        {
            if (_bytes.Length < offset + 2)
                throw new InvalidDataException("WIND item record is truncated.");
            BinaryPrimitives.WriteInt16LittleEndian(_bytes.AsSpan(offset, 2), value);
        }
    }

    /// <summary>
    /// WIND chunk: a window tree with a variable list of 30-byte item records.
    /// The editor only changes item X/Y (and preserves every other byte).
    /// </summary>
    public sealed class WindResource
    {
        private const int HeaderSize = 12;
        private const int ItemCountOffset = 243;
        private const int ItemStart = 261;
        private const int ItemSize = 30;

        private byte[] _bytes;
        private readonly List<WindItem> _items = new();

        private WindResource(byte[] bytes)
        {
            _bytes = bytes;
            ParseItems();
        }

        public IReadOnlyList<WindItem> Items => _items;

        public static WindResource Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderSize + 2 || Encoding.ASCII.GetString(data, 0, 4) != "WIND")
                throw new InvalidDataException("Not a WIND resource.");
            return new WindResource((byte[])data.Clone());
        }

        public WindItem FindItem(string tag, uint id)
        {
            foreach (WindItem item in _items)
            {
                if (string.Equals(item.Tag, tag, StringComparison.Ordinal) && item.Id == id)
                    return item;
            }
            return null;
        }

        public byte[] Build()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                int offset = ItemStart + i * ItemSize;
                if (offset + ItemSize > _bytes.Length)
                    break;
                byte[] itemBytes = _items[i].Build();
                Array.Copy(itemBytes, 0, _bytes, offset, itemBytes.Length);
            }

            if (_bytes.Length >= 8)
                BinaryPrimitives.WriteInt32LittleEndian(_bytes.AsSpan(4, 4), _bytes.Length);
            return (byte[])_bytes.Clone();
        }

        private void ParseItems()
        {
            if (_bytes.Length < ItemStart)
                return;

            int count = BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(ItemCountOffset, 2));
            for (int i = 0; i < count; i++)
            {
                int offset = ItemStart + i * ItemSize;
                if (offset + ItemSize > _bytes.Length)
                    break;
                var itemBytes = new byte[ItemSize];
                Array.Copy(_bytes, offset, itemBytes, 0, ItemSize);
                _items.Add(new WindItem(itemBytes));
            }
        }
    }
}
