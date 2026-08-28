/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: examples/mailbox_parse_mbox.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 748c069ba15e2e6413d8ea5de463826f5f580842709dcdf8f7c66ebe71635d7e
// This file must remain 1:1 with the Rust source file.

using System;
using System.IO;
using System.Text.Json;
using Stalwart.MailParser.Port;

namespace Stalwart.MailParser.Port.Examples;

public static class MailboxParseMboxExample
{
    public static void Main()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        var it = new MboxMessageIterator(reader);
        var parser = new MessageParser();

        foreach (var rawMessage in it)
        {
            var msg = parser.parse(rawMessage.contents);
            if (msg != null)
            {
                Console.WriteLine(JsonSerializer.Serialize(msg));
            }
        }
    }
}
