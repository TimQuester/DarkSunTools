using System;

namespace DSunTools.Common.Gpl
{
    public enum GplParamKind
    {
        None = 0,
        Fixed = 1,
        Log = 2,
        LoadVar = 3,
        Menu = 4,
        Search = 5,
        SetRecord = 6,
        Custom = 7,
    }

    public readonly record struct GplParamSpec(GplParamKind Kind, int Count = 0)
    {
        public bool IsBestEffort => Kind == GplParamKind.Custom;
    }

    /// <summary>
    /// Parameter shapes for the 129 known GPL opcodes (0x00..0x80).
    /// Mirrors gpl-disasm's PARAM_COUNTS table, which in turn comes
    /// from libgff's gpl_commands table / handler bodies.
    /// </summary>
    public static class GplOpcodeTable
    {
        // N = none, digits = fixed parameter count,
        // L = log payload, V = load variable, M = menu,
        // S = search, R = setrecord, C = custom/best-effort.
        private const string Tokens =
            "N 2 1 1 1 1 1 1 1 1 2 2 1 C N 1 3 2 1 1 2 N V 1 1 N 2 4 4 1 1 1 2 2 4 C 1 6 C 2 C 1 N N L 2 C 3 1 N 2 S 1 N 1 2 N 4 2 C 3 1 1 1 R 1 1 1 1 C C C M 2 C C C C C 2 2 N 1 C 1 C C C C 3 2 C 4 1 5 1 C N 1 1 1 3 3 N 5 5 7 7 3 3 3 3 4 C C C C C 2 2 2 2 2 2 2 2 2 2 2";

        private static readonly GplParamSpec[] Specs = Build();

        public static int Count => Specs.Length;

        public static GplParamSpec Get(byte opcode)
            => opcode < Specs.Length ? Specs[opcode] : new GplParamSpec(GplParamKind.Custom);

        public static bool IsKnown(byte opcode) => opcode < Specs.Length;

        private static GplParamSpec[] Build()
        {
            string[] tokens = Tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 0x81)
                throw new InvalidOperationException($"GPL opcode table has {tokens.Length} entries, expected 129");

            var result = new GplParamSpec[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                result[i] = t switch
                {
                    "N" => new GplParamSpec(GplParamKind.None),
                    "L" => new GplParamSpec(GplParamKind.Log),
                    "V" => new GplParamSpec(GplParamKind.LoadVar),
                    "M" => new GplParamSpec(GplParamKind.Menu),
                    "S" => new GplParamSpec(GplParamKind.Search),
                    "R" => new GplParamSpec(GplParamKind.SetRecord),
                    "C" => new GplParamSpec(GplParamKind.Custom),
                    _ => new GplParamSpec(GplParamKind.Fixed, int.Parse(t)),
                };
            }

            return result;
        }
    }
}
