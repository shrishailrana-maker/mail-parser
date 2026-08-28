/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/raw.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 99d32ef3a8aa89855c14d3298d3af7a8832a36e6fca41564d506a0bf3e6271a7
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public partial class MessageStream
{
    // Rust: MessageStream::parse_raw
    public HeaderValue parse_raw()
    {
        int token_start = 0;
        int token_end = 0;

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            if (ch == (byte)'\n')
            {
                if (!try_next_is_space())
                {
                    break;
                }
                else
                {
                    continue;
                }
            }

            if (ch == (byte)' ' || ch == (byte)'\t' || ch == (byte)'\r')
            {
                continue;
            }

            if (token_start == 0)
            {
                token_start = offset();
            }

            token_end = offset();
        }

        if (token_start > 0)
        {
            var span = _data.Span.Slice(token_start - 1, token_end - token_start + 1);
            return HeaderValue.Text(System.Text.Encoding.UTF8.GetString(span));
        }

        return HeaderValue.Empty;
    }

    // Rust: MessageStream::parse_and_ignore
    public void parse_and_ignore()
    {
        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            if (chOpt.Value == (byte)'\n')
            {
                if (!try_next_is_space())
                {
                    break;
                }
            }
        }
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class raw_tests
{
    [TestMethod]
    public void parse_raw_text()
    {
        var inputs = new (string input, string expected)[]
        {
            ("Saying Hello\nMessage-Id", "Saying Hello"),
            ("Re: Saying Hello\r\n \r\nFrom:", "Re: Saying Hello"),
            (
                " from x.y.test\n      by example.net\n      via TCP\n" +
                "      with ESMTP\n      id ABC12345\n      " +
                "for <mary@example.net>;  21 Nov 1997 10:05:43 -0600\n",
                "from x.y.test\n      by example.net\n      via TCP\n" +
                "      with ESMTP\n      id ABC12345\n      " +
                "for <mary@example.net>;  21 Nov 1997 10:05:43 -0600"
            ),
            ("Re: Saying Hello", "Re: Saying Hello"),
        };

        foreach (var (input, expected) in inputs)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(input));
            var res = stream.parse_raw();
            Assert.AreEqual(expected, res.as_text(), $"Failed for '{input}'");
        }
    }

    [TestMethod]
    public void ordered_raw_headers()
    {
        var input = "From: Art Vandelay <art@vandelay.com>\n" +
            "To: jane@example.com\n" +
            "Date: Sat, 20 Nov 2021 14:22:01 -0800\n" +
            "Subject: Why not both importing AND exporting? =?utf-8?b?4pi6?=\n" +
            "Content-Type: multipart/mixed; boundary=\"festivus\";\n\n" +
            "Here's a message body.\n";

        var message = new MessageParser().parse(System.Text.Encoding.UTF8.GetBytes(input));
        Assert.IsNotNull(message);
        using var iter = message.headers_raw().GetEnumerator();
        Assert.IsTrue(iter.MoveNext());
        Assert.AreEqual(("From", " Art Vandelay <art@vandelay.com>\n"), iter.Current);
        Assert.IsTrue(iter.MoveNext());
        Assert.AreEqual(("To", " jane@example.com\n"), iter.Current);
        Assert.IsTrue(iter.MoveNext());
        Assert.AreEqual(("Date", " Sat, 20 Nov 2021 14:22:01 -0800\n"), iter.Current);
        Assert.IsTrue(iter.MoveNext());
        Assert.AreEqual(("Subject", " Why not both importing AND exporting? =?utf-8?b?4pi6?=\n"), iter.Current);
        Assert.IsTrue(iter.MoveNext());
        Assert.AreEqual(("Content-Type", " multipart/mixed; boundary=\"festivus\";\n"), iter.Current);
        Assert.IsFalse(iter.MoveNext());
    }
}
#endif
