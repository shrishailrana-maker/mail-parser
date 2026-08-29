/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/header.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 7123ebb9fb2b6ae80ed79d739c14f0c105d2b1f0ea538365f8a16608ab6a2416
// This file must remain 1:1 with the Rust source file.
//
// PHASE 2 reorganization (PARITY-AUDIT.md, "header.cs reorganization", Boss decision):
// this file previously contained DateTimeUtils (moved to parsers/fields/date.cs, where
// Rust actually defines that logic) instead of the Header/HeaderValue/HeaderName/
// ContentType/Received/Host API this file is supposed to mirror. That API is added here
// now, fixing PARITY-AUDIT.md's confirmed bugs in the process (as_text/as_text_list
// first-vs-last, ContentType.attribute() case-sensitivity, remove_attribute() ordering).
//
// Received's own field-level accessors (from/by/for_/with/tls_version/etc.) are NOT
// added as methods here: they already exist as public settable properties on the
// Received record in lib.cs, and C# does not allow a method and a property to share a
// name on the same type. Direct property access is the established idiom in this port
// for exactly this situation (see Addr/Group in core/address.cs) -- not a gap.
//
// TlsVersion/Greeting/Protocol are plain C# enums: enums cannot have instance methods or
// override ToString() in C#, so their as_str() equivalents are extension methods instead
// of `impl` blocks. Callers needing Rust's Display-via-`{}` behavior must call .as_str()
// explicitly -- there is no way to make .ToString() do it for a C# enum.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Stalwart.MailParser.Port;

public static class HeaderExtensions
{
    public static Header? header(this IList<Header> headers, HeaderName name)
    {
        for (int index = headers.Count - 1; index >= 0; index--)
        {
            if (headers[index].name == name) return headers[index];
        }
        return null;
    }

    public static HeaderValue? header_value(this IList<Header> headers, HeaderName name)
    {
        return headers.header(name)?.value;
    }

    // ASCII-only case-insensitive comparison, matching Rust's eq_ignore_ascii_case exactly
    // (unlike .NET's OrdinalIgnoreCase, which folds a wider Unicode case-mapping table).
    internal static bool EqIgnoreAsciiCase(string? a, string b)
    {
        if (a == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            char ca = a[i], cb = b[i];
            if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
            if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
            if (ca != cb) return false;
        }
        return true;
    }

    // Matches Rust's u8::is_ascii_whitespace() exactly: space, tab, LF, form feed (0x0C),
    // CR -- and nothing else. .NET's char.IsWhiteSpace() follows the much broader Unicode
    // White_Space property (e.g. it includes 0x0B vertical tab, which Rust's check does
    // NOT), which was a confirmed bug at every site that used it directly on a raw byte
    // (PARITY-AUDIT.md: quoted_printable.cs, mime.cs is_multipart_end, header.cs).
    internal static bool IsAsciiWhitespace(byte ch) =>
        ch == (byte)' ' || ch == (byte)'\t' || ch == (byte)'\n' || ch == 0x0C || ch == (byte)'\r';

    // Matches Rust's str::make_ascii_lowercase() / Cow::make_ascii_lowercase(): only
    // A-Z fold to a-z, every other character (including non-ASCII Unicode letters) is
    // left untouched. .NET's ToLowerInvariant() folds a much wider Unicode case-mapping
    // table, which was a confirmed bug at the one site that used it directly on what
    // should have been an ASCII-only fold (PARITY-AUDIT.md FILE 20, content_type.cs).
    internal static string ToAsciiLowercase(string s)
    {
        char[]? buf = null;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 'A' && c <= 'Z')
            {
                buf ??= s.ToCharArray();
                buf[i] = (char)(c + 32);
            }
        }
        return buf == null ? s : new string(buf);
    }
}

// Rust: impl<'x> HeaderValue<'x>
public abstract partial record HeaderValue
{
    // Rust: HeaderValue::as_text -- fixed: TextList now returns the LAST element (was
    // returning null for any TextList at all -- PARITY-AUDIT.md Coordinator Lead #1).
    public string? as_text() => this switch
    {
        TextRecord tr => tr.Value,
        TextListRecord tlr => tlr.Value.Count > 0 ? tlr.Value[^1] : null,
        _ => null
    };

    // Rust: HeaderValue::as_text_list -- fixed: a plain Text now wraps into a one-item
    // list (was returning null for any Text at all -- PARITY-AUDIT.md Coordinator Lead #2).
    public List<string>? as_text_list() => this switch
    {
        TextRecord tr => new List<string> { tr.Value },
        TextListRecord tlr => tlr.Value,
        _ => null
    };

    public string unwrap_text() => this is TextRecord tr ? tr.Value
        : throw new InvalidOperationException("HeaderValue.unwrap_text called on non-Text value");

    public List<string> unwrap_text_list() => this switch
    {
        TextListRecord tlr => tlr.Value,
        TextRecord tr => new List<string> { tr.Value },
        _ => throw new InvalidOperationException("HeaderValue.unwrap_text_list called on non-TextList value")
    };

    public DateTime unwrap_datetime() => this is DateTimeRecord dtr ? dtr.Value
        : throw new InvalidOperationException("HeaderValue.unwrap_datetime called on non-DateTime value");

    public Address unwrap_address() => this is AddressRecord ar ? ar.Value
        : throw new InvalidOperationException("HeaderValue.unwrap_address called on non-Address value");

    public ContentType unwrap_content_type() => this is ContentTypeRecord ctr ? ctr.Value
        : throw new InvalidOperationException("HeaderValue.unwrap_content_type called on non-ContentType value");

    public Received unwrap_received() => this is ReceivedRecord rr ? rr.Value
        : throw new InvalidOperationException("HeaderValue.unwrap_received called on non-Received value");

    public string? into_text() => this is TextRecord tr ? tr.Value : null;

    public List<string>? into_text_list() => this switch
    {
        TextRecord tr => new List<string> { tr.Value },
        TextListRecord tlr => tlr.Value,
        _ => null
    };

    public Address? into_address() => this is AddressRecord ar ? ar.Value : null;
    public DateTime? into_datetime() => this is DateTimeRecord dtr ? dtr.Value : null;
    public ContentType? into_content_type() => this is ContentTypeRecord ctr ? ctr.Value : null;
    public Received? into_received() => this is ReceivedRecord rr ? rr.Value : null;

    // Rust: HeaderValue::into_owned -- C# has no borrowed/owned distinction (strings are
    // already fully owned, GC-managed); a shallow structural copy is the equivalent.
    public HeaderValue into_owned() => this switch
    {
        AddressRecord ar => HeaderValue.Address(ar.Value.into_owned()),
        _ => this
    };

    // Rust: HeaderValue::len
    public int len() => this switch
    {
        TextRecord tr => System.Text.Encoding.UTF8.GetByteCount(tr.Value),
        TextListRecord tlr => Sum(tlr.Value),
        AddressRecord ar => ar.Value switch
        {
            Address.ListRecord lr => SumAddr(lr.Value),
            Address.GroupRecord gr => SumAddrGroups(gr.Value),
            _ => 0
        },
        DateTimeRecord => 24,
        ContentTypeRecord ctr => ctr.Value.c_type.Length
            + (ctr.Value.c_subtype?.Length ?? 0)
            + (ctr.Value.attributes?.ConvertAll(a => a.name.Length + a.value.Length).FindAll(_ => true) is { } lens ? Sum(lens) : 0),
        ReceivedRecord => 1,
        _ => 0
    };

    private static int Sum(List<string> values)
    {
        int total = 0;
        foreach (var v in values) total += System.Text.Encoding.UTF8.GetByteCount(v);
        return total;
    }

    private static int Sum(List<int> values)
    {
        int total = 0;
        foreach (var v in values) total += v;
        return total;
    }

    private static int SumAddr(List<Addr> addrs)
    {
        int total = 0;
        foreach (var a in addrs) total += (a.name?.Length ?? 0) + (a.address?.Length ?? 0);
        return total;
    }

    private static int SumAddrGroups(List<Group> groups)
    {
        int total = 0;
        foreach (var g in groups) total += SumAddr(g.addresses);
        return total;
    }
}

// Rust: impl HeaderName<'_>
public partial struct HeaderName
{
    // Rust: HeaderName::into_string
    public string into_string() => as_str();

    // Rust: HeaderName::is_structured
    public bool is_structured() => Kind switch
    {
        KnownHeader.Subject or KnownHeader.Comments or KnownHeader.ContentDescription
            or KnownHeader.ContentLocation or KnownHeader.ContentTransferEncoding
            or KnownHeader.From or KnownHeader.To or KnownHeader.Cc or KnownHeader.Bcc
            or KnownHeader.ReplyTo or KnownHeader.Sender or KnownHeader.ResentTo
            or KnownHeader.ResentFrom or KnownHeader.ResentBcc or KnownHeader.ResentCc
            or KnownHeader.ResentSender or KnownHeader.ListArchive or KnownHeader.ListHelp
            or KnownHeader.ListId or KnownHeader.ListOwner or KnownHeader.ListPost
            or KnownHeader.ListSubscribe or KnownHeader.ListUnsubscribe or KnownHeader.Date
            or KnownHeader.ResentDate or KnownHeader.MessageId or KnownHeader.References
            or KnownHeader.InReplyTo or KnownHeader.ReturnPath or KnownHeader.ContentId
            or KnownHeader.ResentMessageId or KnownHeader.Keywords or KnownHeader.ContentLanguage
            or KnownHeader.Received or KnownHeader.ContentType or KnownHeader.ContentDisposition => true,
        _ => false
    };

    // Rust: HeaderName::is_mime_header
    public bool is_mime_header() => Kind switch
    {
        KnownHeader.ContentDescription or KnownHeader.ContentId or KnownHeader.ContentLanguage
            or KnownHeader.ContentLocation or KnownHeader.ContentTransferEncoding
            or KnownHeader.ContentType or KnownHeader.ContentDisposition => true,
        _ => false
    };

    // Rust: HeaderName::is_other
    public bool is_other() => Kind == KnownHeader.Other;
}

// Rust: impl<'x> ContentType<'x>
public partial record ContentType
{
    // Rust: ContentType::ctype
    public string ctype() => c_type;

    // Rust: ContentType::attribute -- fixed: exact/ordinal match, not case-insensitive
    // (PARITY-AUDIT.md Coordinator Lead #5 / FILE 6's ArchivedContentType instance of the
    // same bug).
    public string? attribute(string name)
    {
        if (attributes == null) return null;
        foreach (var a in attributes)
        {
            if (string.Equals(a.name, name, StringComparison.Ordinal)) return a.value;
        }
        return null;
    }

    // Rust: ContentType::remove_attribute -- uses Vec::swap_remove (order-breaking: the
    // last element moves into the removed slot), not a stable/ordered removal.
    public string? remove_attribute(string name)
    {
        if (attributes == null) return null;
        for (int i = 0; i < attributes.Count; i++)
        {
            if (string.Equals(attributes[i].name, name, StringComparison.Ordinal))
            {
                var val = attributes[i].value;
                int last = attributes.Count - 1;
                attributes[i] = attributes[last];
                attributes.RemoveAt(last);
                return val;
            }
        }
        return null;
    }

    // Rust: ContentType::attributes
    public List<Attribute>? attributes_() => attributes;

    // Rust: ContentType::is_attachment -- ASCII-only case-insensitive (eq_ignore_ascii_case).
    public bool is_attachment() => HeaderExtensions.EqIgnoreAsciiCase(c_type, "attachment");

    // Rust: ContentType::is_inline -- ASCII-only case-insensitive (eq_ignore_ascii_case).
    public bool is_inline() => HeaderExtensions.EqIgnoreAsciiCase(c_type, "inline");
}

// Rust: impl Host<'_>
public partial record Host
{
    // Rust: impl Display for Host -- prints the name or IP directly.
    public override string ToString() => this switch
    {
        NameRecord n => n.Value,
        IpAddrRecord i => i.Value.ToString(),
        _ => ""
    };

    // Rust: Host::into_owned -- C# has no borrowed/owned distinction.
    public Host into_owned() => this;
}

// Rust: impl<'x> Received<'x>
public partial record Received
{
    // Rust: Received::into_owned -- C# has no borrowed/owned distinction; a shallow
    // structural copy is the equivalent (matches the Address.into_owned() pattern).
    public Received into_owned() => this with { };
}

// Rust: impl TlsVersion -- C# enums cannot have instance methods, so this is an
// extension method. There is no way to make TlsVersion.ToString() do this in C#.
public static class TlsVersionExtensions
{
    public static string as_str(this TlsVersion v) => v switch
    {
        TlsVersion.SSLv2 => "SSLv2",
        TlsVersion.SSLv3 => "SSLv3",
        TlsVersion.TLSv1_0 => "TLSv1.0",
        TlsVersion.TLSv1_1 => "TLSv1.1",
        TlsVersion.TLSv1_2 => "TLSv1.2",
        TlsVersion.TLSv1_3 => "TLSv1.3",
        TlsVersion.DTLSv1_0 => "DTLSv1.0",
        TlsVersion.DTLSv1_2 => "DTLSv1.2",
        TlsVersion.DTLSv1_3 => "DTLSv1.3",
        _ => ""
    };
}

// Rust: impl Greeting
public static class GreetingExtensions
{
    public static string as_str(this Greeting g) => g switch
    {
        Greeting.Helo => "HELO",
        Greeting.Ehlo => "EHLO",
        Greeting.Lhlo => "LHLO",
        _ => ""
    };
}

// Rust: impl Protocol
public static class ProtocolExtensions
{
    public static string as_str(this Protocol p) => p switch
    {
        Protocol.SMTP => "SMTP",
        Protocol.LMTP => "LMTP",
        Protocol.ESMTP => "ESMTP",
        Protocol.ESMTPS => "ESMTPS",
        Protocol.ESMTPA => "ESMTPA",
        Protocol.ESMTPSA => "ESMTPSA",
        Protocol.LMTPA => "LMTPA",
        Protocol.LMTPS => "LMTPS",
        Protocol.LMTPSA => "LMTPSA",
        Protocol.UTF8SMTP => "UTF8SMTP",
        Protocol.UTF8SMTPA => "UTF8SMTPA",
        Protocol.UTF8SMTPS => "UTF8SMTPS",
        Protocol.UTF8SMTPSA => "UTF8SMTPSA",
        Protocol.UTF8LMTP => "UTF8LMTP",
        Protocol.UTF8LMTPA => "UTF8LMTPA",
        Protocol.UTF8LMTPS => "UTF8LMTPS",
        Protocol.UTF8LMTPSA => "UTF8LMTPSA",
        Protocol.HTTP => "HTTP",
        Protocol.HTTPS => "HTTPS",
        Protocol.IMAP => "IMAP",
        Protocol.POP3 => "POP3",
        Protocol.MMS => "MMS",
        Protocol.Local => "Local",
        _ => ""
    };
}
