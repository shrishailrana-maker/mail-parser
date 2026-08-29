/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: examples/message_parse.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 9c877c4db4107ae8ee06b2284c08a79ade3487852389f2939af6a5931d522ae8
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Stalwart.MailParser.Port;

namespace Stalwart.MailParser.Port.Examples;

public static class MessageParseExample
{
    private const string MESSAGE = @"From: Art Vandelay <art@vandelay.com> (Vandelay Industries)
To: ""Colleagues"": ""James Smythe"" <james@vandelay.com>; Friends:
    jane@example.com, =?UTF-8?Q?John_Sm=C3=AEth?= <john@example.com>;
Date: Sat, 20 Nov 2021 14:22:01 -0800
Subject: Why not both importing AND exporting? =?utf-8?b?4pi6?=
Content-Type: multipart/mixed; boundary=""festivus"";

--festivus
Content-Type: text/html; charset=""us-ascii""
Content-Transfer-Encoding: base64

PGh0bWw+PHA+SSB3YXMgdGhpbmtpbmcgYWJvdXQgcXVpdHRpbmcgdGhlICZsZHF1bztle
HBvcnRpbmcmcmRxdW87IHRvIGZvY3VzIGp1c3Qgb24gdGhlICZsZHF1bztpbXBvcnRpbm
cmcmRxdW87LDwvcD48cD5idXQgdGhlbiBJIHRob3VnaHQsIHdoeSBub3QgZG8gYm90aD8
gJiN4MjYzQTs8L3A+PC9odG1sPg==
--festivus
Content-Type: message/rfc822

From: ""Cosmo Kramer"" <kramer@kramerica.com>
Subject: Exporting my book about coffee tables
Content-Type: multipart/mixed; boundary=""giddyup"";

--giddyup
Content-Type: text/plain; charset=""utf-16""
Content-Transfer-Encoding: quoted-printable

=FF=FE=0C!5=D8""=DD5=D8)=DD5=D8-=DD =005=D8*=DD5=D8""=DD =005=D8""=
=DD5=D85=DD5=D8-=DD5=D8,=DD5=D8/=DD5=D81=DD =005=D8*=DD5=D86=DD =
=005=D8=1F=DD5=D8,=DD5=D8,=DD5=D8(=DD =005=D8-=DD5=D8)=DD5=D8""=
=DD5=D8=1E=DD5=D80=DD5=D8""=DD!=00
--giddyup
Content-Type: image/gif; name*1=""about ""; name*0=""Book "";
              name*2*=utf-8''%e2%98%95 tables.gif
Content-Transfer-Encoding: Base64
Content-Disposition: attachment

R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7
--giddyup--
--festivus--
";

    // Rust: assert_eq! panics on mismatch regardless of build config -- Debug.Assert
    // would be stripped in Release, so this throws explicitly to match that behavior for
    // a demo program that verifies its own known-good output as documentation-by-example.
    private static void AssertEq<T>(T actual, T expected, string label)
    {
        if (!Equals(actual, expected))
        {
            throw new InvalidOperationException($"{label}: expected <{expected}> but was <{actual}>");
        }
    }

    private static void AssertGroupsEqual(List<Group>? actual, List<Group>? expected, string label)
    {
        if (actual == null || expected == null || actual.Count != expected.Count)
        {
            throw new InvalidOperationException($"{label}: group list shape mismatch");
        }
        for (int i = 0; i < expected.Count; i++)
        {
            AssertEq(actual[i].name, expected[i].name, $"{label}[{i}].name");
            if (!actual[i].addresses.SequenceEqual(expected[i].addresses))
            {
                throw new InvalidOperationException($"{label}[{i}].addresses mismatch");
            }
        }
    }

    public static void Main()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(MESSAGE);
        var message = new MessageParser().parse(bytes) ?? throw new InvalidOperationException("parse failed");

        // Parses addresses (including comments), lists and groups
        AssertEq(message.from()?.first(), new Addr("Art Vandelay (Vandelay Industries)", "art@vandelay.com"), "from");

        AssertGroupsEqual(message.to()?.as_group(), new List<Group>
        {
            new Group("Colleagues", new List<Addr> { new Addr("James Smythe", "james@vandelay.com") }),
            new Group("Friends", new List<Addr>
            {
                new Addr(null, "jane@example.com"),
                new Addr("John Smîth", "john@example.com"),
            }),
        }, "to");

        // RFC5322 date parsing, RFC3339 formatting
        AssertEq(message.date()?.to_rfc3339(), "2021-11-20T14:22:01-08:00", "date");

        // RFC2047 support for encoded text in message readers
        AssertEq(message.subject(), "Why not both importing AND exporting? ☺", "subject");

        // HTML and text body parts are returned conforming to RFC8621, Section 4.1.4
        AssertEq(message.body_html(0),
            "<html><p>I was thinking about quitting the &ldquo;exporting&rdquo; to " +
            "focus just on the &ldquo;importing&rdquo;,</p><p>but then I thought," +
            " why not do both? &#x263A;</p></html>",
            "body_html(0)");

        // HTML parts are converted to plain text (and viceversa) when missing
        AssertEq(message.body_text(0),
            "I was thinking about quitting the “exporting” to focus just on the" +
            " “importing”,\nbut then I thought, why not do both? ☺\n",
            "body_text(0)");

        // Supports nested messages as well as multipart/digest
        var nestedMessage = message.attachment(0)?.message() ?? throw new InvalidOperationException("nested message missing");

        AssertEq(nestedMessage.subject(), "Exporting my book about coffee tables", "nested subject");

        // Handles UTF-* as well as many legacy encodings
        AssertEq(nestedMessage.body_text(0), "ℌ𝔢𝔩𝔭 𝔪𝔢 𝔢𝔵𝔭𝔬𝔯𝔱 𝔪𝔶 𝔟𝔬𝔬𝔨 𝔭𝔩𝔢𝔞𝔰𝔢!", "nested body_text(0)");
        AssertEq(nestedMessage.body_html(0), "<html><body>ℌ𝔢𝔩𝔭 𝔪𝔢 𝔢𝔵𝔭𝔬𝔯𝔱 𝔪𝔶 𝔟𝔬𝔬𝔨 𝔭𝔩𝔢𝔞𝔰𝔢!</body></html>", "nested body_html(0)");

        var nestedAttachment = nestedMessage.attachment(0) ?? throw new InvalidOperationException("nested attachment missing");

        AssertEq(nestedAttachment.len(), 42, "nested_attachment.len()");

        // Full RFC2231 support for continuations and character sets
        AssertEq(nestedAttachment.attachment_name(), "Book about ☕ tables.gif", "nested_attachment.attachment_name()");

        // Integrates with System.Text.Json
        Console.WriteLine(JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true }));
    }
}
