/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: examples/mailbox_iterate_maildir.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 91cd099664dc9fa3e5562897f35af56ac24176a67c926def36ff317d4ba0c199
// This file must remain 1:1 with the Rust source file.

using System;
using System.IO;
using Stalwart.MailParser.Port;

namespace Stalwart.MailParser.Port.Examples;

public static class MailboxIterateMaildirExample
{
    public static void Main()
    {
        string maildirPath = Path.Combine(AppContext.BaseDirectory, "resources", "maildir");
        var folders = new MaildirFolderIterator(maildirPath, ".");
        foreach (var folder in folders)
        {
            Console.WriteLine($"------\nMailbox: {folder.name ?? "INBOX"}");
            foreach (var message in folder)
            {
                Console.WriteLine($"Message with internal date {message.internal_date}, flags {message.flags.Count} and content {message.contents.Length} bytes.");
            }
        }
    }
}
