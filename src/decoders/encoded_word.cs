/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/encoded_word.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 558e64ab774a97a9540ee45ceb3701e3fd0afe77b75512322fa7974d94ccea99
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public partial class MessageStream
{
    private enum Rfc2047State
    {
        Init,
        Charset,
        Encoding,
        Data,
    }

    // Rust: MessageStream::decode_rfc2047
    public string? decode_rfc2047()
    {
        var state = Rfc2047State.Init;
        int charset_start = 0;
        int charset_end = 0;
        Func<byte[]?>? decode_fnc = null;

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (state)
            {
                case Rfc2047State.Init:
                    if (ch != (byte)'?') return null;
                    state = Rfc2047State.Charset;
                    charset_start = offset();
                    charset_end = offset();
                    break;
                case Rfc2047State.Charset:
                    if (ch == (byte)'?')
                    {
                        if (charset_end == charset_start)
                        {
                            charset_end = offset() - 1;
                        }
                        if ((charset_end - charset_start) < 2)
                        {
                            return null;
                        }
                        state = Rfc2047State.Encoding;
                    }
                    else if (ch == (byte)'*' && charset_end == charset_start)
                    {
                        charset_end = offset() - 1;
                    }
                    else if (ch == (byte)'\n')
                    {
                        return null;
                    }
                    break;
                case Rfc2047State.Encoding:
                    if (ch == (byte)'q' || ch == (byte)'Q')
                    {
                        decode_fnc = decode_quoted_printable_word;
                    }
                    else if (ch == (byte)'b' || ch == (byte)'B')
                    {
                        decode_fnc = decode_base64_word;
                    }
                    else
                    {
                        return null;
                    }
                    state = Rfc2047State.Data;
                    break;
                case Rfc2047State.Data:
                    if (ch != (byte)'?')
                    {
                        return null;
                    }
                    else
                    {
                        goto data_done;
                    }
            }
        }

    data_done:

        if (decode_fnc != null)
        {
            var bytes = decode_fnc();
            if (bytes != null)
            {
                var charsetBytes = bytes_span(charset_start, charset_end);
                var decoder = CharsetMapUtils.charset_decoder(charsetBytes);
                if (decoder != null)
                {
                    return decoder(bytes);
                }
                else
                {
                    try
                    {
                        return System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        return System.Text.Encoding.Latin1.GetString(bytes);
                    }
                }
            }
        }

        return null;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class encoded_word_tests
{
    [TestMethod]
    public void decode_rfc2047()
    {
        var tests = new (string input, string expected)[]
        {
            ("?iso-8859-1?q?this is some text?=", "this is some text"),
            ("?US-ASCII?Q?Keith_Moore?=", "Keith Moore"),
            ("?iso_8859-1:1987?Q?Keld_J=F8rn_Simonsen?=", "Keld Jørn Simonsen"),
            ("?ISO-8859-1?B?SWYgeW91IGNhbiByZWFkIHRoaXMgeW8=?=", "If you can read this yo"),
            ("?ISO-8859-2?B?dSB1bmRlcnN0YW5kIHRoZSBleGFtcGxlLg==?=", "u understand the example."),
            ("?ISO-8859-1?Q?Olle_J=E4rnefors?=", "Olle Järnefors"),
            ("?iso-8859-1?Q?=805.4bn?=", "€5.4bn"),
            ("?ISO-8859-1?Q?Patrik_F=E4ltstr=F6m?=", "Patrik Fältström"),
            ("?ISO-8859-1*?Q?a?=", "a"),
            ("?ISO-8859-1**?Q?a_b?=", "a b"),
            ("?utf-8?b?VGjDrXMgw61zIHbDoWzDrWQgw5pURjg=?=", "Thís ís válíd ÚTF8"),
            ("?utf-8*unknown?q?Th=C3=ADs_=C3=ADs_v=C3=A1l=C3=ADd_=C3=9ATF8?=", "Thís ís válíd ÚTF8"),
            ("?Iso-8859-6?Q?=E5=D1=CD=C8=C7 =C8=C7=E4=D9=C7=E4=E5?=", "مرحبا بالعالم"),
            ("?Iso-8859-6*arabic?b?5dHNyMcgyMfk2cfk5Q==?=", "مرحبا بالعالم"),
            ("?shift_jis?B?g26DjYFbgUWDj4Fbg4uDaA==?=", "ハロー・ワールド"),
            ("?iso-2022-jp?q?=1B$B%O%m!<!&%o!<%k%I=1B(B?=", "ハロー・ワールド"),
        };

        Assert.AreEqual(16, tests.Length);
        foreach (var (input, expected) in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(input));
            var parsed = stream.decode_rfc2047();
            Assert.IsNotNull(parsed, $"Failed to decode '{input}'");
            Assert.AreEqual(expected, parsed, $"Failed for '{input}'");
        }
    }
}
#endif
