/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/message.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 04eaa48c94796e8639a60076e6077f138298b257f8643691533544f8b5681500
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public delegate (int offset_end, byte[] bytes) DecodeFnc(byte[] boundary);

public partial class MessageParser
{
    private static List<Header> TakeHeaders(ref List<Header> headers)
    {
        var result = headers;
        headers = new List<Header>();
        return result;
    }
    private const int MAX_NESTED_ENCODED = 3;

    private enum MimeType
    {
        MultipartMixed,
        MultipartAlternative,
        MultipartRelated,
        MultipartDigest,
        TextPlain,
        TextHtml,
        TextOther,
        Inline,
        Message,
        Other,
    }

    private static (bool is_multipart, bool is_inline, bool is_text, MimeType mime_type) mime_type(ContentType? ct, MimeType parent)
    {
        if (ct != null)
        {
            switch (ct.c_type)
            {
                case "multipart":
                    return (true, false, false, ct.c_subtype switch
                    {
                        "mixed" => MimeType.MultipartMixed,
                        "alternative" => MimeType.MultipartAlternative,
                        "related" => MimeType.MultipartRelated,
                        "digest" => MimeType.MultipartDigest,
                        _ => MimeType.Other,
                    });
                case "text":
                    return ct.c_subtype switch
                    {
                        "plain" => (false, true, true, MimeType.TextPlain),
                        "html" => (false, true, true, MimeType.TextHtml),
                        _ => (false, false, true, MimeType.TextOther),
                    };
                case "image" or "audio" or "video":
                    return (false, true, false, MimeType.Inline);
                case "message" when ct.c_subtype is "rfc822" or "global":
                    return (false, false, false, MimeType.Message);
                default:
                    return (false, false, false, MimeType.Other);
            }
        }
        else if (parent == MimeType.MultipartDigest)
        {
            return (false, false, false, MimeType.Message);
        }
        else
        {
            return (false, true, true, MimeType.TextPlain);
        }
    }

    private class MessageParserState
    {
        public MimeType mime_type = MimeType.Message;
        public byte[]? mime_boundary = null;
        public bool in_alternative = false;
        public int parts = 0;
        public int html_parts = 0;
        public int text_parts = 0;
        public bool need_html_body = true;
        public bool need_text_body = true;
        public uint part_id = 0;
        public List<uint> sub_part_ids = new();
        public int offset_header = 0;
        public int offset_body = 0;
        public int offset_end = 0;
    }

    public Message? parse(byte[] raw_message)
    {
        return parse_(raw_message, MAX_NESTED_ENCODED, false);
    }

    public Message? parse_headers(byte[] raw_message)
    {
        return parse_(raw_message, MAX_NESTED_ENCODED, true);
    }

    private Message? parse_(byte[] raw_message, int depth, bool skip_body)
    {
        var stream = new MessageStream(raw_message);
        var message = new Message();
        var state = new MessageParserState();
        var state_stack = new Stack<(MessageParserState state, Message? prev_message)>();
        var part_headers = new List<Header>();

        while (true)
        {
            state.offset_header = stream.offset();
            if (!stream.parse_headers(this, part_headers))
            {
                break;
            }
            state.offset_body = stream.offset();
            if (skip_body)
            {
                break;
            }

            state.parts++;
            state.sub_part_ids.Add((uint)message.parts.Count);

            var content_type = part_headers.header_value(HeaderName.ContentType)?.as_content_type();
            var (is_multipart, is_inline, is_text, mtype) = mime_type(content_type, state.mime_type);

            if (is_multipart)
            {
                string? boundaryStr = content_type?.attribute("boundary");
                if (!string.IsNullOrEmpty(boundaryStr))
                {
                    byte[] boundary = System.Text.Encoding.ASCII.GetBytes(boundaryStr);
                    if (stream.seek_next_part(boundary))
                    {
                        int part_id = message.parts.Count;
                        var new_state = new MessageParserState
                        {
                            mime_type = mtype,
                            mime_boundary = boundary,
                            in_alternative = state.in_alternative || mtype == MimeType.MultipartAlternative,
                            html_parts = message.html_body.Count,
                            text_parts = message.text_body.Count,
                            need_html_body = state.need_html_body,
                            need_text_body = state.need_text_body,
                            part_id = (uint)part_id,
                            sub_part_ids = new List<uint>()
                        };

                        message.parts.Add(new MessagePart(
                            TakeHeaders(ref part_headers),
                            Encoding.None,
                            PartType.Multipart(new List<uint>()),
                            (uint)state.offset_header,
                            (uint)state.offset_body,
                            0,
                            false
                        ));
                        state_stack.Push((state, null));
                        state = new_state;
                        stream.skip_crlf();
                        continue;
                    }
                    else
                    {
                        mtype = MimeType.TextOther;
                        is_text = true;
                    }
                }
            }

            Encoding encoding = Encoding.None;
            var cteHeader = part_headers.header_value(HeaderName.ContentTransferEncoding)?.as_text();
            if (string.Equals(cteHeader, "base64", StringComparison.OrdinalIgnoreCase))
            {
                encoding = Encoding.Base64;
            }
            else if (string.Equals(cteHeader, "quoted-printable", StringComparison.OrdinalIgnoreCase))
            {
                encoding = Encoding.QuotedPrintable;
            }

            if (mtype == MimeType.Message && encoding == Encoding.None)
            {
                var new_state = new MessageParserState
                {
                    mime_type = MimeType.Message,
                    mime_boundary = state.mime_boundary,
                    need_html_body = true,
                    need_text_body = true,
                    part_id = (uint)message.parts.Count,
                    sub_part_ids = new List<uint>()
                };
                message.attachments.Add((uint)message.parts.Count);
                message.parts.Add(new MessagePart(
                    TakeHeaders(ref part_headers),
                    encoding,
                    PartType.Message(new Message()),
                    (uint)state.offset_header,
                    (uint)state.offset_body,
                    0,
                    false
                ));
                state_stack.Push((state, message));
                message = new Message();
                state = new_state;
                continue;
            }

            byte[]? bnd = state.mime_boundary;
            byte[] decodedBytes;
            int offset_end;
            switch (encoding)
            {
                case Encoding.Base64:
                    var (b64End, b64Bytes) = stream.decode_base64_mime(bnd ?? Array.Empty<byte>());
                    offset_end = b64End;
                    decodedBytes = b64Bytes;
                    break;
                case Encoding.QuotedPrintable:
                    var (qpEnd, qpBytes) = stream.decode_quoted_printable_mime(bnd ?? Array.Empty<byte>());
                    offset_end = qpEnd;
                    decodedBytes = qpBytes;
                    break;
                default:
                    var (oe, mb) = stream.mime_part(bnd ?? Array.Empty<byte>());
                    offset_end = oe;
                    decodedBytes = mb;
                    break;
            }
            byte[] bytes = decodedBytes;
            bool is_encoding_problem = offset_end == int.MaxValue;
            if (is_encoding_problem)
            {
                encoding = Encoding.None;
                if (mtype != MimeType.TextPlain)
                {
                    mtype = MimeType.TextOther;
                }
                is_inline = false;
                is_text = true;

                var (partEnd, boundaryFound) = stream.seek_part_end(state.mime_boundary);
                state.offset_end = partEnd;
                bytes = stream.bytes(state.offset_body, state.offset_end).ToArray();
                if (!boundaryFound)
                {
                    state.mime_boundary = null;
                }
            }
            else
            {
                state.offset_end = offset_end;
            }

            PartType body_part;
            if (mtype != MimeType.Message)
            {
                bool is_disp_att = part_headers.header_value(HeaderName.ContentDisposition)?.as_content_type()?.is_attachment() ?? false;
                // Rust: content_type.is_none_or(|c| !c.has_attribute("name")) -- true
                // when content_type is None (missing Content-Type header, the RFC 2045
                // default), not just when it's present-but-nameless. The prior
                // `content_type != null && ...` required a Content-Type header to be
                // present at all, so a part with no Content-Type header (a common case,
                // not an edge case) was never classified inline (PARITY-AUDIT.md FILE 20).
                bool is_inline_part = is_inline && !is_disp_att && (state.parts == 1 || state.mime_type != MimeType.MultipartRelated && (mtype == MimeType.Inline || (content_type == null || !content_type.has_attribute("name"))));

                is_inline_part = is_inline_part || (state.parts == 1 && state.mime_type == MimeType.Message && mtype == MimeType.TextPlain && is_encoding_problem);

                bool add_to_html = false;
                bool add_to_text = false;

                if (state.mime_type == MimeType.MultipartAlternative)
                {
                    if (mtype == MimeType.TextHtml) add_to_html = true;
                    else if (mtype == MimeType.TextPlain) add_to_text = true;
                }
                else if (is_inline_part)
                {
                    if (state.in_alternative && (state.need_text_body || state.need_html_body))
                    {
                        if (mtype == MimeType.TextHtml) state.need_text_body = false;
                        else if (mtype == MimeType.TextPlain) state.need_html_body = false;
                    }
                    add_to_html = state.need_html_body;
                    add_to_text = state.need_text_body;
                }

                if (add_to_html) message.html_body.Add((uint)message.parts.Count);
                if (add_to_text) message.text_body.Add((uint)message.parts.Count);

                if (is_text)
                {
                    string? charset = content_type?.attribute("charset");
                    string text = CharsetDecoderUtils.decode_charset(charset != null ? System.Text.Encoding.ASCII.GetBytes(charset) : "utf-8"u8.ToArray(), bytes);
                    bool is_html = mtype == MimeType.TextHtml;

                    if ((!add_to_html && is_html) || (!add_to_text && !is_html))
                    {
                        message.attachments.Add((uint)message.parts.Count);
                    }

                    body_part = is_html ? PartType.Html(text) : PartType.Text(text);
                }
                else
                {
                    message.attachments.Add((uint)message.parts.Count);
                    body_part = !is_inline_part ? PartType.Binary(bytes) : PartType.InlineBinary(bytes);
                }
            }
            else
            {
                message.attachments.Add((uint)message.parts.Count);
                if (depth != 0)
                {
                    var nested_message = parse_(bytes, depth - 1, false);
                    if (nested_message != null)
                    {
                        body_part = PartType.Message(nested_message);
                    }
                    else
                    {
                        is_encoding_problem = true;
                        body_part = PartType.Binary(bytes);
                    }
                }
                else
                {
                    is_encoding_problem = true;
                    body_part = PartType.Binary(bytes);
                }
            }

            message.parts.Add(new MessagePart(
                TakeHeaders(ref part_headers),
                encoding,
                body_part,
                (uint)state.offset_header,
                (uint)state.offset_body,
                (uint)state.offset_end,
                is_encoding_problem
            ));

            if (state.mime_boundary != null)
            {
                while (true)
                {
                    if (state.mime_type == MimeType.Message)
                    {
                        if (state_stack.Count > 0 && state_stack.Peek().prev_message != null)
                        {
                            var (prevState, prevMsg) = state_stack.Pop();
                            int nested_offset_end;
                            if (state.mime_boundary != null)
                            {
                                int pos = Math.Max(0, stream.offset() - (state.mime_boundary.Length + 2));
                                if (pos >= 2 && pos - 2 < raw_message.Length && raw_message[pos - 2] == (byte)'\r')
                                {
                                    nested_offset_end = pos - 2;
                                }
                                else
                                {
                                    nested_offset_end = pos > 0 ? pos - 1 : 0;
                                }
                            }
                            else
                            {
                                nested_offset_end = stream.offset();
                            }

                            message.raw_message = raw_message;
                            if (prevMsg!.parts.Count > (int)state.part_id)
                            {
                                var part = prevMsg.parts[(int)state.part_id];
                                part.offset_end = (uint)nested_offset_end;
                                if (message.parts.Count == 0)
                                {
                                    part.is_encoding_problem = true;
                                    part.body = PartType.Text(System.Text.Encoding.UTF8.GetString(raw_message.AsSpan((int)part.offset_body, (int)(part.offset_end - part.offset_body))));
                                }
                                else
                                {
                                    part.body = PartType.Message(message);
                                }
                            }
                            message = prevMsg;
                            prevState.mime_boundary = state.mime_boundary;
                            state = prevState;
                        }
                    }

                    if (stream.is_multipart_end())
                    {
                        if (state.mime_type == MimeType.MultipartAlternative && state.need_html_body && state.need_text_body)
                        {
                            if (state.text_parts == message.text_body.Count && state.html_parts != message.html_body.Count)
                            {
                                for (int i = state.html_parts; i < message.html_body.Count; i++)
                                    message.text_body.Add(message.html_body[i]);
                            }
                            if (state.html_parts == message.html_body.Count && state.text_parts != message.text_body.Count)
                            {
                                for (int i = state.text_parts; i < message.text_body.Count; i++)
                                    message.html_body.Add(message.text_body[i]);
                            }
                        }

                        if (message.parts.Count > (int)state.part_id)
                        {
                            var part = message.parts[(int)state.part_id];
                            if (state.sub_part_ids.Count != 1 || state.sub_part_ids[0] != 0)
                            {
                                part.body = PartType.Multipart(new List<uint>(state.sub_part_ids));
                            }

                            if (state_stack.Count > 0)
                            {
                                var (prevState, _) = state_stack.Pop();
                                state = prevState;

                                if (state.mime_boundary != null)
                                {
                                    var offsetOpt = stream.seek_next_part_offset(state.mime_boundary);
                                    if (offsetOpt.HasValue)
                                    {
                                        part.offset_end = (uint)offsetOpt.Value;
                                        continue;
                                    }
                                }
                            }

                            part.offset_end = (uint)stream.offset();
                        }

                        goto ParsingComplete;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else if (stream.offset() >= raw_message.Length)
            {
                break;
            }
        }

    ParsingComplete:
        while (state_stack.Count > 0)
        {
            var (prevState, prevMsg) = state_stack.Pop();
            if (prevMsg != null)
            {
                prevMsg.raw_message = raw_message;
                if (prevMsg.parts.Count > (int)state.part_id)
                {
                    var part = prevMsg.parts[(int)state.part_id];
                    part.offset_end = (uint)stream.offset();
                    if (message.parts.Count == 0)
                    {
                        part.is_encoding_problem = true;
                        part.body = PartType.Text(DecodeRecoveredMessage(raw_message, (int)part.offset_body, stream.offset(), state.mime_boundary));
                    }
                    else
                    {
                        part.body = PartType.Message(message);
                    }
                }
                message = prevMsg;
            }
            else if (message.parts.Count > (int)state.part_id)
            {
                var part = message.parts[(int)state.part_id];
                part.offset_end = (uint)stream.offset();
                if (state.sub_part_ids.Count != 1 || state.sub_part_ids[0] != 0)
                {
                    part.body = PartType.Multipart(new List<uint>(state.sub_part_ids));
                }
            }
            state = prevState;
        }

        message.raw_message = raw_message;
        if (message.parts.Count > 0)
        {
            message.parts[0].offset_end = (uint)raw_message.Length;
            return message;
        }
        else if (part_headers.Count > 0)
        {
            message.parts.Add(new MessagePart(
                part_headers,
                Encoding.None,
                PartType.Text(""),
                0,
                (uint)raw_message.Length,
                (uint)raw_message.Length,
                true
            ));
            return message;
        }

        return null;
    }

    private static string DecodeRecoveredMessage(byte[] rawMessage, int start, int end, byte[]? boundary)
    {
        var rawText = rawMessage.AsSpan(start, end - start);
        try
        {
            var text = new UTF8Encoding(false, true).GetString(rawText);
            if (boundary is { Length: > 0 })
            {
                var boundaryText = System.Text.Encoding.UTF8.GetString(boundary);
                var boundaryIndex = text.LastIndexOf(boundaryText, StringComparison.Ordinal);
                if (boundaryIndex >= 2 && text.AsSpan(0, boundaryIndex).EndsWith("--", StringComparison.Ordinal))
                {
                    text = text[..(boundaryIndex - 2)];
                    if (text.EndsWith('\n')) text = text[..^1];
                    if (text.EndsWith('\r')) text = text[..^1];
                }
            }
            return text;
        }
        catch (DecoderFallbackException)
        {
            return System.Text.Encoding.UTF8.GetString(rawText);
        }
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class message_tests
{
    private static byte[] add_crlf(byte[] bytes)
    {
        var result = new List<byte>(bytes.Length);
        byte last_ch = 0;
        foreach (byte ch in bytes)
        {
            if (ch == (byte)'\n' && last_ch != (byte)'\r')
            {
                result.Add((byte)'\r');
            }
            result.Add(ch);
            last_ch = ch;
        }
        return result.ToArray();
    }

    private static byte[] strip_crlf(byte[] bytes)
    {
        var result = new List<byte>(bytes.Length);
        foreach (byte ch in bytes)
        {
            if (ch != (byte)'\r')
            {
                result.Add(ch);
            }
        }
        return result.ToArray();
    }

    [TestMethod]
    public void parse_full_messages()
    {
        var expectedCounts = new Dictionary<string, int>
        {
            ["rfc"] = 10,
            ["legacy"] = 54,
            ["thirdparty"] = 20,
            ["malformed"] = 23,
        };

        int totalInputs = 0;
        int totalJsonComparisons = 0;
        int totalRoundTrips = 0;

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            NewLine = "\n",
            Encoder = SerdeJsonEncoder.Instance,
            Converters =
            {
                new JsonStringEnumConverter(),
                new AttributeJsonConverter(),
                new IPAddressJsonConverter(),
                new AddressJsonConverter(),
                new HostJsonConverter(),
                new PartTypeJsonConverter(),
                new HeaderValueJsonConverter(),
                new HeaderNameJsonConverter(),
            }
        };

        foreach (var (testSuite, expectedCount) in expectedCounts)
        {
            string testDir = Path.Combine(AppContext.BaseDirectory, "resources", "eml", testSuite);
            Assert.IsTrue(Directory.Exists(testDir), $"Directory not found: {testDir}");

            var emlFiles = Directory.GetFiles(testDir, "*.eml");
            Array.Sort(emlFiles, StringComparer.Ordinal);
            Assert.AreEqual(expectedCount, emlFiles.Length, $"Suite {testSuite} count mismatch");

            int testsRun = 0;

            foreach (var emlFile in emlFiles)
            {
                byte[] rawOriginal = File.ReadAllBytes(emlFile);
                testsRun++;
                totalInputs++;

                string baseName = Path.ChangeExtension(emlFile, null);
                string jsonFile = baseName + ".json";
                string crlfJsonFile = baseName + ".crlf.json";

                // Test without CRs
                byte[] rawMessage = strip_crlf(rawOriginal);
                byte[] expectedResult = File.ReadAllBytes(jsonFile);

                var message = new MessageParser().parse(rawMessage);
                Assert.IsNotNull(message, $"Failed to parse {emlFile}");

                string jsonMessage = JsonSerializer.Serialize(message, jsonOptions);
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonMessage);

                // Round trip deserialization test
                var deserialized = JsonSerializer.Deserialize<Message>(jsonMessage, jsonOptions);
                Assert.IsNotNull(deserialized, $"Failed to deserialize {jsonFile}");
                totalRoundTrips++;

                if (!jsonBytes.AsSpan().SequenceEqual(expectedResult))
                {
                    string failedDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "failed_fixtures", testSuite);
                    Directory.CreateDirectory(failedDir);
                    File.WriteAllBytes(Path.Combine(failedDir, Path.GetFileName(baseName) + ".failed.json"), jsonBytes);
                    Assert.Fail($"JSON comparison failed for {jsonFile}");
                }
                totalJsonComparisons++;

                // Test with CRs
                byte[] rawMessageCrlf = add_crlf(rawOriginal);
                byte[] expectedResultCrlf = File.ReadAllBytes(crlfJsonFile);

                var messageCrlf = new MessageParser().parse(rawMessageCrlf);
                Assert.IsNotNull(messageCrlf, $"Failed to parse CRLF {emlFile}");

                string jsonMessageCrlf = JsonSerializer.Serialize(messageCrlf, jsonOptions);
                byte[] jsonBytesCrlf = System.Text.Encoding.UTF8.GetBytes(jsonMessageCrlf);

                if (!jsonBytesCrlf.AsSpan().SequenceEqual(expectedResultCrlf))
                {
                    string failedDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "failed_fixtures", testSuite);
                    Directory.CreateDirectory(failedDir);
                    File.WriteAllBytes(Path.Combine(failedDir, Path.GetFileName(baseName) + ".crlf.failed.json"), jsonBytesCrlf);
                    Assert.Fail($"JSON CRLF comparison failed for {crlfJsonFile}");
                }
                totalJsonComparisons++;
            }

            Assert.IsTrue(testsRun > 0, $"Did not find any tests to run in folder {testDir}.");
        }

        Assert.AreEqual(107, totalInputs, "Total inputs count mismatch");
        Assert.AreEqual(214, totalJsonComparisons, "Total JSON comparisons count mismatch");
        Assert.AreEqual(107, totalRoundTrips, "Total round trips count mismatch");
    }

    [TestMethod]
    public void part_with_no_content_type_is_inline_matches_rust()
    {
        // Rust: content_type.is_none_or(|c| !c.has_attribute("name")) is true when
        // content_type is None -- a non-first MIME part with NO Content-Type header at
        // all (the RFC 2045 default, routed through mime_type()'s None branch) must
        // still be classified inline under a non-multipart/related parent, not silently
        // demoted to a plain attachment (PARITY-AUDIT.md FILE 20).
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(
            "Content-Type: multipart/mixed; boundary=\"b\"\r\n\r\n" +
            "--b\r\nContent-Type: text/plain\r\n\r\nfirst part\r\n" +
            "--b\r\n\r\nsecond part, no Content-Type header\r\n" +
            "--b--\r\n");
        var msg = new MessageParser().parse(raw);
        Assert.IsNotNull(msg);
        // The second part (no Content-Type) must be picked up as a text body candidate,
        // not pushed into attachments -- that's the actual fix. (Both parts land in
        // text_body_count() here since neither is under a multipart/alternative parent,
        // so there's no exclusivity between the html/text candidate lists -- that part
        // is unrelated to this fix and is not what's being asserted.)
        Assert.AreEqual(2, msg!.text_body_count());
        Assert.AreEqual(0, msg.attachment_count());
    }
}
#endif
