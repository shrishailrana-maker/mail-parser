/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/body.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 73c309abcc08320146c858e5fb9cf1129093b69c4ef35c743262a8546d7092da
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Stalwart.MailParser.Port;

// Rust: BodyPartIterator
public class BodyPartIterator : IEnumerator<MessagePart>, IEnumerable<MessagePart>
{
    private readonly Message _message;
    private readonly List<uint> _list;
    private int _pos = -1;

    public BodyPartIterator(Message message, List<uint> list)
    {
        _message = message;
        _list = list;
    }

    public MessagePart Current => (_pos >= 0 && _pos < _list.Count && _list[_pos] < _message.parts.Count) ? _message.parts[(int)_list[_pos]] : null!;
    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        _pos++;
        return _pos < _list.Count && _list[_pos] < _message.parts.Count;
    }

    public void Reset() => _pos = -1;
    public void Dispose() { }
    public IEnumerator<MessagePart> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => this;
}

// Rust: AttachmentIterator
public class AttachmentIterator : IEnumerator<MessagePart>, IEnumerable<MessagePart>
{
    private readonly Message _message;
    private int _pos = -1;

    public AttachmentIterator(Message message)
    {
        _message = message;
    }

    public MessagePart Current => _message.attachment((uint)_pos)!;
    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        _pos++;
        return _message.attachment((uint)_pos) != null;
    }

    public void Reset() => _pos = -1;
    public void Dispose() { }
    public IEnumerator<MessagePart> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => this;
}
