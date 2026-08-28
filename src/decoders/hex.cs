/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/hex.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 7de84df7a6d856cd44ce521ebe64d174fb17cf0e89a8e90c7daa51193b68f11e
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static class HexUtils
{
    private enum HexState
    {
        None,
        Percent,
        Hex1,
    }

    // Rust: decode_hex
    public static (bool success, byte[] result) decode_hex(ReadOnlySpan<byte> src)
    {
        var state = HexState.None;
        int hex1 = 0;
        var result = new List<byte>(src.Length);
        bool success = true;

        foreach (byte ch in src)
        {
            switch (ch)
            {
                case (byte)'%':
                    if (state == HexState.None)
                    {
                        state = HexState.Percent;
                    }
                    else
                    {
                        success = false;
                        goto done;
                    }
                    break;
                default:
                    switch (state)
                    {
                        case HexState.None:
                            result.Add(ch);
                            break;
                        case HexState.Percent:
                            hex1 = QuotedPrintableUtils.HEX_MAP[ch];
                            if (hex1 != -1)
                            {
                                state = HexState.Hex1;
                            }
                            else
                            {
                                success = false;
                                goto done;
                            }
                            break;
                        case HexState.Hex1:
                            int hex2 = QuotedPrintableUtils.HEX_MAP[ch];
                            if (hex2 != -1)
                            {
                                result.Add((byte)((hex1 << 4) | hex2));
                                state = HexState.None;
                            }
                            else
                            {
                                success = false;
                                goto done;
                            }
                            break;
                    }
                    break;
            }
        }

    done:
        if (state != HexState.None)
        {
            success = false;
        }

        return (success, result.ToArray());
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class hex_tests
{
    [TestMethod]
    public void decode_hex_line()
    {
        var inputs = new (string input, string expected)[]
        {
            ("this%20is%20some%20text", "this is some text"),
            ("this is some text", "this is some text"),
        };

        foreach (var (input, expected) in inputs)
        {
            var (success, result) = HexUtils.decode_hex(System.Text.Encoding.UTF8.GetBytes(input));
            Assert.IsTrue(success, $"Failed for '{input}'");
            var resultStr = System.Text.Encoding.UTF8.GetString(result);
            Assert.AreEqual(expected, resultStr, $"Failed for '{input}'");
        }
    }
}
#endif
