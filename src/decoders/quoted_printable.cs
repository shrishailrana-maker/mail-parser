/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/quoted_printable.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: c7712bb7930558fdb33f3d2fb07c5e6633117c1752cecf6d2f84e231cca2e9d2
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class QuotedPrintableUtils
{
    public static readonly sbyte[] HEX_MAP = new sbyte[256] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1, -1, -1, -1, -1, -1, 10, 11, 12, 13, 14, 15, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 10, 11, 12, 13, 14, 15, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

    // Rust: quoted_printable_decode
    public static byte[] quoted_printable_decode(ReadOnlySpan<byte> bytes)
    {
        var buf = new List<byte>(bytes.Length);
        int state = 0; // 0 = None, 1 = Eq, 2 = Hex1
        int hex1 = 0;
        int ws_count = 0;
        byte[] crlf = new byte[] { (byte)'\n' };

        foreach (byte ch in bytes)
        {
            switch (ch)
            {
                case (byte)'=':
                    if (state == 0)
                    {
                        state = 1;
                    }
                    else
                    {
                        buf.Add((byte)'=');
                        state = 1;
                    }
                    break;
                case (byte)'\n':
                    if (state == 1)
                    {
                        state = 0;
                    }
                    else
                    {
                        if (ws_count > 0 && buf.Count >= ws_count)
                        {
                            buf.RemoveRange(buf.Count - ws_count, ws_count);
                        }
                        buf.AddRange(crlf);
                    }
                    ws_count = 0;
                    break;
                case (byte)'\r':
                    crlf = new byte[] { (byte)'\r', (byte)'\n' };
                    break;
                default:
                    switch (state)
                    {
                        case 0:
                            if (char.IsWhiteSpace((char)ch))
                            {
                                ws_count++;
                            }
                            else
                            {
                                ws_count = 0;
                            }
                            buf.Add(ch);
                            break;
                        case 1:
                            hex1 = QuotedPrintableUtils.HEX_MAP[ch];
                            if (hex1 != -1)
                            {
                                state = 2;
                            }
                            else if (!char.IsWhiteSpace((char)ch))
                            {
                                state = 0;
                                buf.Add((byte)'=');
                                buf.Add(ch);
                                ws_count = 0;
                            }
                            break;
                        case 2:
                            int hex2 = QuotedPrintableUtils.HEX_MAP[ch];
                            state = 0;
                            if (hex2 != -1)
                            {
                                buf.Add((byte)((hex1 << 4) | hex2));
                                ws_count = 0;
                            }
                            else
                            {
                                buf.Add((byte)'=');
                                buf.Add(ch);
                                ws_count = 0;
                            }
                            break;
                    }
                    break;
            }
        }

        return buf.ToArray();
    }
}

public partial class MessageStream
{
    private enum QuotedPrintableState
    {
        None,
        Eq,
        Hex1,
    }

    // Rust: MessageStream::decode_quoted_printable_mime
    public (int offset_end, byte[] bytes) decode_quoted_printable_mime(ReadOnlySpan<byte> boundary)
    {
        var buf = new List<byte>(128);
        var state = QuotedPrintableState.None;
        int hex1 = 0;
        byte last_ch = 0;
        byte before_last_ch = 0;
        int ws_count = 0;
        int end_pos = offset();
        byte[] crlf = new byte[] { (byte)'\n' };

        checkpoint();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'=':
                    if (state == QuotedPrintableState.None)
                    {
                        state = QuotedPrintableState.Eq;
                    }
                    else
                    {
                        restore();
                        return (int.MaxValue, Array.Empty<byte>());
                    }
                    break;
                case (byte)'\n':
                    end_pos = last_ch == (byte)'\r' ? offset() - 2 : offset() - 1;
                    if (state == QuotedPrintableState.Eq)
                    {
                        state = QuotedPrintableState.None;
                    }
                    else
                    {
                        if (ws_count > 0 && buf.Count >= ws_count)
                        {
                            buf.RemoveRange(buf.Count - ws_count, ws_count);
                        }
                        buf.AddRange(crlf);
                    }
                    ws_count = 0;
                    break;
                case (byte)'\r':
                    crlf = new byte[] { (byte)'\r', (byte)'\n' };
                    break;
                case (byte)'-' when !boundary.IsEmpty && last_ch == (byte)'-' && try_skip(boundary):
                    if (before_last_ch == (byte)'\n')
                    {
                        int trLen = crlf.Length + 1;
                        if (buf.Count >= trLen) buf.RemoveRange(buf.Count - trLen, trLen);
                    }
                    else
                    {
                        if (buf.Count > 0) buf.RemoveAt(buf.Count - 1);
                        end_pos = offset() - boundary.Length - 2;
                    }
                    return (end_pos, buf.ToArray());
                default:
                    switch (state)
                    {
                        case QuotedPrintableState.None:
                            if (char.IsWhiteSpace((char)ch))
                            {
                                ws_count++;
                            }
                            else
                            {
                                ws_count = 0;
                            }
                            buf.Add(ch);
                            break;
                        case QuotedPrintableState.Eq:
                            hex1 = QuotedPrintableUtils.HEX_MAP[ch];
                            if (hex1 != -1)
                            {
                                state = QuotedPrintableState.Hex1;
                            }
                            else if (!char.IsWhiteSpace((char)ch))
                            {
                                state = QuotedPrintableState.None;
                                buf.Add((byte)'=');
                                buf.Add(ch);
                                ws_count = 0;
                            }
                            break;
                        case QuotedPrintableState.Hex1:
                            int hex2 = QuotedPrintableUtils.HEX_MAP[ch];
                            state = QuotedPrintableState.None;
                            if (hex2 != -1)
                            {
                                buf.Add((byte)((hex1 << 4) | hex2));
                                ws_count = 0;
                            }
                            else
                            {
                                buf.Add((byte)'=');
                                buf.Add(last_ch);
                                buf.Add(ch);
                                ws_count = 0;
                            }
                            break;
                    }
                    break;
            }

            before_last_ch = last_ch;
            last_ch = ch;
        }

        if (boundary.IsEmpty)
        {
            return (offset(), buf.ToArray());
        }
        else
        {
            restore();
            return (int.MaxValue, buf.ToArray());
        }
    }

        // Rust: MessageStream::decode_quoted_printable_word
    public byte[]? decode_quoted_printable_word()
    {
        var buf = new List<byte>(64);
        int state = 0; // 0 = None, 1 = Eq, 2 = Hex1
        int hex1 = 0;

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'=':
                    if (state == 0)
                    {
                        state = 1;
                    }
                    else
                    {
                        return null;
                    }
                    break;
                case (byte)'?':
                    if (peek() == (byte)'=')
                    {
                        next();
                        return buf.ToArray();
                    }
                    else
                    {
                        buf.Add((byte)'?');
                    }
                    break;
                case (byte)'\n':
                    if (peek() == (byte)' ' || peek() == (byte)'\t')
                    {
                        while (true)
                        {
                            next();
                            if (!peek_next_is_space())
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        return null;
                    }
                    break;
                case (byte)'_':
                    buf.Add((byte)' ');
                    break;
                case (byte)'\r':
                    break;
                default:
                    switch (state)
                    {
                        case 0:
                            buf.Add(ch);
                            break;
                        case 1:
                            hex1 = QuotedPrintableUtils.HEX_MAP[ch];
                            if (hex1 != -1)
                            {
                                state = 2;
                            }
                            else
                            {
                                return null;
                            }
                            break;
                        case 2:
                            int hex2 = QuotedPrintableUtils.HEX_MAP[ch];
                            state = 0;
                            if (hex2 != -1)
                            {
                                buf.Add((byte)((hex1 << 4) | hex2));
                            }
                            else
                            {
                                return null;
                            }
                            break;
                    }
                    break;
            }
        }

        return null;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class quoted_printable_tests
{
    [TestMethod]
    public void decode_quoted_printable()
    {
        var cases = new (string encoded, string expected)[]
        {
            (
                "J'interdis aux marchands de vanter trop leurs marchandises. " +
                "Car ils se font=\nvite p=C3=A9dagogues et t'enseignent comme but ce " +
                "qui n'est par essence qu=\n'un moyen, et te trompant ainsi sur la route " +
                "=C3=A0 suivre les voil=C3=\n=A0 bient=C3=B4t qui te d=C3=A9gradent, car " +
                "si leur musique est vulgaire il=\ns te fabriquent pour te la vendre une " +
                "=C3=A2me vulgaire.\n=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry, " +
                "Citadelle (1948)",
                "J'interdis aux marchands de vanter trop leurs marchandises. " +
                "Car ils se fontvite pédagogues et t'enseignent comme but ce qui " +
                "n'est par essence qu'un moyen, et te trompant ainsi sur la route " +
                "à suivre les voilà bientôt qui te dégradent, car si leur musique " +
                "est vulgaire ils te fabriquent pour te la vendre une âme vulgaire.\n" +
                "— Antoine de Saint-Exupéry, Citadelle (1948)"
            ),
            (
                "=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry",
                "— Antoine de Saint-Exupéry"
            ),
            (
                "Die Hasen klagten einst uber ihre Lage; \"wir " +
                "leben\", sprach ein=\r\n Redner, \"in steter Furcht vor Menschen" +
                " und Tieren, eine Beute der Hunde,=\r\n der\n",
                "Die Hasen klagten einst uber ihre Lage; \"wir leben\", " +
                "sprach ein Redner, \"in steter Furcht vor Menschen und " +
                "Tieren, eine Beute der Hunde, der\r\n"
            ),
            (
                "hello  \r\nbar=\r\n\r\nfoo\t=\r\nbar\r\nfoo\t \t= \r\n=62\r\nfoo = " +
                "\t\r\nbar\r\nfoo =\r\n=62\r\nfoo  \r\nbar=\r\n\r\nfoo_bar\r\n",
                "hello\r\nbar\r\nfoo\tbar\r\nfoo\t \tb\r\nfoo bar\r\nfoo b\r\nfoo\r\nbar\r\nfoo_bar\r\n"
            ),
            ("\n\n", "\n\n"),
        };

        foreach (var (encoded_str, expected_result) in cases)
        {
            var res = QuotedPrintableUtils.quoted_printable_decode(System.Text.Encoding.UTF8.GetBytes(encoded_str)) ?? Array.Empty<byte>();
            Assert.AreEqual(expected_result, System.Text.Encoding.UTF8.GetString(res), $"Failed for {encoded_str}");
        }
    }

    [TestMethod]
    public void decode_quoted_printable_mime()
    {
        var cases = new (string encoded, string expected)[]
        {
            (
                "<meta content=\"text/html; charset=utf-8\"> h=C3=B6\n--boundary",
                "<meta content=\"text/html; charset=utf-8\"> hö"
            ),
            ("first=AZ second\n--boundary", "first=AZ second"),
            (
                "=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry\n--boundary",
                "— Antoine de Saint-Exupéry"
            ),
            (
                "=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry\n--\n--boundary",
                "— Antoine de Saint-Exupéry\n--"
            ),
            (
                "=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry=\n--\n--boundary",
                "— Antoine de Saint-Exupéry--"
            ),
            (
                "J'interdis aux marchands de vanter trop leurs marchandises. " +
                "Car ils se font=\nvite p=C3=A9dagogues et t'enseignent comme but ce " +
                "qui n'est par essence qu=\n'un moyen, et te trompant ainsi sur la route " +
                "=C3=A0 suivre les voil=C3=\n=A0 bient=C3=B4t qui te d=C3=A9gradent, car " +
                "si leur musique est vulgaire il=\ns te fabriquent pour te la vendre une " +
                "=C3=A2me vulgaire.\n=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry, " +
                "Citadelle (1948)\r\n--boundary--",
                "J'interdis aux marchands de vanter trop leurs marchandises. " +
                "Car ils se fontvite pédagogues et t'enseignent comme but ce qui " +
                "n'est par essence qu'un moyen, et te trompant ainsi sur la route " +
                "à suivre les voilà bientôt qui te dégradent, car si leur musique " +
                "est vulgaire ils te fabriquent pour te la vendre une âme vulgaire.\n" +
                "— Antoine de Saint-Exupéry, Citadelle (1948)"
            ),
            (
                "=E2=80=94=E2=80=89Antoine de Saint-Exup=C3=A9ry\n--\n--boundary",
                "— Antoine de Saint-Exupéry\n--"
            ),
            (
                "Die Hasen klagten einst uber ihre Lage; \"wir " +
                "leben\", sprach ein=\r\n Redner, \"in steter Furcht vor Menschen" +
                " und Tieren, eine Beute der Hunde,=\r\n der\r\n\r\n--boundary \n",
                "Die Hasen klagten einst uber ihre Lage; \"wir leben\", " +
                "sprach ein Redner, \"in steter Furcht vor Menschen und " +
                "Tieren, eine Beute der Hunde, der\r\n"
            ),
            (
                "hello  \r\nbar=\r\n\r\nfoo\t=\r\nbar\r\nfoo\t \t= \r\n=62\r\nfoo = " +
                "\t\r\nbar\r\nfoo =\r\n=62\r\nfoo  \r\nbar=\r\n\r\nfoo_bar\r\n\r\n--boundary",
                "hello\r\nbar\r\nfoo\tbar\r\nfoo\t \tb\r\nfoo bar\r\nfoo b\r\nfoo\r\nbar\r\nfoo_bar\r\n"
            ),
        };

        foreach (var (encoded_str, expected_result) in cases)
        {
            var s = new MessageStream(System.Text.Encoding.UTF8.GetBytes(encoded_str));
            var (bytes_read, result) = s.decode_quoted_printable_mime(System.Text.Encoding.UTF8.GetBytes("boundary"));
            Assert.AreNotEqual(int.MaxValue, bytes_read);
            Assert.AreEqual(expected_result, System.Text.Encoding.UTF8.GetString(result), $"Failed for {encoded_str}");
        }
    }

    [TestMethod]
    public void decode_quoted_printable_word()
    {
        var cases = new (string encoded, string expected)[]
        {
            ("this=20is=20some=20text?=", "this is some text"),
            ("this=20is=20\n  some=20text?=", "this is some text"),
            ("this is some text?=", "this is some text"),
            ("Keith_Moore?=", "Keith Moore"),
            ("=2=123?=", ""),
            ("= 20?=", ""),
            ("=====?=", ""),
            ("=20=20=XX?=", ""),
            ("=AX?=", ""),
            ("=\n=\n==?=", ""),
            ("=\r=1z?=", ""),
            ("=|?=", ""),
            ("????????=", "???????"),
            ("\n\n", ""),
        };

        foreach (var (encoded_str, expected_result) in cases)
        {
            var s = new MessageStream(System.Text.Encoding.UTF8.GetBytes(encoded_str));
            var res = s.decode_quoted_printable_word() ?? Array.Empty<byte>();
            Assert.AreEqual(expected_result, System.Text.Encoding.UTF8.GetString(res), $"Failed for {encoded_str}");
        }
    }
}
#endif
