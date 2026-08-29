/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/mod.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 2ec1e916149173286ad703a6f9023bea1d500d943d246fb60b38330f50ba4168
// This file must remain 1:1 with the Rust source file.

using System;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

// Rust: MessageStream
public partial class MessageStream
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _pos;
    private int _restore_pos;

    // Rust: MessageStream::new
    public MessageStream(ReadOnlyMemory<byte> data)
    {
        _data = data;
        _pos = 0;
        _restore_pos = 0;
    }

    public MessageStream(byte[] data) : this(new ReadOnlyMemory<byte>(data))
    {
    }

    // Rust: MessageStream::peek
    public byte? peek()
    {
        var span = _data.Span;
        return _pos < span.Length ? span[_pos] : null;
    }

    // Rust: MessageStream::offset
    public int offset()
    {
        return Math.Min(_pos, _data.Length);
    }

    // Rust: MessageStream::remaining
    public int remaining()
    {
        return _data.Length - offset();
    }

    // Rust: MessageStream::checkpoint
    public void checkpoint()
    {
        _restore_pos = offset();
    }

    // Rust: MessageStream::restore
    public void restore()
    {
        _pos = _restore_pos;
        _restore_pos = 0;
    }

    // Rust: MessageStream::reset
    public void reset()
    {
        _restore_pos = 0;
    }

    // Rust: MessageStream::peek_bytes
    public ReadOnlySpan<byte> peek_bytes(int len)
    {
        int p = offset();
        var span = _data.Span;
        if (p + len <= span.Length)
        {
            return span.Slice(p, len);
        }
        return ReadOnlySpan<byte>.Empty;
    }

    // Rust: MessageStream::peek_char
    public bool peek_char(byte ch)
    {
        var p = peek();
        return p.HasValue && p.Value == ch;
    }

    // Rust: MessageStream::skip_bytes
    public void skip_bytes(int len)
    {
        _pos += len;
    }

    // Rust: MessageStream::try_skip
    public bool try_skip(ReadOnlySpan<byte> bytes)
    {
        int p = offset();
        var span = _data.Span;
        if (p + bytes.Length <= span.Length && span.Slice(p, bytes.Length).SequenceEqual(bytes))
        {
            _pos += bytes.Length;
            return true;
        }
        return false;
    }

    // Rust: MessageStream::try_skip_char
    public bool try_skip_char(byte ch)
    {
        if (peek_char(ch))
        {
            next();
            return true;
        }
        return false;
    }

    // Rust: MessageStream::bytes -- `self.data.get(range).unwrap_or_default()`; see
    // bytes_span() below for the full explanation (same fix, same reasoning: reject an
    // out-of-bounds range entirely rather than clamping it -- PARITY-AUDIT.md FILE 19).
    public ReadOnlyMemory<byte> bytes(int start, int end)
    {
        if (start < 0 || end > _data.Length || start > end) return ReadOnlyMemory<byte>.Empty;
        return _data.Slice(start, end - start);
    }

    // Rust: MessageStream::bytes -- `self.data.get(range).unwrap_or_default()`. A range
    // is rejected (empty result) if EITHER start > end OR end > data.len() -- there is no
    // clamping. The prior version here clamped an out-of-bounds `end` down to the buffer
    // length instead of rejecting the whole request, which was a confirmed bug
    // (PARITY-AUDIT.md FILE 19): Rust returns empty for an invalid range; this returned a
    // truncated-but-nonempty slice instead.
    public ReadOnlySpan<byte> bytes_span(int start, int end)
    {
        if (start < 0 || end > _data.Length || start > end) return ReadOnlySpan<byte>.Empty;
        return _data.Span.Slice(start, end - start);
    }

    // Rust: MessageStream::seek_end
    public void seek_end()
    {
        _pos = _data.Length;
    }

    // Rust: MessageStream::next_is_space
    public bool next_is_space()
    {
        var b = next();
        return b == 0x20 || b == 0x09;
    }

    // Rust: MessageStream::peek_next_is_space
    public bool peek_next_is_space()
    {
        var b = peek();
        return b == 0x20 || b == 0x09;
    }

    // Rust: MessageStream::try_next_is_space
    public bool try_next_is_space()
    {
        if (peek_next_is_space())
        {
            next();
            return true;
        }
        return false;
    }

    // Rust: MessageStream::len
    public int len()
    {
        return _data.Length;
    }

    // Rust: MessageStream::is_eof
    public bool is_eof()
    {
        return _pos >= _data.Length;
    }

    // Rust: MessageStream::next
    public byte? next()
    {
        var span = _data.Span;
        if (_pos < span.Length)
        {
            return span[_pos++];
        }
        return null;
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class parsers_mod_tests
{
    [TestMethod]
    public void bytes_span_rejects_out_of_bounds_end_matches_rust()
    {
        // Rust: self.data.get(range).unwrap_or_default() -- an out-of-bounds `end`
        // rejects the WHOLE range (empty result), it does not clamp to a partial,
        // nonempty slice (PARITY-AUDIT.md FILE 19).
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes("0123456789"));
        Assert.AreEqual(0, stream.bytes_span(5, 1000).Length);
        Assert.AreEqual(0, stream.bytes(5, 1000).Length);
        // A valid range still works normally.
        Assert.AreEqual("56789", System.Text.Encoding.UTF8.GetString(stream.bytes_span(5, 10)));
    }
}
#endif
