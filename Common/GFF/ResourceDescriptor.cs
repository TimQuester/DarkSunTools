using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSunTools.Common.GFF
{
    public class ResourceDescriptor
    {
        public readonly string Tag;
        public readonly int Number;
        public readonly int Offset;
        public readonly int Size;

        public ResourceDescriptor(string tag, int number, int offset, int size)
        {
            Tag = tag;
            Number = number;
            Offset = offset;
            Size = size;
        }
    }
}
