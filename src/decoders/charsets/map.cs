/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/decoders/charsets/map.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: c1e926aec52fe7d780368f998719d45dc184fa504d0f529864cd2f1ffb4528c7
// This file must remain 1:1 with the Rust source file.

using System;
using System.Text;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public delegate string DecoderFnc(ReadOnlySpan<byte> bytes);

public static class CharsetMapUtils
{
    // Rust: charset_decoder
    public static DecoderFnc? charset_decoder(ReadOnlySpan<byte> charset)
    {
        Span<byte> l_charset = stackalloc byte[Math.Min(charset.Length, 45)];
        for (int i = 0; i < l_charset.Length; i++)
        {
            byte b = charset[i];
            if (b >= (byte)'A' && b <= (byte)'Z')
            {
                l_charset[i] = (byte)(b + 32);
            }
            else if (b == (byte)'-')
            {
                l_charset[i] = (byte)'_';
            }
            else
            {
                l_charset[i] = b;
            }
        }

        string key = System.Text.Encoding.ASCII.GetString(l_charset);
        return key switch
        {
            "850" => SingleByteCharsetUtils.decoder_ibm_850,
            "866" => SingleByteCharsetUtils.decoder_ibm866,
            "ansi_x3.4_1968" => SingleByteCharsetUtils.decoder_cp1252,
            "arabic" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "ascii" => SingleByteCharsetUtils.decoder_cp1252,
            "asmo_708" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "big5" => MultiByteCharsetUtils.decoder_big5,
            "big5_hkscs" => MultiByteCharsetUtils.decoder_big5,
            "chinese" => MultiByteCharsetUtils.decoder_gbk,
            "cn_big5" => MultiByteCharsetUtils.decoder_big5,
            "cp1250" => SingleByteCharsetUtils.decoder_cp1250,
            "cp1251" => SingleByteCharsetUtils.decoder_cp1251,
            "cp1252" => SingleByteCharsetUtils.decoder_cp1252,
            "cp1253" => SingleByteCharsetUtils.decoder_cp1253,
            "cp1254" => SingleByteCharsetUtils.decoder_cp1254,
            "cp1255" => SingleByteCharsetUtils.decoder_cp1255,
            "cp1256" => SingleByteCharsetUtils.decoder_cp1256,
            "cp1257" => SingleByteCharsetUtils.decoder_cp1257,
            "cp1258" => SingleByteCharsetUtils.decoder_cp1258,
            "cp819" => SingleByteCharsetUtils.decoder_cp1252,
            "cp850" => SingleByteCharsetUtils.decoder_ibm_850,
            "cp866" => SingleByteCharsetUtils.decoder_ibm866,
            "cp936" => MultiByteCharsetUtils.decoder_gbk,
            "csbig5" => MultiByteCharsetUtils.decoder_big5,
            "cseuckr" => MultiByteCharsetUtils.decoder_euc_kr,
            "cseucpkdfmtjapanese" => MultiByteCharsetUtils.decoder_euc_jp,
            "csgb18030" => MultiByteCharsetUtils.decoder_gb18030,
            "csgb2312" => MultiByteCharsetUtils.decoder_gbk,
            "csgbk" => MultiByteCharsetUtils.decoder_gbk,
            "csibm866" => SingleByteCharsetUtils.decoder_ibm866,
            "csiso2022jp" => MultiByteCharsetUtils.decoder_iso2022_jp,
            "csiso2022kr" => MultiByteCharsetUtils.decoder_replacement,
            "csiso58gb231280" => MultiByteCharsetUtils.decoder_gbk,
            "csiso885913" => SingleByteCharsetUtils.decoder_iso_8859_13,
            "csiso885914" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "csiso885915" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "csiso885916" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "csiso88596e" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "csiso88596i" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "csiso88598e" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "csiso88598i" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "csisolatin1" => SingleByteCharsetUtils.decoder_cp1252,
            "csisolatin2" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "csisolatin3" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "csisolatin4" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "csisolatin5" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "csisolatin6" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "csisolatin9" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "csisolatinarabic" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "csisolatincyrillic" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "csisolatingreek" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "csisolatinhebrew" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "cskoi8r" => SingleByteCharsetUtils.decoder_koi8_r,
            "cskoi8u" => SingleByteCharsetUtils.decoder_koi8_u,
            "csksc56011987" => MultiByteCharsetUtils.decoder_euc_kr,
            "csmacintosh" => SingleByteCharsetUtils.decoder_macintosh,
            "cspc850multilingual" => SingleByteCharsetUtils.decoder_ibm_850,
            "csshiftjis" => MultiByteCharsetUtils.decoder_shift_jis,
            "cstis620" => SingleByteCharsetUtils.decoder_tis_620,
            "csunicode" => UtfCharsetUtils.decoder_utf16,
            "csutf16" => UtfCharsetUtils.decoder_utf16,
            "csutf16be" => UtfCharsetUtils.decoder_utf16_be,
            "csutf16le" => UtfCharsetUtils.decoder_utf16_le,
            "csutf7" => UtfCharsetUtils.decoder_utf7,
            "cswindows1250" => SingleByteCharsetUtils.decoder_cp1250,
            "cswindows1251" => SingleByteCharsetUtils.decoder_cp1251,
            "cswindows1252" => SingleByteCharsetUtils.decoder_cp1252,
            "cswindows1253" => SingleByteCharsetUtils.decoder_cp1253,
            "cswindows1254" => SingleByteCharsetUtils.decoder_cp1254,
            "cswindows1255" => SingleByteCharsetUtils.decoder_cp1255,
            "cswindows1256" => SingleByteCharsetUtils.decoder_cp1256,
            "cswindows1257" => SingleByteCharsetUtils.decoder_cp1257,
            "cswindows1258" => SingleByteCharsetUtils.decoder_cp1258,
            "cswindows874" => MultiByteCharsetUtils.decoder_windows874,
            "cyrillic" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "dos_874" => MultiByteCharsetUtils.decoder_windows874,
            "ecma_114" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "ecma_118" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "elot_928" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "euc_jp" => MultiByteCharsetUtils.decoder_euc_jp,
            "euc_kr" => MultiByteCharsetUtils.decoder_euc_kr,
            "extended_unix_code_packed_format_for_japanese" => MultiByteCharsetUtils.decoder_euc_jp,
            "gb18030" => MultiByteCharsetUtils.decoder_gb18030,
            "gb2312" => MultiByteCharsetUtils.decoder_gb18030,
            "gb_2312" => MultiByteCharsetUtils.decoder_gbk,
            "gb_2312_80" => MultiByteCharsetUtils.decoder_gbk,
            "gbk" => MultiByteCharsetUtils.decoder_gbk,
            "greek" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "greek8" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "hebrew" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "hz_gb_2312" => MultiByteCharsetUtils.decoder_replacement,
            "ibm819" => SingleByteCharsetUtils.decoder_cp1252,
            "ibm850" => SingleByteCharsetUtils.decoder_ibm_850,
            "ibm866" => SingleByteCharsetUtils.decoder_ibm866,
            "iso88591" => SingleByteCharsetUtils.decoder_cp1252,
            "iso885910" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "iso885911" => SingleByteCharsetUtils.decoder_tis_620,
            "iso885913" => SingleByteCharsetUtils.decoder_iso_8859_13,
            "iso885914" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso885915" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "iso88592" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "iso88593" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "iso88594" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "iso88595" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "iso88596" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso88597" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "iso88598" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso88599" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "iso8859_1" => SingleByteCharsetUtils.decoder_cp1252,
            "iso8859_10" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "iso8859_11" => SingleByteCharsetUtils.decoder_tis_620,
            "iso8859_13" => SingleByteCharsetUtils.decoder_iso_8859_13,
            "iso8859_14" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso8859_15" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "iso8859_2" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "iso8859_3" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "iso8859_4" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "iso8859_5" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "iso8859_6" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso8859_7" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "iso8859_8" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso8859_9" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "iso_10646_ucs_2" => UtfCharsetUtils.decoder_utf16,
            "iso_2022_cn" => MultiByteCharsetUtils.decoder_replacement,
            "iso_2022_cn_ext" => MultiByteCharsetUtils.decoder_replacement,
            "iso_2022_jp" => MultiByteCharsetUtils.decoder_iso2022_jp,
            "iso_2022_kr" => MultiByteCharsetUtils.decoder_replacement,
            "iso_8859_1" => SingleByteCharsetUtils.decoder_cp1252,
            "iso_8859_10" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "iso_8859_10:1992" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "iso_8859_11" => SingleByteCharsetUtils.decoder_tis_620,
            "iso_8859_13" => SingleByteCharsetUtils.decoder_iso_8859_13,
            "iso_8859_14" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso_8859_14:1998" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso_8859_15" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "iso_8859_16" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "iso_8859_16:2001" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "iso_8859_1:1987" => SingleByteCharsetUtils.decoder_cp1252,
            "iso_8859_2" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "iso_8859_2:1987" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "iso_8859_3" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "iso_8859_3:1988" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "iso_8859_4" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "iso_8859_4:1988" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "iso_8859_5" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "iso_8859_5:1988" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "iso_8859_6" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso_8859_6:1987" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso_8859_6_e" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso_8859_6_i" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso_8859_7" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "iso_8859_7:1987" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "iso_8859_8" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso_8859_8:1988" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso_8859_8_e" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso_8859_8_i" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso_8859_9" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "iso_8859_9:1989" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "iso_celtic" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso_ir_100" => SingleByteCharsetUtils.decoder_cp1252,
            "iso_ir_101" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "iso_ir_109" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "iso_ir_110" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "iso_ir_126" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "iso_ir_127" => SingleByteCharsetUtils.decoder_iso_8859_6,
            "iso_ir_138" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "iso_ir_144" => SingleByteCharsetUtils.decoder_iso_8859_5,
            "iso_ir_148" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "iso_ir_149" => MultiByteCharsetUtils.decoder_euc_kr,
            "iso_ir_157" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "iso_ir_199" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "iso_ir_226" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "iso_ir_58" => MultiByteCharsetUtils.decoder_gbk,
            "koi" => SingleByteCharsetUtils.decoder_koi8_r,
            "koi8" => SingleByteCharsetUtils.decoder_koi8_r,
            "koi8_r" => SingleByteCharsetUtils.decoder_koi8_r,
            "koi8_ru" => SingleByteCharsetUtils.decoder_koi8_u,
            "koi8_u" => SingleByteCharsetUtils.decoder_koi8_u,
            "korean" => MultiByteCharsetUtils.decoder_euc_kr,
            "ks_c_5601_1987" => MultiByteCharsetUtils.decoder_euc_kr,
            "ks_c_5601_1989" => MultiByteCharsetUtils.decoder_euc_kr,
            "ksc5601" => MultiByteCharsetUtils.decoder_euc_kr,
            "ksc_5601" => MultiByteCharsetUtils.decoder_euc_kr,
            "l1" => SingleByteCharsetUtils.decoder_cp1252,
            "l10" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "l2" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "l3" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "l4" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "l5" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "l6" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "l8" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "l9" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "latin1" => SingleByteCharsetUtils.decoder_cp1252,
            "latin10" => SingleByteCharsetUtils.decoder_iso_8859_16,
            "latin2" => SingleByteCharsetUtils.decoder_iso_8859_2,
            "latin3" => SingleByteCharsetUtils.decoder_iso_8859_3,
            "latin4" => SingleByteCharsetUtils.decoder_iso_8859_4,
            "latin5" => SingleByteCharsetUtils.decoder_iso_8859_9,
            "latin6" => SingleByteCharsetUtils.decoder_iso_8859_10,
            "latin8" => SingleByteCharsetUtils.decoder_iso_8859_14,
            "latin_9" => SingleByteCharsetUtils.decoder_iso_8859_15,
            "logical" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "mac" => SingleByteCharsetUtils.decoder_macintosh,
            "macintosh" => SingleByteCharsetUtils.decoder_macintosh,
            "ms932" => MultiByteCharsetUtils.decoder_shift_jis,
            "ms936" => MultiByteCharsetUtils.decoder_gbk,
            "ms_kanji" => MultiByteCharsetUtils.decoder_shift_jis,
            "replacement" => MultiByteCharsetUtils.decoder_replacement,
            "shift_jis" => MultiByteCharsetUtils.decoder_shift_jis,
            "sjis" => MultiByteCharsetUtils.decoder_shift_jis,
            "sun_eu_greek" => SingleByteCharsetUtils.decoder_iso_8859_7,
            "tis_620" => SingleByteCharsetUtils.decoder_tis_620,
            "ucs_2" => UtfCharsetUtils.decoder_utf16,
            "unicode" => UtfCharsetUtils.decoder_utf16,
            "unicodefeff" => UtfCharsetUtils.decoder_utf16,
            "unicodefffe" => UtfCharsetUtils.decoder_utf16_be,
            "us_ascii" => SingleByteCharsetUtils.decoder_cp1252,
            "utf_16" => UtfCharsetUtils.decoder_utf16,
            "utf_16be" => UtfCharsetUtils.decoder_utf16_be,
            "utf_16le" => UtfCharsetUtils.decoder_utf16_le,
            "utf_7" => UtfCharsetUtils.decoder_utf7,
            "visual" => SingleByteCharsetUtils.decoder_iso_8859_8,
            "windows_1250" => SingleByteCharsetUtils.decoder_cp1250,
            "windows_1251" => SingleByteCharsetUtils.decoder_cp1251,
            "windows_1252" => SingleByteCharsetUtils.decoder_cp1252,
            "windows_1253" => SingleByteCharsetUtils.decoder_cp1253,
            "windows_1254" => SingleByteCharsetUtils.decoder_cp1254,
            "windows_1255" => SingleByteCharsetUtils.decoder_cp1255,
            "windows_1256" => SingleByteCharsetUtils.decoder_cp1256,
            "windows_1257" => SingleByteCharsetUtils.decoder_cp1257,
            "windows_1258" => SingleByteCharsetUtils.decoder_cp1258,
            "windows_31j" => MultiByteCharsetUtils.decoder_shift_jis,
            "windows_874" => MultiByteCharsetUtils.decoder_windows874,
            "windows_936" => MultiByteCharsetUtils.decoder_gbk,
            "windows_949" => MultiByteCharsetUtils.decoder_euc_kr,
            "x_cp1250" => SingleByteCharsetUtils.decoder_cp1250,
            "x_cp1251" => SingleByteCharsetUtils.decoder_cp1251,
            "x_cp1252" => SingleByteCharsetUtils.decoder_cp1252,
            "x_cp1253" => SingleByteCharsetUtils.decoder_cp1253,
            "x_cp1254" => SingleByteCharsetUtils.decoder_cp1254,
            "x_cp1255" => SingleByteCharsetUtils.decoder_cp1255,
            "x_cp1256" => SingleByteCharsetUtils.decoder_cp1256,
            "x_cp1257" => SingleByteCharsetUtils.decoder_cp1257,
            "x_cp1258" => SingleByteCharsetUtils.decoder_cp1258,
            "x_euc_jp" => MultiByteCharsetUtils.decoder_euc_jp,
            "x_gbk" => MultiByteCharsetUtils.decoder_gbk,
            "x_mac_cyrillic" => MultiByteCharsetUtils.decoder_x_mac_cyrillic,
            "x_mac_roman" => SingleByteCharsetUtils.decoder_macintosh,
            "x_mac_ukrainian" => MultiByteCharsetUtils.decoder_x_mac_cyrillic,
            "x_sjis" => MultiByteCharsetUtils.decoder_shift_jis,
            "x_user_defined" => MultiByteCharsetUtils.decoder_x_user_defined,
            "x_x_big5" => MultiByteCharsetUtils.decoder_big5,
            _ => null
        };
    }
}

#if STALWART_PORT_TESTS
[TestClass]
public class map_tests
{
    [TestMethod]
    public void decoder_charset()
    {
        foreach (var input in new string[] { "gbk", "extended_unix_code_packed_format_for_japanese" })
        {
            var fn = CharsetMapUtils.charset_decoder(System.Text.Encoding.ASCII.GetBytes(input));
            Assert.IsNotNull(fn, $"Failed for {input}");
        }
    }

    [TestMethod]
    public void decoder_charset_encoding_rs_labels()
    {
        var supported = new string[]
        {
            "l9", "koi", "koi8", "sjis", "ucs-2", "ms932", "ascii", "x-gbk", "cp1250", "cp1251",
            "cp1252", "cp1253", "cp1254", "cp1255", "cp1256", "cp1257", "cp1258", "visual",
            "korean", "x-sjis", "ksc5601", "gb_2312", "dos-874", "cn-big5", "unicode", "chinese",
            "logical", "koi8-ru", "x-cp1250", "ksc_5601", "x-cp1251", "iso88591", "csgb2312",
            "x-cp1252", "iso88592", "x-cp1253", "iso88593", "x-cp1254", "iso88594", "x-cp1255",
            "iso88595", "x-x-big5", "x-cp1256", "iso88596", "x-cp1257", "iso88597", "x-cp1258",
            "iso88598", "iso88599", "us-ascii", "x-euc-jp", "iso885910", "iso8859-1", "iso885911",
            "iso8859-2", "iso8859-3", "iso885913", "iso8859-4", "iso885914", "iso8859-5",
            "iso885915", "iso8859-6", "iso8859-7", "iso8859-8", "iso-ir-58", "iso8859-9",
            "csunicode", "iso8859-10", "gb_2312-80", "iso8859-11", "iso8859-13", "iso8859-14",
            "iso8859-15", "iso-ir-149", "big5-hkscs", "windows-949", "csisolatin9", "csiso88596e",
            "csiso88598e", "unicodefffe", "unicodefeff", "csiso88596i", "csiso88598i", "windows-31j",
            "x-mac-roman", "sun_eu_greek", "csksc56011987", "ansi_x3.4-1968", "csiso58gb231280",
            "iso-10646-ucs-2", "iso-8859-6-e", "iso-8859-8-e", "iso-8859-6-i", "replacement",
            "iso-2022-kr", "csiso2022kr", "iso-2022-cn", "iso-2022-cn-ext", "hz-gb-2312",
            "x-user-defined", "x-mac-cyrillic", "x-mac-ukrainian",
        };
        foreach (var input in supported)
        {
            var fn = CharsetMapUtils.charset_decoder(System.Text.Encoding.ASCII.GetBytes(input));
            Assert.IsNotNull(fn, $"Expected a decoder for {input}");
        }

        var unsupported = new string[]
        {
            "utf8", "utf-8", "unicode11utf8", "unicode20utf8", "x-unicode20utf8",
            "unicode-1-1-utf-8",
        };
        foreach (var input in unsupported)
        {
            var fn = CharsetMapUtils.charset_decoder(System.Text.Encoding.ASCII.GetBytes(input));
            Assert.IsNull(fn, $"Did not expect a decoder for {input}");
        }
    }
}
#endif
