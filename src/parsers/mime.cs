/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/mime.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: e068e5f5b6688d62ac63806c8ca958d0ad5381c95421534672052b7bc766338b
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;

namespace Stalwart.MailParser.Port;

public partial class MessageStream
{
    // Rust: MessageStream::seek_next_part
    public bool seek_next_part(ReadOnlySpan<byte> boundary)
    {
        if (!boundary.IsEmpty)
        {
            byte last_ch = 0;
            checkpoint();

            while (true)
            {
                byte? chOpt = next();
                if (!chOpt.HasValue) break;
                byte ch = chOpt.Value;

                if (ch == (byte)'-' && last_ch == (byte)'-' && try_skip(boundary))
                {
                    return true;
                }

                last_ch = ch;
            }

            restore();
        }

        return false;
    }

    // Rust: MessageStream::seek_next_part_offset
    public int? seek_next_part_offset(ReadOnlySpan<byte> boundary)
    {
        byte last_ch = (byte)'\n';
        int offset_pos = offset();
        checkpoint();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            if (ch == (byte)'\n')
            {
                offset_pos = last_ch == (byte)'\r' ? offset() - 2 : offset() - 1;
            }
            else if (ch == (byte)'-' && last_ch == (byte)'-' && try_skip(boundary))
            {
                return offset_pos;
            }

            last_ch = ch;
        }

        restore();
        return null;
    }

    // Rust: MessageStream::mime_part
    public (int offset_end, byte[] bytes) mime_part(ReadOnlySpan<byte> boundary)
    {
        byte last_ch = (byte)'\n';
        byte before_last_ch = 0;
        int start_pos = offset();
        int end_pos = offset();

        checkpoint();

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            if (ch == (byte)'\n')
            {
                end_pos = last_ch == (byte)'\r' ? offset() - 2 : offset() - 1;
            }
            else if (ch == (byte)'-' && !boundary.IsEmpty && last_ch == (byte)'-' && try_skip(boundary))
            {
                if (before_last_ch != (byte)'\n')
                {
                    end_pos = offset() - boundary.Length - 2;
                }
                return (end_pos, bytes(start_pos, end_pos).ToArray());
            }

            before_last_ch = last_ch;
            last_ch = ch;
        }

        if (boundary.IsEmpty)
        {
            return (offset(), bytes(start_pos, len()).ToArray());
        }
        else
        {
            restore();
            return (int.MaxValue, bytes(start_pos, len()).ToArray());
        }
    }

    // Rust: MessageStream::seek_part_end
    public (int offset_end, bool boundary_found) seek_part_end(byte[]? boundary)
    {
        byte last_ch = (byte)'\n';
        byte before_last_ch = 0;
        int end_pos = offset();

        if (boundary != null && boundary.Length > 0)
        {
            while (true)
            {
                byte? chOpt = next();
                if (!chOpt.HasValue) break;
                byte ch = chOpt.Value;

                if (ch == (byte)'\n')
                {
                    end_pos = last_ch == (byte)'\r' ? offset() - 2 : offset() - 1;
                }
                else if (ch == (byte)'-' && last_ch == (byte)'-' && try_skip(boundary))
                {
                    if (before_last_ch != (byte)'\n')
                    {
                        end_pos = offset() - boundary.Length - 2;
                    }
                    return (end_pos, true);
                }

                before_last_ch = last_ch;
                last_ch = ch;
            }

            return (offset(), false);
        }
        else
        {
            seek_end();
            return (offset(), true);
        }
    }

    // Rust: MessageStream::is_multipart_end
    public bool is_multipart_end()
    {
        checkpoint();

        byte? n1 = next();
        byte? pk = peek();

        if (n1 == (byte)'\r' && pk == (byte)'\n')
        {
            next();
            return false;
        }
        if (n1 == (byte)'-' && pk == (byte)'-')
        {
            next();
            return true;
        }
        if (n1 == (byte)'\n')
        {
            return false;
        }
        // Rust: a.is_ascii_whitespace() (PARITY-AUDIT.md FILE 16: was Unicode
        // char.IsWhiteSpace, same recurring pattern fixed elsewhere via this helper).
        if (n1.HasValue && HeaderExtensions.IsAsciiWhitespace(n1.Value))
        {
            skip_crlf();
            return false;
        }

        restore();
        return false;
    }

    // Rust: MessageStream::skip_crlf
    public void skip_crlf()
    {
        while (true)
        {
            byte? ch = peek();
            if (!ch.HasValue) break;
            switch (ch.Value)
            {
                case (byte)'\r':
                case (byte)' ':
                case (byte)'\t':
                    next();
                    break;
                case (byte)'\n':
                    next();
                    return;
                default:
                    return;
            }
        }
    }
}
