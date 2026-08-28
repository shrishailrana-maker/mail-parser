/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/thread.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: eb89d97fe658a71ba7e118c8763422a52af4c58aa1e71dd24a994dc3e970d912
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class ThreadUtils
{
    private static readonly HashSet<string> RePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "re", "res", "sv", "antw", "ref", "aw", "απ", "השב", "vá",
        "r", "rif", "bls", "odp", "ynt", "atb", "رد", "回复", "转发"
    };

    private static readonly HashSet<string> FwdPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fwd", "fw", "rv", "enc", "vs", "doorst", "vl", "tr", "wg", "πρθ",
        "הועבער", "továbbítás", "i", "fs", "trs", "vb",
        "pd", "i̇lt", "yml", "إعادة توجيه", "回覆", "轉寄"
    };

    private static bool is_re_prefix(string prefix) => RePrefixes.Contains(prefix);
    private static bool is_fwd_prefix(string prefix) => FwdPrefixes.Contains(prefix);

    // Rust: thread_name
    public static string thread_name(string text)
    {
        int token_start = 0;
        int token_end = 0;

        int thread_name_start = 0;
        int fwd_start = 0;
        int fwd_end = 0;
        int last_blob_end = 0;

        bool in_blob = false;
        bool in_blob_ignore = false;
        bool seen_header = false;
        bool seen_blob_header = false;
        bool token_found = false;

        for (int pos = 0; pos < text.Length; pos++)
        {
            char ch = text[pos];
            switch (ch)
            {
                case '[':
                    if (!in_blob)
                    {
                        if (token_found)
                        {
                            if (token_end == 0) token_end = pos;
                            string prefix = text.Substring(token_start, token_end - token_start).ToLowerInvariant();
                            if (is_re_prefix(prefix) || is_fwd_prefix(prefix))
                            {
                                seen_header = true;
                            }
                            else
                            {
                                goto loop_end;
                            }
                        }
                        token_found = false;
                        in_blob = true;
                    }
                    else
                    {
                        goto loop_end;
                    }
                    break;
                case ']' when in_blob:
                    if (seen_blob_header && token_found)
                    {
                        fwd_start = token_start;
                        fwd_end = pos;
                    }
                    if (!seen_header)
                    {
                        last_blob_end = pos + 1;
                    }
                    in_blob = false;
                    token_found = false;
                    seen_blob_header = false;
                    in_blob_ignore = false;
                    break;
                case ':' when !in_blob:
                    if ((seen_header && token_found) || (!seen_header && !token_found))
                    {
                        goto loop_end;
                    }
                    else if (!seen_header)
                    {
                        if (token_end == 0) token_end = pos;
                        string prefix = text.Substring(token_start, token_end - token_start).ToLowerInvariant();
                        if (!is_re_prefix(prefix) && !is_fwd_prefix(prefix))
                        {
                            goto loop_end;
                        }
                    }
                    else
                    {
                        seen_header = false;
                    }
                    thread_name_start = pos + 1;
                    token_found = false;
                    break;
                case ':' when in_blob && !in_blob_ignore:
                    if (token_end == 0) token_end = pos;
                    string bPrefix = text.Substring(token_start, token_end - token_start).ToLowerInvariant();
                    if (is_fwd_prefix(bPrefix))
                    {
                        token_found = false;
                        seen_blob_header = true;
                    }
                    else if (seen_blob_header && is_re_prefix(bPrefix))
                    {
                        token_found = false;
                    }
                    else
                    {
                        in_blob_ignore = true;
                    }
                    break;
                case var _ when char.IsWhiteSpace(ch):
                    if (token_end == 0) token_end = pos;
                    break;
                default:
                    if (!token_found)
                    {
                        token_start = pos;
                        token_end = 0;
                        token_found = true;
                    }
                    else if (!in_blob && pos - token_start > 21)
                    {
                        goto loop_end;
                    }
                    break;
            }
        }

    loop_end:

        if (last_blob_end > thread_name_start || (fwd_start > 0 && last_blob_end > fwd_start && fwd_start > thread_name_start))
        {
            string result = trim_trailing_fwd(text.Substring(last_blob_end));
            if (!string.IsNullOrEmpty(result)) return result;
        }

        if (fwd_start > 0 && thread_name_start < fwd_start)
        {
            string result = trim_trailing_fwd(text.Substring(fwd_start, fwd_end - fwd_start));
            if (!string.IsNullOrEmpty(result)) return result;
        }

        return trim_trailing_fwd(text.Substring(thread_name_start));
    }

    // Rust: trim_trailing_fwd
    public static string trim_trailing_fwd(string text)
    {
        bool in_parentheses = false;
        bool trim_end = true;
        bool end_found = false;

        int text_start = 0;
        int text_end = text.Length;
        int fwd_end = 0;

        for (int pos = text.Length - 1; pos >= 0; pos--)
        {
            char ch = text[pos];
            if (ch == '(' && !end_found)
            {
                if (in_parentheses)
                {
                    in_parentheses = false;
                    if (fwd_end - pos > 2 && is_fwd_prefix(text.Substring(pos + 1, fwd_end - pos - 1).ToLowerInvariant()))
                    {
                        text_end = pos;
                        trim_end = true;
                        continue;
                    }
                }
                end_found = true;
            }
            else if (ch == ')' && !end_found)
            {
                if (!in_parentheses)
                {
                    in_parentheses = true;
                    fwd_end = pos;
                }
                else
                {
                    end_found = true;
                }
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (trim_end) text_end = pos;
                continue;
            }
            else
            {
                if (!in_parentheses && !end_found)
                {
                    end_found = true;
                }
            }

            if (trim_end) trim_end = false;
            text_start = pos;
        }

        if (text_end >= text_start)
        {
            return text.Substring(text_start, text_end - text_start);
        }
        return "";
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class thread_tests
{
    [TestMethod]
    public void parse_thread_name()
    {
        var tests = new (string input, string expected)[]
        {
            ("re: hello", "hello"),
            ("re:re: hello", "hello"),
            ("re:fwd: hello", "hello"),
            ("fwd[5]:re[5]: hello", "hello"),
            ("fwd[99]:  re[40]: hello", "hello"),
            (": hello", ": hello"),
            ("z: hello", "z: hello"),
            ("re:: hello", ": hello"),
            ("[10] hello", "hello"),
            ("fwd[a]: hello", "hello"),
            ("re:", ""),
            ("re::", ":"),
            ("", ""),
            (" ", ""),
            ("回复: 轉寄: 轉寄", "轉寄"),
            ("aw[50]: wg: aw[1]: hallo", "hallo"),
            ("res: rv: enc: továbbítás: ", ""),
            ("[fwd: hello world]", "hello world"),
            ("re: enc: re[5]: [fwd: hello world]", "hello world"),
            ("[fwd: re: fw: hello world]", "hello world"),
            ("[fwd: hello world]: another text", ": another text"),
            ("[fwd: re: fwd:] another text", "another text"),
            ("[hello world]", "[hello world]"),
            ("re: fwd[9]: [hello world]", "[hello world]"),
            ("[mailing-list] hello world", "hello world"),
            ("[mailing-list] re: hello world", "hello world"),
            ("[mailing-list] wg[8]:re:  hello world", "hello world"),
            ("hello [world]", "hello [world]"),
            (" [hello] [world] ", "[hello] [world]"),
            ("[mailing-list] hello [world]", "hello [world]"),
            ("[hello [world]", "[hello [world]"),
            ("[]hello [world]", "hello [world]"),
            ("[fwd: re: re:] fwd[6]:re:  fw:", ""),
            ("[fwd hello] world hello", "world hello"),
            ("[fwd: مرحبا بالعالم]", "مرحبا بالعالم"),
            ("[fwd: hello world] مرحبا بالعالم", "مرحبا بالعالم"),
            ("  hello world  ", "hello world"),
            ("[mailing-list] wg[8]:re:  hello world (fwd)(fwd)", "hello world"),
            ("[fwd: re: fw: hello world (fwd)]", "hello world"),
            ("res: rv: enc: továbbítás: hello world (doorst)", "hello world"),
            ("[fwd: re: re: (fwd)] fwd[6]:re:  fw: (fwd)", ""),
        };

        foreach (var (input, expected) in tests)
        {
            Assert.AreEqual(expected, ThreadUtils.thread_name(input), $"Failed for {input}");
        }
    }

    [TestMethod]
    public void parse_trail_fwd()
    {
        var tests = new (string input, string expected)[]
        {
            ("hello (fwd)", "hello"),
            (" hello (fwd)(fwd)", "hello"),
            ("hello (wg) (fwd) (fwd)", "hello"),
            ("(fwd)(fwd)", ""),
            ("(fwd)hello(fwd)", "(fwd)hello"),
            ("  hello  ", "hello"),
            ("  hello world   ", "hello world"),
            ("", ""),
            ("    ", ""),
            ("hello ()(fwd)", "hello ()"),
            ("(hello)", "(hello)"),
            ("hello () (fwd) ()(fwd)", "hello () (fwd) ()"),
            (")(", ")("),
            (" 你好世界(fwd) ", "你好世界"),
            ("你好世界 (回覆)", "你好世界"),
            ("hello(fwd", "hello(fwd"),
            ("hello(fwd))", "hello(fwd))"),
        };

        foreach (var (input, expected) in tests)
        {
            Assert.AreEqual(expected, ThreadUtils.trim_trailing_fwd(input), $"Failed for {input}");
        }
    }
}
#endif
