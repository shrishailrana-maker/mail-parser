/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/charsets/mod.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 266ad022634e5160c9e97b46e3d51a44285e570c6d58f2f3748312a25bdcd08f
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class CharsetDecoderUtils
{
    // Rust: decode_charset
    public static string decode_charset(ReadOnlySpan<byte> charset, ReadOnlySpan<byte> bytes)
    {
        var decoder = CharsetMapUtils.charset_decoder(charset);
        if (decoder != null)
        {
            return decoder(bytes);
        }
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class mod_tests
{
    [TestMethod]
    public void decode_charset()
    {
        var inputs = new (string charset, byte[] bytes, string expected)[]
        {
            ("iso-8859-1", new byte[] { 0xe1, 0xe9, 0xed, 0xf3, 0xfa }, "áéíóú"),
            ("iso-8859-1", new byte[] { 0x80, (byte)'5', (byte)'.', (byte)'4', (byte)'b', (byte)'n' }, "€5.4bn"),
            ("latin1", new byte[] { 0x80, (byte)'5', (byte)'.', (byte)'4', (byte)'b', (byte)'n' }, "€5.4bn"),
            ("iso88591", new byte[] { 0x80, (byte)'5', (byte)'.', (byte)'4', (byte)'b', (byte)'n' }, "€5.4bn"),
            ("us-ascii", new byte[] { 0x80, (byte)'5', (byte)'.', (byte)'4', (byte)'b', (byte)'n' }, "€5.4bn"),
            ("iso8859-5", new byte[] { 0xbf, 0xe0, 0xd8, 0xd2, 0xd5, 0xe2, (byte)',', (byte)' ', 0xdc, 0xd8, 0xe0 }, "Привет, мир"),
            ("cp1252", new byte[] { 0xa1, (byte)'E', (byte)'l', (byte)' ', 0xf1, (byte)'a', (byte)'n', (byte)'d', 0xfa, (byte)' ', (byte)'c', (byte)'o', (byte)'m', (byte)'i', 0xf3, (byte)' ', 0xf1, (byte)'o', (byte)'q', (byte)'u', (byte)'i', (byte)'s', (byte)'!' }, "¡El ñandú comió ñoquis!"),
            ("iso-8859-5", new byte[] { 0xbf, 0xe0, 0xd8, 0xd2, 0xd5, 0xe2, (byte)',', (byte)' ', 0xdc, 0xd8, 0xe0 }, "Привет, мир"),
            ("iso-8859-6", new byte[] { 0xe5, 0xd1, 0xcd, 0xc8, 0xc7, (byte)' ', 0xc8, 0xc7, 0xe4, 0xd9, 0xc7, 0xe4, 0xe5 }, "مرحبا بالعالم"),
            ("iso-8859-7", new byte[] { 0xc3, 0xe5, 0xe9, 0xdc, (byte)' ', 0xf3, 0xef, 0xf5, (byte)' ', 0xca, 0xfc, 0xf3, 0xec, 0xe5 }, "Γειά σου Κόσμε"),
            ("iso-8859-8", new byte[] { 0xf9, 0xec, 0xe5, 0xed, (byte)' ', 0xf2, 0xe5, 0xec, 0xed }, "שלום עולם"),
            ("iso-8859-11", new byte[] { 0xc3, 0xcb, 0xd1, 0xca, 0xca, 0xd3, 0xcb, 0xc3, 0xd1, 0xba, 0xcd, 0xd1, 0xa1, 0xa2, 0xc3, 0xd0, 0xe4, 0xb7, 0xc2, 0xb7, 0xd5, 0xe8, 0xe3, 0xaa, 0xe9, 0xa1, 0xd1, 0xba, 0xa4, 0xcd, 0xc1, 0xbe, 0xd4, 0xc7, 0xe0, 0xb5, 0xcd, 0xc3, 0xec }, "รหัสสำหรับอักขระไทยที่ใช้กับคอมพิวเตอร์"),
            ("windows-1250", new byte[] { (byte)'Z', (byte)'e', (byte)'l', (byte)'o', (byte)' ', (byte)'r', (byte)'a', (byte)'d', (byte)'a', (byte)' ', (byte)'g', (byte)'r', (byte)'e', (byte)'m', (byte)' ', (byte)'v', (byte)' ', (byte)'s', (byte)'l', (byte)'a', 0x9a, 0xe8, (byte)'i', 0xe8, (byte)'a', (byte)'r', (byte)'n', (byte)'o' }, "Zelo rada grem v slaščičarno"),
            ("windows-1251", new byte[] { 0xcf, 0xf0, 0xe8, 0xe2, 0xe5, 0xf2, (byte)',', (byte)' ', 0xec, 0xe8, 0xf0 }, "Привет, мир"),
            ("windows-1252", new byte[] { 0xa1, (byte)'E', (byte)'l', (byte)' ', 0xf1, (byte)'a', (byte)'n', (byte)'d', 0xfa, (byte)' ', (byte)'c', (byte)'o', (byte)'m', (byte)'i', 0xf3, (byte)' ', 0xf1, (byte)'o', (byte)'q', (byte)'u', (byte)'i', (byte)'s', (byte)'!' }, "¡El ñandú comió ñoquis!"),
            ("windows-1253", new byte[] { 0xca, 0xf9, 0xe4, 0xe9, 0xea, 0xef, 0xdf, (byte)' ', 0xd3, 0xf9, 0xea, 0xf1, 0xdc, 0xf4, 0xe7, 0xf2, (byte)' ', 0xf3, 0xf4, 0xef, (byte)' ', (byte)'R', (byte)'u', (byte)'s', (byte)'t' }, "Κωδικοί Σωκράτης στο Rust"),
            ("windows-1254", new byte[] { (byte)'K', (byte)'e', (byte)'b', (byte)'a', (byte)'b', 0xfd, (byte)'m', 0xfd, (byte)' ', (byte)'b', (byte)'a', (byte)'h', (byte)'a', (byte)'r', (byte)'a', (byte)'t', (byte)'l', 0xfd, (byte)' ', (byte)'y', (byte)'a', (byte)'p', (byte)'m', (byte)'a' }, "Kebabımı baharatlı yapma"),
            ("windows-1255", new byte[] { 0xf9, 0xec, 0xe5, 0xed, (byte)' ', 0xf2, 0xe5, 0xec, 0xed }, "שלום עולם"),
            ("windows-1256", new byte[] { 0xe3, 0xd1, 0xcd, 0xc8, 0xc7, (byte)' ', 0xc8, 0xc7, 0xe1, 0xda, 0xc7, 0xe1, 0xe3 }, "مرحبا بالعالم"),
            ("windows-1257", new byte[] { (byte)'M', (byte)'u', (byte)' ', (byte)'h', 0xf5, (byte)'l', (byte)'j', (byte)'u', (byte)'k', (byte)' ', (byte)'o', (byte)'n', (byte)' ', (byte)'a', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'j', (byte)'a', (byte)'i', (byte)'d', (byte)' ', (byte)'t', 0xe4, (byte)'i', (byte)'s' }, "Mu hõljuk on angerjaid täis"),
            ("windows-1258", new byte[] { (byte)'X', (byte)'i', (byte)'n', (byte)' ', (byte)'c', (byte)'h', 0xe0, (byte)'o' }, "Xin chào"),
            ("macintosh", new byte[] { 0x87, 0x8e, 0x92, 0x97, 0x9c }, "áéíóú"),
            ("ibm850", new byte[] { 0x9b, 0x9c, 0x9d, 0x9e }, "ø£Ø×"),
            ("koi8-r", new byte[] { 0xf0, 0xd2, 0xc9, 0xd7, 0xc5, 0xd4, (byte)',', (byte)' ', 0xcd, 0xc9, 0xd2 }, "Привет, мир"),
            ("koi8-u", new byte[] { 0xf0, 0xd2, 0xc9, 0xd7, 0xa6, 0xd4, (byte)' ', 0xf3, 0xd7, 0xa6, 0xd4 }, "Привіт Світ"),
            ("utf-7", System.Text.Encoding.ASCII.GetBytes("+ZYeB9FH6ckh5Pg-, 1980."), "文致出版社, 1980."),
            ("utf-16le", new byte[] { 0xcf, 0x30, 0xed, 0x30, 0xfc, 0x30, 0xfb, 0x30, 0xef, 0x30, 0xfc, 0x30, 0xeb, 0x30, 0xc9, 0x30 }, "ハロー・ワールド"),
            ("utf-16be", new byte[] { 0x30, 0xcf, 0x30, 0xed, 0x30, 0xfc, 0x30, 0xfb, 0x30, 0xef, 0x30, 0xfc, 0x30, 0xeb, 0x30, 0xc9 }, "ハロー・ワールド"),
            ("utf-16", new byte[] { 0xff, 0xfe, 0xe1, 0x00, 0xe9, 0x00, 0xed, 0x00, 0xf3, 0x00, 0xfa, 0x00 }, "áéíóú"),
            ("utf-16", new byte[] { 0xfe, 0xff, 0x00, 0xe1, 0x00, 0xe9, 0x00, 0xed, 0x00, 0xf3, 0x00, 0xfa }, "áéíóú"),
            ("shift_jis", new byte[] { 0x83, 0x6e, 0x83, 0x8d, 0x81, 0x5b, 0x81, 0x45, 0x83, 0x8f, 0x81, 0x5b, 0x83, 0x8b, 0x83, 0x68 }, "ハロー・ワールド"),
            ("big5", new byte[] { 0xa7, 0x41, 0xa6, 0x6e, 0xa1, 0x41, 0xa5, 0x40, 0xac, 0xc9 }, "你好，世界"),
            ("euc-jp", new byte[] { 0xa5, 0xcf, 0xa5, 0xed, 0xa1, 0xbc, 0xa1, 0xa6, 0xa5, 0xef, 0xa1, 0xbc, 0xa5, 0xeb, 0xa5, 0xc9 }, "ハロー・ワールド"),
            ("euc-kr", new byte[] { 0xbe, 0xc8, 0xb3, 0xe7, 0xc7, 0xcf, 0xbc, 0xbc, 0xbf, 0xe4, (byte)' ', 0xbc, 0xbc, 0xb0, 0xe8 }, "안녕하세요 세계"),
            ("iso-2022-jp", new byte[] { 0x1b, (byte)'$', (byte)'B', 0x25, 0x4f, 0x25, 0x6d, 0x21, 0x3c, 0x21, 0x26, 0x25, 0x6f, 0x21, 0x3c, 0x25, 0x6b, 0x25, 0x49, 0x1b, (byte)'(', (byte)'B' }, "ハロー・ワールド"),
            ("gbk", new byte[] { 0xc4, 0xe3, 0xba, 0xc3, 0xa3, 0xac, 0xca, 0xc0, 0xbd, 0xe7 }, "你好，世界"),
            ("gb18030", new byte[] { 0xc4, 0xe3, 0xba, 0xc3, 0xa3, 0xac, 0xca, 0xc0, 0xbd, 0xe7 }, "你好，世界"),
            ("x-mac-cyrillic", new byte[] { 0x8f, 0xf0, 0xe8, 0xe2, 0xe5, 0xf2 }, "Привет"),
            ("x-user-defined", new byte[] { 0x80, 0xff }, "\uf780\uf7ff"),
            ("iso-2022-kr", new byte[] { 0x1b, (byte)'$', (byte)')', (byte)'C', (byte)'a', (byte)'b', (byte)'c', (byte)'d' }, "\ufffd"),
            ("hz-gb-2312", new byte[] { (byte)'~', (byte)'{', (byte)'.', (byte)'.', (byte)'.', (byte)'~', (byte)'}' }, "\ufffd"),
        };

        Assert.AreEqual(41, inputs.Length);
        foreach (var (charset, bytes, expected) in inputs)
        {
            var decoder = CharsetMapUtils.charset_decoder(System.Text.Encoding.ASCII.GetBytes(charset));
            Assert.IsNotNull(decoder, $"Failed to find decoder for {charset}");
            Assert.AreEqual(expected, decoder(bytes), $"Failed for {charset}");
        }
    }
}
#endif
