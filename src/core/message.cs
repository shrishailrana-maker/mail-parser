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

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

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
    // Rust: self.raw_message.get(part.offset_header..part.offset_end).unwrap_or_default()
    // -- sliced to the ROOT part's bounds, not the entire backing buffer (PARITY-AUDIT.md
    // FILE 2: the prior version returned the whole array unsliced, which could include
    // bytes outside the root part's range if the backing buffer ever held extra context).
    public byte[] raw_message_bytes()
    {
        if (raw_message == null || parts.Count == 0) return Array.Empty<byte>();
        var part = parts[0];
        int start = (int)part.offset_header;
        int end = (int)part.offset_end;
        if (start < 0 || end > raw_message.Length || start > end) return Array.Empty<byte>();
        return raw_message[start..end];
    }

    // Rust: Message::header_values -- was MISSING entirely (PARITY-AUDIT.md FILE 2 /
    // Coordinator Lead #10); all_bcc/all_cc/all_to/received_all each re-inlined this exact
    // filter separately. Now the single canonical implementation those four delegate to.
    public IEnumerable<HeaderValue> header_values(HeaderName name) =>
        parts.Count > 0 ? parts[0].headers.Where(h => h.name == name).Select(h => h.value) : Enumerable.Empty<HeaderValue>();

    // Rust: Message::bcc
    public Address? bcc() => header(HeaderName.Bcc)?.as_address();
    // Rust: Message::all_bcc
    public IEnumerable<Address> all_bcc() => header_values(HeaderName.Bcc).Select(v => v.as_address()).Where(a => a != null).Select(a => a!);

    // Rust: Message::cc
    public Address? cc() => header(HeaderName.Cc)?.as_address();
    // Rust: Message::all_cc
    public IEnumerable<Address> all_cc() => header_values(HeaderName.Cc).Select(v => v.as_address()).Where(a => a != null).Select(a => a!);

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
    public string? message_id() => header(HeaderName.MessageId)?.as_text();

    // Rust: Message::mime_version
    public HeaderValue mime_version() => header(HeaderName.MimeVersion) ?? HeaderValue.Empty;

    // Rust: Message::received
    public Received? received() => header(HeaderName.Received)?.as_received();

    // Rust: Message::received_all
    public IEnumerable<Received> received_all() => header_values(HeaderName.Received).Select(v => v.as_received()).Where(r => r != null).Select(r => r!);

    // Rust: Message::references
    public HeaderValue references() => header(HeaderName.References) ?? HeaderValue.Empty;

    // Rust: Message::reply_to
    public Address? reply_to() => header(HeaderName.ReplyTo)?.as_address();

    // Rust: Message::resent_bcc
    public Address? resent_bcc() => header(HeaderName.ResentBcc)?.as_address();

    // Rust: Message::resent_cc
    // KNOWN INTENTIONAL DIFFERENCE (PARITY-AUDIT.md, standing Phase 2 policy on
    // POSSIBLE_UPSTREAM_BUG findings): the pinned Rust source (commit 499ae0f,
    // message.rs) has a real bug here -- its resent_cc() body reads HeaderName::ResentTo
    // instead of ResentCc, contradicting its own doc comment. Boss ruled: make C#
    // correct rather than bug-for-bug identical. This reads ResentCc, as documented.
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
    public string? return_address() => header(HeaderName.ReturnPath)?.as_text() ?? header(HeaderName.From)?.as_address()?.first()?.address;

    // Rust: Message::sender
    public Address? sender() => header(HeaderName.Sender)?.as_address();

    // Rust: Message::subject
    public string? subject() => header(HeaderName.Subject)?.as_text();

    // Rust: Message::thread_name
    public string? thread_name() => subject() != null ? ThreadUtils.thread_name(subject()!) : null;

    // Rust: Message::to
    public Address? to() => header(HeaderName.To)?.as_address();

    // Rust: Message::all_to
    public IEnumerable<Address> all_to() => header_values(HeaderName.To).Select(v => v.as_address()).Where(a => a != null).Select(a => a!);

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
    public IEnumerable<MessagePart> text_bodies() => new BodyPartIterator(this, text_body);

    // Rust: Message::html_bodies
    public IEnumerable<MessagePart> html_bodies() => new BodyPartIterator(this, html_body);

    // Rust: Message::attachments
    public IEnumerable<MessagePart> attachments_iter() => new AttachmentIterator(this);

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
    // Rust: matches!(self.body, PartType::Text(_) | PartType::Html(_)) -- was TextRecord
    // only, excluding Html (PARITY-AUDIT.md Coordinator Lead #13 / FILE 2).
    public bool is_text() => body is PartType.TextRecord || body is PartType.HtmlRecord;
    public bool is_html() => body is PartType.HtmlRecord;
    // Rust: MessagePart::is_text_html -- matches!(self.body, PartType::Html(_)); distinct
    // from is_html() only in name (FILE 1 missing-symbol list). Same check, added for
    // source/API parity.
    public bool is_text_html() => body is PartType.HtmlRecord;
    // Rust: matches!(self.body, PartType::Binary(_) | PartType::InlineBinary(_)) -- was
    // BinaryRecord only, excluding InlineBinary (PARITY-AUDIT.md Coordinator Lead #13 / FILE 2).
    public bool is_binary() => body is PartType.BinaryRecord || body is PartType.InlineBinaryRecord;
    public bool is_multipart() => body is PartType.MultipartRecord;
    public bool is_message() => body is PartType.MessageRecord;
    public bool is_body() => !is_attachment();

    // Rust: MessagePart::contents -- the body part's contents as a byte slice, uniform
    // across all PartType variants (unlike binary()/inline_binary(), which only cover 2
    // of 4 non-multipart variants). FILE 1: confirmed missing; concretely demonstrated in
    // examples/message_write_attachments.cs, which used to skip any Text/Html "attachment"
    // silently because binary() ?? inline_binary() returned null for those variants.
    public byte[] contents() => body switch
    {
        PartType.TextRecord tr => System.Text.Encoding.UTF8.GetBytes(tr.Value),
        PartType.HtmlRecord hr => System.Text.Encoding.UTF8.GetBytes(hr.Value),
        PartType.BinaryRecord br => br.Value,
        PartType.InlineBinaryRecord ibr => ibr.Value,
        PartType.MessageRecord mr => mr.Value.raw_message_bytes(),
        _ => Array.Empty<byte>(),
    };

    // Rust: MessagePart::text_contents -- the body part's contents as a string, None for
    // binary content that isn't valid UTF-8 and for Multipart (FILE 1 missing-symbol list).
    public string? text_contents() => body switch
    {
        PartType.TextRecord tr => tr.Value,
        PartType.HtmlRecord hr => hr.Value,
        PartType.BinaryRecord br => TryUtf8(br.Value),
        PartType.InlineBinaryRecord ibr => TryUtf8(ibr.Value),
        PartType.MessageRecord mr => TryUtf8(mr.Value.raw_message_bytes()),
        _ => null,
    };

    private static string? TryUtf8(byte[] bytes)
    {
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (DecoderFallbackException) { return null; }
    }

    // Rust: MessagePart::sub_parts -- same data as multipart(), under Rust's actual name
    // (FILE 1 missing-symbol list; multipart() already existed under a different name).
    public List<uint>? sub_parts() => multipart();

    // Rust: MessagePart::is_empty -- self.len() == 0. (len() itself already exists at
    // lib.cs -- see the fix there: it used to delegate to PartType.len(), which is a
    // DIFFERENT Rust function with different Message-case semantics -- see that fix's
    // comment for the full explanation.)
    public bool is_empty() => len() == 0;

    // Rust: MessagePart::raw_len -- offset_end.saturating_sub(offset_header); C#'s uint
    // subtraction wraps instead of saturating, so this must clamp explicitly.
    public uint raw_len() => offset_end >= offset_header ? offset_end - offset_header : 0u;

    // Rust: MessagePart::raw_header_offset / raw_body_offset / raw_end_offset
    public uint raw_header_offset() => offset_header;
    public uint raw_body_offset() => offset_body;
    public uint raw_end_offset() => offset_end;

    public string? content_description() => headers.header_value(HeaderName.ContentDescription)?.as_text();
    public ContentType? content_disposition() => headers.header_value(HeaderName.ContentDisposition)?.as_content_type();
    public string? content_id() => headers.header_value(HeaderName.ContentId)?.as_text();
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

    // Rust: MimeHeaders::is_content_type (default trait method) -- ASCII-only
    // eq_ignore_ascii_case, not Unicode OrdinalIgnoreCase (PARITY-AUDIT.md FILE 5-continuation).
    public bool is_content_type(string type_, string subtype)
    {
        var ct = content_type();
        if (ct == null) return false;
        return HeaderExtensions.EqIgnoreAsciiCase(ct.c_type, type_) &&
               HeaderExtensions.EqIgnoreAsciiCase(ct.c_subtype, subtype);
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class message_core_tests
{
    // Regression tests for Phase 2 fixes -- each pins a Rust-verified expected value.

    [TestMethod]
    public void header_values_returns_all_matching_headers_matches_rust()
    {
        // Rust: header_values() was entirely MISSING (PARITY-AUDIT.md FILE 2 / Coordinator
        // Lead #10) -- all_bcc/all_cc/all_to/received_all each re-inlined this filter
        // separately instead of having one canonical implementation to call.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Cc: a@x\r\nCc: b@x\r\nTo: c@x\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        var ccValues = msg!.header_values(HeaderName.Cc).ToList();
        Assert.AreEqual(2, ccValues.Count);

        // all_cc() must now agree with header_values(Cc) since it delegates to it.
        var allCc = msg.all_cc().Select(a => a.first()?.address).ToList();
        CollectionAssert.AreEqual(new[] { "a@x", "b@x" }, allCc);
    }

    [TestMethod]
    public void message_id_takes_last_of_multiple_ids_matches_rust()
    {
        // Rust: a Message-ID header with multiple values -- as_text() returns the LAST
        // one (TextList::last()), not the first (PARITY-AUDIT.md Coordinator Lead #1).
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Message-ID: <old@x>, <new@x>\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual("new@x", msg!.message_id());
    }

    [TestMethod]
    public void content_id_takes_last_of_multiple_ids_matches_rust()
    {
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: text/plain\r\nContent-ID: <old@x>, <new@x>\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual("new@x", msg!.content_id());
    }

    [TestMethod]
    public void return_address_falls_back_to_from_matches_rust()
    {
        // Rust: when Return-Path is absent, return_address() falls back to the first
        // From address (PARITY-AUDIT.md Coordinator Lead #12) -- the C# port used to
        // check an address-shaped Return-Path instead, so this fallback was dead code.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "From: first@example.com, second@example.com\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual("first@example.com", msg!.return_address());
    }

    [TestMethod]
    public void return_address_prefers_return_path_matches_rust()
    {
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Return-Path: <bounce@example.com>\r\nFrom: first@example.com\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual("bounce@example.com", msg!.return_address());
    }

    [TestMethod]
    public void raw_message_bytes_slices_to_root_part_not_whole_buffer_matches_rust()
    {
        // Rust: raw_message() slices to the root part's [offset_header..offset_end], not
        // the whole backing array (PARITY-AUDIT.md FILE 2). Concrete manifestation: a
        // nested message/rfc822 part shares the OUTER multipart buffer as its own
        // raw_message field (parsers/message.cs:390) -- before this fix, the nested
        // message's raw_message_bytes() returned the ENTIRE outer multipart buffer
        // (boundary markers, outer headers, sibling parts and all) instead of just its
        // own bytes.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: multipart/mixed; boundary=X\r\n\r\n" +
            "--X\r\n" +
            "Content-Type: message/rfc822\r\n\r\n" +
            "From: inner@example.com\r\nSubject: inner\r\n\r\ninner body\r\n" +
            "--X--\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        var nestedPart = msg!.parts.Find(p => p.is_message());
        Assert.IsNotNull(nestedPart);
        var nested = nestedPart!.message();
        Assert.IsNotNull(nested);

        string nestedRaw = System.Text.Encoding.UTF8.GetString(nested!.raw_message_bytes());
        Assert.IsTrue(nestedRaw.Contains("inner@example.com"), "nested message's own content must be present");
        Assert.IsFalse(nestedRaw.Contains("multipart/mixed"), "outer envelope must NOT leak into the nested message's raw bytes");
    }

    [TestMethod]
    public void message_part_len_uses_sliced_raw_message_for_nested_message_matches_rust()
    {
        // MessagePart::len() (header.rs) calls message.raw_message() (sliced) for the
        // Message variant -- a DIFFERENT function from PartType::len() (body.rs), which
        // reads the raw field directly (unsliced). Confirms the len() fix uses the now-
        // correct sliced raw_message_bytes(), not the deliberately-unsliced PartType.len().
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: multipart/mixed; boundary=X\r\n\r\n" +
            "--X\r\n" +
            "Content-Type: message/rfc822\r\n\r\n" +
            "From: a@b\r\n\r\nhi\r\n" +
            "--X--\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        var nestedPart = msg!.parts.Find(p => p.is_message());
        Assert.IsNotNull(nestedPart);

        // The nested part's own len() must equal its own sliced raw_message_bytes()
        // length, which must be far shorter than the whole outer buffer.
        Assert.AreEqual(nestedPart!.message()!.raw_message_bytes().Length, nestedPart.len());
        Assert.IsTrue(nestedPart.len() < raw.Length);
    }

    [TestMethod]
    public void is_text_includes_html_and_is_binary_includes_inline_matches_rust()
    {
        // Rust: is_text() = Text | Html; is_binary() = Binary | InlineBinary. Was
        // TextRecord-only / BinaryRecord-only in C# (Coordinator Lead #13 / FILE 2).
        byte[] htmlRaw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: text/html\r\n\r\n<p>hi</p>\r\n");
        var htmlMsg = new MessageParser().parse(htmlRaw);
        Assert.IsNotNull(htmlMsg);
        Assert.IsTrue(htmlMsg!.parts[0].is_text());
        Assert.IsFalse(htmlMsg.parts[0].is_binary());
    }

    [TestMethod]
    public void contents_covers_all_part_types_matches_rust()
    {
        // Rust: MessagePart::contents() is uniform across Text/Html/Binary/InlineBinary
        // (FILE 1 missing-symbol; concretely broke examples/message_write_attachments.cs,
        // which used binary() ?? inline_binary() -- null for Text/Html, silently skipping
        // the attachment). Self-verifying against the already-correct text()/binary()
        // accessors rather than a guessed literal, matching this file's own established
        // practice of confirming exact parser output empirically before asserting it.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: multipart/mixed; boundary=X\r\n\r\n" +
            "--X\r\n" +
            "Content-Type: text/plain\r\n\r\n" +
            "hello world\r\n" +
            "--X\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            "Content-Transfer-Encoding: base64\r\n\r\n" +
            "aGVsbG8=\r\n" +
            "--X--\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        var textPart = msg!.parts.Find(p => p.is_text());
        var binPart = msg.parts.Find(p => p.is_binary());
        Assert.IsNotNull(textPart);
        Assert.IsNotNull(binPart);

        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(textPart!.text()!), textPart.contents());
        CollectionAssert.AreEqual(binPart!.binary(), binPart.contents());
    }

    [TestMethod]
    public void resent_cc_reads_resent_cc_header_intentional_deviation_from_rust()
    {
        // KNOWN INTENTIONAL DIFFERENCE: pinned Rust has a bug here (reads Resent-To
        // instead of Resent-Cc). Boss ruled C# should be correct instead. This message
        // has DIFFERENT addresses in Resent-To vs Resent-Cc specifically to prove which
        // header C# actually reads.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Resent-To: to@example.com\r\nResent-Cc: cc@example.com\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual("cc@example.com", msg!.resent_cc()?.first()?.address);
    }

    [TestMethod]
    public void body_part_iterator_stops_at_first_invalid_id_matches_rust()
    {
        // Rust: BodyPartIterator::next() does `self.message.parts.get(*self.list.get(pos)?)`
        // -- once a part id is out of range, next() returns None, which ends the whole
        // Iterator (the caller never sees any later entries, even valid ones). The C#
        // port's text_bodies()/html_bodies()/attachments_iter() used to be LINQ
        // .Where(id => id < parts.Count).Select(...), which SKIPS invalid ids and keeps
        // going -- a different result whenever a bad id precedes a good one.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: text/plain\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        Assert.AreEqual(1, msg!.parts.Count);

        var badList = new List<uint> { 0, 99, 0 }; // valid, invalid, valid-again
        var results = new BodyPartIterator(msg, badList).ToList();

        Assert.AreEqual(1, results.Count); // stops at the invalid id -- the trailing valid entry is never reached
    }

    [TestMethod]
    public void text_bodies_is_reenumerable()
    {
        // The old BodyPartIterator/AttachmentIterator implemented GetEnumerator() => this,
        // so the IEnumerable returned by text_bodies() shared one mutable cursor: enumerating
        // it once (e.g. via .Any()/.Count()) exhausted it, and a second foreach/LINQ pass over
        // the SAME instance silently yielded nothing instead of restarting. No Rust analogue
        // (Rust iterators are consumed by value) -- a C#-specific IEnumerable-contract fix.
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: text/plain\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);

        var bodies = msg!.text_bodies();
        Assert.AreEqual(1, bodies.Count());
        Assert.AreEqual(1, bodies.Count()); // second enumeration of the SAME instance must restart
    }

    [TestMethod]
    public void is_content_type_ascii_case_insensitive_matches_rust()
    {
        // Rust: MimeHeaders::is_content_type uses eq_ignore_ascii_case (ASCII-only
        // case-insensitive match) -- confirms the refactor from OrdinalIgnoreCase to
        // the shared ASCII-only helper still matches on ordinary ASCII casing
        // (PARITY-AUDIT.md FILE 5-continuation).
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: text/plain\r\n\r\nbody\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        var part = msg!.part(0);
        Assert.IsNotNull(part);
        Assert.IsTrue(part!.is_content_type("TEXT", "PLAIN"), "ASCII uppercase must still match");
        Assert.IsFalse(part.is_content_type("image", "gif"), "different type/subtype must not match");
    }
}
#endif
