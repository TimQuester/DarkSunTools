using System;
using System.Collections.Generic;
using System.Linq;

namespace DSunTools.Common.Gff
{
    public class GffFileList : IGffReader
    {
        private readonly IReadOnlyList<GffFile> gffFiles;

        public GffFileList(IEnumerable<GffFile> gffFiles)
        {
            this.gffFiles = gffFiles.ToList().AsReadOnly();
        }

        public List<ResourceDescriptor> DescribeResources()
        {
            return gffFiles
                .SelectMany(g => g.DescribeResources())
                .ToList();
        }

        public bool HasResource(string tag, int resourceNumber)
        {
            return gffFiles.Any(g => g.HasResource(tag, resourceNumber));
        }

        public byte[] GetResourceData(string tag, int resourceNumber)
        {
            var gffFile = gffFiles
                .FirstOrDefault(g => g.HasResource(tag, resourceNumber));

            if (gffFile == null)
            {
                throw new InvalidOperationException(
                    $"Resource {tag}-{resourceNumber} not found in any GFF file");
            }

            return gffFile.GetResourceData(tag, resourceNumber);
        }
    }
}