/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/charsets/utf.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 1fec0cd00f3069d21ab49969ad0491968f4e3bb4f456a3147eb541af6adccac1
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class UtfCharsetUtils
{
    private class Utf7DecoderState
    {
        public List<ushort> utf16_bytes = new(10);
        public byte? pending_byte = null;
        public uint b64_bytes = 0;
    }

    private static void add_utf16_bytes(Utf7DecoderState state, int n_bytes)
    {
        byte[] leBytes = new byte[4]
        {
            (byte)(state.b64_bytes & 0xff),
            (byte)((state.b64_bytes >> 8) & 0xff),
            (byte)((state.b64_bytes >> 16) & 0xff),
            (byte)((state.b64_bytes >> 24) & 0xff)
        };

        for (int i = 0; i < n_bytes; i++)
        {
            byte b = leBytes[i];
            if (state.pending_byte.HasValue)
            {
                ushort u16 = (ushort)((state.pending_byte.Value << 8) | b);
                state.utf16_bytes.Add(u16);
                state.pending_byte = null;
            }
            else
            {
                state.pending_byte = b;
            }
        }
    }

    // Rust: decoder_utf7
    public static string decoder_utf7(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder(bytes.Length);
        byte byte_count = 0;
        bool in_b64 = false;
        var state = new Utf7DecoderState();

        foreach (byte ch in bytes)
        {
            if (in_b64)
            {
                uint val = Base64Utils.BASE64_MAP[byte_count][ch];
                if (val < 0x01ffffff)
                {
                    byte_count = (byte)((byte_count + 1) & 3);
                    if (byte_count == 1)
                    {
                        state.b64_bytes = val;
                    }
                    else
                    {
                        state.b64_bytes |= val;
                        if (byte_count == 0)
                        {
                            add_utf16_bytes(state, 3);
                        }
                    }
                }
                else
                {
                    switch (byte_count)
                    {
                        case 1:
                        case 2:
                            add_utf16_bytes(state, 1);
                            break;
                        case 3:
                            add_utf16_bytes(state, 2);
                            break;
                    }

                    if (state.utf16_bytes.Count > 0)
                    {
                        result.Append(DecodeUtf16(state.utf16_bytes));
                        state.utf16_bytes.Clear();
                    }
                    else if (byte_count > 0 || state.pending_byte.HasValue)
                    {
                        result.Append('�');
                    }
                    else
                    {
                        result.Append('+');
                        result.Append((char)ch);
                    }

                    state.pending_byte = null;
                    byte_count = 0;
                    in_b64 = false;
                }
            }
            else if (ch == (byte)'+')
            {
                in_b64 = true;
            }
            else
            {
                result.Append((char)ch);
            }
        }

        return result.ToString();
    }

    private static string DecodeUtf16(List<ushort> u16List)
    {
        var sb = new StringBuilder(u16List.Count);
        for (int i = 0; i < u16List.Count; i++)
        {
            ushort u = u16List[i];
            if (char.IsSurrogate((char)u))
            {
                if (char.IsHighSurrogate((char)u) && i + 1 < u16List.Count && char.IsLowSurrogate((char)u16List[i + 1]))
                {
                    sb.Append((char)u);
                    sb.Append((char)u16List[++i]);
                }
                else
                {
                    sb.Append('�');
                }
            }
            else
            {
                sb.Append((char)u);
            }
        }
        return sb.ToString();
    }

    // Rust: decoder_utf16_le
    public static string decoder_utf16_le(ReadOnlySpan<byte> bytes)
    {
        return DecodeUtf16(bytes, System.Text.Encoding.Unicode);
    }

    // Rust: decoder_utf16_be
    public static string decoder_utf16_be(ReadOnlySpan<byte> bytes)
    {
        return DecodeUtf16(bytes, System.Text.Encoding.BigEndianUnicode);
    }

    // Rust: decoder_utf16
    public static string decoder_utf16(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xfe && bytes[1] == 0xff)
            {
                return DecodeUtf16(bytes.Slice(2), System.Text.Encoding.BigEndianUnicode);
            }
            if (bytes[0] == 0xff && bytes[1] == 0xfe)
            {
                return DecodeUtf16(bytes.Slice(2), System.Text.Encoding.Unicode);
            }
            return DecodeUtf16(bytes, System.Text.Encoding.Unicode);
        }
        return "";
    }

    private static string DecodeUtf16(ReadOnlySpan<byte> bytes, System.Text.Encoding encoding)
    {
        return encoding.GetString(bytes[..(bytes.Length & ~1)]);
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class utf_tests
{
    [TestMethod]
    public void decode_utf7()
    {
        var inputs = new (string input, string expected)[]
        {
            ("Hello, World+ACE-", "Hello, World!"),
            ("Hi Mom -+Jjo--!", "Hi Mom -\u263a-!"),
            ("+ZeVnLIqe-", "\u65e5\u672c\u8a9e"),
            ("Item 3 is +AKM-1.", "Item 3 is \u00a31."),
            ("Plus minus +- -+ +--", "Plus minus +- -+ +--"),
            ("+APw-ber ihre mi+AN8-liche Lage+ADs- +ACI-wir", "\u00fcber ihre mi\u00dfliche Lage; \"wir"),
            ("+ACI-The sayings of Confucius,+ACI- James R. Ware, trans.  +U/BTFw-:\n+ZYeB9FH6ckh5Pg-, 1980.\n+Vttm+E6UfZM-, +W4tRQ066bOg-, +UxdOrA-:  +Ti1XC2b4Xpc-, 1990.",
             "\"The sayings of Confucius,\" James R. Ware, trans.  \u53f0\u5317:\n\u6587\u81f4\u51fa\u7248\u793e, 1980.\n\u56db\u66f8\u4e94\u7d93, \u5b8b\u5143\u4eba\u6ce8, \u5317\u4eac:  \u4e2d\u570b\u66f8\u5e97, 1990.")
        };

        foreach (var (input, expected) in inputs)
        {
            var res = UtfCharsetUtils.decoder_utf7(System.Text.Encoding.UTF8.GetBytes(input));
            Assert.AreEqual(expected, res, $"Failed for {input}");
        }
    }
}
#endif
