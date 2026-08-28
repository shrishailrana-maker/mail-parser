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
using System.Text;
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

    public static void Main()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(MESSAGE);
        var message = new MessageParser().parse(bytes);
        if (message != null)
        {
            Console.WriteLine($"From: {message.from()?.first()?.address}");
            Console.WriteLine($"Subject: {message.subject()}");
        }
    }
}
