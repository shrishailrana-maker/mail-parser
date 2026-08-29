/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/content_type.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: d3c03e8033fd8f73c5a9bd530f9d43f5fc4136345751aa9a1f4024d1a73b4aed
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public partial class MessageStream
{
    private enum ContentState
    {
        Type,
        SubType,
        AttributeName,
        AttributeValue,
        AttributeQuotedValue,
        Comment,
    }

    private class Continuation : IComparable<Continuation>
    {
        public string key;
        public uint position;
        public string value;

        public Continuation(string key, uint position, string value)
        {
            this.key = key;
            this.position = position;
            this.value = value;
        }

        public int CompareTo(Continuation? other)
        {
            if (other == null) return 1;
            int kc = string.CompareOrdinal(key, other.key);
            return kc != 0 ? kc : position.CompareTo(other.position);
        }
    }

    private class ContentTypeParser
    {
        public ContentState state = ContentState.Type;
        public Stack<ContentState> state_stack = new();

        public string? c_type = null;
        public string? c_subtype = null;

        public string? attr_name = null;
        public string? attr_charset = null;
        public uint attr_position = 0;

        public List<string> values = new();
        public List<Attribute> attributes = new();
        public List<Continuation>? continuations = null;

        public int token_start = 0;
        public int token_end = 0;

        public bool is_continuation = false;
        public bool is_encoded_attribute = false;
        public bool is_escaped = false;
        public bool remove_crlf = false;
        public bool is_lower_case = true;
        public bool is_token_start = true;

        public void reset_parser()
        {
            token_start = 0;
            is_token_start = true;
        }

        public bool add_attribute(MessageStream stream)
        {
            if (token_start > 0)
            {
                var bytes = stream.bytes_span(token_start - 1, token_end);
                string attr = System.Text.Encoding.UTF8.GetString(bytes);
                if (!is_lower_case)
                {
                    attr = HeaderExtensions.ToAsciiLowercase(attr);
                    is_lower_case = true;
                }

                switch (state)
                {
                    case ContentState.AttributeName:
                        attr_name = attr;
                        break;
                    case ContentState.Type:
                        c_type = attr;
                        break;
                    case ContentState.SubType:
                        c_subtype = attr;
                        break;
                }

                reset_parser();
                return true;
            }
            return false;
        }

        public void add_attribute_parameter(MessageStream stream)
        {
            if (token_start > 0)
            {
                var bytes = stream.bytes_span(token_start - 1, token_end);
                string attr_part = System.Text.Encoding.UTF8.GetString(bytes);

                if (attr_charset == null)
                {
                    attr_charset = attr_part;
                }
                else
                {
                    string name = (attr_name ?? "unknown") + "-language";
                    if (!attributes.Any(a => a.name == name))
                    {
                        attributes.Add(new Attribute(name, attr_part));
                    }
                    else
                    {
                        values.Add("'");
                        values.Add(attr_part);
                    }
                }

                reset_parser();
            }
        }

        public void add_partial_value(MessageStream stream, bool to_cur_pos)
        {
            if (token_start > 0)
            {
                bool in_quote = state == ContentState.AttributeQuotedValue;
                int end = in_quote && to_cur_pos ? stream.offset() - 1 : token_end;
                var bytes = stream.bytes_span(token_start - 1, end);
                values.Add(System.Text.Encoding.UTF8.GetString(bytes));
                if (!in_quote)
                {
                    values.Add(" ");
                }
                reset_parser();
            }
        }

        public void add_value(MessageStream stream)
        {
            if (attr_name == null) return;

            bool has_values = values.Count > 0;
            string? value = null;
            if (token_start > 0)
            {
                var bytes = stream.bytes_span(token_start - 1, token_end);
                if (!remove_crlf)
                {
                    value = System.Text.Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    remove_crlf = false;
                    var filtered = new List<byte>(bytes.Length);
                    foreach (byte b in bytes)
                    {
                        if (b != (byte)'\r' && b != (byte)'\n') filtered.Add(b);
                    }
                    value = System.Text.Encoding.UTF8.GetString(filtered.ToArray());
                }
            }
            else
            {
                if (!has_values) return;
            }

            if (!is_continuation)
            {
                string finalVal = !has_values ? value! : (value != null ? string.Concat(values) + value : string.Concat(values));
                attributes.Add(new Attribute(attr_name, finalVal));
                attr_name = null;
            }
            else
            {
                string aName = attr_name;
                attr_name = null;
                string val = value != null ? (has_values ? string.Concat(values) + value : value) : string.Concat(values);

                if (is_encoded_attribute)
                {
                    var (success, decoded) = HexUtils.decode_hex(System.Text.Encoding.UTF8.GetBytes(val));
                    if (success)
                    {
                        var decoder = attr_charset != null ? CharsetMapUtils.charset_decoder(System.Text.Encoding.ASCII.GetBytes(attr_charset)) : null;
                        val = decoder != null ? decoder(decoded) : System.Text.Encoding.UTF8.GetString(decoded);
                    }
                    is_encoded_attribute = false;
                }

                if (attr_position > 0)
                {
                    var cont = new Continuation(aName, attr_position, val);
                    continuations ??= new List<Continuation>();
                    continuations.Add(cont);
                    attr_position = 0;
                }
                else
                {
                    attributes.Add(new Attribute(aName, val));
                }
                is_continuation = false;
                attr_charset = null;
            }

            if (has_values)
            {
                values.Clear();
            }

            reset_parser();
        }

        public bool add_attr_position(MessageStream stream)
        {
            if (token_start > 0)
            {
                var bytes = stream.bytes_span(token_start - 1, token_end);
                string str = System.Text.Encoding.UTF8.GetString(bytes);
                uint.TryParse(str, out attr_position);
                reset_parser();
                return true;
            }
            return false;
        }

        public void merge_continuations()
        {
            if (continuations == null) return;
            continuations.Sort();
            foreach (var cont in continuations)
            {
                var old = attributes.FirstOrDefault(a => a.name == cont.key);
                if (old != null)
                {
                    old.value = old.value + cont.value;
                }
                else
                {
                    attributes.Add(new Attribute(cont.key, cont.value));
                }
            }
            continuations.Clear();
        }
    }

    // Rust: MessageStream::parse_content_type
    public HeaderValue parse_content_type()
    {
        var parser = new ContentTypeParser();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)' ' or (byte)'\t':
                    if (!parser.is_token_start) parser.is_token_start = true;
                    if (parser.state == ContentState.AttributeQuotedValue)
                    {
                        if (parser.token_start == 0)
                        {
                            parser.token_start = offset();
                            parser.token_end = parser.token_start;
                        }
                        else
                        {
                            parser.token_end = offset();
                        }
                    }
                    continue;
                case >= (byte)'A' and <= (byte)'Z' when parser.is_lower_case:
                    if (parser.state is ContentState.Type or ContentState.SubType or ContentState.AttributeName)
                    {
                        parser.is_lower_case = false;
                    }
                    break;
                case (byte)'\n':
                    bool next_is_space = peek_next_is_space();
                    switch (parser.state)
                    {
                        case ContentState.Type or ContentState.AttributeName or ContentState.SubType:
                            parser.add_attribute(this);
                            break;
                        case ContentState.AttributeValue:
                            parser.add_value(this);
                            break;
                        case ContentState.AttributeQuotedValue:
                            if (next_is_space)
                            {
                                next();
                                parser.remove_crlf = true;
                                continue;
                            }
                            else
                            {
                                parser.add_value(this);
                            }
                            break;
                    }

                    if (next_is_space)
                    {
                        if (parser.state == ContentState.Type) continue;
                        parser.state = ContentState.AttributeName;
                        next();
                        if (!parser.is_token_start) parser.is_token_start = true;
                        continue;
                    }
                    else
                    {
                        if (parser.continuations != null)
                        {
                            parser.merge_continuations();
                        }

                        if (parser.c_type != null)
                        {
                            return HeaderValue.ContentType(new ContentType(
                                parser.c_type,
                                parser.c_subtype,
                                parser.attributes.Count > 0 ? parser.attributes : null
                            ));
                        }
                        return HeaderValue.Empty;
                    }
                case (byte)'/' when parser.state == ContentState.Type:
                    parser.add_attribute(this);
                    parser.state = ContentState.SubType;
                    continue;
                case (byte)';':
                    switch (parser.state)
                    {
                        case ContentState.Type or ContentState.SubType or ContentState.AttributeName:
                            parser.add_attribute(this);
                            parser.state = ContentState.AttributeName;
                            continue;
                        case ContentState.AttributeValue:
                            if (!parser.is_escaped)
                            {
                                parser.add_value(this);
                                parser.state = ContentState.AttributeName;
                            }
                            else
                            {
                                parser.is_escaped = false;
                            }
                            continue;
                    }
                    break;
                case (byte)'*' when parser.state == ContentState.AttributeName:
                    if (!parser.is_continuation)
                    {
                        parser.is_continuation = parser.add_attribute(this);
                    }
                    else if (!parser.is_encoded_attribute)
                    {
                        parser.add_attr_position(this);
                        parser.is_encoded_attribute = true;
                    }
                    else
                    {
                        parser.reset_parser();
                    }
                    continue;
                case (byte)'=':
                    switch (parser.state)
                    {
                        case ContentState.AttributeName:
                            if (!parser.is_continuation)
                            {
                                if (!parser.add_attribute(this)) continue;
                            }
                            else if (!parser.is_encoded_attribute)
                            {
                                parser.is_encoded_attribute = !parser.add_attr_position(this);
                            }
                            else
                            {
                                parser.reset_parser();
                            }
                            parser.state = ContentState.AttributeValue;
                            continue;
                        case ContentState.AttributeValue or ContentState.AttributeQuotedValue when parser.is_token_start && peek_char((byte)'?'):
                            checkpoint();
                            var token = decode_rfc2047();
                            if (token != null)
                            {
                                parser.add_partial_value(this, false);
                                parser.values.Add(token);
                                continue;
                            }
                            restore();
                            break;
                    }
                    break;
                case (byte)'"':
                    switch (parser.state)
                    {
                        case ContentState.AttributeValue:
                            if (!parser.is_token_start) parser.is_token_start = true;
                            parser.state = ContentState.AttributeQuotedValue;
                            continue;
                        case ContentState.AttributeQuotedValue:
                            if (!parser.is_escaped)
                            {
                                parser.add_value(this);
                                parser.state = ContentState.AttributeName;
                                continue;
                            }
                            else
                            {
                                parser.is_escaped = false;
                            }
                            break;
                        default:
                            continue;
                    }
                    break;
                case (byte)'\\':
                    switch (parser.state)
                    {
                        case ContentState.AttributeQuotedValue or ContentState.AttributeValue:
                            if (!parser.is_escaped)
                            {
                                parser.add_partial_value(this, true);
                                parser.is_escaped = true;
                                continue;
                            }
                            else
                            {
                                parser.is_escaped = false;
                            }
                            break;
                        case ContentState.Comment:
                            parser.is_escaped = !parser.is_escaped;
                            break;
                        default:
                            continue;
                    }
                    break;
                case (byte)'\'' when parser.is_encoded_attribute && !parser.is_escaped && (parser.state is ContentState.AttributeValue or ContentState.AttributeQuotedValue):
                    parser.add_attribute_parameter(this);
                    continue;
                case (byte)'(' when parser.state != ContentState.AttributeQuotedValue:
                    if (!parser.is_escaped)
                    {
                        switch (parser.state)
                        {
                            case ContentState.Type:
                            case ContentState.AttributeName:
                            case ContentState.SubType:
                                parser.add_attribute(this);
                                break;
                            case ContentState.AttributeValue:
                                parser.add_value(this);
                                break;
                        }
                        parser.state_stack.Push(parser.state);
                        parser.state = ContentState.Comment;
                    }
                    else
                    {
                        parser.is_escaped = false;
                    }
                    continue;
                case (byte)')' when parser.state == ContentState.Comment:
                    if (!parser.is_escaped)
                    {
                        parser.state = parser.state_stack.Pop();
                        parser.reset_parser();
                    }
                    else
                    {
                        parser.is_escaped = false;
                    }
                    continue;
                case (byte)'\r':
                    continue;
            }

            if (parser.is_escaped) parser.is_escaped = false;
            if (parser.is_token_start) parser.is_token_start = false;

            if (parser.token_start == 0)
            {
                parser.token_start = offset();
                parser.token_end = parser.token_start;
            }
            else
            {
                parser.token_end = offset();
            }
        }

        // Rust's loop only ever returns a real ContentType from inside the '\n' match arm
        // (when a trailing newline is actually seen); if the loop exits because input ran
        // out first, Rust falls straight through to Empty here -- no finalization of
        // whatever was in progress. The finalization block that used to be here (fixing up
        // the in-progress attribute/value and returning a real ContentType anyway) had no
        // Rust counterpart and was a confirmed bug (PARITY-AUDIT.md FILE 15): parsing the
        // literal bytes "text/plain" with no trailing newline must return Empty, not a
        // fabricated ContentType{c_type:"text", c_subtype:"plain"}.
        return HeaderValue.Empty;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class content_type_tests
{
    [TestMethod]
    public void parse_content_fields()
    {
        var tests = FieldTestUtils.load_tests<ContentType?>("content_type");
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var parsed = stream.parse_content_type().as_content_type();
            if (test.expected != null)
            {
                Assert.IsNotNull(parsed, $"Failed to parse {test.header}");
                Assert.AreEqual(test.expected.c_type, parsed.c_type, $"c_type mismatch for {test.header}");
                Assert.AreEqual(test.expected.c_subtype, parsed.c_subtype, $"c_subtype mismatch for {test.header}");
                var expAttrs = test.expected.attributes ?? new List<Attribute>();
                var actAttrs = parsed.attributes ?? new List<Attribute>();
                Assert.AreEqual(expAttrs.Count, actAttrs.Count, $"Attributes count mismatch for {test.header}");
                for (int i = 0; i < expAttrs.Count; i++)
                {
                    Assert.AreEqual(expAttrs[i].name, actAttrs[i].name, $"Attr name mismatch at {i} for {test.header}");
                    Assert.AreEqual(expAttrs[i].value, actAttrs[i].value, $"Attr value mismatch at {i} for {test.header}");
                }
            }
            else
            {
                Assert.IsNull(parsed, $"Expected null for {test.header}");
            }
        }
    }

    // Regression tests for Phase 2 fixes -- each pins a Rust-verified expected value.

    [TestMethod]
    public void eof_with_no_trailing_newline_returns_empty_matches_rust()
    {
        // Rust: parsing the literal bytes "text/plain" with NO trailing newline at all
        // returns HeaderValue::Empty -- everything parsed so far is discarded, not
        // salvaged into a real ContentType (PARITY-AUDIT.md FILE 15).
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes("text/plain"));
        var result = stream.parse_content_type();
        Assert.IsNull(result.as_content_type());
    }

    [TestMethod]
    public void type_lowering_is_ascii_only_matches_rust()
    {
        // Rust: make_ascii_lowercase() only folds ASCII A-Z; a non-ASCII
        // "uppercase-like" character is left untouched (PARITY-AUDIT.md FILE 20).
        // Attribute name is ASCII 'A' + Kelvin sign (Unicode code point 212A hex,
        // used directly as a Unicode character in this source file). The ASCII 'A' is what triggers the
        // is_lower_case=false path in the first place (the parser's uppercase
        // detection only matches literal ASCII A-Z bytes, so a lone non-ASCII char by
        // itself would not trigger lowering at all). The Kelvin sign is
        // Unicode-equivalent to plain 'k' under full case folding, so
        // ToLowerInvariant() would incorrectly fold it; ASCII-only folding must not.
        string attrName = "A" + "K";
        string expectedName = "a" + "K";
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes("text/plain; " + attrName + "=\"v\"\n"));
        var result = stream.parse_content_type().as_content_type();
        Assert.IsNotNull(result);
        Assert.IsNotNull(result!.attributes);
        Assert.AreEqual(expectedName, result.attributes![0].name);
    }
}
#endif
