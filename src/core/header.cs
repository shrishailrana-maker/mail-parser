/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/header.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 7123ebb9fb2b6ae80ed79d739c14f0c105d2b1f0ea538365f8a16608ab6a2416
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Stalwart.MailParser.Port;

public static partial class DateTimeUtils
{
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
        string[] months = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        int dow = DayOfWeek(dt);
        string dayName = (dow >= 0 && dow < 7) ? days[dow] : "Sun";
        string monthName = (dt.month >= 1 && dt.month <= 12) ? months[dt.month] : "Jan";

        char tzSign = dt.tz_before_gmt ? '-' : '+';
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

public static class HeaderExtensions
{
    public static Header? header(this IList<Header> headers, HeaderName name)
    {
        for (int index = headers.Count - 1; index >= 0; index--)
        {
            if (headers[index].name == name) return headers[index];
        }
        return null;
    }

    public static HeaderValue? header_value(this IList<Header> headers, HeaderName name)
    {
        return headers.header(name)?.value;
    }
}
