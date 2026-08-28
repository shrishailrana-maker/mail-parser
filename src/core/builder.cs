/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/builder.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 7d023549b1965d17d87b196b28ea3c256f86ab31c3550f208657067e7f6ae067
// This file must remain 1:1 with the Rust source file.

using System;

namespace Stalwart.MailParser.Port;

public partial class MessageParser
{
    // Rust: MessageParser::new
    public MessageParser()
    {
        header_map = new();
        def_hdr_parse_fnc = (s) => s.parse_raw();
    }

    // Rust: MessageParser::with_mime_headers
    public MessageParser with_mime_headers()
    {
        return header_content_type(HeaderName.ContentType)
            .header_content_type(HeaderName.ContentDisposition)
            .header_id(HeaderName.ContentId)
            .header_text(HeaderName.ContentDescription)
            .header_text(HeaderName.ContentLocation)
            .header_text(HeaderName.ContentTransferEncoding);
    }

    // Rust: MessageParser::with_date_headers
    public MessageParser with_date_headers()
    {
        return header_date(HeaderName.Date)
            .header_date(HeaderName.ResentDate);
    }

    // Rust: MessageParser::with_address_headers
    public MessageParser with_address_headers()
    {
        return header_address(HeaderName.From)
            .header_address(HeaderName.Sender)
            .header_address(HeaderName.ReplyTo)
            .header_address(HeaderName.To)
            .header_address(HeaderName.Cc)
            .header_address(HeaderName.Bcc)
            .header_address(HeaderName.ResentFrom)
            .header_address(HeaderName.ResentSender)
            .header_address(HeaderName.ResentTo)
            .header_address(HeaderName.ResentCc)
            .header_address(HeaderName.ResentBcc);
    }

    // Rust: MessageParser::with_message_ids
    public MessageParser with_message_ids()
    {
        return header_id(HeaderName.MessageId)
            .header_id(HeaderName.InReplyTo)
            .header_id(HeaderName.References)
            .header_id(HeaderName.ResentMessageId);
    }

    // Rust: MessageParser::with_minimal_headers
    public MessageParser with_minimal_headers()
    {
        return with_mime_headers()
            .header_date(HeaderName.Date)
            .header_text(HeaderName.Subject)
            .header_address(HeaderName.From)
            .header_address(HeaderName.ReplyTo)
            .header_address(HeaderName.To)
            .header_address(HeaderName.Cc)
            .header_address(HeaderName.Bcc);
    }

    // Rust: MessageParser::without_header
    public MessageParser without_header(HeaderName header)
    {
        header_map.Remove(header);
        return this;
    }

    // Rust: MessageParser::header_text
    public MessageParser header_text(HeaderName header)
    {
        header_map[header] = (s) => s.parse_unstructured();
        return this;
    }

    // Rust: MessageParser::header_date
    public MessageParser header_date(HeaderName header)
    {
        header_map[header] = (s) => s.parse_date();
        return this;
    }

    // Rust: MessageParser::header_address
    public MessageParser header_address(HeaderName header)
    {
        header_map[header] = (s) => s.parse_address();
        return this;
    }

    // Rust: MessageParser::header_id
    public MessageParser header_id(HeaderName header)
    {
        header_map[header] = (s) => s.parse_id();
        return this;
    }

    // Rust: MessageParser::header_content_type
    public MessageParser header_content_type(HeaderName header)
    {
        header_map[header] = (s) => s.parse_content_type();
        return this;
    }

    // Rust: MessageParser::header_comma_separated
    public MessageParser header_comma_separated(HeaderName header)
    {
        header_map[header] = (s) => s.parse_comma_separared();
        return this;
    }

    // Rust: MessageParser::header_received
    public MessageParser header_received(HeaderName header)
    {
        header_map[header] = (s) => s.parse_received();
        return this;
    }

    // Rust: MessageParser::header_raw
    public MessageParser header_raw(HeaderName header)
    {
        header_map[header] = (s) => s.parse_raw();
        return this;
    }

    // Rust: MessageParser::ignore_header
    public MessageParser ignore_header(HeaderName header)
    {
        header_map[header] = (s) =>
        {
            s.parse_and_ignore();
            return HeaderValue.Empty;
        };
        return this;
    }

    // Rust: MessageParser::default_header_text
    public MessageParser default_header_text()
    {
        def_hdr_parse_fnc = (s) => s.parse_unstructured();
        return this;
    }

    // Rust: MessageParser::default_header_raw
    public MessageParser default_header_raw()
    {
        def_hdr_parse_fnc = (s) => s.parse_raw();
        return this;
    }

    // Rust: MessageParser::default_header_ignore
    public MessageParser default_header_ignore()
    {
        def_hdr_parse_fnc = (s) =>
        {
            s.parse_and_ignore();
            return HeaderValue.Empty;
        };
        return this;
    }
}
