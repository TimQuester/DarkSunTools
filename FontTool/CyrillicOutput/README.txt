FONT_cyrillic.bin is a FONT resource chunk, built from the supplied game's FONT resource.
The 66 Russian uppercase/lowercase glyphs use printable ASCII slots for compatibility with 7-bit GPL strings.
The repurposed ASCII slots are A-Z, a-z, % [ ] ^ ` { | } ~ @ " # + /. Those Latin letters/symbols change.
Use cyrillic_rules.txt in Project Settings. It also maps five punctuation aliases at bytes 0xC2-0xC6 for TEXT/NAME.
Export and re-import TEXT/NAME resources for these five symbols to be moved to their high-byte aliases.
GPL strings are 7-bit packed; high-byte punctuation aliases are filtered from GPL encoding.
cyrillic_rules_gpl.txt contains only the 7-bit Russian glyph mappings.
Original glyphs 0x80-0xC1 are preserved because the installed game data uses some of those byte values.
Corpus scan found five uses of repurposed punctuation in parsed DS1/DS2 GPL strings (#, +, /); inspect these.
This is a prototype: five low-frequency punctuation glyphs in GPL cannot be preserved by high-byte aliases.
Replace FONT-100 in each game's RESOURCE.GFF with this chunk. Use a matching game font version.
The source game font must be the same format/version as the target game.
