/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: tests/integration_test.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 860698372fe080e16c8826f0b1b7d85bebaf0ed15905ef5a3ef60b21e473f751
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stalwart.MailParser.Port.Tests;

[TestClass]
public class IntegrationTests
{
    private static readonly byte[] MESSAGE = System.Text.Encoding.UTF8.GetBytes(
        "From: Art Vandelay <art@vandelay.com> (Vandelay Industries)\n" +
        "To: \"Colleagues\": \"James Smythe\" <james@vandelay.com>; Friends:\n" +
        "    jane@example.com, =?UTF-8?Q?John_Sm=C3=AEth?= <john@example.com>;\n" +
        "Date: Sat, 20 Nov 2021 14:22:01 -0800\n" +
        "Subject: Why not both importing AND exporting? =?utf-8?b?4pi6?=\n" +
        "Content-Type: multipart/mixed; boundary=\"festivus\";\n\n" +
        "--festivus\n" +
        "Content-Type: text/html; charset=\"us-ascii\"\n" +
        "Content-Transfer-Encoding: base64\n\n" +
        "PGh0bWw+PHA+SSB3YXMgdGhpbmtpbmcgYWJvdXQgcXVpdHRpbmcgdGhlICZsZHF1bztle\n" +
        "HBvcnRpbmcmcmRxdW87IHRvIGZvY3VzIGp1c3Qgb24gdGhlICZsZHF1bztpbXBvcnRpbm\n" +
        "cmcmRxdW87LDwvcD48cD5idXQgdGhlbiBJIHRob3VnaHQsIHdoeSBub3QgZG8gYm90aD8\n" +
        "gJiN4MjYzQTs8L3A+PC9odG1sPg==\n" +
        "--festivus\n" +
        "Content-Type: message/rfc822\n\n" +
        "From: \"Cosmo Kramer\" <kramer@kramerica.com>\n" +
        "Subject: Exporting my book about coffee tables\n" +
        "Content-Type: multipart/mixed; boundary=\"giddyup\";\n\n" +
        "--giddyup\n" +
        "Content-Type: text/plain; charset=\"utf-16\"\n" +
        "Content-Transfer-Encoding: quoted-printable\n\n" +
        "=FF=FE=0C!5=D8\"=DD5=D8)=DD5=D8-=DD =005=D8*=DD5=D8\"=DD =005=D8\"=\n" +
        "=DD5=D85=DD5=D8-=DD5=D8,=DD5=D8/=DD5=D81=DD =005=D8*=DD5=D86=DD =\n" +
        "=005=D8=1F=DD5=D8,=DD5=D8,=DD5=D8(=DD =005=D8-=DD5=D8)=DD5=D8\"=\n" +
        "=DD5=D8=1E=DD5=D80=DD5=D8\"=DD!=00\n" +
        "--giddyup\n" +
        "Content-Type: image/gif; name*1=\"about \"; name*0=\"Book \";\n" +
        "              name*2*=utf-8''%e2%98%95 tables.gif\n" +
        "Content-Transfer-Encoding: Base64\n" +
        "Content-Disposition: attachment\n\n" +
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\n" +
        "--giddyup--\n" +
        "--festivus--\n"
    );

    private static void AssertHeaderEqual(Header expected, Header actual)
    {
        Assert.AreEqual(expected.name, actual.name);
        AssertHeaderValueEqual(expected.value, actual.value);
        Assert.AreEqual(expected.offset_field, actual.offset_field);
        Assert.AreEqual(expected.offset_start, actual.offset_start);
        Assert.AreEqual(expected.offset_end, actual.offset_end);
    }

    private static void AssertHeadersEqual(IList<Header> expected, IList<Header> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertHeaderEqual(expected[i], actual[i]);
        }
    }

    private static void AssertContentTypeEqual(ContentType? expected, ContentType? actual)
    {
        if (expected == null)
        {
            Assert.IsNull(actual);
            return;
        }
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.c_type, actual.c_type);
        Assert.AreEqual(expected.c_subtype, actual.c_subtype);
        if (expected.attributes == null)
        {
            Assert.IsNull(actual.attributes);
        }
        else
        {
            Assert.IsNotNull(actual.attributes);
            Assert.AreEqual(expected.attributes.Count, actual.attributes.Count);
            for (int i = 0; i < expected.attributes.Count; i++)
            {
                Assert.AreEqual(expected.attributes[i].name, actual.attributes[i].name);
                Assert.AreEqual(expected.attributes[i].value, actual.attributes[i].value);
            }
        }
    }

    private static void AssertAddressEqual(Address? expected, Address? actual)
    {
        if (expected == null)
        {
            Assert.IsNull(actual);
            return;
        }
        Assert.IsNotNull(actual);
        switch (expected)
        {
            case Address.ListRecord lrExpected:
                Assert.IsInstanceOfType(actual, typeof(Address.ListRecord));
                var lrActual = (Address.ListRecord)actual;
                Assert.AreEqual(lrExpected.Value.Count, lrActual.Value.Count);
                for (int i = 0; i < lrExpected.Value.Count; i++)
                {
                    Assert.AreEqual(lrExpected.Value[i].name, lrActual.Value[i].name);
                    Assert.AreEqual(lrExpected.Value[i].address, lrActual.Value[i].address);
                }
                break;
            case Address.GroupRecord grExpected:
                Assert.IsInstanceOfType(actual, typeof(Address.GroupRecord));
                var grActual = (Address.GroupRecord)actual;
                Assert.AreEqual(grExpected.Value.Count, grActual.Value.Count);
                for (int i = 0; i < grExpected.Value.Count; i++)
                {
                    Assert.AreEqual(grExpected.Value[i].name, grActual.Value[i].name);
                    Assert.AreEqual(grExpected.Value[i].addresses.Count, grActual.Value[i].addresses.Count);
                    for (int j = 0; j < grExpected.Value[i].addresses.Count; j++)
                    {
                        Assert.AreEqual(grExpected.Value[i].addresses[j].name, grActual.Value[i].addresses[j].name);
                        Assert.AreEqual(grExpected.Value[i].addresses[j].address, grActual.Value[i].addresses[j].address);
                    }
                }
                break;
        }
    }

    private static void AssertReceivedEqual(Received? expected, Received? actual)
    {
        if (expected == null)
        {
            Assert.IsNull(actual);
            return;
        }
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.from, actual.from);
        Assert.AreEqual(expected.from_ip, actual.from_ip);
        Assert.AreEqual(expected.from_iprev, actual.from_iprev);
        Assert.AreEqual(expected.by, actual.by);
        Assert.AreEqual(expected.for_, actual.for_);
        Assert.AreEqual(expected.with, actual.with);
        Assert.AreEqual(expected.tls_version, actual.tls_version);
        Assert.AreEqual(expected.tls_cipher, actual.tls_cipher);
        Assert.AreEqual(expected.id, actual.id);
        Assert.AreEqual(expected.ident, actual.ident);
        Assert.AreEqual(expected.helo, actual.helo);
        Assert.AreEqual(expected.helo_cmd, actual.helo_cmd);
        Assert.AreEqual(expected.via, actual.via);
        Assert.AreEqual(expected.date, actual.date);
    }

    private static void AssertHeaderValueEqual(HeaderValue expected, HeaderValue actual)
    {
        switch (expected)
        {
            case HeaderValue.TextRecord trExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.TextRecord));
                Assert.AreEqual(trExpected.Value, ((HeaderValue.TextRecord)actual).Value);
                break;
            case HeaderValue.TextListRecord tlrExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.TextListRecord));
                CollectionAssert.AreEqual(tlrExpected.Value, ((HeaderValue.TextListRecord)actual).Value);
                break;
            case HeaderValue.DateTimeRecord dtrExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.DateTimeRecord));
                Assert.AreEqual(dtrExpected.Value, ((HeaderValue.DateTimeRecord)actual).Value);
                break;
            case HeaderValue.ContentTypeRecord ctrExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.ContentTypeRecord));
                var ctrActual = (HeaderValue.ContentTypeRecord)actual;
                AssertContentTypeEqual(ctrExpected.Value, ctrActual.Value);
                break;
            case HeaderValue.AddressRecord arExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.AddressRecord));
                AssertAddressEqual(arExpected.Value, ((HeaderValue.AddressRecord)actual).Value);
                break;
            case HeaderValue.ReceivedRecord rrExpected:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.ReceivedRecord));
                AssertReceivedEqual(rrExpected.Value, ((HeaderValue.ReceivedRecord)actual).Value);
                break;
            case HeaderValue.EmptyRecord:
                Assert.IsInstanceOfType(actual, typeof(HeaderValue.EmptyRecord));
                break;
            default:
                Assert.AreEqual(expected, actual);
                break;
        }
    }

    private static void AssertPartTypeEqual(PartType expected, PartType actual)
    {
        switch (expected)
        {
            case PartType.TextRecord tr:
                Assert.IsInstanceOfType(actual, typeof(PartType.TextRecord));
                Assert.AreEqual(tr.Value, ((PartType.TextRecord)actual).Value);
                break;
            case PartType.HtmlRecord hr:
                Assert.IsInstanceOfType(actual, typeof(PartType.HtmlRecord));
                Assert.AreEqual(hr.Value, ((PartType.HtmlRecord)actual).Value);
                break;
            case PartType.BinaryRecord br:
                Assert.IsInstanceOfType(actual, typeof(PartType.BinaryRecord));
                CollectionAssert.AreEqual(br.Value, ((PartType.BinaryRecord)actual).Value);
                break;
            case PartType.InlineBinaryRecord ibr:
                Assert.IsInstanceOfType(actual, typeof(PartType.InlineBinaryRecord));
                CollectionAssert.AreEqual(ibr.Value, ((PartType.InlineBinaryRecord)actual).Value);
                break;
            case PartType.MultipartRecord mpr:
                Assert.IsInstanceOfType(actual, typeof(PartType.MultipartRecord));
                CollectionAssert.AreEqual(mpr.Value, ((PartType.MultipartRecord)actual).Value);
                break;
            case PartType.MessageRecord mr:
                Assert.IsInstanceOfType(actual, typeof(PartType.MessageRecord));
                AssertMessageEqual(mr.Value, ((PartType.MessageRecord)actual).Value);
                break;
        }
    }

    private static void AssertMessagePartEqual(MessagePart expected, MessagePart actual)
    {
        AssertHeadersEqual(expected.headers, actual.headers);
        Assert.AreEqual(expected.encoding, actual.encoding);
        Assert.AreEqual(expected.offset_header, actual.offset_header);
        Assert.AreEqual(expected.offset_body, actual.offset_body);
        Assert.AreEqual(expected.offset_end, actual.offset_end);
        Assert.AreEqual(expected.is_encoding_problem, actual.is_encoding_problem);
        AssertPartTypeEqual(expected.body, actual.body);
    }

    private static void AssertMessageEqual(Message expected, Message actual)
    {
        CollectionAssert.AreEqual(expected.html_body, actual.html_body);
        CollectionAssert.AreEqual(expected.text_body, actual.text_body);
        CollectionAssert.AreEqual(expected.attachments, actual.attachments);
        CollectionAssert.AreEqual(expected.raw_message, actual.raw_message);
        Assert.AreEqual(expected.parts.Count, actual.parts.Count);
        for (int i = 0; i < expected.parts.Count; i++)
        {
            AssertMessagePartEqual(expected.parts[i], actual.parts[i]);
        }
    }

    [TestMethod]
    public void test_api()
    {
        byte[] input = MESSAGE;

        // Default parser
        var message = new MessageParser().parse(input);
        Assert.IsNotNull(message);
        var headers = new MessageParser().parse_headers(input);
        Assert.IsNotNull(headers);
        var custom_message = new MessageParser()
            .with_minimal_headers()
            .parse(input);
        Assert.IsNotNull(custom_message);

        AssertHeadersEqual(message.headers(), headers.headers());
        AssertHeadersEqual(message.headers(), custom_message.headers());
        Assert.AreEqual(3, message.parts.Count);
        Assert.AreEqual(1, headers.parts.Count);
        Assert.AreEqual(message.parts.Count, custom_message.parts.Count);
        for (int i = 0; i < message.parts.Count; i++)
        {
            AssertMessagePartEqual(message.parts[i], custom_message.parts[i]);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(message.parts[0].headers);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<Header>>(json);
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(message.parts[0].headers.Count, deserialized.Count);
        for (int i = 0; i < message.parts[0].headers.Count; i++)
        {
            AssertHeaderEqual(message.parts[0].headers[i], deserialized[i]);
        }

        var fromAddr = message.from()?.first();
        Assert.IsNotNull(fromAddr);
        Assert.AreEqual("Art Vandelay (Vandelay Industries)", fromAddr.name);
        Assert.AreEqual("art@vandelay.com", fromAddr.address);

        var toGroup = message.to()?.as_group();
        Assert.IsNotNull(toGroup);
        Assert.AreEqual(2, toGroup.Count);
        Assert.AreEqual("Colleagues", toGroup[0].name);
        Assert.AreEqual(1, toGroup[0].addresses.Count);
        Assert.AreEqual("James Smythe", toGroup[0].addresses[0].name);
        Assert.AreEqual("james@vandelay.com", toGroup[0].addresses[0].address);
        Assert.AreEqual("Friends", toGroup[1].name);
        Assert.AreEqual(2, toGroup[1].addresses.Count);
        Assert.IsNull(toGroup[1].addresses[0].name);
        Assert.AreEqual("jane@example.com", toGroup[1].addresses[0].address);
        Assert.AreEqual("John Smîth", toGroup[1].addresses[1].name);
        Assert.AreEqual("john@example.com", toGroup[1].addresses[1].address);

        Assert.AreEqual("2021-11-20T14:22:01-08:00", message.date()?.to_rfc3339());
        Assert.AreEqual("Why not both importing AND exporting? ☺", message.subject());

        Assert.AreEqual(
            "<html><p>I was thinking about quitting the &ldquo;exporting&rdquo; to focus just on the &ldquo;importing&rdquo;,</p><p>but then I thought, why not do both? &#x263A;</p></html>",
            message.body_html(0)
        );

        Assert.AreEqual(
            "I was thinking about quitting the “exporting” to focus just on the “importing”,\nbut then I thought, why not do both? ☺\n",
            message.body_text(0)
        );

        var nested_message = message.attachment(0)?.message();
        Assert.IsNotNull(nested_message);
        Assert.AreEqual("Exporting my book about coffee tables", nested_message.subject());

        Assert.AreEqual("ℌ𝔢𝔩𝔭 𝔪𝔢 𝔢𝔵𝔭𝔬𝔯𝔱 𝔪𝔶 𝔟𝔬𝔬𝔨 𝔭𝔩𝔢𝔞𝔰𝔢!", nested_message.body_text(0));
        Assert.AreEqual("<html><body>ℌ𝔢𝔩𝔭 𝔪𝔢 𝔢𝔵𝔭𝔬𝔯𝔱 𝔪𝔶 𝔟𝔬𝔬𝔨 𝔭𝔩𝔢𝔞𝔰𝔢!</body></html>", nested_message.body_html(0));

        var nested_attachment = nested_message.attachment(0);
        Assert.IsNotNull(nested_attachment);
        Assert.AreEqual(42, nested_attachment.len());
        Assert.AreEqual("Book about ☕ tables.gif", nested_attachment.attachment_name());
    }
}
#endif
