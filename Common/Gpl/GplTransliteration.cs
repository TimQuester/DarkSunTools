using System;
using System.Collections.Generic;
using System.Text;

namespace DSunTools.Common.Gpl
{
    /// <summary>
    /// One transliteration pair: <see cref="From"/> is a character of the
    /// game's 7-bit font (ASCII), <see cref="To"/> is what that glyph shows
    /// on screen (for example, Cyrillic).
    /// </summary>
    public readonly record struct GplTransliterationRule(string From, string To);

    /// <summary>
    /// Converts translated text to the characters the game can actually
    /// store. GPL strings are 7-bit, so a Russian translation typed in
    /// Cyrillic has to be mapped back onto the Latin glyph codes whose
    /// bitmaps the font resource replaced.
    ///
    /// Rules are stored in display order (game glyph =&gt; visible
    /// character), which is also how the font preview shows them. Encoding
    /// therefore uses the reverse map. Only one-character-to-one-character
    /// rules can be applied: multi-character replacements would change the
    /// string length, and GPL string spans are fixed-length.
    /// </summary>
    public static class GplTransliteration
    {
        /// <summary>
        /// Map display characters back to game glyph codes. Characters that
        /// are not covered by a rule are left untouched; non-7-bit leftovers
        /// are reported by the caller when the string is encoded.
        /// </summary>
        public static string EncodeToGameCharset(
            string? input,
            IEnumerable<GplTransliterationRule>? rules)
        {
            if (string.IsNullOrEmpty(input) || rules == null)
                return input ?? string.Empty;

            Dictionary<char, char>? map = BuildEncodingMap(rules);
            if (map == null || map.Count == 0)
                return input;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(map.TryGetValue(c, out char gameChar) ? gameChar : c);
            return sb.ToString();
        }

        /// <summary>
        /// Map game glyph codes to the characters they are displayed as.
        /// Useful for previews; translation editing uses the reverse
        /// direction through <see cref="EncodeToGameCharset"/>.
        /// </summary>
        public static string DecodeToDisplay(
            string? input,
            IEnumerable<GplTransliterationRule>? rules)
        {
            if (string.IsNullOrEmpty(input) || rules == null)
                return input ?? string.Empty;

            var map = new Dictionary<char, char>();
            foreach (GplTransliterationRule rule in rules)
            {
                if (rule.From is { Length: 1 } && rule.To is { Length: 1 })
                    map[rule.From[0]] = rule.To[0];
            }

            if (map.Count == 0)
                return input;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(map.TryGetValue(c, out char displayChar) ? displayChar : c);
            return sb.ToString();
        }

        private static Dictionary<char, char>? BuildEncodingMap(
            IEnumerable<GplTransliterationRule> rules)
        {
            var map = new Dictionary<char, char>();
            foreach (GplTransliterationRule rule in rules)
            {
                if (rule.From is not { Length: 1 } || rule.To is not { Length: 1 })
                    continue;

                // Rule is game glyph -> displayed character, so encoding
                // goes the other way. Skip the identity pairs and pairs
                // where both sides are the same alphabet to avoid
                // surprising rewrites.
                if (rule.From[0] == rule.To[0])
                    continue;

                map[rule.To[0]] = rule.From[0];
            }

            return map;
        }
    }
}
