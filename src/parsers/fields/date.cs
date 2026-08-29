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
//
// DateTimeUtils (the DateTime::to_timestamp/from_timestamp/to_rfc822/to_rfc3339/
// to_timezone/day_of_week implementation) was moved here from core/header.cs, where it
// had been misplaced (PARITY-AUDIT.md, PHASE 2, "header.cs reorganization" — Boss
// decision). Two bugs were fixed in the move: see PARITY-AUDIT.md FILE 4 for both.

using System;
using System.Collections.Generic;
using System.Globalization;
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

    public static bool IsLeapYear(int y) => (y % 4 == 0 && y % 100 != 0) || (y % 400 == 0);

    public static long ToTimestamp(DateTime dt)
    {
        long days = DaysFromCivil(dt.year, dt.month, dt.day);
        long secs = days * 86400L + (long)dt.hour * 3600L + (long)dt.minute * 60L + (long)dt.second;
        long tzOffsetSecs = ((long)dt.tz_hour * 3600L + (long)dt.tz_minute * 60L) * (dt.tz_before_gmt ? -1L : 1L);
        return secs - tzOffsetSecs;
    }

    public static long ToTimestampLocal(DateTime dt)
    {
        long days = DaysFromCivil(dt.year, dt.month, dt.day);
        return days * 86400L + (long)dt.hour * 3600L + (long)dt.minute * 60L + (long)dt.second;
    }

    public static DateTime FromTimestamp(long timestamp)
    {
        long secs = timestamp;
        long days = secs / 86400L;
        long rem = secs % 86400L;
        if (rem < 0)
        {
            rem += 86400L;
            days -= 1;
        }

        var (y, m, d) = CivilFromDays(days);
        byte hour = (byte)(rem / 3600L);
        rem %= 3600L;
        byte minute = (byte)(rem / 60L);
        byte second = (byte)(rem % 60L);

        return new DateTime((ushort)y, (byte)m, (byte)d, hour, minute, second, false, 0, 0);
    }

    private static long DaysFromCivil(int y, int m, int d)
    {
        if (m <= 2) y -= 1;
        long era = (y >= 0 ? y : y - 399) / 400;
        long yoe = y - era * 400;
        long doy = (153 * (m > 2 ? m - 3 : m + 9) + 2) / 5 + d - 1;
        long doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era * 146097 + doe - 719468;
    }

    private static (int year, int month, int day) CivilFromDays(long z)
    {
        z += 719468;
        long era = (z >= 0 ? z : z - 146096) / 146097;
        long doe = z - era * 146097;
        long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        long y = yoe + era * 400;
        long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        long mp = (5 * doy + 2) / 153;
        long d = doy - (153 * mp + 2) / 5 + 1;
        long m = mp < 10 ? mp + 3 : mp - 9;
        if (m <= 2) y += 1;
        return ((int)y, (int)m, (int)d);
    }

    public static string ToRfc3339(DateTime dt)
    {
        var sb = new StringBuilder();
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}T{3:D2}:{4:D2}:{5:D2}", dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second);
        if (dt.tz_hour == 0 && dt.tz_minute == 0)
        {
            sb.Append('Z');
        }
        else
        {
            sb.Append(dt.tz_before_gmt ? '-' : '+');
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", dt.tz_hour, dt.tz_minute);
        }
        return sb.ToString();
    }

    public static string ToRfc822(DateTime dt)
    {
        string[] days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        // Rust: MONTH.get(month.saturating_sub(1)).unwrap_or(&"") -- empty string for an
        // out-of-range month, NOT a fabricated "Jan" (PARITY-AUDIT.md FILE 4, confirmed bug).
        string[] months = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        int dow = DayOfWeek(dt);
        string dayName = (dow >= 0 && dow < 7) ? days[dow] : "Sun";
        string monthName = (dt.month >= 1 && dt.month <= 12) ? months[dt.month] : "";

        // Rust: a zero offset always prints '+', even when tz_before_gmt happens to be true
        // (PARITY-AUDIT.md FILE 4, confirmed bug: C# used to print '-0000' for that case).
        char tzSign = (dt.tz_before_gmt && (dt.tz_hour > 0 || dt.tz_minute > 0)) ? '-' : '+';
        return $"{dayName}, {dt.day} {monthName} {dt.year:D4} {dt.hour:D2}:{dt.minute:D2}:{dt.second:D2} {tzSign}{dt.tz_hour:D2}{dt.tz_minute:D2}";
    }

    public static int DayOfWeek(DateTime dt)
    {
        long days = DaysFromCivil(dt.year, dt.month, dt.day);
        int dow = (int)((days + 4) % 7);
        if (dow < 0) dow += 7;
        return dow;
    }

    public static DateTime ToTimezone(DateTime dt, long tzOffsetSecs)
    {
        long utcTs = ToTimestamp(dt);
        long targetLocalTs = utcTs + tzOffsetSecs;
        var res = FromTimestamp(targetLocalTs);
        bool before = tzOffsetSecs < 0;
        long absSecs = Math.Abs(tzOffsetSecs);
        byte tzH = (byte)(absSecs / 3600);
        byte tzM = (byte)((absSecs % 3600) / 60);
        return new DateTime(res.year, res.month, res.day, res.hour, res.minute, res.second, before, tzH, tzM);
    }
}

// Rust: impl DateTime (parse_rfc822/parse_rfc3339/julian_day) -- previously missing
// entirely (PARITY-AUDIT.md FILE 4). Added here as a partial struct extension, matching
// where Rust defines them.
public partial struct DateTime
{
    // Rust: DateTime::parse_rfc822
    public static DateTime? parse_rfc822(string value)
    {
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes(value));
        return stream.parse_date().as_datetime();
    }

    // Rust: DateTime::parse_rfc3339
    public static DateTime? parse_rfc3339(string value)
    {
        int pos = 0;
        uint[] parts = new uint[8];
        uint[] parts_sizes = new uint[8] { 4, 2, 2, 2, 2, 2, 2, 2 };
        bool skip_digits = false;
        bool is_plus = true;

        foreach (byte ch in System.Text.Encoding.UTF8.GetBytes(value))
        {
            if (ch >= (byte)'0' && ch <= (byte)'9' && !skip_digits)
            {
                if (parts_sizes[pos] > 0)
                {
                    parts_sizes[pos]--;
                    parts[pos] += (uint)(ch - (byte)'0') * (uint)Math.Pow(10, parts_sizes[pos]);
                }
                else
                {
                    return null;
                }
            }
            else if (ch == (byte)'-')
            {
                if (pos <= 1) { pos++; }
                else if (pos == 5) { pos++; is_plus = false; skip_digits = false; }
                else { return null; }
            }
            else if (ch == (byte)'T')
            {
                if (pos == 2) { pos++; }
                else { return null; }
            }
            else if (ch == (byte)':')
            {
                if (pos == 3 || pos == 4 || pos == 6) { pos++; }
                else { return null; }
            }
            else if (ch == (byte)'+')
            {
                if (pos == 5) { pos++; skip_digits = false; }
                else { return null; }
            }
            else if (ch == (byte)'.')
            {
                if (pos == 5) { skip_digits = true; }
                else { return null; }
            }
        }

        if (pos >= 5)
        {
            return new DateTime(
                (ushort)parts[0], (byte)parts[1], (byte)parts[2],
                (byte)parts[3], (byte)parts[4], (byte)parts[5],
                !is_plus, (byte)parts[6], (byte)parts[7]);
        }
        return null;
    }

    // Rust: DateTime::julian_day
    public long julian_day()
    {
        long day = this.day;
        long month, year;
        if (this.month > 2)
        {
            month = this.month - 3;
            year = this.year;
        }
        else
        {
            month = this.month + 9;
            year = this.year - 1;
        }

        long c = year / 100;
        return c * 146097 / 4 + (year - c * 100) * 1461 / 4 + (month * 153 + 2) / 5 + day + 1721119;
    }
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

        // Rust: hashify::tiny_map! does exact-byte matching against the uppercase literals
        // shown below -- no case-normalization step. The prior .ToUpperInvariant() here was
        // a confirmed bug (PARITY-AUDIT.md FILE 4 / cross-check #4): a lowercase obsolete
        // zone like "edt" must NOT match, same as Rust silently falling through to UTC.
        string s = System.Text.Encoding.ASCII.GetString(buf, 0, i);
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
    public void datetime_tostring_uses_rfc3339_not_rfc822_matches_rust()
    {
        // Rust: impl fmt::Display for DateTime -- self.to_rfc3339(). Was to_rfc822()
        // (PARITY-AUDIT.md; Boss's own review caught this was never actually fixed).
        var dt = new DateTime(2021, 11, 20, 14, 22, 1, true, 8, 0);
        Assert.AreEqual(dt.to_rfc3339(), dt.ToString());
        Assert.AreNotEqual(dt.to_rfc822(), dt.ToString());
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

    // Regression tests for Phase 2 fixes -- each pins a Rust-verified expected value.

    [TestMethod]
    public void parse_rfc822_matches_rust()
    {
        // Rust: DateTime::parse_rfc822("Mon, 1 Jan 2024 10:20:30 +0500") ==
        // Some(DateTime { year:2024, month:1, day:1, hour:10, minute:20, second:30,
        //                  tz_before_gmt:false, tz_hour:5, tz_minute:0 })
        var dt = DateTime.parse_rfc822("Mon, 1 Jan 2024 10:20:30 +0500");
        Assert.IsTrue(dt.HasValue);
        Assert.AreEqual(new DateTime(2024, 1, 1, 10, 20, 30, false, 5, 0), dt!.Value);
    }

    [TestMethod]
    public void parse_rfc3339_matches_rust()
    {
        // Rust: DateTime::parse_rfc3339("2004-06-28T23:43:45.000Z") ==
        // Some(DateTime { year:2004, month:6, day:28, hour:23, minute:43, second:45,
        //                  tz_before_gmt:false, tz_hour:0, tz_minute:0 })
        var dt = DateTime.parse_rfc3339("2004-06-28T23:43:45.000Z");
        Assert.IsTrue(dt.HasValue);
        Assert.AreEqual(new DateTime(2004, 6, 28, 23, 43, 45, false, 0, 0), dt!.Value);

        // Rust: DateTime::parse_rfc3339("1969-02-13T23:32:00-03:30") ==
        // Some(DateTime { year:1969, month:2, day:13, hour:23, minute:32, second:0,
        //                  tz_before_gmt:true, tz_hour:3, tz_minute:30 })
        var dt2 = DateTime.parse_rfc3339("1969-02-13T23:32:00-03:30");
        Assert.IsTrue(dt2.HasValue);
        Assert.AreEqual(new DateTime(1969, 2, 13, 23, 32, 0, true, 3, 30), dt2!.Value);
    }

    [TestMethod]
    public void julian_day_matches_rust()
    {
        // Rust: DateTime{year:2004,month:6,day:28,...}.julian_day() == 2453185
        var dt = new DateTime(2004, 6, 28, 0, 0, 0, false, 0, 0);
        Assert.AreEqual(2453185L, dt.julian_day());
    }

    [TestMethod]
    public void to_rfc822_zero_offset_before_gmt_matches_rust()
    {
        // Rust: a DateTime with tz_before_gmt=true but tz_hour==0 && tz_minute==0 still
        // prints '+', not '-' -- the sign guard is (tz_before_gmt && (tz_hour>0 || tz_minute>0)).
        var dt = new DateTime(2021, 1, 1, 0, 0, 0, true, 0, 0);
        Assert.AreEqual("Fri, 1 Jan 2021 00:00:00 +0000", dt.to_rfc822());
    }

    [TestMethod]
    public void to_rfc822_invalid_month_matches_rust()
    {
        // Rust: MONTH.get(month.saturating_sub(1)).unwrap_or(&"") -- an out-of-range month
        // renders as an empty string, not a fabricated "Jan".
        var dt = new DateTime(2021, 0, 1, 0, 0, 0, false, 0, 0);
        Assert.AreEqual("Tue, 1  2021 00:00:00 +0000", dt.to_rfc822());
    }

    [TestMethod]
    public void obs_zone_is_case_sensitive_matches_rust()
    {
        // Rust: hashify::tiny_map! matches exact bytes only -- a lowercase obsolete zone
        // is not recognized and falls through to UTC (+0000), unlike the prior C# bug
        // which uppercased before matching.
        var stream = new MessageStream(System.Text.Encoding.UTF8.GetBytes("1 Jan 2024 00:00:00 est\n"));
        var dt = stream.parse_date().as_datetime();
        Assert.IsTrue(dt.HasValue);
        Assert.AreEqual(false, dt!.Value.tz_before_gmt);
        Assert.AreEqual(0, dt.Value.tz_hour);
        Assert.AreEqual(0, dt.Value.tz_minute);
    }
}
#endif
