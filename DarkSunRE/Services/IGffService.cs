using DarkSunRE.Models;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public interface IGffService
    {
        GffFile LoadGffFile(string filePath);
        void SaveGffFile(GffFile gffFile, string filePath);
        List<ResourceInfo> ExtractResources(GffFile gffFile);
        Task<GffFile> LoadGffFileAsync(string filePath);
    }
}
