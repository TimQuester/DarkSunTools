using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarkSunRE.Models;
using DSunTools.Common.Gpl;

namespace DarkSunRE.Models
{
    public class GPLDocument
    {
        public string Name { get; }
        public string Path { get; set; }
        public string FileName { get; }
        public string ResourceName { get; }
        public byte[] originalBytes { get; }
        public byte[] newBytes { get; private set; }
        public List<GPLString> decodedStrings { get; set; } = new List<GPLString>();
        public List<TransliterationRule> TransliterationRules { get; set; } = new();

        /// <summary>Set when the instruction walker could not consume the whole chunk.</summary>
        public string ParseError { get; private set; }
        public bool ParseAligned { get; private set; }

        public GPLDocument(
            byte[] resourceBytes,
            string name,
            string path = null,
            string fileName = null,
            string resourceName = null,
            List<TransliterationRule> transliterationRules = null)
        {
            Name = name;
            Path = path ?? name;
            FileName = fileName ?? name;
            ResourceName = resourceName ?? name;
            TransliterationRules = transliterationRules ?? new List<TransliterationRule>();
            originalBytes = resourceBytes ?? Array.Empty<byte>();
            newBytes = (byte[])originalBytes.Clone();

            decodedStrings = DecodeToGPLStrings(originalBytes);
        }

        public void ApplyStringChanges()
        {
            newBytes = ApplyStringChangesToBytes(originalBytes, decodedStrings, TransliterationRules);
        }

        public void RefreshNewBytesAfterLoadJson()
        {
            ApplyStringChanges();
        }

        // --- Decoding / encoding -------------------------------------------------

        private List<GPLString> DecodeToGPLStrings(byte[] bytes)
        {
            var result = new List<GPLString>();
            if (bytes == null || bytes.Length == 0)
            {
                ParseAligned = true;
                return result;
            }

            GplParseResult parsed = GplChunkParser.Parse(bytes);
            ParseAligned = parsed.Aligned;
            ParseError = parsed.Error;

            int stringIndex = 0;
            foreach (GplStringSpan span in parsed.Strings)
            {
                if (span.SubType == GplStringSubType.Introduce)
                    continue; // INTRODUCE has no editable payload.

                string decoded = span.Text ?? string.Empty;
                var gpl = new GPLString
                {
                    StringNumber = stringIndex++,
                    StartPosition = span.ContentStart,
                    EndPosition = span.ContentLength > 0
                        ? span.ContentStart + span.ContentLength - 1
                        : span.ContentStart - 1,
                    EncodedLength = span.ContentLength,
                    SubType = span.SubType,
                    OriginalString = decoded,
                    NewString = decoded,
                    RealString = !string.IsNullOrWhiteSpace(decoded),
                    OriginalBytes = ExtractRange(bytes, span.ContentStart, span.ContentLength),
                    NewBytes = ExtractRange(bytes, span.ContentStart, span.ContentLength),
                };

                result.Add(gpl);
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

        private static byte[] ApplyStringChangesToBytes(
            byte[] originalBytes,
            List<GPLString> strings,
            List<TransliterationRule> transliterationRules)
        {
            if (originalBytes == null) return Array.Empty<byte>();

            var result = (byte[])originalBytes.Clone();

            // Since every edit is fixed-length, order is not strictly
            // important; descend anyway so a malformed span cannot move
            // a later span's start.
            foreach (GPLString gpl in strings
                         .Where(s => s != null && s.IsEditable)
                         .OrderByDescending(s => s.StartPosition))
            {
                string originalString = gpl.OriginalString ?? string.Empty;
                string newString = gpl.NewString ?? string.Empty;

                if (transliterationRules != null && transliterationRules.Count > 0)
                    newString = ApplyTransliteration(newString, transliterationRules);

                if (string.Equals(originalString, newString, StringComparison.Ordinal))
                    continue;

                if (newString.Length != originalString.Length)
                {
                    throw new InvalidOperationException(
                        $"GPL string #{gpl.StringNumber} at 0x{gpl.StartPosition:X}: " +
                        $"length {newString.Length} != original {originalString.Length}. " +
                        "Fixed-length mode refuses to resize a span.");
                }

                int encodedLength = gpl.EncodedLength;
                if (encodedLength <= 0 && gpl.OriginalBytes != null)
                    encodedLength = gpl.OriginalBytes.Length;
                if (encodedLength <= 0 && gpl.EndPosition >= gpl.StartPosition)
                    encodedLength = gpl.EndPosition - gpl.StartPosition + 1;
                if (encodedLength <= 0)
                    encodedLength = GplStringCodec.PackedLength(originalString.Length);

                int minimal = GplStringCodec.PackedLength(newString.Length);
                if (minimal > encodedLength)
                {
                    throw new InvalidOperationException(
                        $"GPL string #{gpl.StringNumber} at 0x{gpl.StartPosition:X}: " +
                        $"needs {minimal} bytes but the original span is {encodedLength}.");
                }

                if (gpl.StartPosition < 0 ||
                    gpl.StartPosition + encodedLength > result.Length)
                {
                    throw new InvalidOperationException(
                        $"GPL string #{gpl.StringNumber}: span 0x{gpl.StartPosition:X} " +
                        $"+{encodedLength} is outside the chunk.");
                }

                byte[] encoded = GplStringCodec.PackCompressed(newString, encodedLength);
                Buffer.BlockCopy(encoded, 0, result, gpl.StartPosition, encoded.Length);
                gpl.NewBytes = encoded;
            }

            if (result.Length != originalBytes.Length)
            {
                throw new InvalidOperationException(
                    "Fixed-length apply changed the chunk size; this must never happen.");
            }

            return result;
        }

        /// <summary>
        /// One-pass, one-character-to-one-character transliteration.
        /// Multi-character rules are ignored here: they cannot preserve
        /// the fixed-length invariant. (They remain useful for previews.)
        /// </summary>
        private static string ApplyTransliteration(string input, List<TransliterationRule> rules)
        {
            if (string.IsNullOrEmpty(input) || rules == null || rules.Count == 0)
                return input;

            var map = new Dictionary<char, char>();
            foreach (TransliterationRule rule in rules)
            {
                if (rule == null) continue;
                string from = rule.From ?? string.Empty;
                string to = rule.To ?? string.Empty;
                if (from.Length == 1 && to.Length == 1)
                    map[from[0]] = to[0];
            }

            if (map.Count == 0)
                return input;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(map.TryGetValue(c, out char replacement) ? replacement : c);
            return sb.ToString();
        }
    }
}
