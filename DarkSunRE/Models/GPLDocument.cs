using System;
using System.Collections.Generic;
using System.Linq;
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
        public string? ParseError { get; private set; }
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

        /// <summary>
        /// Check every editable string against the same rules the encoder
        /// uses. The result is empty when the chunk can be written safely.
        /// </summary>
        public IReadOnlyList<GplValidationIssue> Validate()
        {
            var issues = new List<GplValidationIssue>();

            if (!ParseAligned)
            {
                issues.Add(new GplValidationIssue(
                    -1,
                    -1,
                    $"Parser stopped before the end of the chunk: {ParseError ?? "unknown reason"}. " +
                    "Strings after that point may be missing."));
            }

            foreach (GPLString gpl in decodedStrings.Where(s => s != null && s.IsEditable))
            {
                if (!TryEncodeString(gpl, TransliterationRules, originalBytes.Length, out _, out _, out string error))
                    issues.Add(new GplValidationIssue(gpl.StringNumber, gpl.StartPosition, error));
            }

            return issues;
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
                if (!TryEncodeString(gpl, transliterationRules, result.Length,
                        out byte[] encoded, out bool changed, out string error))
                {
                    throw new InvalidOperationException(
                        $"GPL string #{gpl.StringNumber} at 0x{gpl.StartPosition:X}: {error}");
                }

                if (!changed)
                    continue;

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
        /// Transliterate and pack one string without touching the chunk.
        /// Returns false with a human-readable reason when the edit cannot
        /// preserve the fixed-length layout.
        /// </summary>
        private static bool TryEncodeString(
            GPLString gpl,
            List<TransliterationRule> transliterationRules,
            int chunkLength,
            out byte[] encoded,
            out bool changed,
            out string error)
        {
            encoded = Array.Empty<byte>();
            changed = false;
            error = string.Empty;

            string originalString = gpl.OriginalString ?? string.Empty;
            string newString = gpl.NewString ?? string.Empty;

            // The rules are stored as "game glyph = displayed character"
            // (the font preview shows them that way), so encoding a
            // Cyrillic translation goes through the reverse map.
            newString = GplTransliteration.EncodeToGameCharset(
                newString,
                transliterationRules?
                    .Where(r => r != null)
                    .Select(r => new GplTransliterationRule(r.From ?? string.Empty, r.To ?? string.Empty))
                    .ToList());

            if (string.Equals(originalString, newString, StringComparison.Ordinal))
                return true;

            if (newString.Length != originalString.Length)
            {
                error = $"length {newString.Length} != original {originalString.Length}; " +
                        "fixed-length mode refuses to resize a span. " +
                        "Use a translation of the same character count (pad with spaces if needed).";
                return false;
            }

            for (int i = 0; i < newString.Length; i++)
            {
                char bad = newString[i];
                if (bad <= 0x7F) continue;

                error = $"character U+{(int)bad:X4} '{bad}' has no 7-bit mapping. " +
                        "Add a transliteration rule (game glyph=displayed character).";
                return false;
            }

            int encodedLength = gpl.EncodedLength;
            if (encodedLength <= 0 && gpl.OriginalBytes != null)
                encodedLength = gpl.OriginalBytes.Length;
            if (encodedLength <= 0 && gpl.EndPosition >= gpl.StartPosition)
                encodedLength = gpl.EndPosition - gpl.StartPosition + 1;
            if (encodedLength <= 0)
                encodedLength = GplStringCodec.PackedLength(originalString.Length);

            int minimal = GplStringCodec.PackedLength(newString.Length);
            if (minimal != encodedLength)
            {
                // Same character count implies the same packed byte count.
                // A mismatch means the span metadata is wrong (for example
                // a hand-edited JSON); refusing here is safer than writing
                // padding, because zero padding would be executed as the
                // next GPL opcode.
                error = $"span is {encodedLength} bytes but {newString.Length} characters need {minimal}. " +
                        "The span metadata does not match the fixed-length string.";
                return false;
            }

            if (gpl.StartPosition < 0 || gpl.StartPosition + encodedLength > chunkLength)
            {
                error = $"span 0x{gpl.StartPosition:X}+{encodedLength} is outside the chunk.";
                return false;
            }

            try
            {
                encoded = GplStringCodec.PackCompressed(newString, encodedLength);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            changed = true;
            return true;
        }
    }

    public sealed record GplValidationIssue(int StringNumber, int StartPosition, string Message);
}
