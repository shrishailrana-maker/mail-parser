/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/received.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 539c1345bd6429c65fc9955ef3c05f2af6e0635f3419edfe549c3d97bbef5e5a
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public enum ReceivedTokenType
{
    BracketOpen,
    BracketClose,
    AngleOpen,
    AngleClose,
    ParenthesisOpen,
    ParenthesisClose,
    Semicolon,
    Colon,
    Equal,
    Slash,
    Quote,
    Comma,
    IpAddr,
    Integer,
    Text,
    Domain,
    Email,
    Month,
    Protocol,
    Greeting,
    TlsVersion,
    Cipher,
    By,
    For,
    From,
    Id,
    Via,
    With,
    Ident,
}

public readonly struct ReceivedToken
{
    public readonly ReceivedTokenType Type;
    public readonly IPAddress? Ip;
    public readonly long IntVal;
    public readonly Month MonthVal;
    public readonly Protocol ProtoVal;
    public readonly Greeting GreetingVal;
    public readonly TlsVersion TlsVal;

    public ReceivedToken(ReceivedTokenType type)
    {
        Type = type;
        Ip = null;
        IntVal = 0;
        MonthVal = Month.Jan;
        ProtoVal = Protocol.SMTP;
        GreetingVal = Greeting.Helo;
        TlsVal = TlsVersion.TLSv1_0;
    }

    public ReceivedToken(IPAddress ip) : this(ReceivedTokenType.IpAddr) { Ip = ip; }
    public ReceivedToken(long intVal) : this(ReceivedTokenType.Integer) { IntVal = intVal; }
    public ReceivedToken(Month month) : this(ReceivedTokenType.Month) { MonthVal = month; }
    public ReceivedToken(Protocol proto) : this(ReceivedTokenType.Protocol) { ProtoVal = proto; }
    public ReceivedToken(Greeting greeting) : this(ReceivedTokenType.Greeting) { GreetingVal = greeting; }
    public ReceivedToken(TlsVersion tls) : this(ReceivedTokenType.TlsVersion) { TlsVal = tls; }

    public static ReceivedToken BracketOpen => new(ReceivedTokenType.BracketOpen);
    public static ReceivedToken BracketClose => new(ReceivedTokenType.BracketClose);
    public static ReceivedToken AngleOpen => new(ReceivedTokenType.AngleOpen);
    public static ReceivedToken AngleClose => new(ReceivedTokenType.AngleClose);
    public static ReceivedToken ParenthesisOpen => new(ReceivedTokenType.ParenthesisOpen);
    public static ReceivedToken ParenthesisClose => new(ReceivedTokenType.ParenthesisClose);
    public static ReceivedToken Semicolon => new(ReceivedTokenType.Semicolon);
    public static ReceivedToken Colon => new(ReceivedTokenType.Colon);
    public static ReceivedToken Equal => new(ReceivedTokenType.Equal);
    public static ReceivedToken Slash => new(ReceivedTokenType.Slash);
    public static ReceivedToken Quote => new(ReceivedTokenType.Quote);
    public static ReceivedToken Comma => new(ReceivedTokenType.Comma);
    public static ReceivedToken Text => new(ReceivedTokenType.Text);
    public static ReceivedToken Domain => new(ReceivedTokenType.Domain);
    public static ReceivedToken Email => new(ReceivedTokenType.Email);
    public static ReceivedToken Cipher => new(ReceivedTokenType.Cipher);
    public static ReceivedToken By => new(ReceivedTokenType.By);
    public static ReceivedToken For => new(ReceivedTokenType.For);
    public static ReceivedToken From => new(ReceivedTokenType.From);
    public static ReceivedToken Id => new(ReceivedTokenType.Id);
    public static ReceivedToken Via => new(ReceivedTokenType.Via);
    public static ReceivedToken With => new(ReceivedTokenType.With);
    public static ReceivedToken Ident => new(ReceivedTokenType.Ident);

    public bool is_separator() => Type is ReceivedTokenType.BracketOpen
        or ReceivedTokenType.BracketClose
        or ReceivedTokenType.AngleOpen
        or ReceivedTokenType.AngleClose
        or ReceivedTokenType.ParenthesisOpen
        or ReceivedTokenType.ParenthesisClose
        or ReceivedTokenType.Semicolon
        or ReceivedTokenType.Colon
        or ReceivedTokenType.Equal
        or ReceivedTokenType.Slash
        or ReceivedTokenType.Quote
        or ReceivedTokenType.Comma;
}

public readonly struct ReceivedTokenData
{
    public readonly ReceivedToken token;
    public readonly string text;
    public readonly uint comment_depth;
    public readonly uint bracket_depth;

    public ReceivedTokenData(ReceivedToken token, string text, uint comment_depth, uint bracket_depth)
    {
        this.token = token;
        this.text = text;
        this.comment_depth = comment_depth;
        this.bracket_depth = bracket_depth;
    }

    public ReceivedTokenData(ReceivedToken token) : this(token, "", 0, 0) { }
}

public enum Month
{
    Jan = 1,
    Feb = 2,
    Mar = 3,
    Apr = 4,
    May = 5,
    Jun = 6,
    Jul = 7,
    Aug = 8,
    Sep = 9,
    Oct = 10,
    Nov = 11,
    Dec = 12,
}

public class ReceivedTokenizer
{
    private readonly MessageStream stream;
    private ReceivedTokenData? next_token;
    private ReceivedTokenData? peeked;
    private bool eof;
    private bool in_quote;
    private uint bracket_depth;
    private uint comment_depth;
    private bool in_date;

    public ReceivedTokenizer(MessageStream stream)
    {
        this.stream = stream;
    }

    public ReceivedTokenData? peek()
    {
        if (peeked.HasValue) return peeked;
        peeked = next_token_internal();
        return peeked;
    }

    public ReceivedTokenData? next()
    {
        if (peeked.HasValue)
        {
            var tok = peeked.Value;
            peeked = null;
            return tok;
        }
        return next_token_internal();
    }

    private ReceivedTokenData? next_token_internal()
    {
        if (next_token.HasValue)
        {
            var tok = next_token.Value;
            next_token = null;
            return tok;
        }

        if (eof)
        {
            return null;
        }

        int start_pos = stream.offset();
        int end_pos = start_pos;

        uint n_total = 0;
        uint n_digit = 0;
        uint n_alpha = 0;
        uint n_hex = 0;
        uint n_at = 0;
        uint n_dot = 0;
        uint n_plus = 0;
        uint n_minus = 0;
        uint n_other = 0;
        uint n_colon = 0;
        uint n_utf = 0;
        uint n_uppercase = 0;
        uint n_underscore = 0;

        UInt128 hash = 0;
        int hash_shift = 0;

        uint comment_depth = this.comment_depth;
        uint bracket_depth = this.bracket_depth;

        while (true)
        {
            byte? chOpt = stream.next();
            if (!chOpt.HasValue)
            {
                eof = true;
                break;
            }
            byte ch = chOpt.Value;

            switch (ch)
            {
                case >= (byte)'0' and <= (byte)'9':
                    n_digit++;
                    if (hash_shift < 128)
                    {
                        hash |= ((UInt128)ch) << hash_shift;
                        hash_shift += 8;
                    }
                    break;
                case >= (byte)'a' and <= (byte)'f':
                    n_hex++;
                    if (hash_shift < 128)
                    {
                        hash |= ((UInt128)ch) << hash_shift;
                        hash_shift += 8;
                    }
                    break;
                case >= (byte)'g' and <= (byte)'z':
                    n_alpha++;
                    if (hash_shift < 128)
                    {
                        hash |= ((UInt128)ch) << hash_shift;
                        hash_shift += 8;
                    }
                    break;
                case >= (byte)'A' and <= (byte)'F':
                    n_hex++;
                    n_uppercase++;
                    if (hash_shift < 128)
                    {
                        hash |= ((UInt128)(ch - (byte)'A' + (byte)'a')) << hash_shift;
                        hash_shift += 8;
                    }
                    break;
                case >= (byte)'G' and <= (byte)'Z':
                    n_alpha++;
                    n_uppercase++;
                    if (hash_shift < 128)
                    {
                        hash |= ((UInt128)(ch - (byte)'A' + (byte)'a')) << hash_shift;
                        hash_shift += 8;
                    }
                    break;
                case (byte)'@':
                    n_at++;
                    break;
                case (byte)'.':
                    n_dot++;
                    break;
                case (byte)'+':
                    n_plus++;
                    break;
                case (byte)'-':
                    n_minus++;
                    break;
                case (byte)'\n':
                    if (!stream.try_next_is_space())
                    {
                        eof = true;
                        goto loop_done;
                    }
                    else if (n_total > 0)
                    {
                        goto loop_done;
                    }
                    else
                    {
                        start_pos = stream.offset();
                        end_pos = start_pos;
                        continue;
                    }
                case (byte)'(':
                    if (!in_quote)
                    {
                        this.comment_depth = this.comment_depth == uint.MaxValue ? uint.MaxValue : this.comment_depth + 1;
                    }
                    next_token = new ReceivedTokenData(ReceivedToken.ParenthesisOpen);
                    goto loop_done;
                case (byte)')':
                    if (!in_quote)
                    {
                        this.comment_depth = this.comment_depth > 0 ? this.comment_depth - 1 : 0;
                    }
                    next_token = new ReceivedTokenData(ReceivedToken.ParenthesisClose);
                    goto loop_done;
                case (byte)'<':
                    next_token = new ReceivedTokenData(ReceivedToken.AngleOpen);
                    goto loop_done;
                case (byte)'>':
                    next_token = new ReceivedTokenData(ReceivedToken.AngleClose);
                    goto loop_done;
                case (byte)'[':
                    if (!in_quote)
                    {
                        this.bracket_depth = this.comment_depth == uint.MaxValue ? uint.MaxValue : this.comment_depth + 1;
                    }
                    next_token = new ReceivedTokenData(ReceivedToken.BracketOpen);
                    goto loop_done;
                case (byte)']':
                    if (!in_quote)
                    {
                        this.bracket_depth = this.comment_depth > 0 ? this.comment_depth - 1 : 0;
                    }
                    next_token = new ReceivedTokenData(ReceivedToken.BracketClose);
                    goto loop_done;
                case (byte)':':
                    if (in_date || n_at > 0 || n_dot > 0 || n_alpha > 0 || n_other > 0 || n_plus > 0 || n_minus > 0 || n_utf > 0 || n_colon == 7)
                    {
                        next_token = new ReceivedTokenData(ReceivedToken.Colon);
                        goto loop_done;
                    }
                    else
                    {
                        n_colon++;
                    }
                    break;
                case (byte)'=':
                    next_token = new ReceivedTokenData(ReceivedToken.Equal);
                    goto loop_done;
                case (byte)';':
                    if (this.comment_depth == 0)
                    {
                        in_date = true;
                    }
                    next_token = new ReceivedTokenData(ReceivedToken.Semicolon);
                    goto loop_done;
                case (byte)'/':
                    next_token = new ReceivedTokenData(ReceivedToken.Slash);
                    goto loop_done;
                case (byte)'"':
                    in_quote = !in_quote;
                    next_token = new ReceivedTokenData(ReceivedToken.Quote);
                    goto loop_done;
                case (byte)',':
                    next_token = new ReceivedTokenData(ReceivedToken.Comma);
                    goto loop_done;
                case (byte)' ' or (byte)'\t' or (byte)'\r':
                    if (n_total > 0)
                    {
                        goto loop_done;
                    }
                    else
                    {
                        start_pos++;
                        end_pos = start_pos;
                        continue;
                    }
                case >= 0x7f:
                    n_utf++;
                    break;
                case (byte)'_':
                    n_underscore++;
                    n_other++;
                    break;
                default:
                    n_other++;
                    break;
            }

            n_total++;
            end_pos = stream.offset();
        }

    loop_done:
        if (n_total == 0)
        {
            if (next_token.HasValue)
            {
                var tok = next_token.Value;
                next_token = null;
                return tok;
            }
            return null;
        }

        var textBytes = stream.bytes(start_pos, end_pos);
        string text = System.Text.Encoding.UTF8.GetString(textBytes.Span);

        ReceivedToken token;

        if (n_alpha == 0 && n_digit is >= 4 and <= 12 && n_hex == 0 && n_dot == 3 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0)
        {
            token = IPAddress.TryParse(text, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? new ReceivedToken(ip) : ReceivedToken.Text;
        }
        else if ((n_alpha == 0 && n_hex is >= 1 and <= 32 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon >= 2 && n_plus == 0 && n_minus == 0 && n_utf == 0)
            || (n_alpha == 0 && n_digit is >= 1 and <= 32 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon >= 2 && n_plus == 0 && n_minus == 0 && n_utf == 0)
            || (n_alpha == 0 && n_digit is >= 4 and <= 12 && n_hex == 4 && n_dot == 3 && n_at == 0 && n_other == 0 && n_colon == 3 && n_plus == 0 && n_minus == 0 && n_utf == 0))
        {
            token = IPAddress.TryParse(text, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? new ReceivedToken(ip) : ReceivedToken.Text;
        }
        else if ((n_alpha == 0 && n_digit >= 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0)
            || (n_alpha == 0 && n_digit >= 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 1 && n_utf == 0)
            || (n_alpha == 0 && n_digit >= 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 1 && n_minus == 0 && n_utf == 0))
        {
            token = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long num) ? new ReceivedToken(num) : ReceivedToken.Text;
        }
        else if ((n_alpha >= 1 && n_at == 1) || (n_hex >= 1 && n_at == 1))
        {
            token = ReceivedToken.Email;
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x727061)
        {
            token = new ReceivedToken(Month.Apr);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d7362)
        {
            token = new ReceivedToken(Protocol.SMTP);
        }
        else if (n_alpha == 1 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7962)
        {
            token = ReceivedToken.By;
        }
        else if (n_alpha == 0 && n_digit == 0 && n_hex == 3 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x636564)
        {
            token = new ReceivedToken(Month.Dec);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6f6c6865)
        {
            token = new ReceivedToken(Greeting.Ehlo);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d7365)
        {
            token = new ReceivedToken(Protocol.ESMTP);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6170746d7365)
        {
            token = new ReceivedToken(Protocol.ESMTPA);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7370746d7365)
        {
            token = new ReceivedToken(Protocol.ESMTPS);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x726f66)
        {
            token = ReceivedToken.For;
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6d6f7266)
        {
            token = ReceivedToken.From;
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6f6c6568)
        {
            token = new ReceivedToken(Greeting.Helo);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70747468)
        {
            token = new ReceivedToken(Protocol.HTTP);
        }
        else if (n_alpha == 7 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7473657270747468)
        {
            token = new ReceivedToken(Protocol.HTTP);
        }
        else if (n_alpha == 1 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6469)
        {
            token = ReceivedToken.Id;
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70616d69)
        {
            token = new ReceivedToken(Protocol.IMAP);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6e616a)
        {
            token = new ReceivedToken(Month.Jan);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6c756a)
        {
            token = new ReceivedToken(Month.Jul);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6e756a)
        {
            token = new ReceivedToken(Month.Jun);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6f6c686c)
        {
            token = new ReceivedToken(Greeting.Lhlo);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d6c)
        {
            token = new ReceivedToken(Protocol.LMTP);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6170746d6c)
        {
            token = new ReceivedToken(Protocol.LMTPA);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6c61636f6c)
        {
            token = new ReceivedToken(Protocol.Local);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d736c)
        {
            token = new ReceivedToken(Protocol.LMTP);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x72616d)
        {
            token = new ReceivedToken(Month.Mar);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x79616d)
        {
            token = new ReceivedToken(Month.May);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x766f6e)
        {
            token = new ReceivedToken(Month.Nov);
        }
        else if (n_alpha == 3 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x33706f70)
        {
            token = new ReceivedToken(Protocol.POP3);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x706573)
        {
            token = new ReceivedToken(Month.Sep);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d73)
        {
            token = new ReceivedToken(Protocol.SMTP);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6470746d73)
        {
            token = new ReceivedToken(Protocol.SMTP);
        }
        else if (n_alpha == 6 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x63767370746d73)
        {
            token = new ReceivedToken(Protocol.SMTP);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x74656b636f73)
        {
            token = new ReceivedToken(Protocol.Local);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x6e69647473)
        {
            token = new ReceivedToken(Protocol.Local);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x616976)
        {
            token = ReceivedToken.Via;
        }
        else if (n_alpha == 0 && n_digit == 0 && n_hex == 3 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x626566)
        {
            token = new ReceivedToken(Month.Feb);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x677561)
        {
            token = new ReceivedToken(Month.Aug);
        }
        else if (n_alpha == 2 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x74636f)
        {
            token = new ReceivedToken(Month.Oct);
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x68746977)
        {
            token = ReceivedToken.With;
        }
        else if (n_alpha == 4 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d7361)
        {
            token = new ReceivedToken(Protocol.ESMTPA);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7570747468)
        {
            token = new ReceivedToken(Protocol.HTTP);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7370747468)
        {
            token = new ReceivedToken(Protocol.HTTPS);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x746e656469)
        {
            token = ReceivedToken.Ident;
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x617370746d7365)
        {
            token = new ReceivedToken(Protocol.ESMTPSA);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x7370746d6c)
        {
            token = new ReceivedToken(Protocol.LMTPS);
        }
        else if (n_alpha == 5 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x617370746d6c)
        {
            token = new ReceivedToken(Protocol.LMTPSA);
        }
        else if (n_alpha == 3 && n_digit == 0 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x736d6d)
        {
            token = new ReceivedToken(Protocol.MMS);
        }
        else if (n_alpha == 6 && n_digit == 1 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d7338667475)
        {
            token = new ReceivedToken(Protocol.UTF8SMTP);
        }
        else if (n_alpha == 6 && n_digit == 1 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x61UL, 0x70746d7338667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8SMTPA);
        }
        else if (n_alpha == 7 && n_digit == 1 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x73UL, 0x70746d7338667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8SMTPS);
        }
        else if (n_alpha == 7 && n_digit == 1 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x6173UL, 0x70746d7338667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8SMTPSA);
        }
        else if (n_alpha == 6 && n_digit == 1 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x70746d6c38667475)
        {
            token = new ReceivedToken(Protocol.UTF8LMTP);
        }
        else if (n_alpha == 6 && n_digit == 1 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x61UL, 0x70746d6c38667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8LMTPA);
        }
        else if (n_alpha == 7 && n_digit == 1 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x73UL, 0x70746d6c38667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8LMTPS);
        }
        else if (n_alpha == 7 && n_digit == 1 && n_hex == 2 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == new UInt128(0x6173UL, 0x70746d6c38667475UL))
        {
            token = new ReceivedToken(Protocol.UTF8LMTPSA);
        }
        else if (n_alpha == 7 && n_digit == 0 && n_hex == 3 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == new UInt128(0x7074UL, 0x6d73656c61636f6cUL))
        {
            token = new ReceivedToken(Protocol.ESMTP);
        }
        else if (n_alpha == 8 && n_digit == 0 && n_hex == 3 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == new UInt128(0x737074UL, 0x6d73656c61636f6cUL))
        {
            token = new ReceivedToken(Protocol.ESMTPS);
        }
        else if (n_alpha == 7 && n_digit == 0 && n_hex == 3 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == new UInt128(0x7074UL, 0x6d73626c61636f6cUL))
        {
            token = new ReceivedToken(Protocol.SMTP);
        }
        else if (n_alpha == 7 && n_digit == 0 && n_hex == 1 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == 0x736c7470746d7365)
        {
            token = new ReceivedToken(Protocol.ESMTPS);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == 0x3031736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_0);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == 0x3131736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_1);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == 0x3231736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_2);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0 && hash == 0x3331736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_3);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x303176736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_0);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x313176736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_1);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x323176736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_2);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x333176736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_3);
        }
        else if (n_alpha == 3 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x326c7373)
        {
            token = new ReceivedToken(TlsVersion.SSLv2);
        }
        else if (n_alpha == 3 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x336c7373)
        {
            token = new ReceivedToken(TlsVersion.SSLv3);
        }
        else if (n_alpha == 4 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x32766c7373)
        {
            token = new ReceivedToken(TlsVersion.SSLv2);
        }
        else if (n_alpha == 4 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x33766c7373)
        {
            token = new ReceivedToken(TlsVersion.SSLv3);
        }
        else if (n_alpha == 3 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x31736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_0);
        }
        else if (n_alpha == 4 && n_digit == 1 && n_hex == 0 && n_dot == 0 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x3176736c74)
        {
            token = new ReceivedToken(TlsVersion.TLSv1_0);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x303176736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_0);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x323176736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_2);
        }
        else if (n_alpha == 4 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x333176736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_3);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x3031736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_0);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x3231736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_2);
        }
        else if (n_alpha == 3 && n_digit == 2 && n_hex == 1 && n_at == 0 && n_colon == 0 && n_plus == 0 && n_minus == 0 && n_utf == 0 && hash == 0x3331736c7464)
        {
            token = new ReceivedToken(TlsVersion.DTLSv1_3);
        }
        else if ((n_alpha >= 1 && n_dot >= 1 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0)
            || (n_hex >= 1 && n_dot >= 1 && n_at == 0 && n_other == 0 && n_colon == 0 && n_plus == 0 && n_utf == 0))
        {
            token = ReceivedToken.Domain;
        }
        else
        {
            ulong hashLow = (ulong)(hash & 0xffffff);
            if (n_alpha + n_hex == n_uppercase
                && n_total > 6
                && (n_underscore > 0 || n_minus > 0)
                && n_digit > 0
                && n_dot == 0
                && n_at == 0
                && n_plus == 0
                && (n_other == 0 || n_other == n_underscore)
                && n_colon == 0
                && n_utf == 0
                && (hashLow is 0x617372 or 0x646365 or 0x656864 or 0x6b7370 or 0x707273 or 0x736561 or 0x736564 or 0x736c74))
            {
                token = ReceivedToken.Cipher;
            }
            else
            {
                token = ReceivedToken.Text;
            }
        }

        return new ReceivedTokenData(text: text, token: token, comment_depth: comment_depth, bracket_depth: bracket_depth);
    }
}

public partial class MessageStream
{
    private enum ReceivedState
    {
        From,
        By,
        For,
        Id,
        With,
        Via,
        Date,
        None,
    }

    // Rust: MessageStream::parse_received
    public HeaderValue parse_received()
    {
        var tokenizer = new ReceivedTokenizer(this);
        var received = new Received();

        var state = ReceivedState.None;
        long[] date = new long[7] { long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue };
        int dateIdx = 0;

        ReceivedTokenData? tokenOpt;
        while ((tokenOpt = tokenizer.next()).HasValue)
        {
            var token = tokenOpt.Value;
            switch (token.token.Type)
            {
                case ReceivedTokenType.From when received.from == null:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type == ReceivedTokenType.BracketOpen)
                        {
                            tokenizer.next();
                        }
                        else if (peekTok.token.Type == ReceivedTokenType.IpAddr)
                        {
                            tokenizer.next();
                            received.from = Host.IpAddr(peekTok.token.Ip!);
                            break;
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.from = Host.Name(tokenizer.next()!.Value.text);
                            }
                            break;
                        }
                    }
                    state = ReceivedState.From;
                    break;
                case ReceivedTokenType.By when token.comment_depth == 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type is ReceivedTokenType.BracketOpen or ReceivedTokenType.AngleOpen)
                        {
                            tokenizer.next();
                        }
                        else if (peekTok.token.Type == ReceivedTokenType.IpAddr)
                        {
                            tokenizer.next();
                            received.by = Host.IpAddr(peekTok.token.Ip!);
                            break;
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.by = Host.Name(tokenizer.next()!.Value.text);
                            }
                            break;
                        }
                    }
                    state = ReceivedState.By;
                    break;
                case ReceivedTokenType.For when token.comment_depth == 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type is ReceivedTokenType.Equal or ReceivedTokenType.AngleOpen)
                        {
                            tokenizer.next();
                        }
                        else if (peekTok.token.Type == ReceivedTokenType.Email)
                        {
                            received.for_ = tokenizer.next()!.Value.text;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                    state = ReceivedState.For;
                    break;
                case ReceivedTokenType.Semicolon when token.comment_depth == 0:
                    state = ReceivedState.Date;
                    break;
                case ReceivedTokenType.Id when token.comment_depth == 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type is ReceivedTokenType.Equal or ReceivedTokenType.AngleOpen or ReceivedTokenType.BracketOpen or ReceivedTokenType.Colon)
                        {
                            tokenizer.next();
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.id = tokenizer.next()!.Value.text;
                            }
                            break;
                        }
                    }
                    state = ReceivedState.Id;
                    break;
                case ReceivedTokenType.With when token.comment_depth == 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type == ReceivedTokenType.Protocol)
                        {
                            tokenizer.next();
                            received.with = peekTok.token.ProtoVal;
                            break;
                        }
                        else if (peekTok.token.Type is ReceivedTokenType.Semicolon or ReceivedTokenType.TlsVersion or ReceivedTokenType.By or ReceivedTokenType.For or ReceivedTokenType.From or ReceivedTokenType.Id or ReceivedTokenType.Via or ReceivedTokenType.With)
                        {
                            break;
                        }
                        else
                        {
                            tokenizer.next();
                        }
                    }
                    state = ReceivedState.With;
                    break;
                case ReceivedTokenType.Via when token.comment_depth == 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type == ReceivedTokenType.Equal)
                        {
                            tokenizer.next();
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.via = tokenizer.next()!.Value.text;
                            }
                            break;
                        }
                    }
                    state = ReceivedState.Via;
                    break;
                case ReceivedTokenType.Ident when token.comment_depth > 0:
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type is ReceivedTokenType.Equal or ReceivedTokenType.AngleOpen or ReceivedTokenType.BracketOpen or ReceivedTokenType.Colon)
                        {
                            tokenizer.next();
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.ident = tokenizer.next()!.Value.text;
                            }
                            break;
                        }
                    }
                    break;
                case ReceivedTokenType.Greeting when state == ReceivedState.From && token.comment_depth > 0:
                    received.helo_cmd = token.token.GreetingVal;
                    while (tokenizer.peek().HasValue)
                    {
                        var peekTok = tokenizer.peek()!.Value;
                        if (peekTok.token.Type is ReceivedTokenType.Equal or ReceivedTokenType.BracketOpen or ReceivedTokenType.Colon)
                        {
                            tokenizer.next();
                        }
                        else if (peekTok.token.Type == ReceivedTokenType.IpAddr)
                        {
                            tokenizer.next();
                            received.helo = Host.IpAddr(peekTok.token.Ip!);
                            break;
                        }
                        else
                        {
                            if (!peekTok.token.is_separator())
                            {
                                received.helo = Host.Name(tokenizer.next()!.Value.text);
                            }
                            break;
                        }
                    }
                    break;
                case ReceivedTokenType.IpAddr when state == ReceivedState.From && (token.bracket_depth > 0 || (token.comment_depth > 0 && received.from_ip == null)):
                    received.from_ip = token.token.Ip;
                    break;
                case ReceivedTokenType.Domain when state == ReceivedState.From && token.comment_depth > 0:
                    received.from_iprev = token.text;
                    break;
                case ReceivedTokenType.Email when state == ReceivedState.From:
                    received.ident = token.text.EndsWith('@') ? token.text[..^1] : token.text;
                    break;
                case ReceivedTokenType.Integer when state == ReceivedState.Date:
                    if (dateIdx < 7)
                    {
                        date[dateIdx++] = token.token.IntVal;
                    }
                    break;
                case ReceivedTokenType.Month when state == ReceivedState.Date:
                    if (dateIdx < 7)
                    {
                        date[dateIdx++] = (long)token.token.MonthVal;
                    }
                    break;
                case ReceivedTokenType.Cipher when token.comment_depth > 0 || received.tls_cipher == null:
                    received.tls_cipher = token.text;
                    break;
                case ReceivedTokenType.TlsVersion when token.comment_depth > 0 && received.tls_version == null:
                    received.tls_version = token.token.TlsVal;
                    break;
            }
        }

        if (date[5] != long.MaxValue)
        {
            long tz;
            bool is_plus;
            if (date[6] != long.MaxValue)
            {
                if (date[6] < 0)
                {
                    tz = Math.Abs(date[6]);
                    is_plus = false;
                }
                else
                {
                    tz = date[6];
                    is_plus = true;
                }
            }
            else
            {
                tz = 0;
                is_plus = false;
            }

            ushort yr = (ushort)(date[2] is >= 1 and <= 99 ? date[2] + 1900 : date[2]);
            received.date = new DateTime(
                yr,
                (byte)date[1],
                (byte)date[0],
                (byte)date[3],
                (byte)date[4],
                (byte)date[5],
                !is_plus,
                (byte)(tz / 100),
                (byte)(tz % 100)
            );
        }

        if (received.from != null
            || received.from_ip != null
            || received.from_iprev != null
            || received.by != null
            || received.for_ != null
            || received.with != null
            || received.tls_version != null
            || received.tls_cipher != null
            || received.id != null
            || received.ident != null
            || received.helo != null
            || received.helo_cmd != null
            || received.via != null
            || received.date != null)
        {
            return HeaderValue.Received(received);
        }
        else
        {
            return HeaderValue.Empty;
        }
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class received_tests
{
    [TestMethod]
    public void parse_received()
    {
        var tests = FieldTestUtils.load_tests<Received>("received");
        Assert.AreEqual(189, tests.Count);
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var res = stream.parse_received();
            var r = res.as_received();
            Assert.AreEqual(test.expected, r, $"Failed for {test.header}");
        }
    }

    [TestMethod]
    public void parse_received_truncated_fold()
    {
        byte[][] messages = new byte[][]
        {
            "Received:\n\t"u8.ToArray(),
            "Received:\r\n "u8.ToArray(),
            "Received: x\r\n "u8.ToArray(),
            "Received: x\r\n\t"u8.ToArray(),
            "Received: x\n "u8.ToArray(),
            "Received: x\n\t"u8.ToArray(),
            "Received: x\r\n  "u8.ToArray(),
            "Received: x\r\n \t"u8.ToArray(),
            "Received: x\r\n \t "u8.ToArray(),
            "Received: x\r\n \r"u8.ToArray(),
            "Received: x\r\n \r\n"u8.ToArray(),
            "Received: x\r\n \r\nbody"u8.ToArray(),
            "Received: x\r\n y\r\n "u8.ToArray(),
            "Received: x;\r\n "u8.ToArray(),
            "To: a@b\r\nReceived: x\r\n "u8.ToArray(),
            "Received: from x (y)\r\n\t"u8.ToArray(),
            "Received: from x by y;\r\n\t"u8.ToArray(),
            "Received: "u8.ToArray(),
            "Received:"u8.ToArray(),
            "Received: x"u8.ToArray(),
        };

        Assert.AreEqual(20, messages.Length);
        foreach (var message in messages)
        {
            new MessageParser().parse(message);
            new MessageParser().parse_headers(message);
        }
    }
}
#endif
