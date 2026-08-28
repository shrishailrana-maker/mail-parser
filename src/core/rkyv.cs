/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/rkyv.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 97fc48839faba45827a1dc21dfb6a3518cee81bfc0e6d39ef28db57989a62fcb
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Net;

namespace Stalwart.MailParser.Port;

// Rust: rkyv integration types and archival representations

public class ArchivedAddr
{
    public string? name { get; set; }
    public string? address { get; set; }

    // get_name() accessor: use .name property
    // get_address() accessor: use .address property

    public Addr ToNative() => new Addr(name, address);
}

public class ArchivedGroup
{
    public string? name { get; set; }
    public List<ArchivedAddr> addresses { get; set; } = new();

    public Group ToNative() => new Group(name, addresses.ConvertAll(a => a.ToNative()));
}

public class ArchivedContentType
{
    public string c_type { get; set; } = "";
    public string? c_subtype { get; set; }
    public List<Attribute>? attributes { get; set; }

    public string ctype() => c_type;
    public string? subtype() => c_subtype;
    public string? attribute(string attrName) => attributes?.Find(a => string.Equals(a.name, attrName, StringComparison.OrdinalIgnoreCase))?.value;

    public ContentType ToNative() => new ContentType(c_type, c_subtype, attributes);
}

public class ArchivedReceived
{
    public Host? from { get; set; }
    public IPAddress? from_ip { get; set; }
    public string? from_iprev { get; set; }
    public Host? by { get; set; }
    public string? for_ { get; set; }
    public Protocol? with { get; set; }
    public TlsVersion? tls_version { get; set; }
    public string? tls_cipher { get; set; }
    public string? id { get; set; }
    public string? ident { get; set; }
    public Host? helo { get; set; }
    public Greeting? helo_cmd { get; set; }
    public string? via { get; set; }
    public DateTime? date { get; set; }

    public Received ToNative() => new Received
    {
        from = from,
        from_ip = from_ip,
        from_iprev = from_iprev,
        by = by,
        for_ = for_,
        with = with,
        tls_version = tls_version,
        tls_cipher = tls_cipher,
        id = id,
        ident = ident,
        helo = helo,
        helo_cmd = helo_cmd,
        via = via,
        date = date,
    };
}
