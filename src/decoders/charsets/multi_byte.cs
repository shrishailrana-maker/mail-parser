/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/charsets/multi_byte.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 664aee7363ccffe6b90391bdea1879765301c5ad6ffbf6cce4ba8fe0825486a9
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

namespace Stalwart.MailParser.Port;

public static class MultiByteCharsetUtils
{
    static MultiByteCharsetUtils()
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    private static string DecodeWithEncoding(string encodingName, ReadOnlySpan<byte> bytes)
    {
        try
        {
            var enc = System.Text.Encoding.GetEncoding(encodingName, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
            return enc.GetString(bytes);
        }
        catch
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }

    public static string decoder_shift_jis(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("shift_jis", bytes);
    public static string decoder_big5(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("big5", bytes);
    public static string decoder_euc_jp(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("euc-jp", bytes);
    public static string decoder_euc_kr(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("euc-kr", bytes);
    public static string decoder_gb18030(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("gb18030", bytes);
    public static string decoder_gbk(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("gbk", bytes);
    public static string decoder_iso2022_jp(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("iso-2022-jp", bytes);
    public static string decoder_windows874(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("windows-874", bytes);
    public static string decoder_ibm866(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("cp866", bytes);
    public static string decoder_x_mac_cyrillic(ReadOnlySpan<byte> bytes) => DecodeWithEncoding("x-mac-cyrillic", bytes);
    public static string decoder_x_user_defined(ReadOnlySpan<byte> bytes)
    {
        var characters = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            characters[index] = value < 0x80 ? (char)value : (char)(0xF700 + value);
        }

        return new string(characters);
    }
    public static string decoder_replacement(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? string.Empty : "\uFFFD";
}
