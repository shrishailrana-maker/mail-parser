/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/message.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 2f7c45f6bf52eefd489994ba09cc2d3f7e7ee9196ba9c662cfd353e9be15421c
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Stalwart.MailParser.Port;

public partial record Message : IMimeHeaders
{
    // Rust: Message::root_part
    public MessagePart root_part() => parts.Count > 0 ? parts[0] : new MessagePart();

    // Rust: Message::header
    public HeaderValue? header(HeaderName header) => parts.Count > 0 ? parts[0].headers.header_value(header) : null;

    // Rust: Message::remove_header
    public HeaderValue? remove_header(HeaderName header)
    {
        if (parts.Count == 0) return null;
        var headers = parts[0].headers;
        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i].name == header)
            {
                var val = headers[i].value;
                headers.RemoveAt(i);
                return val;
            }
        }
        return null;
    }

    // Rust: Message::header_raw
    public string? header_raw(HeaderName header)
    {
        if (parts.Count == 0 || raw_message == null) return null;
        var h = parts[0].headers.header(header);
        if (h == null) return null;
        int start = (int)h.offset_start;
        int end = (int)h.offset_end;
        if (start >= 0 && end <= raw_message.Length && start <= end)
        {
            return System.Text.Encoding.UTF8.GetString(raw_message, start, end - start);
        }
        return null;
    }

    // Rust: Message::header_as
    public List<HeaderValue> header_as(HeaderName header, HeaderForm form)
    {
        var results = new List<HeaderValue>();
        if (parts.Count == 0 || raw_message == null) return results;
        foreach (var h in parts[0].headers)
        {
            if (h.name == header)
            {
                int start = (int)h.offset_start;
                int end = (int)h.offset_end;
                if (start >= 0 && end <= raw_message.Length && start <= end)
                {
                    var stream = new MessageStream(new ReadOnlyMemory<byte>(raw_message, start, end - start));
                    results.Add(form switch
                    {
                        HeaderForm.Raw => stream.parse_raw(),
                        HeaderForm.Text => stream.parse_unstructured(),
                        HeaderForm.Addresses or HeaderForm.GroupedAddresses => stream.parse_address(),
                        HeaderForm.MessageIds => stream.parse_id(),
                        HeaderForm.Date => stream.parse_date(),
                        _ => stream.parse_raw(),
                    });
                }
            }
        }
        return results;
    }

    // Rust: Message::headers
    public IList<Header> headers() => parts.Count > 0 ? parts[0].headers : Array.Empty<Header>();

    // Rust: Message::headers_raw
    public IEnumerable<(string name, string value)> headers_raw()
    {
        if (parts.Count == 0 || raw_message == null) yield break;
        foreach (var header in parts[0].headers)
        {
            int start = (int)header.offset_start;
            int end = (int)header.offset_end;
            if (start >= 0 && end <= raw_message.Length && start <= end)
            {
                string val = System.Text.Encoding.UTF8.GetString(raw_message, start, end - start);
                yield return (header.name.as_str(), val);
            }
        }
    }

    // Rust: Message::raw_message
    public byte[] raw_message_bytes() => raw_message ?? Array.Empty<byte>();

    // Rust: Message::bcc
    public Address? bcc() => header(HeaderName.Bcc)?.as_address();
    // Rust: Message::all_bcc
    public IEnumerable<Address> all_bcc() => parts.Count > 0 ? parts[0].headers.Where(h => h.name == HeaderName.Bcc).Select(h => h.value.as_address()).Where(a => a != null).Select(a => a!) : Enumerable.Empty<Address>();

    // Rust: Message::cc
    public Address? cc() => header(HeaderName.Cc)?.as_address();
    // Rust: Message::all_cc
    public IEnumerable<Address> all_cc() => parts.Count > 0 ? parts[0].headers.Where(h => h.name == HeaderName.Cc).Select(h => h.value.as_address()).Where(a => a != null).Select(a => a!) : Enumerable.Empty<Address>();

    // Rust: Message::comments
    public HeaderValue comments() => header(HeaderName.Comments) ?? HeaderValue.Empty;

    // Rust: Message::date
    public DateTime? date() => header(HeaderName.Date)?.as_datetime();

    // Rust: Message::from
    public Address? from() => header(HeaderName.From)?.as_address();

    // Rust: Message::in_reply_to
    public HeaderValue in_reply_to() => header(HeaderName.InReplyTo) ?? HeaderValue.Empty;

    // Rust: Message::keywords
    public HeaderValue keywords() => header(HeaderName.Keywords) ?? HeaderValue.Empty;

    // Rust: Message::list_archive
    public HeaderValue list_archive() => header(HeaderName.ListArchive) ?? HeaderValue.Empty;

    // Rust: Message::list_help
    public HeaderValue list_help() => header(HeaderName.ListHelp) ?? HeaderValue.Empty;

    // Rust: Message::list_id
    public HeaderValue list_id() => header(HeaderName.ListId) ?? HeaderValue.Empty;

    // Rust: Message::list_owner
    public HeaderValue list_owner() => header(HeaderName.ListOwner) ?? HeaderValue.Empty;

    // Rust: Message::list_post
    public HeaderValue list_post() => header(HeaderName.ListPost) ?? HeaderValue.Empty;

    // Rust: Message::list_subscribe
    public HeaderValue list_subscribe() => header(HeaderName.ListSubscribe) ?? HeaderValue.Empty;

    // Rust: Message::list_unsubscribe
    public HeaderValue list_unsubscribe() => header(HeaderName.ListUnsubscribe) ?? HeaderValue.Empty;

    // Rust: Message::message_id
    public string? message_id() => header(HeaderName.MessageId)?.as_text_list()?.FirstOrDefault() ?? header(HeaderName.MessageId)?.as_text();

    // Rust: Message::mime_version
    public HeaderValue mime_version() => header(HeaderName.MimeVersion) ?? HeaderValue.Empty;

    // Rust: Message::received
    public Received? received() => header(HeaderName.Received)?.as_received();

    // Rust: Message::received_all
    public IEnumerable<Received> received_all() => parts.Count > 0 ? parts[0].headers.Where(h => h.name == HeaderName.Received).Select(h => h.value.as_received()).Where(r => r != null).Select(r => r!) : Enumerable.Empty<Received>();

    // Rust: Message::references
    public HeaderValue references() => header(HeaderName.References) ?? HeaderValue.Empty;

    // Rust: Message::reply_to
    public Address? reply_to() => header(HeaderName.ReplyTo)?.as_address();

    // Rust: Message::resent_bcc
    public Address? resent_bcc() => header(HeaderName.ResentBcc)?.as_address();

    // Rust: Message::resent_cc
    public Address? resent_cc() => header(HeaderName.ResentCc)?.as_address();

    // Rust: Message::resent_date
    public HeaderValue resent_date() => header(HeaderName.ResentDate) ?? HeaderValue.Empty;

    // Rust: Message::resent_from
    public Address? resent_from() => header(HeaderName.ResentFrom)?.as_address();

    // Rust: Message::resent_message_id
    public HeaderValue resent_message_id() => header(HeaderName.ResentMessageId) ?? HeaderValue.Empty;

    // Rust: Message::resent_sender
    public Address? resent_sender() => header(HeaderName.ResentSender)?.as_address();

    // Rust: Message::resent_to
    public Address? resent_to() => header(HeaderName.ResentTo)?.as_address();

    // Rust: Message::return_path
    public HeaderValue return_path() => header(HeaderName.ReturnPath) ?? HeaderValue.Empty;

    // Rust: Message::return_address
    public string? return_address() => header(HeaderName.ReturnPath)?.as_text_list()?.FirstOrDefault() ?? header(HeaderName.ReturnPath)?.as_text() ?? header(HeaderName.ReturnPath)?.as_address()?.first()?.address;

    // Rust: Message::sender
    public Address? sender() => header(HeaderName.Sender)?.as_address();

    // Rust: Message::subject
    public string? subject() => header(HeaderName.Subject)?.as_text();

    // Rust: Message::thread_name
    public string? thread_name() => subject() != null ? ThreadUtils.thread_name(subject()!) : null;

    // Rust: Message::to
    public Address? to() => header(HeaderName.To)?.as_address();

    // Rust: Message::all_to
    public IEnumerable<Address> all_to() => parts.Count > 0 ? parts[0].headers.Where(h => h.name == HeaderName.To).Select(h => h.value.as_address()).Where(a => a != null).Select(a => a!) : Enumerable.Empty<Address>();

    // Rust: Message::body_preview
    public string? body_preview(int preview_len)
    {
        if (text_body.Count > 0 && text_body[0] < parts.Count)
        {
            var txt = parts[(int)text_body[0]].text();
            if (txt != null) return PreviewUtils.preview_text(txt, preview_len);
        }
        if (html_body.Count > 0 && html_body[0] < parts.Count)
        {
            var html = parts[(int)html_body[0]].html();
            if (html != null) return PreviewUtils.preview_html(html, preview_len);
        }
        return null;
    }

    // Rust: Message::body_html
    public string? body_html(int pos)
    {
        if (pos < html_body.Count && html_body[pos] < parts.Count)
        {
            var part = parts[(int)html_body[pos]];
            return part.body switch
            {
                PartType.HtmlRecord hr => hr.Value,
                PartType.TextRecord tr => HtmlUtils.text_to_html(tr.Value),
                _ => null
            };
        }
        return null;
    }

    // Rust: Message::body_text
    public string? body_text(int pos)
    {
        if (pos < text_body.Count && text_body[pos] < parts.Count)
        {
            var part = parts[(int)text_body[pos]];
            return part.body switch
            {
                PartType.TextRecord tr => tr.Value,
                PartType.HtmlRecord hr => HtmlUtils.html_to_text(hr.Value),
                _ => null
            };
        }
        return null;
    }

    // Rust: Message::part
    public MessagePart? part(uint pos) => pos < parts.Count ? parts[(int)pos] : null;

    // Rust: Message::html_part
    public MessagePart? html_part(uint pos) => (pos < html_body.Count && html_body[(int)pos] < parts.Count) ? parts[(int)html_body[(int)pos]] : null;

    // Rust: Message::text_part
    public MessagePart? text_part(uint pos) => (pos < text_body.Count && text_body[(int)pos] < parts.Count) ? parts[(int)text_body[(int)pos]] : null;

    // Rust: Message::attachment
    public MessagePart? attachment(uint pos) => (pos < attachments.Count && attachments[(int)pos] < parts.Count) ? parts[(int)attachments[(int)pos]] : null;

    // Rust: Message::text_body_count
    public int text_body_count() => text_body.Count;

    // Rust: Message::html_body_count
    public int html_body_count() => html_body.Count;

    // Rust: Message::attachment_count
    public int attachment_count() => attachments.Count;

    // Rust: Message::text_bodies
    public IEnumerable<MessagePart> text_bodies() => text_body.Where(id => id < parts.Count).Select(id => parts[(int)id]);

    // Rust: Message::html_bodies
    public IEnumerable<MessagePart> html_bodies() => html_body.Where(id => id < parts.Count).Select(id => parts[(int)id]);

    // Rust: Message::attachments
    public IEnumerable<MessagePart> attachments_iter() => attachments.Where(id => id < parts.Count).Select(id => parts[(int)id]);

    // IMimeHeaders implementation on Message
    public string? content_description() => root_part().content_description();
    public ContentType? content_disposition() => root_part().content_disposition();
    public string? content_id() => root_part().content_id();
    public string? content_transfer_encoding() => root_part().content_transfer_encoding();
    public ContentType? content_type() => root_part().content_type();
    public HeaderValue content_language() => root_part().content_language();
    public string? content_location() => root_part().content_location();
    public string? attachment_name() => root_part().attachment_name();
    public bool is_content_type(string type_, string subtype) => root_part().is_content_type(type_, subtype);
}

public partial record MessagePart : IMimeHeaders
{
    // Rust: MessagePart::text
    public string? text() => body is PartType.TextRecord tr ? tr.Value : null;

    // Rust: MessagePart::html
    public string? html() => body is PartType.HtmlRecord hr ? hr.Value : null;

    // Rust: MessagePart::binary
    public byte[]? binary() => body is PartType.BinaryRecord br ? br.Value : null;

    // Rust: MessagePart::inline_binary
    public byte[]? inline_binary() => body is PartType.InlineBinaryRecord ibr ? ibr.Value : null;

    // Rust: MessagePart::message
    public Message? message() => body is PartType.MessageRecord mr ? mr.Value : null;

    // Rust: MessagePart::multipart
    public List<uint>? multipart() => body is PartType.MultipartRecord mpr ? mpr.Value : null;

    // Rust: MessagePart::attachment
    public byte[]? attachment() => body switch
    {
        PartType.BinaryRecord br => br.Value,
        PartType.InlineBinaryRecord ibr => ibr.Value,
        _ => null
    };

    public string? attachment_filename() => attachment_name();
    public string? attachment_mimetype() => content_type()?.mimetype();
    public string? attachment_type() => content_type()?.mimetype();

    public bool is_attachment() => content_disposition()?.mimetype().Equals("attachment", StringComparison.OrdinalIgnoreCase) == true;
    public bool is_inline_attachment() => content_disposition()?.mimetype().Equals("inline", StringComparison.OrdinalIgnoreCase) == true;
    public bool is_text() => body is PartType.TextRecord;
    public bool is_html() => body is PartType.HtmlRecord;
    public bool is_binary() => body is PartType.BinaryRecord;
    public bool is_multipart() => body is PartType.MultipartRecord;
    public bool is_message() => body is PartType.MessageRecord;
    public bool is_body() => !is_attachment();

    public string? content_description() => headers.header_value(HeaderName.ContentDescription)?.as_text();
    public ContentType? content_disposition() => headers.header_value(HeaderName.ContentDisposition)?.as_content_type();
    public string? content_id() => headers.header_value(HeaderName.ContentId)?.as_text_list()?.FirstOrDefault() ?? headers.header_value(HeaderName.ContentId)?.as_text();
    public string? content_transfer_encoding() => headers.header_value(HeaderName.ContentTransferEncoding)?.as_text();
    public ContentType? content_type() => headers.header_value(HeaderName.ContentType)?.as_content_type();
    public HeaderValue content_language() => headers.header_value(HeaderName.ContentLanguage) ?? HeaderValue.Empty;
    public string? content_location() => headers.header_value(HeaderName.ContentLocation)?.as_text();

    public string? attachment_name()
    {
        var cd = content_disposition();
        if (cd != null)
        {
            var fn = cd.attribute("filename");
            if (fn != null) return fn;
        }
        var ct = content_type();
        if (ct != null)
        {
            var name = ct.attribute("name");
            if (name != null) return name;
        }
        return null;
    }

    public bool is_content_type(string type_, string subtype)
    {
        var ct = content_type();
        if (ct == null) return false;
        return string.Equals(ct.c_type, type_, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ct.c_subtype, subtype, StringComparison.OrdinalIgnoreCase);
    }
}
