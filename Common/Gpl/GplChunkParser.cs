using System;
using System.Collections.Generic;

namespace DSunTools.Common.Gpl
{
    public sealed class GplParseResult
    {
        public GplParseResult(
            bool aligned,
            int bytesConsumed,
            string? error,
            IReadOnlyList<GplStringSpan> strings)
        {
            Aligned = aligned;
            BytesConsumed = bytesConsumed;
            Error = error;
            Strings = strings;
        }

        public bool Aligned { get; }
        public int BytesConsumed { get; }
        public string? Error { get; }
        public IReadOnlyList<GplStringSpan> Strings { get; }
    }

    /// <summary>
    /// Instruction-aware GPL/MAS walker. It does not execute anything;
    /// it only follows the byte stream so string payloads can be located
    /// without the false positives of a raw 0x05 scan.
    ///
    /// The decoder is a port of gpl-disasm's expression/param walker.
    /// Unknown/custom opcodes stop the walk and mark the chunk unaligned;
    /// the caller can then fall back to a conservative scan or report it.
    /// </summary>
    public static class GplChunkParser
    {
        private const int MaxRetValDepth = 4;

        public static GplParseResult Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var strings = new List<GplStringSpan>();
            var ctx = new Context(data, strings);
            int pos = 0;
            bool aligned = true;
            string? error = null;

            while (pos < data.Length)
            {
                int instructionOffset = pos;
                byte opcode = data[pos++];
                var spec = GplOpcodeTable.Get(opcode);

                ctx.InstructionOffset = instructionOffset;
                ctx.Opcode = opcode;

                int consumed = ReadParams(ctx, opcode, pos, 0, out bool bestEffort);
                pos += consumed;

                if (bestEffort)
                {
                    aligned = false;
                    error = $"best-effort or unsupported opcode 0x{opcode:X2} at 0x{instructionOffset:X}";
                    break;
                }

                if (consumed == 0 && spec.Kind != GplParamKind.None)
                {
                    aligned = false;
                    error = $"opcode 0x{opcode:X2} at 0x{instructionOffset:X} consumed no bytes";
                    break;
                }
            }

            if (pos != data.Length)
                aligned = false;

            return new GplParseResult(aligned, pos, error, strings);
        }

        private sealed class Context
        {
            public Context(byte[] data, List<GplStringSpan> strings)
            {
                Data = data;
                Strings = strings;
            }

            public byte[] Data { get; }
            public List<GplStringSpan> Strings { get; }
            public int InstructionOffset { get; set; }
            public byte Opcode { get; set; }
        }

        private static int ReadParams(
            Context ctx,
            byte opcode,
            int pos,
            int retvalDepth,
            out bool bestEffort)
        {
            bestEffort = false;
            var data = ctx.Data;
            var spec = GplOpcodeTable.Get(opcode);
            int start = pos;

            switch (spec.Kind)
            {
                case GplParamKind.None:
                    return 0;

                case GplParamKind.Fixed:
                    for (int i = 0; i < spec.Count; i++)
                    {
                        int n = ReadExpression(ctx, pos, retvalDepth, out bool be);
                        pos += n;
                        bestEffort |= be;
                        if (n == 0)
                        {
                            bestEffort = true;
                            break;
                        }
                    }
                    return pos - start;

                case GplParamKind.Log:
                {
                    int n = ReadStringAt(ctx, pos, out bool be);
                    bestEffort = be;
                    return n;
                }

                case GplParamKind.LoadVar:
                {
                    int n = ReadExpression(ctx, pos, retvalDepth, out bool be);
                    pos += n;
                    bestEffort |= be;

                    if (pos >= data.Length)
                    {
                        bestEffort = true;
                        return pos - start;
                    }

                    byte raw = data[pos++];
                    byte stripped = (byte)(raw & 0x7F);
                    bool extended = (stripped & 0x40) != 0;
                    byte datatype = (byte)(stripped & ~0x40);

                    if (datatype < 0x10)
                    {
                        pos += ReadSimpleVar(data, pos, extended, out bool ok);
                        if (!ok) bestEffort = true;
                    }
                    else
                    {
                        int complex = ReadComplex(data, pos, out bool ok);
                        if (!ok || complex == 0) bestEffort = true;
                        else pos += complex;
                    }

                    return pos - start;
                }

                case GplParamKind.Menu:
                {
                    int n = ReadExpression(ctx, pos, retvalDepth, out bool be);
                    pos += n;
                    bestEffort |= be;

                    int items = 0;
                    while (pos < data.Length && data[pos] != 0x4A && items < 24)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            n = ReadExpression(ctx, pos, retvalDepth, out be);
                            pos += n;
                            bestEffort |= be;
                            if (n == 0)
                            {
                                bestEffort = true;
                                break;
                            }
                        }
                        if (bestEffort) break;
                        items++;
                    }

                    if (pos < data.Length && data[pos] == 0x4A) pos++;
                    else bestEffort = true;

                    return pos - start;
                }

                case GplParamKind.Search:
                {
                    int n = ReadExpression(ctx, pos, retvalDepth, out bool be);
                    pos += n;
                    bestEffort |= be;

                    if (pos + 2 > data.Length)
                    {
                        bestEffort = true;
                        return pos - start;
                    }
                    pos += 2;

                    while (true)
                    {
                        if (pos < data.Length && data[pos] == 0x53) pos++;

                        if (pos + 2 > data.Length)
                        {
                            bestEffort = true;
                            break;
                        }

                        pos++; // field
                        byte type = data[pos++];

                        if (type >= 4 && type <= 6)
                        {
                            n = ReadExpression(ctx, pos, retvalDepth, out be);
                            pos += n;
                            bestEffort |= be;
                            if (n == 0)
                            {
                                bestEffort = true;
                                break;
                            }
                        }

                        if (pos >= data.Length || data[pos] != 0x53) break;
                    }

                    return pos - start;
                }

                case GplParamKind.SetRecord:
                {
                    int complex = ReadComplex(data, pos, out bool ok);
                    if (!ok || complex == 0)
                    {
                        bestEffort = true;
                        return pos - start;
                    }
                    pos += complex;

                    int n = ReadExpression(ctx, pos, retvalDepth, out bool be);
                    pos += n;
                    bestEffort |= be;
                    return pos - start;
                }

                case GplParamKind.Custom:
                default:
                    bestEffort = true;
                    return 0;
            }
        }

        private static int ReadExpression(
            Context ctx,
            int cursor,
            int retvalDepth,
            out bool bestEffort)
        {
            bestEffort = false;
            var data = ctx.Data;
            int pos = cursor;
            int parenLevel = 0;

            while (true)
            {
                if (pos >= data.Length)
                {
                    bestEffort = true;
                    break;
                }

                bool doNext = false;
                byte cop = data[pos++];

                if (cop < 0x80)
                {
                    // 14-bit immediate: one more byte.
                    if (pos >= data.Length)
                    {
                        bestEffort = true;
                        break;
                    }
                    pos++;
                }
                else
                {
                    bool extended = cop < 0xD0 && (cop & 0x40) != 0;
                    byte dispatch = extended ? (byte)(cop & ~0x40) : cop;
                    byte tag = (byte)(dispatch & 0x7F);

                    if (cop == 0x80)
                    {
                        // ACCM in an operand position: libgff treats this as fatal.
                        bestEffort = true;
                        break;
                    }

                    if (IsVariableTag(tag))
                    {
                        pos += ReadSimpleVar(data, pos, extended, out bool ok);
                        if (!ok)
                        {
                            bestEffort = true;
                            break;
                        }
                    }
                    else
                    {
                        switch (dispatch)
                        {
                            case 0x8B: // IMMED_BIGNUM
                                if (pos + 4 > data.Length) { bestEffort = true; goto done; }
                                pos += 4;
                                break;

                            case 0x8C: // RETVAL: one nested opcode + its params
                            {
                                if (pos >= data.Length) { bestEffort = true; goto done; }
                                byte inner = data[pos++];
                                if (retvalDepth >= MaxRetValDepth ||
                                    !GplOpcodeTable.IsKnown(inner) ||
                                    GplOpcodeTable.Get(inner).Kind == GplParamKind.Custom)
                                {
                                    bestEffort = true;
                                    goto done;
                                }

                                int n = ReadParams(ctx, inner, pos, retvalDepth + 1, out bool be);
                                pos += n;
                                bestEffort |= be;
                                if (n == 0) { bestEffort = true; goto done; }
                                break;
                            }

                            case 0x8F: // IMMED_BYTE
                                if (pos >= data.Length) { bestEffort = true; goto done; }
                                pos++;
                                break;

                            case 0x90: // IMMED_WORD (libgff bails; skip two bytes)
                                if (pos + 2 > data.Length) { bestEffort = true; goto done; }
                                pos += 2;
                                break;

                            case 0x91: // IMMED_NAME
                                if (pos + 2 > data.Length) { bestEffort = true; goto done; }
                                pos += 2;
                                break;

                            case 0x92: // IMMED_STRING
                            {
                                int n = ReadStringAt(ctx, pos, out bool be);
                                pos += n;
                                bestEffort |= be;
                                if (be) goto done;
                                break;
                            }

                            case 0xD1:
                            case 0xD2:
                            case 0xD3:
                            case 0xD4:
                            case 0xD5:
                            case 0xD6:
                            case 0xD7:
                            case 0xD8:
                            case 0xD9:
                            case 0xDA:
                            case 0xDB:
                            case 0xDC:
                            case 0xDD:
                            case 0xDE:
                            case 0xDF:
                                doNext = true;
                                break;

                            case 0xE2: // '('
                                parenLevel++;
                                doNext = true;
                                break;

                            case 0xE1: // ')'
                                parenLevel--;
                                break;

                            default:
                                if (dispatch >= 0xB0 && dispatch <= 0xBF)
                                {
                                    int complex = ReadComplex(data, pos, out bool ok);
                                    if (!ok || complex == 0) { bestEffort = true; goto done; }
                                    pos += complex;
                                }
                                else
                                {
                                    bestEffort = true;
                                    goto done;
                                }
                                break;
                        }
                    }
                }

                if (!doNext && pos < data.Length)
                {
                    byte next = data[pos];
                    if (next > 0xD0 && next <= 0xDF) doNext = true;
                }

                if (!doNext && parenLevel <= 0) break;
                if (bestEffort && parenLevel <= 0) break;
            }

        done:
            return pos - cursor;
        }

        private static bool IsVariableTag(byte tag)
            => tag is >= 0x01 and <= 0x0A or 0x0D or 0x0E;

        private static int ReadSimpleVar(byte[] data, int pos, bool extended, out bool ok)
        {
            ok = pos < data.Length;
            if (!ok) return 0;

            if (!extended) return 1;

            if (pos + 1 >= data.Length)
            {
                ok = false;
                return 1;
            }

            return 2;
        }

        private static int ReadComplex(byte[] data, int pos, out bool ok)
        {
            ok = false;
            if (data == null || pos < 0 || pos + 3 > data.Length) return 0;

            int depth = data[pos + 2];
            int total = 3 + depth;
            if (pos + total > data.Length) return 0;

            ok = true;
            return total;
        }

        private static int ReadStringAt(Context ctx, int subTypeOffset, out bool bestEffort)
        {
            bestEffort = false;

            if (!GplStringCodec.TryDecodeSpan(
                    ctx.Data,
                    subTypeOffset,
                    ctx.InstructionOffset,
                    ctx.Opcode,
                    out var span,
                    out _))
            {
                // Consume the sub-type marker so a malformed payload
                // cannot loop forever. The caller marks the chunk
                // unaligned and stops.
                bestEffort = true;
                return 1;
            }

            ctx.Strings.Add(span!);
            return 1 + span!.ContentLength;
        }
    }
}
