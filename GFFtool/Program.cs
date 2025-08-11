using DSunTools.Common.Gff;
using DSunTools.Common.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dsun.GffTool
{
    class Program
    {
        private class Options : IOptionsWithHelp
        {
            public string GffFile { get; }
            public bool ListContents { get; }
            public string ExtractToDir { get; }
            public string ReplaceWith { get; }
            public string Tag { get; }
            public int? Number { get; }
            public bool IsHelpRequested { get; }

            public Options(
                string gffFile,
                bool listContents,
                string extractToDir,
                string replaceWith,
                string tag,
                int? number,
                bool helpRequested)
            {
                GffFile = gffFile;
                ListContents = listContents;
                ExtractToDir = extractToDir;
                ReplaceWith = replaceWith;
                Tag = tag;
                Number = number;
                IsHelpRequested = helpRequested;
            }
        }

        private class OptionsProcessor : OptionsProcessor<Options>
        {
            public override Options ParseArgs(string[] args)
            {
                string gffFile = null;
                bool listContents = false;
                string extractToDir = null;
                string replaceWith = null;
                string tag = null;
                int? number = null;
                bool helpRequested = false;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];

                    if (arg.StartsWith("--"))
                    {
                        string[] parts = arg.Split(new[] { '=' }, 2);
                        string optionName = parts[0];
                        string optionValue = parts.Length > 1 ? parts[1] : null;

                        switch (optionName)
                        {
                            case "--help":
                                helpRequested = true;
                                break;
                            case "--gff":
                                if (string.IsNullOrEmpty(optionValue))
                                    throw new ArgumentException("Missing GFF file path after --gff");
                                gffFile = optionValue;
                                break;
                            case "--list-contents":
                                listContents = true;
                                break;
                            case "--extract-to-dir":
                                if (string.IsNullOrEmpty(optionValue))
                                    throw new ArgumentException("Missing directory path after --extract-to-dir");
                                extractToDir = optionValue;
                                break;
                            case "--replace-with":
                                if (string.IsNullOrEmpty(optionValue))
                                    throw new ArgumentException("Missing file path after --replace-with");
                                replaceWith = optionValue;
                                break;
                            case "--tag":
                                if (string.IsNullOrEmpty(optionValue))
                                    throw new ArgumentException("Missing tag value after --tag");
                                tag = optionValue;
                                break;
                            case "--number":
                                if (string.IsNullOrEmpty(optionValue) || !int.TryParse(optionValue, out int num))
                                    throw new ArgumentException("Invalid or missing number after --number");
                                number = num;
                                break;
                            default:
                                throw new ArgumentException($"Unknown option: {optionName}");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid argument format: {arg}");
                    }
                }

                return new Options(gffFile, listContents, extractToDir, replaceWith, tag, number, helpRequested);
            }

            public override bool IsUsageValid(Options options)
            {
                int commandCount = (options.ListContents ? 1 : 0)
                    + (options.ExtractToDir != null ? 1 : 0)
                    + (options.ReplaceWith != null ? 1 : 0);

                bool areReplaceWithOptionsCorrect = !(options.ReplaceWith != null ^
                    (options.Tag != null || options.Number != null));

                return options.GffFile != null && commandCount == 1 && areReplaceWithOptionsCorrect;
            }

            public override void PrintUsage()
            {
                Console.WriteLine("To list the resources in a GFF:");
                Console.WriteLine("  GffTool.exe --gff=<gffFile> --list-contents");
                Console.WriteLine("");
                Console.WriteLine("To extract all resources in a GFF into a directory:");
                Console.WriteLine("  GffTool.exe --gff=<gffFile> --extract-to-dir=<dir>");
                Console.WriteLine("");
                Console.WriteLine("To replace a resource in a GFF:");
                Console.WriteLine("  GffTool.exe --gff=<gffFile> --replace-with=<file> --tag=TAG --number=n");
            }
        }

        static void Main(string[] args)
        {
            try
            {
                var options = new OptionsProcessor().Process(args);

                byte[] bytes = File.ReadAllBytes(options.GffFile);
                var gffFile = GffFile.Create(bytes);

                if (options.ListContents)
                {
                    ListContents(gffFile, bytes.Length);
                }

                if (options.ExtractToDir != null)
                {
                    ExtractAllResources(gffFile, options.ExtractToDir);
                }

                if (options.ReplaceWith != null)
                {
                    if (options.Tag == null || options.Number == null)
                    {
                        Console.Error.WriteLine("Error: Both tag and number must be specified for replacement");
                        Environment.Exit(1);
                    }

                    byte[] replacement = File.ReadAllBytes(options.ReplaceWith);
                    byte[] bytesWithReplacement = gffFile.ReplaceResource(
                        options.Tag, options.Number.Value, replacement);

                    File.WriteAllBytes(options.GffFile, bytesWithReplacement);
                    Console.WriteLine($"Resource {options.Tag}-{options.Number} replaced successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void ListContents(GffFile gffFile, int length)
        {
            var descriptors = gffFile.DescribeResources();

            Console.WriteLine("Listing contents.");
            Console.WriteLine("  offset   tag  number size");
            Console.WriteLine("  -------- ---- ------ --------");

            string resourceTemplate = "  0x{0:X6} {1,4}  {2,5} {3,8}";
            string nonResourceTemplate = "  0x{0:X6} .... ...... {1,8}";

            int lastEnd = 0;
            foreach (var descriptor in descriptors)
            {
                int offset = descriptor.Offset;

                if (lastEnd < offset)
                {
                    Console.WriteLine(string.Format(nonResourceTemplate, lastEnd, offset - lastEnd));
                }

                Console.WriteLine(string.Format(
                    resourceTemplate, offset, descriptor.Tag, descriptor.Number, descriptor.Size));

                lastEnd = offset + descriptor.Size;
            }

            if (lastEnd < length)
            {
                Console.WriteLine(string.Format(nonResourceTemplate, lastEnd, length - lastEnd));
            }
        }

        private static void ExtractAllResources(GffFile gffFile, string dirPath)
        {
            Console.WriteLine($"Extracting all resources to directory {dirPath}.");

            Directory.CreateDirectory(dirPath);

            var gffResources = gffFile.DescribeResources();
            var maxNumberDigitsForTag = new ConcurrentDictionary<string, int>();

            Parallel.ForEach(gffResources, descriptor =>
            {
                int digits = descriptor.Number.ToString().Length;
                maxNumberDigitsForTag.AddOrUpdate(
                    descriptor.Tag,
                    digits,
                    (_, currentMax) => Math.Max(currentMax, digits));
            });

            var templateForTag = maxNumberDigitsForTag.ToDictionary(
                kv => kv.Key,
                kv => $"{kv.Key}-{{0:D{kv.Value}}}");

            Parallel.ForEach(gffResources, descriptor =>
            {
                string template = templateForTag[descriptor.Tag];
                string name = string.Format(template, descriptor.Number);
                byte[] resourceData = gffFile.GetResourceData(descriptor.Tag, descriptor.Number);
                File.WriteAllBytes(Path.Combine(dirPath, name), resourceData);
            });

            Console.WriteLine($"Extracted {gffResources.Count} resources.");
        }
    }
}