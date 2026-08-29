/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: examples/message_write_attachments.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 7e244d0fa0758f2ccadbe0cff0df232b47190a716ecd41c8ca31601750974920
// This file must remain 1:1 with the Rust source file.

using System;
using System.IO;
using System.Text;
using Stalwart.MailParser.Port;

namespace Stalwart.MailParser.Port.Examples;

public static class MessageWriteAttachmentsExample
{
    private const string MESSAGE = @"From: Art Vandelay <art@vandelay.com>
To: recipient@example.com
Subject: Attachments test
Content-Type: multipart/mixed; boundary=""festivus"";

--festivus
Content-Type: text/plain
Content-Transfer-Encoding: 7bit

Hello world!
--festivus
Content-Type: image/gif; name=""test.gif""
Content-Transfer-Encoding: base64
Content-Disposition: attachment

R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7
--festivus--
";

    public static void Main()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(MESSAGE);
        var message = new MessageParser().parse(bytes);
        if (message != null)
        {
            write_attachments(message);
        }
    }

    private static void write_attachments(Message message)
    {
        foreach (var attachment in message.attachments_iter())
        {
            if (!attachment.is_message())
            {
                // Rust: attachment.contents() -- uniform across all non-message PartType
                // variants. binary() ?? inline_binary() used to silently skip a Text/Html
                // "attachment" (PARITY-AUDIT.md examples/message_write_attachments.cs finding).
                File.WriteAllBytes(attachment.attachment_name() ?? "Untitled", attachment.contents());
            }
            else
            {
                var nested = attachment.message();
                if (nested != null)
                {
                    write_attachments(nested);
                }
            }
        }
    }
}
