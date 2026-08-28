/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/preview.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: cead47dc59dac587df9871930b560787ada7d4a8d3ff940ebf3ad4633e945e7c
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class PreviewUtils
{
    // Rust: preview_html
    public static string preview_html(string html, int max_len)
    {
        return preview_text(HtmlUtils.html_to_text(html), max_len);
    }

    // Rust: preview_text
    public static string preview_text(string text, int max_len)
    {
        byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        if (textBytes.Length > max_len)
        {
            bool add_dots = max_len > 6;
            if (add_dots)
            {
                max_len -= 3;
            }
            int current_len = 0;
            var sb = new StringBuilder();
            foreach (var rune in text.EnumerateRunes())
            {
                int runeLen = rune.Utf8SequenceLength;
                if (current_len + runeLen > max_len)
                {
                    break;
                }
                sb.Append(rune.ToString());
                current_len += runeLen;
            }
            if (add_dots)
            {
                sb.Append("...");
            }
            return sb.ToString();
        }
        return text;
    }

    // Rust: truncate_text
    public static string truncate_text(string text, int max_len)
    {
        return preview_text(text, max_len);
    }

    // Rust: truncate_html
    public static string truncate_html(string html, int max_len)
    {
        byte[] htmlBytes = System.Text.Encoding.UTF8.GetBytes(html);
        if (htmlBytes.Length > max_len)
        {
            bool add_dots = max_len > 6;
            if (add_dots)
            {
                max_len -= 3;
            }

            bool in_tag = false;
            bool in_comment = false;
            int last_tag_end_pos = 0;
            int byte_pos = 0;

            ReadOnlySpan<byte> span = htmlBytes;

            while (byte_pos < span.Length)
            {
                var status = Rune.DecodeFromUtf8(span.Slice(byte_pos), out var rune, out int bytesConsumed);
                if (status != System.Buffers.OperationStatus.Done)
                {
                    bytesConsumed = 1;
                    rune = new Rune(span[byte_pos]);
                }

                int set_last_tag = 0;
                int chValue = rune.Value;

                if (chValue == '<' && !in_tag)
                {
                    in_tag = true;
                    if (byte_pos + 4 <= span.Length && span[byte_pos + 1] == (byte)'!' && span[byte_pos + 2] == (byte)'-' && span[byte_pos + 3] == (byte)'-')
                    {
                        in_comment = true;
                    }
                    set_last_tag = byte_pos;
                }
                else if (chValue == '>' && in_tag)
                {
                    if (in_comment)
                    {
                        if (byte_pos >= 2 && span[byte_pos - 2] == (byte)'-' && span[byte_pos - 1] == (byte)'-')
                        {
                            in_comment = false;
                            in_tag = false;
                            set_last_tag = byte_pos + 1;
                        }
                    }
                    else
                    {
                        in_tag = false;
                        set_last_tag = byte_pos + 1;
                    }
                }

                if (bytesConsumed + byte_pos > max_len)
                {
                    int cut_pos = ((in_tag || set_last_tag > 0) && last_tag_end_pos > 0) ? last_tag_end_pos : byte_pos;
                    string slice = System.Text.Encoding.UTF8.GetString(span.Slice(0, cut_pos));
                    return add_dots ? slice + "..." : slice;
                }
                else if (set_last_tag > 0)
                {
                    last_tag_end_pos = set_last_tag;
                }

                byte_pos += bytesConsumed;
            }

            return html;
        }
        return html;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class preview_tests
{
    [TestMethod]
    public void text_preview()
    {
        string text_1 = "J'interdis aux marchands de vanter trop leurs marchandises. " +
            "Car ils se fontvite pédagogues et t'enseignent comme but ce qui " +
            "n'est par essence qu'un moyen, et te trompant ainsi sur la route " +
            "à suivre les voilà bientôt qui te dégradent, car si leur musique " +
            "est vulgaire ils te fabriquent pour te la vendre une âme vulgaire.\n" +
            "— Antoine de Saint-Exupéry, Citadelle (1948)";

        string text_2 = "長沮、桀溺耦而耕，孔子過之，使子路問津焉。長沮曰：「夫執輿者為誰？」" +
            "子路曰：「為孔丘。」曰：「是魯孔丘與？」曰：「是也。」曰：「是知津矣。」問於桀溺，" +
            "桀溺曰：「子為誰？」曰：「為仲由。」曰：「是魯孔丘之徒與？」對曰：「然。" +
            "」曰：「滔滔者天下皆是也，而誰以易之？且而與其從辟人之士也，豈若從" +
            "辟世之士哉？」耰而不輟。子路行以告。夫子憮然曰：「鳥獸不可與同群，吾非斯人之徒" +
            "與而誰與？天下有道，丘不與易也。」" +
            "子路從而後，遇丈人，以杖荷蓧。子路問曰：「子見夫子乎？」丈人曰：「四體不勤，" +
            "五穀不分。孰為夫子？」植其杖而芸。子路拱而立。止子路宿，殺雞為黍而食之，見其二" +
            "子焉。明日，子路行以告。子曰：「隱者也。」使子路反見之。至則行矣。子路曰：「" +
            "不仕無義。長幼之節，不可廢也；君臣之義，如之何其廢之？欲潔其身，而亂大倫。君" +
            "子之仕也，行其義也。道之不行，已知之矣。」";

        Assert.AreEqual(
            "J'interdis aux marchands de vanter trop leurs marchandises. Car ils se fontvite pédagogues et t'enseignent...",
            PreviewUtils.truncate_text(text_1, 110)
        );

        Assert.AreEqual(
            "長沮、桀溺耦而耕，孔子過之，使子路問津焉。長沮曰：「夫執輿者為誰？」子...",
            PreviewUtils.truncate_text(text_2, 110)
        );
    }

    [TestMethod]
    public void html_truncate()
    {
        var cases = new (string html, string expected)[]
        {
            (
                "<html>hello<br/>world<br/></html>",
                "<html>hello<br/>world..."
            ),
            ("<html>using &lt;><br/></html>", "<html>using &lt;><br/>..."),
            (
                "test <not br/>tag<br />test <not br/>tag<br />",
                "test <not br/>tag..."
            ),
            (
                "<>< ><tag\n/>>hello    world< br \n />",
                "<>< ><tag\n/>>hello    ..."
            ),
            (
                "<head><title>ignore head</title><not head>xyz</not head></head><h1>&lt;body&gt;</h1>",
                "<head><title>ignore he..."
            ),
            (
                "<p>what is &heartsuit;?</p><p>&#x000DF;&Abreve;&#914;&gamma; don&apos;t hurt me.</p>",
                "<p>what is &heartsuit;..."
            ),
            (
                "<!-- <> < < < -->the actual<!--> text",
                "<!-- <> < < < -->the a..."
            ),
            (
                "   < p >  hello < / p > < p > world < / p >   !!! < br > ",
                "   < p >  hello ..."
            ),
            (
                " <p>please unsubscribe <a href=#>here</a>.</p> ",
                " <p>please unsubscribe..."
            ),
        };

        foreach (var (html, expected) in cases)
        {
            Assert.AreEqual(expected, PreviewUtils.truncate_html(html, 25), $"Failed for '{html}'");
        }
    }
}
#endif
