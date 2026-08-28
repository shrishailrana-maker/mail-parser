/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/list.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: b89d4590f578788acbe8aa11395abd7f27e91535f375acfefa3065bb1abab10b
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public partial class MessageStream
{
    private class ListParser
    {
        public int token_start = 0;
        public int token_end = 0;
        public bool is_token_start = true;
        public List<string> tokens = new();
        public List<string> list = new();

        public void add_token(MessageStream stream, bool add_space)
        {
            if (token_start > 0)
            {
                if (tokens.Count > 0)
                {
                    tokens.Add(" ");
                }
                tokens.Add(System.Text.Encoding.UTF8.GetString(stream.bytes_span(token_start - 1, token_end)));

                if (add_space)
                {
                    tokens.Add(" ");
                }

                token_start = 0;
                is_token_start = true;
            }
        }

        public void add_tokens_to_list()
        {
            if (tokens.Count > 0)
            {
                if (tokens.Count == 1)
                {
                    list.Add(tokens[0]);
                    tokens.Clear();
                }
                else
                {
                    string value = string.Concat(tokens);
                    tokens.Clear();
                    list.Add(value);
                }
            }
        }
    }

    // Rust: MessageStream::parse_comma_separared
    public HeaderValue parse_comma_separared()
    {
        var parser = new ListParser();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'\n':
                    parser.add_token(this, false);
                    if (!try_next_is_space())
                    {
                        parser.add_tokens_to_list();
                        return parser.list.Count switch
                        {
                            1 => HeaderValue.Text(parser.list[0]),
                            0 => HeaderValue.Empty,
                            _ => HeaderValue.TextList(parser.list)
                        };
                    }
                    else
                    {
                        continue;
                    }
                case (byte)' ' or (byte)'\t':
                    if (!parser.is_token_start)
                    {
                        parser.is_token_start = true;
                    }
                    continue;
                case (byte)'=' when parser.is_token_start && peek_char((byte)'?'):
                    checkpoint();
                    var token = decode_rfc2047();
                    if (token != null)
                    {
                        parser.add_token(this, true);
                        parser.tokens.Add(token);
                        continue;
                    }
                    restore();
                    break;
                case (byte)',':
                    parser.add_token(this, false);
                    parser.add_tokens_to_list();
                    continue;
                case (byte)'\r':
                    continue;
                default:
                    break;
            }

            if (parser.is_token_start)
            {
                parser.is_token_start = false;
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
public class list_tests
{
    [TestMethod]
    public void parse_comma_separated_text()
    {
        var tests = FieldTestUtils.load_tests<List<string>>("list");
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var parsed = stream.parse_comma_separared();
            var list = parsed.as_text_list() ?? (parsed.as_text() != null ? new List<string> { parsed.as_text()! } : new List<string>());
            Assert.AreEqual(test.expected.Count, list.Count, $"Count mismatch for {test.header}");
            for (int i = 0; i < test.expected.Count; i++)
            {
                Assert.AreEqual(test.expected[i], list[i], $"Mismatch at {i} for {test.header}");
            }
        }
    }
}
#endif
