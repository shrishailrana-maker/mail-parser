/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/unstructured.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: e3e6291a280ec4d5de5b28473d30500dbfc1268a7b7ebb736cab106f43d34f5e
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

internal class UnstructuredParser
{
    public int token_start = 0;
    public int token_end = 0;
    public List<string> tokens = new();
    public bool last_is_encoded = true;

    public void add_token(MessageStream stream)
    {
        if (token_start > 0)
        {
            if (tokens.Count > 0)
            {
                tokens.Add(" ");
            }
            var raw = stream.bytes_span(token_start - 1, token_end);
            tokens.Add(System.Text.Encoding.UTF8.GetString(raw));
            token_start = 0;
            last_is_encoded = false;
        }
    }

    public void add_rfc2047(string token)
    {
        if (!last_is_encoded)
        {
            tokens.Add(" ");
        }
        tokens.Add(token);
        last_is_encoded = true;
    }
}

public partial class MessageStream
{
    // Rust: MessageStream::parse_unstructured
    public HeaderValue parse_unstructured()
    {
        var parser = new UnstructuredParser
        {
            token_start = 0,
            token_end = 0,
            tokens = new List<string>(),
            last_is_encoded = true
        };

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'\n':
                    parser.add_token(this);
                    if (!try_next_is_space())
                    {
                        return parser.tokens.Count switch
                        {
                            1 => HeaderValue.Text(parser.tokens[0]),
                            0 => HeaderValue.Empty,
                            _ => HeaderValue.Text(string.Concat(parser.tokens))
                        };
                    }
                    else
                    {
                        continue;
                    }
                case (byte)' ':
                case (byte)'\t':
                case (byte)'\r':
                    continue;
                case (byte)'=' when peek_char((byte)'?'):
                    checkpoint();
                    var token = decode_rfc2047();
                    if (token != null)
                    {
                        parser.add_token(this);
                        parser.add_rfc2047(token);
                        continue;
                    }
                    restore();
                    break;
            }

            if (parser.token_start == 0)
            {
                parser.token_start = offset();
            }
            parser.token_end = offset();
        }

        return HeaderValue.Empty;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class unstructured_tests
{
    [TestMethod]
    public void parse_unstructured()
    {
        foreach (var test in FieldTestUtils.load_tests<string>("unstructured"))
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var parsed = stream.parse_unstructured().as_text();
            Assert.AreEqual(test.expected, parsed, $"failed for {test.header}");
        }
    }
}
#endif
