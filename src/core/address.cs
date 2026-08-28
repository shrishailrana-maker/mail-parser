/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/address.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 4472f8b09463887f164d7893c52922a7504a69f2a84a302973be9bdbb0d8cc8b
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Stalwart.MailParser.Port;

public abstract partial record Address
{
    // Rust: Address::first
    public Addr? first() => this switch
    {
        ListRecord lr => lr.Value.FirstOrDefault(),
        GroupRecord gr => gr.Value.SelectMany(g => g.addresses).FirstOrDefault(),
        _ => null
    };

    // Rust: Address::last
    public Addr? last() => this switch
    {
        ListRecord lr => lr.Value.LastOrDefault(),
        GroupRecord gr => gr.Value.SelectMany(g => g.addresses).LastOrDefault(),
        _ => null
    };

    // Rust: Address::into_list
    public List<Addr> into_list() => this switch
    {
        ListRecord lr => lr.Value,
        GroupRecord gr => gr.Value.SelectMany(g => g.addresses).ToList(),
        _ => new List<Addr>()
    };

    // Rust: Address::into_group
    public List<Group> into_group() => this switch
    {
        ListRecord lr => lr.Value.Select(a => new Group(null, new List<Addr> { a })).ToList(),
        GroupRecord gr => gr.Value,
        _ => new List<Group>()
    };

    // Rust: Address::iter
    public IEnumerable<Addr> iter() => this switch
    {
        ListRecord lr => lr.Value,
        GroupRecord gr => gr.Value.SelectMany(g => g.addresses),
        _ => Enumerable.Empty<Addr>()
    };

    // Rust: Address::contains
    public bool contains(string addr) => iter().Any(a => string.Equals(a.address, addr, StringComparison.OrdinalIgnoreCase));

    // Rust: Address::into_owned
    public Address into_owned() => this switch
    {
        ListRecord lr => List(lr.Value.Select(a => a with { }).ToList()),
        GroupRecord gr => Group(gr.Value.Select(g => g with { addresses = g.addresses.Select(a => a with { }).ToList() }).ToList()),
        _ => this
    };
}
