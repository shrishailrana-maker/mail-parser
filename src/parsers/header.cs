/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/header.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: c3b4e556e08c44fd4b974ad427ef339134e69031134455d3ad0dd082eda7ffe8
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
    private const int MAX_HEADER_NAME_LEN = 34;

    // Rust: MessageStream::parse_headers
    public bool parse_headers(MessageParser conf, List<Header> headers)
    {
        while (true)
        {
            while (true)
            {
                byte? pk = peek();
                if (pk == (byte)'\n')
                {
                    next();
                    return true;
                }
                if (!pk.HasValue) return false;
                if (!char.IsWhiteSpace((char)pk.Value))
                {
                    break;
                }
                next();
            }

            int offset_field = offset();

            var header_name = parse_header_name();
            if (header_name.HasValue)
            {
                int from_offset = offset();
                HeaderValue value;
                if (conf.header_map.Count == 0)
                {
                    var hn = header_name.Value;
                    if (hn == HeaderName.Subject || hn == HeaderName.Comments || hn == HeaderName.ContentDescription || hn == HeaderName.ContentLocation || hn == HeaderName.ContentTransferEncoding)
                    {
                        value = parse_unstructured();
                    }
                    else if (hn == HeaderName.From || hn == HeaderName.To || hn == HeaderName.Cc || hn == HeaderName.Bcc || hn == HeaderName.ReplyTo || hn == HeaderName.Sender || hn == HeaderName.ResentTo || hn == HeaderName.ResentFrom || hn == HeaderName.ResentBcc || hn == HeaderName.ResentCc || hn == HeaderName.ResentSender || hn == HeaderName.ListArchive || hn == HeaderName.ListHelp || hn == HeaderName.ListId || hn == HeaderName.ListOwner || hn == HeaderName.ListPost || hn == HeaderName.ListSubscribe || hn == HeaderName.ListUnsubscribe)
                    {
                        value = parse_address();
                    }
                    else if (hn == HeaderName.Date || hn == HeaderName.ResentDate)
                    {
                        value = parse_date();
                    }
                    else if (hn == HeaderName.MessageId || hn == HeaderName.References || hn == HeaderName.InReplyTo || hn == HeaderName.ReturnPath || hn == HeaderName.ContentId || hn == HeaderName.ResentMessageId)
                    {
                        value = parse_id();
                    }
                    else if (hn == HeaderName.Keywords || hn == HeaderName.ContentLanguage)
                    {
                        value = parse_comma_separared();
                    }
                    else if (hn == HeaderName.Received)
                    {
                        value = parse_received();
                    }
                    else if (hn == HeaderName.MimeVersion)
                    {
                        value = parse_raw();
                    }
                    else if (hn == HeaderName.ContentType || hn == HeaderName.ContentDisposition)
                    {
                        value = parse_content_type();
                    }
                    else
                    {
                        value = parse_raw();
                    }
                }
                else
                {
                    if (conf.header_map.TryGetValue(header_name.Value, out var fnc))
                    {
                        value = fnc(this);
                    }
                    else
                    {
                        value = conf.def_hdr_parse_fnc(this);
                    }
                }

                headers.Add(new Header(
                    header_name.Value,
                    value,
                    (uint)offset_field,
                    (uint)from_offset,
                    (uint)offset()
                ));
            }
            else if (is_eof())
            {
                return false;
            }
        }
    }

    // Rust: MessageStream::parse_header_name
    public HeaderName? parse_header_name()
    {
        int token_start = 0;
        int token_end = 0;
        int token_len = 0;
        byte[] header = new byte[MAX_HEADER_NAME_LEN];

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            if (ch == (byte)':')
            {
                if (token_start != 0) break;
            }
            else if (ch == (byte)'\n')
            {
                return null;
            }
            else
            {
                if (!char.IsWhiteSpace((char)ch))
                {
                    if (token_start == 0)
                    {
                        token_start = offset();
                        token_end = token_start;
                    }
                    else
                    {
                        token_end = offset();
                    }

                    if (token_len < header.Length)
                    {
                        header[token_len] = (byte)char.ToLowerInvariant((char)ch);
                    }
                    token_len++;
                }
            }
        }

        if (token_start != 0)
        {
            var rawSpan = _data.Span.Slice(token_start - 1, token_end - token_start + 1);
            string rawStr = System.Text.Encoding.UTF8.GetString(rawSpan);
            return HeaderName.parse(rawStr) ?? HeaderName.Other(rawStr);
        }
        return null;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class header_tests
{
    [TestMethod]
    public void header_name_parse()
    {
        var inputs = new (string input, HeaderName expected)[]
        {
            ("From: ", HeaderName.From),
            ("receiVED: ", HeaderName.Received),
            (" subject   : ", HeaderName.Subject),
            ("X-Custom-Field : ", HeaderName.Other("X-Custom-Field")),
            (" T : ", HeaderName.Other("T")),
            ("mal formed: ", HeaderName.Other("mal formed")),
            ("MIME-version : ", HeaderName.MimeVersion),
            ("Delivered-To: ", HeaderName.DeliveredTo),
            ("archived-AT: ", HeaderName.ArchivedAt),
            ("X-Face: ", HeaderName.Other("X-Face")),
            ("Disposition-Notification-Options: ", HeaderName.DispositionNotificationOptions),
            ("MMHS-Other-Recipients-Indicator-To: ", HeaderName.MmhsOtherRecipientsIndicatorTo),
            ("Original-Encoded-Information-Types-Extra: ", HeaderName.Other("Original-Encoded-Information-Types-Extra")),
        };

        foreach (var (input, expected) in inputs)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(input));
            var parsed = stream.parse_header_name();
            Assert.IsNotNull(parsed, $"Failed for {input}");
            Assert.AreEqual(expected, parsed.Value, $"Failed for {input}");
        }
    }
}
#endif
