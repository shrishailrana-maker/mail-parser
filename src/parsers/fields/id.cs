/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/id.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: e400917312a94deb8508f697284cf55a5a533993d879ada601fc0859b70dd5ad
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
    // Rust: MessageStream::parse_id
    public HeaderValue parse_id()
    {
        int token_start = 0;
        int token_end = 0;
        int token_invalid_start = 0;
        int token_invalid_end = 0;
        bool is_id_part = false;
        var ids = new List<string>();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'\n':
                    if (!try_next_is_space())
                    {
                        return ids.Count switch
                        {
                            1 => HeaderValue.Text(ids[0]),
                            0 => token_invalid_start > 0
                                ? HeaderValue.Text(System.Text.Encoding.UTF8.GetString(bytes_span(token_invalid_start - 1, token_invalid_end)))
                                : HeaderValue.Empty,
                            _ => HeaderValue.TextList(ids)
                        };
                    }
                    else
                    {
                        continue;
                    }
                case (byte)'<':
                    is_id_part = true;
                    continue;
                case (byte)'>':
                    is_id_part = false;
                    if (token_start > 0)
                    {
                        ids.Add(System.Text.Encoding.UTF8.GetString(bytes_span(token_start - 1, token_end)));
                        token_start = 0;
                    }
                    continue;
                case (byte)' ' or (byte)'\t' or (byte)'\r':
                    continue;
                default:
                    break;
            }

            if (is_id_part)
            {
                if (token_start == 0)
                {
                    token_start = offset();
                }
                token_end = offset();
            }
            else
            {
                if (token_invalid_start == 0)
                {
                    token_invalid_start = offset();
                }
                token_invalid_end = offset();
            }
        }

        return HeaderValue.Empty;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class id_tests
{
    [TestMethod]
    public void parse_message_ids()
    {
        var tests = FieldTestUtils.load_tests<List<string>?>("id");
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var res = stream.parse_id();
            var list = res.as_text_list() ?? (res.as_text() != null ? new List<string> { res.as_text()! } : new List<string>());
            var exp = test.expected ?? new List<string>();
            Assert.AreEqual(exp.Count, list.Count, $"Count mismatch for {test.header}");
            for (int i = 0; i < exp.Count; i++)
            {
                Assert.AreEqual(exp[i], list[i], $"Mismatch at {i} for {test.header}");
            }
        }
    }
}
#endif
