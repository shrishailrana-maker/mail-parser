/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: fuzz/fuzz_targets/mail_parser.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: a6823ea5bfba974ce1576c592ff45b15c5cbccb9f902015e1db4e652e9754a7c
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;
using Stalwart.MailParser.Port;

namespace Stalwart.MailParser.Port.Fuzz;

public static class MailParserFuzzTarget
{
    public static void Main(string[] args) => Run(Array.Empty<byte>());

    private static readonly byte[] RFC822_ALPHABET = System.Text.Encoding.ASCII.GetBytes("0123456789abcdefghijklm:=- \r\n");

    public static void Run(byte[] data)
    {
        var versions = new byte[][]
        {
            data,
            into_alphabet(data, RFC822_ALPHABET)
        };

        foreach (var d in versions)
        {
            new MessageStream(d).parse_date();
            new MessageStream(d).parse_address();
            new MessageStream(d).parse_id();
            new MessageStream(d).parse_comma_separared();
            new MessageStream(d).parse_and_ignore();
            new MessageStream(d).parse_raw();
            new MessageStream(d).parse_unstructured();
            new MessageStream(d).parse_content_type();
            new MessageStream(d).parse_headers(new MessageParser(), new List<Header>());
            new MessageStream(d).parse_header_name();
            new MessageStream(d).decode_rfc2047();

            new MessageStream(d).seek_next_part(new byte[] { (byte)'\n' });
            new MessageStream(d).mime_part(new byte[] { (byte)'\n' });
            new MessageStream(d).seek_part_end(new byte[] { (byte)'\n' });
            new MessageStream(d).skip_crlf();
            new MessageStream(d).is_multipart_end();

            Base64Utils.base64_decode(d);
            new MessageStream(d).decode_base64_word();
            new MessageStream(d).decode_base64_mime(new byte[] { (byte)'\n' });
            QuotedPrintableUtils.quoted_printable_decode(d);
            new MessageStream(d).decode_quoted_printable_word();
            new MessageStream(d).decode_quoted_printable_mime(new byte[] { (byte)'\n' });

            var sb = new StringBuilder(d.Length);
            string strData = System.Text.Encoding.UTF8.GetString(d);
            HtmlUtils.add_html_token(sb, d, false);
            HtmlUtils.html_to_text(strData);
            HtmlUtils.text_to_html(strData);
            ThreadUtils.thread_name(strData);
            ThreadUtils.trim_trailing_fwd(strData);

            HexUtils.decode_hex(d);
            CharsetMapUtils.charset_decoder(d);

            new MessageParser().parse(d);
        }
    }

    private static byte[] into_alphabet(byte[] data, byte[] alphabet)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = alphabet[data[i] % alphabet.Length];
        }
        return result;
    }
}
