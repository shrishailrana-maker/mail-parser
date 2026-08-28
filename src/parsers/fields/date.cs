/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/date.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 46ec7c5d6626b2dd3844b4dee4a5541b3dbcbac842193e1555ae4d76419b7510
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public static partial class DateTimeUtils
{
    public static readonly string[] DOW = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    public static readonly string[] MONTH = new string[]
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

        public static readonly byte[] MONTH_MAP = new byte[31] { 5, 0, 0, 0, 10, 3, 0, 0, 0, 7, 1, 0, 0, 0, 12, 6, 0, 0, 0, 8, 4, 0, 0, 0, 2, 9, 0, 0, 0, 0, 11 };

        public static readonly byte[] MONTH_HASH = new byte[256] { 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 0, 14, 4, 31, 10, 31, 14, 31, 31, 31, 31, 4, 31, 10, 15, 15, 31, 5, 31, 0, 5, 15, 31, 31, 0, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31 };

}

public partial class MessageStream
{
    // Rust: MessageStream::parse_date
    public HeaderValue parse_date()
    {
        int pos = 0;
        uint[] parts = new uint[7];
        uint[] parts_sizes = new uint[7] { 2, 2, 4, 2, 2, 2, 4 };
        int month_hash = 0;
        int month_pos = 0;

        bool is_plus = true;
        bool is_new_token = true;
        bool ignore = true;
        int comment_count = 0;

        while (true)
        {
            byte? chOpt = next();
            if (!chOpt.HasValue) break;
            byte ch = chOpt.Value;

            bool next_part = false;

            switch (ch)
            {
                case (byte)'\n':
                    if (try_next_is_space())
                    {
                        if (!is_new_token && !ignore && comment_count == 0)
                        {
                            next_part = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        goto parse_date_done;
                    }
                    break;
                case var _ when comment_count > 0:
                    if (ch == (byte)')')
                    {
                        comment_count--;
                    }
                    else if (ch == (byte)'(')
                    {
                        comment_count++;
                    }
                    else if (ch == (byte)'\\')
                    {
                        try_skip_char((byte)')');
                    }
                    continue;
                case >= (byte)'0' and <= (byte)'9':
                    if (pos < 7 && parts_sizes[pos] > 0)
                    {
                        parts_sizes[pos]--;
                        parts[pos] += (uint)(ch - (byte)'0') * (uint)Math.Pow(10, parts_sizes[pos]);
                        ignore = false;
                    }
                    is_new_token = false;
                    break;
                case (byte)':':
                    if (!is_new_token && !ignore && (pos == 3 || pos == 4))
                    {
                        next_part = true;
                    }
                    break;
                case (byte)'+':
                    pos = 6;
                    break;
                case (byte)'-':
                    is_plus = false;
                    pos = 6;
                    break;
                case (byte)' ' or (byte)'\t':
                    if (!is_new_token && !ignore)
                    {
                        next_part = true;
                    }
                    break;
                case >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z':
                    if (pos == 1)
                    {
                        if (month_pos >= 1 && month_pos <= 2)
                        {
                            byte bLow = ch <= (byte)'Z' ? (byte)(ch + 32) : ch;
                            month_hash += DateTimeUtils.MONTH_HASH[bLow];
                        }
                        month_pos++;
                    }
                    if (pos == 6)
                    {
                        byte[] buf = new byte[3] { ch, 0, 0 };
                        int zone = obs_zone(buf);
                        is_plus = zone >= 0;
                        parts[pos] = (uint)(100 * Math.Abs(zone));
                        parts_sizes[pos] = 0;
                        next_part = true;
                    }
                    is_new_token = false;
                    break;
                case (byte)'(':
                    comment_count++;
                    is_new_token = true;
                    continue;
                case (byte)',' or (byte)'\r':
                    break;
                case (byte)';':
                    pos = 0;
                    parts = new uint[7];
                    parts_sizes = new uint[7] { 2, 2, 4, 2, 2, 2, 4 };
                    month_hash = 0;
                    month_pos = 0;
                    is_plus = true;
                    is_new_token = true;
                    ignore = true;
                    continue;
            }

            if (next_part)
            {
                if (pos < 7 && parts_sizes[pos] > 0)
                {
                    parts[pos] /= (uint)Math.Pow(10, parts_sizes[pos]);
                }
                pos++;
                is_new_token = true;
            }
        }

    parse_date_done:

        if (pos >= 6)
        {
            byte month = month_pos == 3 ? (month_hash < DateTimeUtils.MONTH_MAP.Length ? DateTimeUtils.MONTH_MAP[month_hash] : (byte)0) : (byte)parts[1];
            if (month < 1 || month > 12)
            {
                return HeaderValue.Empty;
            }

            ushort year = parts[2] switch
            {
                >= 0 and <= 49 => (ushort)(parts[2] + 2000),
                >= 50 and <= 99 => (ushort)(parts[2] + 1900),
                _ => (ushort)parts[2]
            };

            return HeaderValue.DateTime(new DateTime(
                year,
                month,
                (byte)parts[0],
                (byte)parts[3],
                (byte)parts[4],
                (byte)parts[5],
                !is_plus,
                (byte)((parts[6] / 100) % 24),
                (byte)((parts[6] % 100) % 60)
            ));
        }

        return HeaderValue.Empty;
    }

    private int obs_zone(byte[] buf)
    {
        int i = 1;
        while (i < 3)
        {
            byte? b = next();
            if (!b.HasValue) break;
            buf[i++] = b.Value;
        }

        string s = System.Text.Encoding.ASCII.GetString(buf, 0, i).ToUpperInvariant();
        return s switch
        {
            "EDT" => -4,
            "EST" => -5,
            "CDT" => -5,
            "CST" => -6,
            "MDT" => -6,
            "MST" => -7,
            "PDT" => -7,
            "PST" => -8,
            _ => 0
        };
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class date_tests
{
    [TestMethod]
    public void parse_dates()
    {
        var tests = FieldTestUtils.load_tests<DateTime?>("date");
        Assert.AreEqual(40, tests.Count);
        foreach (var test in tests)
        {
            var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(test.header));
            var res = stream.parse_date();
            var dt = res.as_datetime();
            Assert.AreEqual(test.expected, dt, $"Failed for {test.header}");

            if (dt.HasValue && dt.Value.is_valid())
            {
                long ts = dt.Value.to_timestamp();
                Assert.AreEqual(DateTime.from_timestamp(ts).to_timestamp(), ts);
            }
        }
    }

    [TestMethod]
    public void datetime_to_timezone()
    {
        var dt = new DateTime(2021, 1, 1, 0, 0, 0, false, 0, 0);

        var cases = new (long tz, string expected)[]
        {
            (0L, "2021-01-01T00:00:00Z"),
            (3600L, "2021-01-01T01:00:00+01:00"),
            (-3600L, "2020-12-31T23:00:00-01:00"),
            (19800L, "2021-01-01T05:30:00+05:30"),
            (-12600L, "2020-12-31T20:30:00-03:30"),
            (20700L, "2021-01-01T05:45:00+05:45"),
            (16200L, "2021-01-01T04:30:00+04:30"),
            (34200L, "2021-01-01T09:30:00+09:30"),
            (-45900L, "2020-12-31T11:15:00-12:45"),
        };

        foreach (var (tz, expected) in cases)
        {
            var converted = dt.to_timezone(tz);
            Assert.AreEqual(expected, converted.to_rfc3339(), $"failed for tz {tz}");
            Assert.IsTrue(converted.is_valid(), $"invalid datetime for tz {tz}");
            Assert.AreEqual(dt.to_timestamp(), converted.to_timestamp(), $"roundtrip failed for tz {tz}");
        }
    }
}
#endif
