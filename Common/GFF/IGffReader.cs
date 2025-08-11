using DSunTools.Common.Gff;
using DSunTools.Common.GFF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DSunTools.Common.Gff
{
    public interface IGffReader
    {
        List<ResourceDescriptor> DescribeResources();

        bool HasResource(string tag, int resourceNumber);

        byte[] GetResourceData(string tag, int resourceNumber);
    }
}