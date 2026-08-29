/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/address.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 0ade75d2ad008d477aafa3da6e50bd570e6d53de2180b81be225ed120e67bad3
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class AddressParserUtils
{
    public static string? parse_address_local_part(string addr)
    {
        for (int pos = 0; pos < addr.Length; pos++)
        {
            char ch = addr[pos];
            if (ch == '@')
            {
                return (pos > 0 && pos + 1 < addr.Length) ? addr.Substring(0, pos) : null;
            }
            else if (ch > 127)
            {
                return null;
            }
        }
        return null;
    }

    public static string? parse_address_domain(string addr)
    {
        for (int pos = 0; pos < addr.Length; pos++)
        {
            char ch = addr[pos];
            if (ch == '@')
            {
                return (pos > 0 && pos + 1 < addr.Length) ? addr.Substring(pos + 1) : null;
            }
            else if (ch > 127)
            {
                return null;
            }
        }
        return null;
    }

    public static string? parse_address_user_part(string addr)
    {
        // Rust's outer loop has an `else if !ch.is_ascii() { return None; }` arm that was
        // missing here entirely -- non-ASCII bytes before any '+'/'@' silently fell through
        // instead of aborting the parse (PARITY-AUDIT.md: found during the AddressParser
        // full-depth read). Note Rust's INNER scan (after a '+') has no such check either,
        // by design -- only the outer loop checks ASCII-ness, so this fix must not add the
        // check inside the inner for-loop below.
        for (int pos = 0; pos < addr.Length; pos++)
        {
            char ch = addr[pos];
            if (ch == '+')
            {
                if (pos > 0)
                {
                    for (int p2 = pos + 1; p2 < addr.Length; p2++)
                    {
                        if (addr[p2] == '@' && p2 + 1 < addr.Length)
                        {
                            return addr.Substring(0, pos);
                        }
                    }
                }
                return null;
            }
            else if (ch == '@')
            {
                return (pos > 0 && pos + 1 < addr.Length) ? addr.Substring(0, pos) : null;
            }
            else if (ch > 127)
            {
                return null;
            }
        }
        return null;
    }

    // Rust: parse_address_detail_part -- fixed (PARITY-AUDIT.md cross-check #5): Rust
    // tracks the LAST '+' seen (unconditionally overwriting plus_pos on every '+'), and
    // allows an empty detail (a '+' immediately before '@'). The prior version here
    // locked onto the FIRST '+' via a nested loop and required at least one character
    // between '+' and '@', which is a different, wrong function: for
    // "user+a+detail@x", Rust returns "detail" (after the last '+'); the prior code
    // returned "a+detail" (after the first '+', engulfing the second '+' as text).
    public static string? parse_address_detail_part(string addr)
    {
        int plus_pos = -1; // -1 == Rust's usize::MAX sentinel (no '+' seen yet)
        for (int pos = 0; pos < addr.Length; pos++)
        {
            char ch = addr[pos];
            if (ch == '+')
            {
                plus_pos = pos + 1;
            }
            else if (ch == '@')
            {
                if (plus_pos != -1 && pos + 1 < addr.Length)
                {
                    return addr.Substring(plus_pos, pos - plus_pos);
                }
                return null;
            }
            else if (ch > 127)
            {
                return null;
            }
        }
        return null;
    }
}

public partial class MessageStream
{
    private enum AddressState
    {
        Address,
        Name,
        Quote,
        Comment,
    }

    private class AddressParser
    {
        public int token_start = 0;
        public int token_end = 0;

        public bool is_token_email = false;
        public bool is_token_start = true;
        public bool is_escaped = false;
        public bool last_is_encoded = true;

        public List<string> name_tokens = new(3);
        public List<string> mail_tokens = new(3);
        public List<string> comment_tokens = new(3);

        public AddressState state = AddressState.Name;
        public Stack<AddressState> state_stack = new(5);

        public List<Addr> addresses = new();
        public string? group_name = null;
        public string? group_comment = null;
        public List<Group> result = new();

        public void add_token(MessageStream stream)
        {
            if (token_start > 0)
            {
                var bytes = stream.bytes_span(token_start - 1, token_end);
                string token = System.Text.Encoding.UTF8.GetString(bytes);
                bool add_space = false;
                List<string> list = state switch
                {
                    AddressState.Address => mail_tokens,
                    AddressState.Name => is_token_email ? mail_tokens : (add_space = true, name_tokens).Item2,
                    AddressState.Quote => name_tokens,
                    AddressState.Comment => (add_space = true, comment_tokens).Item2,
                    _ => name_tokens
                };

                if (add_space && list.Count > 0)
                {
                    list.Add(" ");
                }

                list.Add(token);

                token_start = 0;
                is_token_email = false;
                is_token_start = true;
                is_escaped = false;
                last_is_encoded = false;
            }
        }

        public void add_rfc2047(string token)
        {
            bool add_space = !last_is_encoded && state != AddressState.Quote;
            var list = state != AddressState.Comment ? name_tokens : comment_tokens;

            if (add_space && list.Count > 0)
            {
                list.Add(" ");
            }

            list.Add(token);
            last_is_encoded = true;
        }

        public void add_address()
        {
            bool has_mail = mail_tokens.Count > 0;
            bool has_name = name_tokens.Count > 0;
            bool has_comment = comment_tokens.Count > 0;

            if (has_mail && has_name && has_comment)
            {
                addresses.Add(new Addr($"{concat_tokens(name_tokens)} ({concat_tokens(comment_tokens)})", concat_tokens(mail_tokens)));
            }
            else if (has_name && has_mail)
            {
                addresses.Add(new Addr(concat_tokens(name_tokens), concat_tokens(mail_tokens)));
            }
            else if (has_mail && has_comment)
            {
                addresses.Add(new Addr(concat_tokens(comment_tokens), concat_tokens(mail_tokens)));
            }
            else if (has_mail)
            {
                addresses.Add(new Addr(null, concat_tokens(mail_tokens)));
            }
            else if (has_name && has_comment)
            {
                string name = concat_tokens(name_tokens);
                string comment = concat_tokens(comment_tokens);
                // Rust: !name.chars().any(char::is_whitespace) -- full Unicode whitespace
                // (e.g. non-breaking space U+00A0), not just ASCII space/tab. A name built
                // from a decoded RFC2047 token or unusual quoted content can contain other
                // Unicode whitespace with no literal space/tab present, which took the wrong
                // branch here (PARITY-AUDIT.md: found during the AddressParser full-depth read).
                if (!name.Any(char.IsWhiteSpace))
                {
                    addresses.Add(new Addr(comment, name));
                }
                else
                {
                    addresses.Add(new Addr($"{name} ({comment})", null));
                }
            }
            else if (has_name)
            {
                addresses.Add(new Addr(concat_tokens(name_tokens), null));
            }
            else if (has_comment)
            {
                addresses.Add(new Addr(concat_tokens(comment_tokens), null));
            }
        }

        public void add_group_details()
        {
            if (name_tokens.Count > 0)
            {
                group_name = concat_tokens(name_tokens);
            }
            if (comment_tokens.Count > 0)
            {
                group_comment = concat_tokens(comment_tokens);
            }
            if (mail_tokens.Count > 0)
            {
                if (group_name != null)
                {
                    group_name = $"{group_name} {concat_tokens(mail_tokens)}";
                }
                else
                {
                    group_name = concat_tokens(mail_tokens);
                }
            }
        }

        public void add_group()
        {
            bool has_name = group_name != null;
            bool has_comment = group_comment != null;
            bool has_addresses = addresses.Count > 0;

            if (has_name && has_addresses && has_comment)
            {
                result.Add(new Group($"{group_name} ({group_comment})", new List<Addr>(addresses)));
                addresses.Clear();
                group_name = null;
                group_comment = null;
            }
            else if (has_addresses && has_name)
            {
                result.Add(new Group(group_name, new List<Addr>(addresses)));
                addresses.Clear();
                group_name = null;
            }
            else if (has_addresses)
            {
                result.Add(new Group(group_comment, new List<Addr>(addresses)));
                addresses.Clear();
                group_comment = null;
            }
            else if (has_name)
            {
                result.Add(new Group(group_name, new List<Addr>()));
                group_name = null;
            }
        }

        private static string concat_tokens(List<string> tokens)
        {
            if (tokens.Count == 1)
            {
                string r = tokens[0];
                tokens.Clear();
                return r;
            }
            else if (tokens.Count > 1)
            {
                string r = string.Concat(tokens);
                tokens.Clear();
                return r;
            }
            return "";
        }
    }

    // Rust: MessageStream::parse_address
    public HeaderValue parse_address()
    {
        var parser = new AddressParser();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            switch (ch)
            {
                case (byte)'\n':
                    parser.add_token(this);
                    if (parser.state == AddressState.Quote)
                    {
                        if (peek_next_is_space())
                        {
                            continue;
                        }
                        else
                        {
                            goto address_done;
                        }
                    }
                    if (try_next_is_space())
                    {
                        if (!parser.is_token_start)
                        {
                            parser.is_token_start = true;
                        }
                        continue;
                    }
                    else
                    {
                        goto address_done;
                    }
                case (byte)'\\' when parser.state != AddressState.Name && !parser.is_escaped:
                    if (parser.token_start > 0)
                    {
                        if (parser.state == AddressState.Quote)
                        {
                            parser.token_end = offset() - 1;
                        }
                        parser.add_token(this);
                    }
                    parser.is_escaped = true;
                    continue;
                case (byte)',' when parser.state == AddressState.Name:
                    parser.add_token(this);
                    parser.add_address();
                    continue;
                case (byte)'<' when parser.state == AddressState.Name:
                    parser.is_token_email = false;
                    parser.add_token(this);
                    parser.state_stack.Push(AddressState.Name);
                    parser.state = AddressState.Address;
                    continue;
                case (byte)'>' when parser.state == AddressState.Address:
                    parser.add_token(this);
                    parser.state = parser.state_stack.Pop();
                    continue;
                case (byte)'"' when !parser.is_escaped:
                    switch (parser.state)
                    {
                        case AddressState.Name:
                            parser.state_stack.Push(AddressState.Name);
                            parser.state = AddressState.Quote;
                            parser.add_token(this);
                            continue;
                        case AddressState.Quote:
                            parser.add_token(this);
                            parser.state = parser.state_stack.Pop();
                            continue;
                    }
                    break;
                case (byte)'@' when parser.state == AddressState.Name:
                    parser.is_token_email = true;
                    break;
                case (byte)'=' when parser.is_token_start && !parser.is_escaped && peek_char((byte)'?'):
                    checkpoint();
                    var token = decode_rfc2047();
                    if (token != null)
                    {
                        parser.add_token(this);
                        parser.add_rfc2047(token);
                        continue;
                    }
                    restore();
                    break;
                case (byte)' ' or (byte)'\t':
                    if (!parser.is_token_start)
                    {
                        parser.is_token_start = true;
                    }
                    if (parser.is_escaped)
                    {
                        parser.is_escaped = false;
                    }
                    if (parser.state == AddressState.Quote)
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
                case (byte)'\r':
                    continue;
                case (byte)'(' when parser.state != AddressState.Quote && !parser.is_escaped:
                    parser.state_stack.Push(parser.state);
                    if (parser.state != AddressState.Comment)
                    {
                        parser.add_token(this);
                        parser.state = AddressState.Comment;
                        parser.last_is_encoded = false;
                        continue;
                    }
                    break;
                case (byte)')' when parser.state == AddressState.Comment && !parser.is_escaped:
                    var new_state = parser.state_stack.Pop();
                    if (parser.state != new_state)
                    {
                        parser.add_token(this);
                        parser.state = new_state;
                        parser.last_is_encoded = false;
                        continue;
                    }
                    break;
                case (byte)':' when parser.state == AddressState.Name && !parser.is_escaped:
                    parser.add_group();
                    parser.add_token(this);
                    parser.add_group_details();
                    continue;
                case (byte)';' when parser.state == AddressState.Name:
                    parser.add_token(this);
                    parser.add_address();
                    parser.add_group();
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

    address_done:
        parser.add_address();

        if (parser.group_name != null || parser.result.Count > 0)
        {
            parser.add_group();
            return HeaderValue.Address(Address.Group(parser.result));
        }
        else if (parser.addresses.Count > 0)
        {
            return HeaderValue.Address(Address.List(parser.addresses));
        }
        else
        {
            return HeaderValue.Empty;
        }
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class address_tests
{
    [TestMethod]
    public void parse_addresses()
    {
        var tests = FieldTestUtils.load_tests<Address>("address");
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var parsed = stream.parse_address().as_address();
            Assert.IsNotNull(parsed, $"Failed to parse {test.header}");

            if (test.expected is Address.ListRecord elr)
            {
                var plr = parsed.into_list();
                Assert.AreEqual(elr.Value.Count, plr.Count, $"Count mismatch for {test.header}");
                for (int i = 0; i < elr.Value.Count; i++)
                {
                    Assert.AreEqual(elr.Value[i].name, plr[i].name, $"Name mismatch at {i} for {test.header}");
                    Assert.AreEqual(elr.Value[i].address, plr[i].address, $"Address mismatch at {i} for {test.header}");
                }
            }
            else if (test.expected is Address.GroupRecord egr)
            {
                var pgr = parsed.into_group();
                Assert.AreEqual(egr.Value.Count, pgr.Count, $"Group count mismatch for {test.header}");
                for (int i = 0; i < egr.Value.Count; i++)
                {
                    Assert.AreEqual(egr.Value[i].name, pgr[i].name, $"Group name mismatch at {i} for {test.header}");
                    Assert.AreEqual(egr.Value[i].addresses.Count, pgr[i].addresses.Count, $"Group addresses count mismatch at {i} for {test.header}");
                    for (int j = 0; j < egr.Value[i].addresses.Count; j++)
                    {
                        Assert.AreEqual(egr.Value[i].addresses[j].name, pgr[i].addresses[j].name, $"Group member name mismatch at {i},{j} for {test.header}");
                        Assert.AreEqual(egr.Value[i].addresses[j].address, pgr[i].addresses[j].address, $"Group member address mismatch at {i},{j} for {test.header}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void parse_address_detail_part_uses_last_plus_matches_rust()
    {
        // Rust: tracks the LAST '+', not the first (PARITY-AUDIT.md cross-check #5).
        Assert.AreEqual("detail", AddressParserUtils.parse_address_detail_part("user+a+detail@example.com"));
        // Rust: an empty detail (a '+' immediately before '@') is valid, not rejected.
        Assert.AreEqual("", AddressParserUtils.parse_address_detail_part("user+@example.com"));
    }

    [TestMethod]
    public void parse_address_user_part_rejects_non_ascii_matches_rust()
    {
        // Rust's outer loop has `else if !ch.is_ascii() { return None; }` -- was missing
        // here entirely, so a non-ASCII byte before '+'/'@' silently passed through instead
        // of aborting (PARITY-AUDIT.md: found during the AddressParser full-depth read).
        Assert.IsNull(AddressParserUtils.parse_address_user_part("usér@example.com"));
        // Sanity: pure-ASCII input must still work normally.
        Assert.AreEqual("user", AddressParserUtils.parse_address_user_part("user@example.com"));
        Assert.AreEqual("user", AddressParserUtils.parse_address_user_part("user+detail@example.com"));
    }

    [TestMethod]
    public void add_address_uses_full_unicode_whitespace_check_matches_rust()
    {
        // Rust: !name.chars().any(char::is_whitespace) -- full Unicode whitespace (e.g.
        // U+00A0 non-breaking space), not just ASCII space/tab. A name with a non-breaking
        // space and no literal space/tab used to take the WRONG branch here (treated as a
        // bare address with the comment as its name), instead of Rust's "name (comment)"
        // combined-name branch (PARITY-AUDIT.md: found during the AddressParser full-depth read).
        string header = "First Last (comment text)";
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(header));
        var parsed = stream.parse_address().as_address();
        Assert.IsNotNull(parsed);
        var list = parsed!.into_list();
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("First Last (comment text)", list[0].name);
        Assert.IsNull(list[0].address);
    }
}
#endif
