/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/mailbox/mbox.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: a16f7c91477aa3d89fb79aeafe00cc195141070494f966a6095b57ef5d6d08b7
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public class MboxMessage
{
    public ulong internal_date { get; set; }
    public string from { get; set; } = "";
    public byte[] contents { get; set; } = Array.Empty<byte>();

    public ulong internal_date_() => internal_date;
    public string from_() => from;
    public byte[] contents_() => contents;
    public byte[] unwrap_contents() => contents;

    public MboxMessage() { }

    public MboxMessage(ReadOnlySpan<byte> line)
    {
        string fromLine = System.Text.Encoding.UTF8.GetString(line);
        var parts = fromLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            from = parts[1].Trim();
        }
        if (parts.Length >= 6)
        {
            // Parts: From <addr> <DayOfWeek> <Month> <Day> <Time> <Year>
            var dt = new DateTime();
            int pIdx = 2;
            if (parts.Length > 2)
            {
                // check day of week
                pIdx++;
            }
            if (parts.Length > 3)
            {
                dt.month = parse_month(parts[3]);
            }
            if (parts.Length > 4 && byte.TryParse(parts[4], out byte d))
            {
                dt.day = d;
            }
            if (parts.Length > 5)
            {
                var timeParts = parts[5].Split(':');
                if (timeParts.Length == 3)
                {
                    byte.TryParse(timeParts[0], out byte h);
                    byte.TryParse(timeParts[1], out byte m);
                    byte.TryParse(timeParts[2], out byte s);
                    dt.hour = h;
                    dt.minute = m;
                    dt.second = s;
                }
            }
            if (parts.Length > 6 && ushort.TryParse(parts[6], out ushort y))
            {
                dt.year = y;
            }

            if (dt.year > 0 && dt.month > 0 && dt.day > 0)
            {
                internal_date = (ulong)dt.to_timestamp();
            }
        }
    }

    private static byte parse_month(string m) => m.ToLowerInvariant() switch
    {
        "jan" => 1, "feb" => 2, "mar" => 3, "apr" => 4, "may" => 5, "jun" => 6,
        "jul" => 7, "aug" => 8, "sep" => 9, "oct" => 10, "nov" => 11, "dec" => 12,
        _ => 0
    };
}

public class MboxMessageIterator : IEnumerable<MboxMessage>, IEnumerator<MboxMessage>
{
    private readonly Stream _stream;
    private MboxMessage? _current;
    private bool _isEof;
    private readonly List<byte> _currentLine = new();
    private readonly List<byte> _messageBuffer = new();
    private MboxMessage? _pendingMessage;

    public MboxMessageIterator(Stream stream)
    {
        _stream = stream;
    }

    public MboxMessageIterator(TextReader reader)
    {
        _stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(reader.ReadToEnd()));
    }

    public MboxMessageIterator(byte[] bytes)
    {
        _stream = new MemoryStream(bytes);
    }

    public MboxMessage Current => _current!;
    object IEnumerator.Current => _current!;

    public bool MoveNext()
    {
        if (_isEof && _pendingMessage == null)
        {
            _current = null;
            return false;
        }

        while (true)
        {
            var line = ReadLineBytes();
            if (line == null)
            {
                _isEof = true;
                if (_pendingMessage != null)
                {
                    _pendingMessage.contents = _messageBuffer.ToArray();
                    _current = _pendingMessage;
                    _pendingMessage = null;
                    _messageBuffer.Clear();
                    return true;
                }
                _current = null;
                return false;
            }

            if (line.StartsWith(new byte[] { (byte)'F', (byte)'r', (byte)'o', (byte)'m', (byte)' ' }))
            {
                if (_pendingMessage != null)
                {
                    _pendingMessage.contents = _messageBuffer.ToArray();
                    _current = _pendingMessage;
                    _pendingMessage = new MboxMessage(line);
                    _messageBuffer.Clear();
                    return true;
                }
                else
                {
                    _pendingMessage = new MboxMessage(line);
                    _messageBuffer.Clear();
                }
            }
            else
            {
                if (_pendingMessage != null)
                {
                    // If line starts with ">From ", unescape to "From "
                    // If line starts with ">>From ", unescape to ">From "
                    if (line.StartsWith(new byte[] { (byte)'>' }))
                    {
                        int gtCount = 0;
                        while (gtCount < line.Length && line[gtCount] == (byte)'>') gtCount++;
                        if (gtCount < line.Length && line.AsSpan(gtCount).StartsWith(new byte[] { (byte)'F', (byte)'r', (byte)'o', (byte)'m', (byte)' ' }))
                        {
                            line = line.AsSpan(1).ToArray();
                        }
                    }
                    _messageBuffer.AddRange(line);
                    _messageBuffer.Add((byte)'\n');
                }
            }
        }
    }

    private byte[]? ReadLineBytes()
    {
        _currentLine.Clear();
        int b;
        while ((b = _stream.ReadByte()) != -1)
        {
            if (b == '\n')
            {
                if (_currentLine.Count > 0 && _currentLine[_currentLine.Count - 1] == '\r')
                {
                    _currentLine.RemoveAt(_currentLine.Count - 1);
                }
                return _currentLine.ToArray();
            }
            _currentLine.Add((byte)b);
        }
        return _currentLine.Count > 0 ? _currentLine.ToArray() : null;
    }

    public void Reset() => throw new NotSupportedException();
    public void Dispose() => _stream.Dispose();
    public IEnumerator<MboxMessage> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => this;
}

#if STALWART_PORT_TESTS
[TestClass]
public class mbox_tests
{
    [TestMethod]
    public void parse_mbox()
    {
        byte[] message = System.Text.Encoding.UTF8.GetBytes(
            "From god@heaven.af.mil Sat Jan  3 01:05:34 1996\n" +
            "Message 1\n\n" +
            "From cras@irccrew.org  Tue Jul 23 19:39:23 2002\n" +
            "Message 2\n\n" +
            "From test@test.com Tue Aug  6 13:34:34 2002\n" +
            "Message 3\n" +
            ">From hello\n" +
            ">>From world\n" +
            ">>>From test\n\n" +
            "From other@domain.com Mon Jan 15  15:30:00  2018\n" +
            "Message 4\n" +
            "> From\n" +
            ">F\n"
        );

        var it = new MboxMessageIterator(message);
        var msgs = new List<MboxMessage>();
        foreach (var msg in it)
        {
            msgs.Add(msg);
        }

        Assert.AreEqual(4, msgs.Count);
        Assert.AreEqual((ulong)820631134, msgs[0].internal_date);
        Assert.AreEqual("god@heaven.af.mil", msgs[0].from);
        Assert.AreEqual("Message 1\n\n", System.Text.Encoding.UTF8.GetString(msgs[0].contents));

        Assert.AreEqual((ulong)1027453163, msgs[1].internal_date);
        Assert.AreEqual("cras@irccrew.org", msgs[1].from);
        Assert.AreEqual("Message 2\n\n", System.Text.Encoding.UTF8.GetString(msgs[1].contents));

        Assert.AreEqual((ulong)1028640874, msgs[2].internal_date);
        Assert.AreEqual("test@test.com", msgs[2].from);
        Assert.AreEqual("Message 3\nFrom hello\n>From world\n>>From test\n\n", System.Text.Encoding.UTF8.GetString(msgs[2].contents));

        Assert.AreEqual((ulong)1516030200, msgs[3].internal_date);
        Assert.AreEqual("other@domain.com", msgs[3].from);
        Assert.AreEqual("Message 4\n> From\n>F\n", System.Text.Encoding.UTF8.GetString(msgs[3].contents));
    }
}
#endif
