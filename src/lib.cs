/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/lib.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 89f26c847e57ba10eb990e1cdf605c70553b1b08dcef25167e0cc39c58b372b7
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stalwart.MailParser.Port;

public sealed class SerdeJsonEncoder : JavaScriptEncoder
{
    public static SerdeJsonEncoder Instance { get; } = new();

    private SerdeJsonEncoder() { }

    public override int MaxOutputCharactersPerInputCharacter => 6;

    public override bool WillEncode(int unicodeScalar)
    {
        return unicodeScalar <= 0x1f || unicodeScalar is '"' or '\\';
    }

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        for (int index = 0; index < textLength; index++)
        {
            char value = text[index];
            if (WillEncode(value) || char.IsLowSurrogate(value))
            {
                return index;
            }
            if (char.IsHighSurrogate(value))
            {
                if (index + 1 >= textLength || !char.IsLowSurrogate(text[index + 1]))
                {
                    return index;
                }
                index++;
            }
        }
        return -1;
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        char escape = unicodeScalar switch
        {
            '"' => '"',
            '\\' => '\\',
            '\b' => 'b',
            '\t' => 't',
            '\n' => 'n',
            '\f' => 'f',
            '\r' => 'r',
            _ => '\0',
        };

        if (escape != '\0')
        {
            if (bufferLength < 2)
            {
                numberOfCharactersWritten = 0;
                return false;
            }
            buffer[0] = '\\';
            buffer[1] = escape;
            numberOfCharactersWritten = 2;
            return true;
        }

        if (unicodeScalar <= 0x1f)
        {
            if (bufferLength < 6)
            {
                numberOfCharactersWritten = 0;
                return false;
            }
            const string hex = "0123456789abcdef";
            buffer[0] = '\\';
            buffer[1] = 'u';
            buffer[2] = '0';
            buffer[3] = '0';
            buffer[4] = hex[unicodeScalar >> 4];
            buffer[5] = hex[unicodeScalar & 0xf];
            numberOfCharactersWritten = 6;
            return true;
        }

        if (!Rune.IsValid(unicodeScalar))
        {
            unicodeScalar = Rune.ReplacementChar.Value;
        }
        if (unicodeScalar <= 0xffff)
        {
            if (bufferLength < 1)
            {
                numberOfCharactersWritten = 0;
                return false;
            }
            buffer[0] = (char)unicodeScalar;
            numberOfCharactersWritten = 1;
            return true;
        }
        if (bufferLength < 2)
        {
            numberOfCharactersWritten = 0;
            return false;
        }
        unicodeScalar -= 0x10000;
        buffer[0] = (char)((unicodeScalar >> 10) + 0xd800);
        buffer[1] = (char)((unicodeScalar & 0x3ff) + 0xdc00);
        numberOfCharactersWritten = 2;
        return true;
    }
}

// Rust: Encoding
public enum Encoding : byte
{
    None = 0,
    QuotedPrintable = 1,
    Base64 = 2,
}

// Rust: PartType
[JsonConverter(typeof(PartTypeJsonConverter))]
public abstract record PartType
{
    public sealed record TextRecord(string Value) : PartType;
    public sealed record HtmlRecord(string Value) : PartType;
    public sealed record BinaryRecord(byte[] Value) : PartType;
    public sealed record InlineBinaryRecord(byte[] Value) : PartType;
    public sealed record MessageRecord(Message Value) : PartType;
    public sealed record MultipartRecord(List<uint> Value) : PartType;

    public static PartType Text(string text) => new TextRecord(text);
    public static PartType Html(string html) => new HtmlRecord(html);
    public static PartType Binary(byte[] binary) => new BinaryRecord(binary);
    public static PartType InlineBinary(byte[] inlineBinary) => new InlineBinaryRecord(inlineBinary);
    public static PartType Message(Message message) => new MessageRecord(message);
    public static PartType Multipart(List<uint> parts) => new MultipartRecord(parts);

    public int len() => this switch
    {
        TextRecord t => System.Text.Encoding.UTF8.GetByteCount(t.Value),
        HtmlRecord h => System.Text.Encoding.UTF8.GetByteCount(h.Value),
        BinaryRecord b => b.Value.Length,
        InlineBinaryRecord ib => ib.Value.Length,
        MessageRecord m => m.Value.raw_message?.Length ?? 0,
        MultipartRecord => 0,
        _ => 0
    };
}

// Rust: Addr
public record Addr
{
    [JsonPropertyName("name")]
    public string? name { get; set; }

    [JsonPropertyName("address")]
    public string? address { get; set; }

    public Addr() { }

    public Addr(string? name, string? address)
    {
        this.name = name;
        this.address = address;
    }

    public Addr(string? name, string address, bool _)
    {
        this.name = name;
        this.address = address;
    }

    [JsonIgnore]
    public string? Name => name;
    [JsonIgnore]
    public string? Address => address;
}

// Rust: Group
public record Group
{
    [JsonPropertyName("name")]
    public string? name { get; set; }

    [JsonPropertyName("addresses")]
    public List<Addr> addresses { get; set; } = new();

    public Group() { }

    public Group(string? name, List<Addr> addresses)
    {
        this.name = name;
        this.addresses = addresses;
    }
}

// Rust: Address
[JsonConverter(typeof(AddressJsonConverter))]
public abstract partial record Address
{
    public sealed record ListRecord(List<Addr> Value) : Address;
    public sealed record GroupRecord(List<Group> Value) : Address;

    public static Address List(List<Addr> list) => new ListRecord(list);
    public static Address Group(List<Group> group) => new GroupRecord(group);

    public List<Addr>? as_list() => this is ListRecord lr ? lr.Value : null;
    public List<Group>? as_group() => this is GroupRecord gr ? gr.Value : null;
    public Addr? as_addr() => this is ListRecord lr && lr.Value.Count > 0 ? lr.Value[0] : null;

    public bool is_empty() => this switch
    {
        ListRecord lr => lr.Value.Count == 0,
        GroupRecord gr => gr.Value.Count == 0,
        _ => true
    };

    public int len() => this switch
    {
        ListRecord lr => lr.Value.Count,
        GroupRecord gr => gr.Value.Count,
        _ => 0
    };
}

// Rust: HeaderForm
public enum HeaderForm
{
    Raw,
    Text,
    Addresses,
    GroupedAddresses,
    MessageIds,
    Date,
    URLs,
}

// Rust: Attribute
[JsonConverter(typeof(AttributeJsonConverter))]
public record Attribute
{
    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("value")]
    public string value { get; set; } = "";

    public Attribute() { }

    public Attribute(string name, string value)
    {
        this.name = name;
        this.value = value;
    }
}

// Rust: ContentType
public record ContentType
{
    [JsonPropertyName("c_type")]
    public string c_type { get; set; } = "";

    [JsonPropertyName("c_subtype")]
    public string? c_subtype { get; set; }

    [JsonPropertyName("attributes")]
    public List<Attribute>? attributes { get; set; }

    public ContentType() { }

    public ContentType(string c_type, string? c_subtype = null, List<Attribute>? attributes = null)
    {
        this.c_type = c_type;
        this.c_subtype = c_subtype;
        this.attributes = attributes;
    }

    public string mimetype() => c_type;
    public string? subtype() => c_subtype;
    public string? attribute(string attrName)
    {
        if (attributes == null) return null;
        foreach (var attr in attributes)
        {
            if (string.Equals(attr.name, attrName, StringComparison.OrdinalIgnoreCase))
                return attr.value;
        }
        return null;
    }
    public bool has_attribute(string attrName) => attribute(attrName) != null;
    public bool is_attachment() => string.Equals(c_type, "attachment", StringComparison.OrdinalIgnoreCase);
}

// Rust: DateTime
public struct DateTime : IEquatable<DateTime>, IComparable<DateTime>
{
    [JsonPropertyName("year")]
    public ushort year { get; set; }

    [JsonPropertyName("month")]
    public byte month { get; set; }

    [JsonPropertyName("day")]
    public byte day { get; set; }

    [JsonPropertyName("hour")]
    public byte hour { get; set; }

    [JsonPropertyName("minute")]
    public byte minute { get; set; }

    [JsonPropertyName("second")]
    public byte second { get; set; }

    [JsonPropertyName("tz_before_gmt")]
    public bool tz_before_gmt { get; set; }

    [JsonPropertyName("tz_hour")]
    public byte tz_hour { get; set; }

    [JsonPropertyName("tz_minute")]
    public byte tz_minute { get; set; }

    public DateTime(ushort year, byte month, byte day, byte hour, byte minute, byte second, bool tz_before_gmt, byte tz_hour, byte tz_minute)
    {
        this.year = year;
        this.month = month;
        this.day = day;
        this.hour = hour;
        this.minute = minute;
        this.second = second;
        this.tz_before_gmt = tz_before_gmt;
        this.tz_hour = tz_hour;
        this.tz_minute = tz_minute;
    }

    public bool Equals(DateTime other) =>
        year == other.year && month == other.month && day == other.day &&
        hour == other.hour && minute == other.minute && second == other.second &&
        tz_before_gmt == other.tz_before_gmt && tz_hour == other.tz_hour && tz_minute == other.tz_minute;

    public override bool Equals(object? obj) => obj is DateTime dt && Equals(dt);
    public override int GetHashCode() => HashCode.Combine(year, month, day, hour, minute, second, tz_before_gmt, HashCode.Combine(tz_hour, tz_minute));
    public static bool operator ==(DateTime left, DateTime right) => left.Equals(right);
    public static bool operator !=(DateTime left, DateTime right) => !left.Equals(right);

    public int CompareTo(DateTime other) => to_timestamp().CompareTo(other.to_timestamp());

    public long to_timestamp()
    {
        return DateTimeUtils.ToTimestamp(this);
    }

    public bool is_valid()
    {
        return tz_hour <= 23 && year is >= 1900 and <= 3000 && tz_minute <= 59
            && month is >= 1 and <= 12 && day is >= 1 and <= 31
            && hour <= 23 && minute <= 59 && second <= 59;
    }

    public DateTime to_timezone(long tz) => DateTimeUtils.ToTimezone(this, tz);
    public static DateTime from_timestamp(long timestamp) => DateTimeUtils.FromTimestamp(timestamp);

    public string to_rfc822() => DateTimeUtils.ToRfc822(this);
    public string to_rfc3339() => DateTimeUtils.ToRfc3339(this);
    public override string ToString() => to_rfc822();
}

// Rust: Host
[JsonConverter(typeof(HostJsonConverter))]
public abstract record Host
{
    public sealed record NameRecord(string Value) : Host;
    public sealed record IpAddrRecord(IPAddress Value) : Host;

    public static Host Name(string name) => new NameRecord(name);
    public static Host IpAddr(IPAddress ip) => new IpAddrRecord(ip);
}

// Rust: TlsVersion
public enum TlsVersion
{
    SSLv2,
    SSLv3,
    TLSv1_0,
    TLSv1_1,
    TLSv1_2,
    TLSv1_3,
    DTLSv1_0,
    DTLSv1_2,
    DTLSv1_3,
}

// Rust: Greeting
public enum Greeting
{
    Helo,
    Ehlo,
    Lhlo,
}

// Rust: Protocol
public enum Protocol
{
    SMTP,
    ESMTP,
    ESMTPA,
    ESMTPS,
    ESMTPSA,
    LMTP,
    LMTPA,
    LMTPS,
    LMTPSA,
    MMS,
    UTF8SMTP,
    UTF8SMTPA,
    UTF8SMTPS,
    UTF8SMTPSA,
    UTF8LMTP,
    UTF8LMTPA,
    UTF8LMTPS,
    UTF8LMTPSA,
    HTTP,
    HTTPS,
    IMAP,
    POP3,
    Local,
}

// Rust: Received
public record Received
{
    [JsonPropertyName("from")]
    public Host? from { get; set; }

    [JsonPropertyName("from_ip")]
    public IPAddress? from_ip { get; set; }

    [JsonPropertyName("from_iprev")]
    public string? from_iprev { get; set; }

    [JsonPropertyName("by")]
    public Host? by { get; set; }

    [JsonPropertyName("for_")]
    public string? for_ { get; set; }

    [JsonPropertyName("with")]
    public Protocol? with { get; set; }

    [JsonPropertyName("tls_version")]
    public TlsVersion? tls_version { get; set; }

    [JsonPropertyName("tls_cipher")]
    public string? tls_cipher { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? id { get; set; }

    [JsonPropertyName("ident")]
    public string? ident { get; set; }

    [JsonPropertyName("helo")]
    public Host? helo { get; set; }

    [JsonPropertyName("helo_cmd")]
    public Greeting? helo_cmd { get; set; }

    [JsonPropertyName("via")]
    public string? via { get; set; }

    [JsonPropertyName("date")]
    public DateTime? date { get; set; }
}

// Rust: HeaderValue
[JsonConverter(typeof(HeaderValueJsonConverter))]
public abstract record HeaderValue
{
    public sealed record AddressRecord(Address Value) : HeaderValue;
    public sealed record TextRecord(string Value) : HeaderValue;
    public sealed record TextListRecord(List<string> Value) : HeaderValue;
    public sealed record DateTimeRecord(DateTime Value) : HeaderValue;
    public sealed record ContentTypeRecord(ContentType Value) : HeaderValue;
    public sealed record ReceivedRecord(Received Value) : HeaderValue;
    public sealed record EmptyRecord : HeaderValue;

    public static HeaderValue Address(Address address) => new AddressRecord(address);
    public static HeaderValue Text(string text) => new TextRecord(text);
    public static HeaderValue TextList(List<string> textList) => new TextListRecord(textList);
    public static HeaderValue DateTime(DateTime dateTime) => new DateTimeRecord(dateTime);
    public static HeaderValue ContentType(ContentType contentType) => new ContentTypeRecord(contentType);
    public static HeaderValue Received(Received received) => new ReceivedRecord(received);
    public static readonly HeaderValue Empty = new EmptyRecord();

    public string? as_text() => this is TextRecord tr ? tr.Value : null;
    public List<string>? as_text_list() => this is TextListRecord tlr ? tlr.Value : null;
    public Address? as_address() => this is AddressRecord ar ? ar.Value : null;
    public Addr? as_addr() => this is AddressRecord ar ? ar.Value.as_addr() : null;
    public List<Addr>? as_list() => this is AddressRecord ar ? ar.Value.as_list() : null;
    public List<Group>? as_group() => this is AddressRecord ar ? ar.Value.as_group() : null;
    public DateTime? as_datetime() => this is DateTimeRecord dtr ? dtr.Value : null;
    public ContentType? as_content_type() => this is ContentTypeRecord ctr ? ctr.Value : null;
    public Received? as_received() => this is ReceivedRecord rr ? rr.Value : null;
    public bool is_empty() => this is EmptyRecord;
}

    public enum KnownHeader : byte
{
    Other = 37,
    Subject = 0,
    From = 1,
    To = 2,
    Cc = 3,
    Date = 4,
    Bcc = 5,
    ReplyTo = 6,
    Sender = 7,
    Comments = 8,
    InReplyTo = 9,
    Keywords = 10,
    Received = 11,
    MessageId = 12,
    References = 13,
    ReturnPath = 14,
    MimeVersion = 15,
    ContentDescription = 16,
    ContentId = 17,
    ContentLanguage = 18,
    ContentLocation = 19,
    ContentTransferEncoding = 20,
    ContentType = 21,
    ContentDisposition = 22,
    ResentTo = 23,
    ResentFrom = 24,
    ResentBcc = 25,
    ResentCc = 26,
    ResentSender = 27,
    ResentDate = 28,
    ResentMessageId = 29,
    ListArchive = 30,
    ListHelp = 31,
    ListId = 32,
    ListOwner = 33,
    ListPost = 34,
    ListSubscribe = 35,
    ListUnsubscribe = 36,
    DkimSignature = 38,
    ArcAuthenticationResults = 39,
    ArcMessageSignature = 40,
    ArcSeal = 41,
    Dkim2Signature = 42,
    MessageInstance = 43,
    AcceptLanguage = 44,
    AlternateRecipient = 45,
    ArchivedAt = 46,
    AuthenticationResults = 47,
    AutoSubmitted = 48,
    Autoforwarded = 49,
    Autosubmitted = 50,
    ContentAlternative = 51,
    ContentDuration = 52,
    ContentFeatures = 53,
    ContentMd5 = 54,
    ContentTranslationType = 55,
    Conversion = 56,
    ConversionWithLoss = 57,
    DlExpansionHistory = 58,
    DeferredDelivery = 59,
    DeliveryDate = 60,
    DiscardedX400IpmsExtensions = 61,
    DiscardedX400MtsExtensions = 62,
    DiscloseRecipients = 63,
    DispositionNotificationOptions = 64,
    DispositionNotificationTo = 65,
    DowngradedFinalRecipient = 66,
    DowngradedInReplyTo = 67,
    DowngradedMessageId = 68,
    DowngradedOriginalRecipient = 69,
    DowngradedReferences = 70,
    Encoding = 71,
    Expires = 72,
    GenerateDeliveryReport = 73,
    HpOuter = 74,
    Importance = 75,
    IncompleteCopy = 76,
    Language = 77,
    LatestDeliveryTime = 78,
    ListUnsubscribePost = 79,
    MessageContext = 80,
    MessageType = 81,
    MmhsExemptedAddress = 82,
    MmhsExtendedAuthorisationInfo = 83,
    MmhsSubjectIndicatorCodes = 84,
    MmhsHandlingInstructions = 85,
    MmhsMessageInstructions = 86,
    MmhsCodressMessageIndicator = 87,
    MmhsOriginatorReference = 88,
    MmhsPrimaryPrecedence = 89,
    MmhsCopyPrecedence = 90,
    MmhsMessageType = 91,
    MmhsOtherRecipientsIndicatorTo = 92,
    MmhsOtherRecipientsIndicatorCc = 93,
    MmhsAcp127MessageIdentifier = 94,
    MmhsOriginatorPlad = 95,
    MtPriority = 96,
    Organization = 97,
    OriginalEncodedInformationTypes = 98,
    OriginalFrom = 99,
    OriginalMessageId = 100,
    OriginalRecipient = 101,
    OriginatorReturnAddress = 102,
    OriginalSubject = 103,
    PicsLabel = 104,
    PreventNonDeliveryReport = 105,
    Priority = 106,
    ReceivedSpf = 107,
    ReplyBy = 108,
    RequireRecipientValidSince = 109,
    Sensitivity = 110,
    Solicitation = 111,
    Supersedes = 112,
    TlsReportDomain = 113,
    TlsReportSubmitter = 114,
    TlsRequired = 115,
    VbrInfo = 116,
    X400ContentIdentifier = 117,
    X400ContentReturn = 118,
    X400ContentType = 119,
    X400MtsIdentifier = 120,
    X400Originator = 121,
    X400Received = 122,
    X400Recipients = 123,
    X400Trace = 124,
    ApparentlyTo = 125,
    Author = 126,
    CfblAddress = 127,
    CfblFeedbackId = 128,
    DeliveredTo = 129,
    EdiintFeatures = 130,
    EesstVersion = 131,
    ErrorsTo = 132,
    Face = 133,
    FormSub = 134,
    JabberId = 135,
    MmhsAuthorizingUsers = 136,
    Privicon = 137,
    SioLabel = 138,
    SioLabelHistory = 139,
    WrongRecipient = 140,
}



public class AttributeJsonConverter : JsonConverter<Attribute>
{
    public override Attribute? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            string name = reader.GetString() ?? "";
            reader.Read();
            string val = reader.GetString() ?? "";
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                reader.Read();
            }
            return new Attribute(name, val);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            string name = doc.RootElement.GetProperty("name").GetString() ?? "";
            string val = doc.RootElement.GetProperty("value").GetString() ?? "";
            return new Attribute(name, val);
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Attribute value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.name);
        writer.WriteString("value", value.value);
        writer.WriteEndObject();
    }
}

public class IPAddressJsonConverter : JsonConverter<IPAddress>
{
    public override IPAddress? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        return s != null ? IPAddress.Parse(s) : null;
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class AddressJsonConverter : JsonConverter<Address>
{
    public override Address? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.TryGetProperty("List", out var listElem))
            {
                var list = JsonSerializer.Deserialize<List<Addr>>(listElem.GetRawText(), options) ?? new();
                return Address.List(list);
            }
            if (root.TryGetProperty("Group", out var groupElem))
            {
                var group = JsonSerializer.Deserialize<List<Group>>(groupElem.GetRawText(), options) ?? new();
                return Address.Group(group);
            }
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value is Address.ListRecord lr)
        {
            writer.WritePropertyName("List");
            JsonSerializer.Serialize(writer, lr.Value, options);
        }
        else if (value is Address.GroupRecord gr)
        {
            writer.WritePropertyName("Group");
            JsonSerializer.Serialize(writer, gr.Value, options);
        }
        writer.WriteEndObject();
    }
}

public class HostJsonConverter : JsonConverter<Host>
{
    public override Host? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.TryGetProperty("Name", out var nameElem))
            {
                return Host.Name(nameElem.GetString() ?? "");
            }
            if (root.TryGetProperty("IpAddr", out var ipElem))
            {
                return Host.IpAddr(IPAddress.Parse(ipElem.GetString() ?? "127.0.0.1"));
            }
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Host value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value is Host.NameRecord nr)
        {
            writer.WriteString("Name", nr.Value);
        }
        else if (value is Host.IpAddrRecord ir)
        {
            writer.WriteString("IpAddr", ir.Value.ToString());
        }
        writer.WriteEndObject();
    }
}

public class PartTypeJsonConverter : JsonConverter<PartType>
{
    public override PartType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.TryGetProperty("Text", out var textElem)) return PartType.Text(textElem.GetString() ?? "");
            if (root.TryGetProperty("Html", out var htmlElem)) return PartType.Html(htmlElem.GetString() ?? "");
            if (root.TryGetProperty("Binary", out var binElem)) return PartType.Binary(JsonSerializer.Deserialize<List<byte>>(binElem.GetRawText(), options)?.ToArray() ?? Array.Empty<byte>());
            if (root.TryGetProperty("InlineBinary", out var ibElem)) return PartType.InlineBinary(JsonSerializer.Deserialize<List<byte>>(ibElem.GetRawText(), options)?.ToArray() ?? Array.Empty<byte>());
            if (root.TryGetProperty("Message", out var msgElem)) return PartType.Message(JsonSerializer.Deserialize<Message>(msgElem.GetRawText(), options) ?? new Message());
            if (root.TryGetProperty("Multipart", out var mpElem)) return PartType.Multipart(JsonSerializer.Deserialize<List<uint>>(mpElem.GetRawText(), options) ?? new());
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, PartType value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value is PartType.TextRecord tr)
        {
            writer.WriteString("Text", tr.Value);
        }
        else if (value is PartType.HtmlRecord hr)
        {
            writer.WriteString("Html", hr.Value);
        }
        else if (value is PartType.BinaryRecord br)
        {
            writer.WritePropertyName("Binary");
            WriteBytes(writer, br.Value);
        }
        else if (value is PartType.InlineBinaryRecord ibr)
        {
            writer.WritePropertyName("InlineBinary");
            WriteBytes(writer, ibr.Value);
        }
        else if (value is PartType.MessageRecord mr)
        {
            writer.WritePropertyName("Message");
            JsonSerializer.Serialize(writer, mr.Value, options);
        }
        else if (value is PartType.MultipartRecord mpr)
        {
            writer.WritePropertyName("Multipart");
            JsonSerializer.Serialize(writer, mpr.Value, options);
        }
        writer.WriteEndObject();
    }

    private static void WriteBytes(Utf8JsonWriter writer, byte[] bytes)
    {
        writer.WriteStartArray();
        foreach (var value in bytes) writer.WriteNumberValue(value);
        writer.WriteEndArray();
    }
}

public class HeaderValueJsonConverter : JsonConverter<HeaderValue>
{
    public override HeaderValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() == "Empty")
        {
            return HeaderValue.Empty;
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.TryGetProperty("Address", out var addrElem))
            {
                return HeaderValue.Address(JsonSerializer.Deserialize<Address>(addrElem.GetRawText(), options) ?? Address.List(new()));
            }
            if (root.TryGetProperty("Text", out var textElem))
            {
                return HeaderValue.Text(textElem.GetString() ?? "");
            }
            if (root.TryGetProperty("TextList", out var listElem))
            {
                return HeaderValue.TextList(JsonSerializer.Deserialize<List<string>>(listElem.GetRawText(), options) ?? new());
            }
            if (root.TryGetProperty("DateTime", out var dtElem))
            {
                return HeaderValue.DateTime(JsonSerializer.Deserialize<DateTime>(dtElem.GetRawText(), options));
            }
            if (root.TryGetProperty("ContentType", out var ctElem))
            {
                return HeaderValue.ContentType(JsonSerializer.Deserialize<ContentType>(ctElem.GetRawText(), options) ?? new ContentType());
            }
            if (root.TryGetProperty("Received", out var rcElem))
            {
                return HeaderValue.Received(JsonSerializer.Deserialize<Received>(rcElem.GetRawText(), options) ?? new Received());
            }
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, HeaderValue value, JsonSerializerOptions options)
    {
        if (value is HeaderValue.EmptyRecord)
        {
            writer.WriteStringValue("Empty");
            return;
        }
        writer.WriteStartObject();
        if (value is HeaderValue.AddressRecord ar)
        {
            writer.WritePropertyName("Address");
            JsonSerializer.Serialize(writer, ar.Value, options);
        }
        else if (value is HeaderValue.TextRecord tr)
        {
            writer.WriteString("Text", tr.Value);
        }
        else if (value is HeaderValue.TextListRecord tlr)
        {
            writer.WritePropertyName("TextList");
            JsonSerializer.Serialize(writer, tlr.Value, options);
        }
        else if (value is HeaderValue.DateTimeRecord dtr)
        {
            writer.WritePropertyName("DateTime");
            JsonSerializer.Serialize(writer, dtr.Value, options);
        }
        else if (value is HeaderValue.ContentTypeRecord ctr)
        {
            writer.WritePropertyName("ContentType");
            JsonSerializer.Serialize(writer, ctr.Value, options);
        }
        else if (value is HeaderValue.ReceivedRecord rr)
        {
            writer.WritePropertyName("Received");
            JsonSerializer.Serialize(writer, rr.Value, options);
        }
        writer.WriteEndObject();
    }
}

public class HeaderNameJsonConverter : JsonConverter<HeaderName>
{
    public override HeaderName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return HeaderNameUtils.ParseHeaderName((reader.GetString() ?? "").Replace('_', '-'));
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return HeaderName.Other(document.RootElement.GetProperty("other").GetString() ?? "");
    }

    public override void Write(Utf8JsonWriter writer, HeaderName value, JsonSerializerOptions options)
    {
        if (value.Kind == KnownHeader.Other)
        {
            writer.WriteStartObject();
            writer.WriteString("other", value.CustomName);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStringValue(value.as_str().ToLowerInvariant().Replace('-', '_'));
    }
}

[JsonConverter(typeof(HeaderNameJsonConverter))]
public struct HeaderName : IEquatable<HeaderName>
{
    public KnownHeader Kind { get; }
    public string? CustomName { get; }

    public HeaderName(KnownHeader kind)
    {
        Kind = kind;
        CustomName = null;
    }

    public HeaderName(string customName)
    {
        var parsed = HeaderNameUtils.ParseKnown(customName);
        if (parsed.HasValue)
        {
            Kind = parsed.Value;
            CustomName = null;
        }
        else
        {
            Kind = KnownHeader.Other;
            CustomName = customName;
        }
    }

    public static readonly HeaderName Subject = new(KnownHeader.Subject);
    public static readonly HeaderName From = new(KnownHeader.From);
    public static readonly HeaderName To = new(KnownHeader.To);
    public static readonly HeaderName Cc = new(KnownHeader.Cc);
    public static readonly HeaderName Date = new(KnownHeader.Date);
    public static readonly HeaderName Bcc = new(KnownHeader.Bcc);
    public static readonly HeaderName ReplyTo = new(KnownHeader.ReplyTo);
    public static readonly HeaderName Sender = new(KnownHeader.Sender);
    public static readonly HeaderName Comments = new(KnownHeader.Comments);
    public static readonly HeaderName InReplyTo = new(KnownHeader.InReplyTo);
    public static readonly HeaderName Keywords = new(KnownHeader.Keywords);
    public static readonly HeaderName Received = new(KnownHeader.Received);
    public static readonly HeaderName MessageId = new(KnownHeader.MessageId);
    public static readonly HeaderName References = new(KnownHeader.References);
    public static readonly HeaderName ReturnPath = new(KnownHeader.ReturnPath);
    public static readonly HeaderName MimeVersion = new(KnownHeader.MimeVersion);
    public static readonly HeaderName ContentDescription = new(KnownHeader.ContentDescription);
    public static readonly HeaderName ContentId = new(KnownHeader.ContentId);
    public static readonly HeaderName ContentLanguage = new(KnownHeader.ContentLanguage);
    public static readonly HeaderName ContentLocation = new(KnownHeader.ContentLocation);
    public static readonly HeaderName ContentTransferEncoding = new(KnownHeader.ContentTransferEncoding);
    public static readonly HeaderName ContentType = new(KnownHeader.ContentType);
    public static readonly HeaderName ContentDisposition = new(KnownHeader.ContentDisposition);
    public static readonly HeaderName ResentTo = new(KnownHeader.ResentTo);
    public static readonly HeaderName ResentFrom = new(KnownHeader.ResentFrom);
    public static readonly HeaderName ResentBcc = new(KnownHeader.ResentBcc);
    public static readonly HeaderName ResentCc = new(KnownHeader.ResentCc);
    public static readonly HeaderName ResentSender = new(KnownHeader.ResentSender);
    public static readonly HeaderName ResentDate = new(KnownHeader.ResentDate);
    public static readonly HeaderName ResentMessageId = new(KnownHeader.ResentMessageId);
    public static readonly HeaderName ListArchive = new(KnownHeader.ListArchive);
    public static readonly HeaderName ListHelp = new(KnownHeader.ListHelp);
    public static readonly HeaderName ListId = new(KnownHeader.ListId);
    public static readonly HeaderName ListOwner = new(KnownHeader.ListOwner);
    public static readonly HeaderName ListPost = new(KnownHeader.ListPost);
    public static readonly HeaderName ListSubscribe = new(KnownHeader.ListSubscribe);
    public static readonly HeaderName ListUnsubscribe = new(KnownHeader.ListUnsubscribe);
    public static readonly HeaderName DkimSignature = new(KnownHeader.DkimSignature);
    public static readonly HeaderName ArcAuthenticationResults = new(KnownHeader.ArcAuthenticationResults);
    public static readonly HeaderName ArcMessageSignature = new(KnownHeader.ArcMessageSignature);
    public static readonly HeaderName ArcSeal = new(KnownHeader.ArcSeal);
    public static readonly HeaderName Dkim2Signature = new(KnownHeader.Dkim2Signature);
    public static readonly HeaderName MessageInstance = new(KnownHeader.MessageInstance);
    public static readonly HeaderName AcceptLanguage = new(KnownHeader.AcceptLanguage);
    public static readonly HeaderName AlternateRecipient = new(KnownHeader.AlternateRecipient);
    public static readonly HeaderName ArchivedAt = new(KnownHeader.ArchivedAt);
    public static readonly HeaderName AuthenticationResults = new(KnownHeader.AuthenticationResults);
    public static readonly HeaderName AutoSubmitted = new(KnownHeader.AutoSubmitted);
    public static readonly HeaderName Autoforwarded = new(KnownHeader.Autoforwarded);
    public static readonly HeaderName Autosubmitted = new(KnownHeader.Autosubmitted);
    public static readonly HeaderName ContentAlternative = new(KnownHeader.ContentAlternative);
    public static readonly HeaderName ContentDuration = new(KnownHeader.ContentDuration);
    public static readonly HeaderName ContentFeatures = new(KnownHeader.ContentFeatures);
    public static readonly HeaderName ContentMd5 = new(KnownHeader.ContentMd5);
    public static readonly HeaderName ContentTranslationType = new(KnownHeader.ContentTranslationType);
    public static readonly HeaderName Conversion = new(KnownHeader.Conversion);
    public static readonly HeaderName ConversionWithLoss = new(KnownHeader.ConversionWithLoss);
    public static readonly HeaderName DlExpansionHistory = new(KnownHeader.DlExpansionHistory);
    public static readonly HeaderName DeferredDelivery = new(KnownHeader.DeferredDelivery);
    public static readonly HeaderName DeliveryDate = new(KnownHeader.DeliveryDate);
    public static readonly HeaderName DiscardedX400IpmsExtensions = new(KnownHeader.DiscardedX400IpmsExtensions);
    public static readonly HeaderName DiscardedX400MtsExtensions = new(KnownHeader.DiscardedX400MtsExtensions);
    public static readonly HeaderName DiscloseRecipients = new(KnownHeader.DiscloseRecipients);
    public static readonly HeaderName DispositionNotificationOptions = new(KnownHeader.DispositionNotificationOptions);
    public static readonly HeaderName DispositionNotificationTo = new(KnownHeader.DispositionNotificationTo);
    public static readonly HeaderName DowngradedFinalRecipient = new(KnownHeader.DowngradedFinalRecipient);
    public static readonly HeaderName DowngradedInReplyTo = new(KnownHeader.DowngradedInReplyTo);
    public static readonly HeaderName DowngradedMessageId = new(KnownHeader.DowngradedMessageId);
    public static readonly HeaderName DowngradedOriginalRecipient = new(KnownHeader.DowngradedOriginalRecipient);
    public static readonly HeaderName DowngradedReferences = new(KnownHeader.DowngradedReferences);
    public static readonly HeaderName Encoding = new(KnownHeader.Encoding);
    public static readonly HeaderName Expires = new(KnownHeader.Expires);
    public static readonly HeaderName GenerateDeliveryReport = new(KnownHeader.GenerateDeliveryReport);
    public static readonly HeaderName HpOuter = new(KnownHeader.HpOuter);
    public static readonly HeaderName Importance = new(KnownHeader.Importance);
    public static readonly HeaderName IncompleteCopy = new(KnownHeader.IncompleteCopy);
    public static readonly HeaderName Language = new(KnownHeader.Language);
    public static readonly HeaderName LatestDeliveryTime = new(KnownHeader.LatestDeliveryTime);
    public static readonly HeaderName ListUnsubscribePost = new(KnownHeader.ListUnsubscribePost);
    public static readonly HeaderName MessageContext = new(KnownHeader.MessageContext);
    public static readonly HeaderName MessageType = new(KnownHeader.MessageType);
    public static readonly HeaderName MmhsExemptedAddress = new(KnownHeader.MmhsExemptedAddress);
    public static readonly HeaderName MmhsExtendedAuthorisationInfo = new(KnownHeader.MmhsExtendedAuthorisationInfo);
    public static readonly HeaderName MmhsSubjectIndicatorCodes = new(KnownHeader.MmhsSubjectIndicatorCodes);
    public static readonly HeaderName MmhsHandlingInstructions = new(KnownHeader.MmhsHandlingInstructions);
    public static readonly HeaderName MmhsMessageInstructions = new(KnownHeader.MmhsMessageInstructions);
    public static readonly HeaderName MmhsCodressMessageIndicator = new(KnownHeader.MmhsCodressMessageIndicator);
    public static readonly HeaderName MmhsOriginatorReference = new(KnownHeader.MmhsOriginatorReference);
    public static readonly HeaderName MmhsPrimaryPrecedence = new(KnownHeader.MmhsPrimaryPrecedence);
    public static readonly HeaderName MmhsCopyPrecedence = new(KnownHeader.MmhsCopyPrecedence);
    public static readonly HeaderName MmhsMessageType = new(KnownHeader.MmhsMessageType);
    public static readonly HeaderName MmhsOtherRecipientsIndicatorTo = new(KnownHeader.MmhsOtherRecipientsIndicatorTo);
    public static readonly HeaderName MmhsOtherRecipientsIndicatorCc = new(KnownHeader.MmhsOtherRecipientsIndicatorCc);
    public static readonly HeaderName MmhsAcp127MessageIdentifier = new(KnownHeader.MmhsAcp127MessageIdentifier);
    public static readonly HeaderName MmhsOriginatorPlad = new(KnownHeader.MmhsOriginatorPlad);
    public static readonly HeaderName MtPriority = new(KnownHeader.MtPriority);
    public static readonly HeaderName Organization = new(KnownHeader.Organization);
    public static readonly HeaderName OriginalEncodedInformationTypes = new(KnownHeader.OriginalEncodedInformationTypes);
    public static readonly HeaderName OriginalFrom = new(KnownHeader.OriginalFrom);
    public static readonly HeaderName OriginalMessageId = new(KnownHeader.OriginalMessageId);
    public static readonly HeaderName OriginalRecipient = new(KnownHeader.OriginalRecipient);
    public static readonly HeaderName OriginatorReturnAddress = new(KnownHeader.OriginatorReturnAddress);
    public static readonly HeaderName OriginalSubject = new(KnownHeader.OriginalSubject);
    public static readonly HeaderName PicsLabel = new(KnownHeader.PicsLabel);
    public static readonly HeaderName PreventNonDeliveryReport = new(KnownHeader.PreventNonDeliveryReport);
    public static readonly HeaderName Priority = new(KnownHeader.Priority);
    public static readonly HeaderName ReceivedSpf = new(KnownHeader.ReceivedSpf);
    public static readonly HeaderName ReplyBy = new(KnownHeader.ReplyBy);
    public static readonly HeaderName RequireRecipientValidSince = new(KnownHeader.RequireRecipientValidSince);
    public static readonly HeaderName Sensitivity = new(KnownHeader.Sensitivity);
    public static readonly HeaderName Solicitation = new(KnownHeader.Solicitation);
    public static readonly HeaderName Supersedes = new(KnownHeader.Supersedes);
    public static readonly HeaderName TlsReportDomain = new(KnownHeader.TlsReportDomain);
    public static readonly HeaderName TlsReportSubmitter = new(KnownHeader.TlsReportSubmitter);
    public static readonly HeaderName TlsRequired = new(KnownHeader.TlsRequired);
    public static readonly HeaderName VbrInfo = new(KnownHeader.VbrInfo);
    public static readonly HeaderName X400ContentIdentifier = new(KnownHeader.X400ContentIdentifier);
    public static readonly HeaderName X400ContentReturn = new(KnownHeader.X400ContentReturn);
    public static readonly HeaderName X400ContentType = new(KnownHeader.X400ContentType);
    public static readonly HeaderName X400MtsIdentifier = new(KnownHeader.X400MtsIdentifier);
    public static readonly HeaderName X400Originator = new(KnownHeader.X400Originator);
    public static readonly HeaderName X400Received = new(KnownHeader.X400Received);
    public static readonly HeaderName X400Recipients = new(KnownHeader.X400Recipients);
    public static readonly HeaderName X400Trace = new(KnownHeader.X400Trace);
    public static readonly HeaderName ApparentlyTo = new(KnownHeader.ApparentlyTo);
    public static readonly HeaderName Author = new(KnownHeader.Author);
    public static readonly HeaderName CfblAddress = new(KnownHeader.CfblAddress);
    public static readonly HeaderName CfblFeedbackId = new(KnownHeader.CfblFeedbackId);
    public static readonly HeaderName DeliveredTo = new(KnownHeader.DeliveredTo);
    public static readonly HeaderName EdiintFeatures = new(KnownHeader.EdiintFeatures);
    public static readonly HeaderName EesstVersion = new(KnownHeader.EesstVersion);
    public static readonly HeaderName ErrorsTo = new(KnownHeader.ErrorsTo);
    public static readonly HeaderName Face = new(KnownHeader.Face);
    public static readonly HeaderName FormSub = new(KnownHeader.FormSub);
    public static readonly HeaderName JabberId = new(KnownHeader.JabberId);
    public static readonly HeaderName MmhsAuthorizingUsers = new(KnownHeader.MmhsAuthorizingUsers);
    public static readonly HeaderName Privicon = new(KnownHeader.Privicon);
    public static readonly HeaderName SioLabel = new(KnownHeader.SioLabel);
    public static readonly HeaderName SioLabelHistory = new(KnownHeader.SioLabelHistory);
    public static readonly HeaderName WrongRecipient = new(KnownHeader.WrongRecipient);

    public static HeaderName Other(string name) => new(name);
    public static HeaderName? parse(string? data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        return HeaderNameUtils.ParseHeaderName(data);
    }
    public static HeaderName? parse(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return null;
        return HeaderNameUtils.ParseHeaderName(System.Text.Encoding.ASCII.GetString(data));
    }

    public static implicit operator HeaderName(string name) => HeaderNameUtils.ParseHeaderName(name);

    public bool Equals(HeaderName other)
    {
        if (Kind != other.Kind) return false;
        if (Kind == KnownHeader.Other)
        {
            return string.Equals(CustomName, other.CustomName, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is HeaderName hn && Equals(hn);
    public override int GetHashCode() => Kind == KnownHeader.Other ? StringComparer.OrdinalIgnoreCase.GetHashCode(CustomName ?? "") : Kind.GetHashCode();
    public static bool operator ==(HeaderName left, HeaderName right) => left.Equals(right);
    public static bool operator !=(HeaderName left, HeaderName right) => !left.Equals(right);

    public string as_str() => HeaderNameUtils.HeaderNameAsStr(this);
    public string as_static_str() => HeaderNameUtils.HeaderNameAsStaticStr(this);
    public byte id() => HeaderNameUtils.HeaderNameId(this);
    public override string ToString() => as_str();
}

public static class HeaderNameUtils
{
    public static KnownHeader? ParseKnown(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "subject" => KnownHeader.Subject,
            "from" => KnownHeader.From,
            "to" => KnownHeader.To,
            "cc" => KnownHeader.Cc,
            "date" => KnownHeader.Date,
            "bcc" => KnownHeader.Bcc,
            "reply-to" => KnownHeader.ReplyTo,
            "sender" => KnownHeader.Sender,
            "comments" => KnownHeader.Comments,
            "in-reply-to" => KnownHeader.InReplyTo,
            "keywords" => KnownHeader.Keywords,
            "received" => KnownHeader.Received,
            "message-id" => KnownHeader.MessageId,
            "references" => KnownHeader.References,
            "return-path" => KnownHeader.ReturnPath,
            "mime-version" => KnownHeader.MimeVersion,
            "content-description" => KnownHeader.ContentDescription,
            "content-id" => KnownHeader.ContentId,
            "content-language" => KnownHeader.ContentLanguage,
            "content-location" => KnownHeader.ContentLocation,
            "content-transfer-encoding" => KnownHeader.ContentTransferEncoding,
            "content-type" => KnownHeader.ContentType,
            "content-disposition" => KnownHeader.ContentDisposition,
            "resent-to" => KnownHeader.ResentTo,
            "resent-from" => KnownHeader.ResentFrom,
            "resent-bcc" => KnownHeader.ResentBcc,
            "resent-cc" => KnownHeader.ResentCc,
            "resent-sender" => KnownHeader.ResentSender,
            "resent-date" => KnownHeader.ResentDate,
            "resent-message-id" => KnownHeader.ResentMessageId,
            "list-archive" => KnownHeader.ListArchive,
            "list-help" => KnownHeader.ListHelp,
            "list-id" => KnownHeader.ListId,
            "list-owner" => KnownHeader.ListOwner,
            "list-post" => KnownHeader.ListPost,
            "list-subscribe" => KnownHeader.ListSubscribe,
            "list-unsubscribe" => KnownHeader.ListUnsubscribe,
            "dkim-signature" => KnownHeader.DkimSignature,
            "arc-authentication-results" => KnownHeader.ArcAuthenticationResults,
            "arc-message-signature" => KnownHeader.ArcMessageSignature,
            "arc-seal" => KnownHeader.ArcSeal,
            "dkim2-signature" => KnownHeader.Dkim2Signature,
            "message-instance" => KnownHeader.MessageInstance,
            "accept-language" => KnownHeader.AcceptLanguage,
            "alternate-recipient" => KnownHeader.AlternateRecipient,
            "archived-at" => KnownHeader.ArchivedAt,
            "authentication-results" => KnownHeader.AuthenticationResults,
            "auto-submitted" => KnownHeader.AutoSubmitted,
            "autoforwarded" => KnownHeader.Autoforwarded,
            "autosubmitted" => KnownHeader.Autosubmitted,
            "content-alternative" => KnownHeader.ContentAlternative,
            "content-duration" => KnownHeader.ContentDuration,
            "content-features" => KnownHeader.ContentFeatures,
            "content-md5" => KnownHeader.ContentMd5,
            "content-translation-type" => KnownHeader.ContentTranslationType,
            "conversion" => KnownHeader.Conversion,
            "conversion-with-loss" => KnownHeader.ConversionWithLoss,
            "dl-expansion-history" => KnownHeader.DlExpansionHistory,
            "deferred-delivery" => KnownHeader.DeferredDelivery,
            "delivery-date" => KnownHeader.DeliveryDate,
            "discarded-x400-ipms-extensions" => KnownHeader.DiscardedX400IpmsExtensions,
            "discarded-x400-mts-extensions" => KnownHeader.DiscardedX400MtsExtensions,
            "disclose-recipients" => KnownHeader.DiscloseRecipients,
            "disposition-notification-options" => KnownHeader.DispositionNotificationOptions,
            "disposition-notification-to" => KnownHeader.DispositionNotificationTo,
            "downgraded-final-recipient" => KnownHeader.DowngradedFinalRecipient,
            "downgraded-in-reply-to" => KnownHeader.DowngradedInReplyTo,
            "downgraded-message-id" => KnownHeader.DowngradedMessageId,
            "downgraded-original-recipient" => KnownHeader.DowngradedOriginalRecipient,
            "downgraded-references" => KnownHeader.DowngradedReferences,
            "encoding" => KnownHeader.Encoding,
            "expires" => KnownHeader.Expires,
            "generate-delivery-report" => KnownHeader.GenerateDeliveryReport,
            "hp-outer" => KnownHeader.HpOuter,
            "importance" => KnownHeader.Importance,
            "incomplete-copy" => KnownHeader.IncompleteCopy,
            "language" => KnownHeader.Language,
            "latest-delivery-time" => KnownHeader.LatestDeliveryTime,
            "list-unsubscribe-post" => KnownHeader.ListUnsubscribePost,
            "message-context" => KnownHeader.MessageContext,
            "message-type" => KnownHeader.MessageType,
            "mmhs-exempted-address" => KnownHeader.MmhsExemptedAddress,
            "mmhs-extended-authorisation-info" => KnownHeader.MmhsExtendedAuthorisationInfo,
            "mmhs-subject-indicator-codes" => KnownHeader.MmhsSubjectIndicatorCodes,
            "mmhs-handling-instructions" => KnownHeader.MmhsHandlingInstructions,
            "mmhs-message-instructions" => KnownHeader.MmhsMessageInstructions,
            "mmhs-codress-message-indicator" => KnownHeader.MmhsCodressMessageIndicator,
            "mmhs-originator-reference" => KnownHeader.MmhsOriginatorReference,
            "mmhs-primary-precedence" => KnownHeader.MmhsPrimaryPrecedence,
            "mmhs-copy-precedence" => KnownHeader.MmhsCopyPrecedence,
            "mmhs-message-type" => KnownHeader.MmhsMessageType,
            "mmhs-other-recipients-indicator-to" => KnownHeader.MmhsOtherRecipientsIndicatorTo,
            "mmhs-other-recipients-indicator-cc" => KnownHeader.MmhsOtherRecipientsIndicatorCc,
            "mmhs-acp127-message-identifier" => KnownHeader.MmhsAcp127MessageIdentifier,
            "mmhs-originator-plad" => KnownHeader.MmhsOriginatorPlad,
            "mt-priority" => KnownHeader.MtPriority,
            "organization" => KnownHeader.Organization,
            "original-encoded-information-types" => KnownHeader.OriginalEncodedInformationTypes,
            "original-from" => KnownHeader.OriginalFrom,
            "original-message-id" => KnownHeader.OriginalMessageId,
            "original-recipient" => KnownHeader.OriginalRecipient,
            "originator-return-address" => KnownHeader.OriginatorReturnAddress,
            "original-subject" => KnownHeader.OriginalSubject,
            "pics-label" => KnownHeader.PicsLabel,
            "prevent-nondelivery-report" => KnownHeader.PreventNonDeliveryReport,
            "priority" => KnownHeader.Priority,
            "received-spf" => KnownHeader.ReceivedSpf,
            "reply-by" => KnownHeader.ReplyBy,
            "require-recipient-valid-since" => KnownHeader.RequireRecipientValidSince,
            "sensitivity" => KnownHeader.Sensitivity,
            "solicitation" => KnownHeader.Solicitation,
            "supersedes" => KnownHeader.Supersedes,
            "tls-report-domain" => KnownHeader.TlsReportDomain,
            "tls-report-submitter" => KnownHeader.TlsReportSubmitter,
            "tls-required" => KnownHeader.TlsRequired,
            "vbr-info" => KnownHeader.VbrInfo,
            "x400-content-identifier" => KnownHeader.X400ContentIdentifier,
            "x400-content-return" => KnownHeader.X400ContentReturn,
            "x400-content-type" => KnownHeader.X400ContentType,
            "x400-mts-identifier" => KnownHeader.X400MtsIdentifier,
            "x400-originator" => KnownHeader.X400Originator,
            "x400-received" => KnownHeader.X400Received,
            "x400-recipients" => KnownHeader.X400Recipients,
            "x400-trace" => KnownHeader.X400Trace,
            "apparently-to" => KnownHeader.ApparentlyTo,
            "author" => KnownHeader.Author,
            "cfbl-address" => KnownHeader.CfblAddress,
            "cfbl-feedback-id" => KnownHeader.CfblFeedbackId,
            "delivered-to" => KnownHeader.DeliveredTo,
            "ediint-features" => KnownHeader.EdiintFeatures,
            "eesst-version" => KnownHeader.EesstVersion,
            "errors-to" => KnownHeader.ErrorsTo,
            "face" => KnownHeader.Face,
            "form-sub" => KnownHeader.FormSub,
            "jabber-id" => KnownHeader.JabberId,
            "mmhs-authorizing-users" => KnownHeader.MmhsAuthorizingUsers,
            "privicon" => KnownHeader.Privicon,
            "sio-label" => KnownHeader.SioLabel,
            "sio-label-history" => KnownHeader.SioLabelHistory,
            "wrong-recipient" => KnownHeader.WrongRecipient,
            _ => null
        };
    }

    public static HeaderName ParseHeaderName(string name)
    {
        var known = ParseKnown(name);
        return known.HasValue ? new HeaderName(known.Value) : HeaderName.Other(name);
    }

    public static string HeaderNameAsStr(HeaderName hn)
    {
        if (hn.Kind == KnownHeader.Other) return hn.CustomName ?? "";
        return HeaderNameAsStaticStr(hn);
    }

    public static string HeaderNameAsStaticStr(HeaderName hn)
    {
        return hn.Kind switch
        {
            KnownHeader.Subject => "Subject",
            KnownHeader.From => "From",
            KnownHeader.To => "To",
            KnownHeader.Cc => "Cc",
            KnownHeader.Date => "Date",
            KnownHeader.Bcc => "Bcc",
            KnownHeader.ReplyTo => "Reply-To",
            KnownHeader.Sender => "Sender",
            KnownHeader.Comments => "Comments",
            KnownHeader.InReplyTo => "In-Reply-To",
            KnownHeader.Keywords => "Keywords",
            KnownHeader.Received => "Received",
            KnownHeader.MessageId => "Message-ID",
            KnownHeader.References => "References",
            KnownHeader.ReturnPath => "Return-Path",
            KnownHeader.MimeVersion => "MIME-Version",
            KnownHeader.ContentDescription => "Content-Description",
            KnownHeader.ContentId => "Content-ID",
            KnownHeader.ContentLanguage => "Content-Language",
            KnownHeader.ContentLocation => "Content-Location",
            KnownHeader.ContentTransferEncoding => "Content-Transfer-Encoding",
            KnownHeader.ContentType => "Content-Type",
            KnownHeader.ContentDisposition => "Content-Disposition",
            KnownHeader.ResentTo => "Resent-To",
            KnownHeader.ResentFrom => "Resent-From",
            KnownHeader.ResentBcc => "Resent-Bcc",
            KnownHeader.ResentCc => "Resent-Cc",
            KnownHeader.ResentSender => "Resent-Sender",
            KnownHeader.ResentDate => "Resent-Date",
            KnownHeader.ResentMessageId => "Resent-Message-ID",
            KnownHeader.ListArchive => "List-Archive",
            KnownHeader.ListHelp => "List-Help",
            KnownHeader.ListId => "List-ID",
            KnownHeader.ListOwner => "List-Owner",
            KnownHeader.ListPost => "List-Post",
            KnownHeader.ListSubscribe => "List-Subscribe",
            KnownHeader.ListUnsubscribe => "List-Unsubscribe",
            KnownHeader.DkimSignature => "DKIM-Signature",
            KnownHeader.ArcAuthenticationResults => "ARC-Authentication-Results",
            KnownHeader.ArcMessageSignature => "ARC-Message-Signature",
            KnownHeader.ArcSeal => "ARC-Seal",
            KnownHeader.Dkim2Signature => "DKIM2-Signature",
            KnownHeader.MessageInstance => "Message-Instance",
            KnownHeader.AcceptLanguage => "Accept-Language",
            KnownHeader.AlternateRecipient => "Alternate-Recipient",
            KnownHeader.ArchivedAt => "Archived-At",
            KnownHeader.AuthenticationResults => "Authentication-Results",
            KnownHeader.AutoSubmitted => "Auto-Submitted",
            KnownHeader.Autoforwarded => "Autoforwarded",
            KnownHeader.Autosubmitted => "Autosubmitted",
            KnownHeader.ContentAlternative => "Content-Alternative",
            KnownHeader.ContentDuration => "Content-Duration",
            KnownHeader.ContentFeatures => "Content-features",
            KnownHeader.ContentMd5 => "Content-MD5",
            KnownHeader.ContentTranslationType => "Content-Translation-Type",
            KnownHeader.Conversion => "Conversion",
            KnownHeader.ConversionWithLoss => "Conversion-With-Loss",
            KnownHeader.DlExpansionHistory => "DL-Expansion-History",
            KnownHeader.DeferredDelivery => "Deferred-Delivery",
            KnownHeader.DeliveryDate => "Delivery-Date",
            KnownHeader.DiscardedX400IpmsExtensions => "Discarded-X400-IPMS-Extensions",
            KnownHeader.DiscardedX400MtsExtensions => "Discarded-X400-MTS-Extensions",
            KnownHeader.DiscloseRecipients => "Disclose-Recipients",
            KnownHeader.DispositionNotificationOptions => "Disposition-Notification-Options",
            KnownHeader.DispositionNotificationTo => "Disposition-Notification-To",
            KnownHeader.DowngradedFinalRecipient => "Downgraded-Final-Recipient",
            KnownHeader.DowngradedInReplyTo => "Downgraded-In-Reply-To",
            KnownHeader.DowngradedMessageId => "Downgraded-Message-Id",
            KnownHeader.DowngradedOriginalRecipient => "Downgraded-Original-Recipient",
            KnownHeader.DowngradedReferences => "Downgraded-References",
            KnownHeader.Encoding => "Encoding",
            KnownHeader.Expires => "Expires",
            KnownHeader.GenerateDeliveryReport => "Generate-Delivery-Report",
            KnownHeader.HpOuter => "HP-Outer",
            KnownHeader.Importance => "Importance",
            KnownHeader.IncompleteCopy => "Incomplete-Copy",
            KnownHeader.Language => "Language",
            KnownHeader.LatestDeliveryTime => "Latest-Delivery-Time",
            KnownHeader.ListUnsubscribePost => "List-Unsubscribe-Post",
            KnownHeader.MessageContext => "Message-Context",
            KnownHeader.MessageType => "Message-Type",
            KnownHeader.MmhsExemptedAddress => "MMHS-Exempted-Address",
            KnownHeader.MmhsExtendedAuthorisationInfo => "MMHS-Extended-Authorisation-Info",
            KnownHeader.MmhsSubjectIndicatorCodes => "MMHS-Subject-Indicator-Codes",
            KnownHeader.MmhsHandlingInstructions => "MMHS-Handling-Instructions",
            KnownHeader.MmhsMessageInstructions => "MMHS-Message-Instructions",
            KnownHeader.MmhsCodressMessageIndicator => "MMHS-Codress-Message-Indicator",
            KnownHeader.MmhsOriginatorReference => "MMHS-Originator-Reference",
            KnownHeader.MmhsPrimaryPrecedence => "MMHS-Primary-Precedence",
            KnownHeader.MmhsCopyPrecedence => "MMHS-Copy-Precedence",
            KnownHeader.MmhsMessageType => "MMHS-Message-Type",
            KnownHeader.MmhsOtherRecipientsIndicatorTo => "MMHS-Other-Recipients-Indicator-To",
            KnownHeader.MmhsOtherRecipientsIndicatorCc => "MMHS-Other-Recipients-Indicator-CC",
            KnownHeader.MmhsAcp127MessageIdentifier => "MMHS-Acp127-Message-Identifier",
            KnownHeader.MmhsOriginatorPlad => "MMHS-Originator-PLAD",
            KnownHeader.MtPriority => "MT-Priority",
            KnownHeader.Organization => "Organization",
            KnownHeader.OriginalEncodedInformationTypes => "Original-Encoded-Information-Types",
            KnownHeader.OriginalFrom => "Original-From",
            KnownHeader.OriginalMessageId => "Original-Message-ID",
            KnownHeader.OriginalRecipient => "Original-Recipient",
            KnownHeader.OriginatorReturnAddress => "Originator-Return-Address",
            KnownHeader.OriginalSubject => "Original-Subject",
            KnownHeader.PicsLabel => "PICS-Label",
            KnownHeader.PreventNonDeliveryReport => "Prevent-NonDelivery-Report",
            KnownHeader.Priority => "Priority",
            KnownHeader.ReceivedSpf => "Received-SPF",
            KnownHeader.ReplyBy => "Reply-By",
            KnownHeader.RequireRecipientValidSince => "Require-Recipient-Valid-Since",
            KnownHeader.Sensitivity => "Sensitivity",
            KnownHeader.Solicitation => "Solicitation",
            KnownHeader.Supersedes => "Supersedes",
            KnownHeader.TlsReportDomain => "TLS-Report-Domain",
            KnownHeader.TlsReportSubmitter => "TLS-Report-Submitter",
            KnownHeader.TlsRequired => "TLS-Required",
            KnownHeader.VbrInfo => "VBR-Info",
            KnownHeader.X400ContentIdentifier => "X400-Content-Identifier",
            KnownHeader.X400ContentReturn => "X400-Content-Return",
            KnownHeader.X400ContentType => "X400-Content-Type",
            KnownHeader.X400MtsIdentifier => "X400-MTS-Identifier",
            KnownHeader.X400Originator => "X400-Originator",
            KnownHeader.X400Received => "X400-Received",
            KnownHeader.X400Recipients => "X400-Recipients",
            KnownHeader.X400Trace => "X400-Trace",
            KnownHeader.ApparentlyTo => "Apparently-To",
            KnownHeader.Author => "Author",
            KnownHeader.CfblAddress => "CFBL-Address",
            KnownHeader.CfblFeedbackId => "CFBL-Feedback-ID",
            KnownHeader.DeliveredTo => "Delivered-To",
            KnownHeader.EdiintFeatures => "EDIINT-Features",
            KnownHeader.EesstVersion => "Eesst-Version",
            KnownHeader.ErrorsTo => "Errors-To",
            KnownHeader.Face => "Face",
            KnownHeader.FormSub => "Form-Sub",
            KnownHeader.JabberId => "Jabber-ID",
            KnownHeader.MmhsAuthorizingUsers => "MMHS-Authorizing-Users",
            KnownHeader.Privicon => "Privicon",
            KnownHeader.SioLabel => "SIO-Label",
            KnownHeader.SioLabelHistory => "SIO-Label-History",
            KnownHeader.WrongRecipient => "Wrong-Recipient",
            _ => ""
        };
    }

    public static byte HeaderNameId(HeaderName hn)
    {
        return hn.Kind switch
        {
            KnownHeader.Subject => 0,
            KnownHeader.From => 1,
            KnownHeader.To => 2,
            KnownHeader.Cc => 3,
            KnownHeader.Date => 4,
            KnownHeader.Bcc => 5,
            KnownHeader.ReplyTo => 6,
            KnownHeader.Sender => 7,
            KnownHeader.Comments => 8,
            KnownHeader.InReplyTo => 9,
            KnownHeader.Keywords => 10,
            KnownHeader.Received => 11,
            KnownHeader.MessageId => 12,
            KnownHeader.References => 13,
            KnownHeader.ReturnPath => 14,
            KnownHeader.MimeVersion => 15,
            KnownHeader.ContentDescription => 16,
            KnownHeader.ContentId => 17,
            KnownHeader.ContentLanguage => 18,
            KnownHeader.ContentLocation => 19,
            KnownHeader.ContentTransferEncoding => 20,
            KnownHeader.ContentType => 21,
            KnownHeader.ContentDisposition => 22,
            KnownHeader.ResentTo => 23,
            KnownHeader.ResentFrom => 24,
            KnownHeader.ResentBcc => 25,
            KnownHeader.ResentCc => 26,
            KnownHeader.ResentSender => 27,
            KnownHeader.ResentDate => 28,
            KnownHeader.ResentMessageId => 29,
            KnownHeader.ListArchive => 30,
            KnownHeader.ListHelp => 31,
            KnownHeader.ListId => 32,
            KnownHeader.ListOwner => 33,
            KnownHeader.ListPost => 34,
            KnownHeader.ListSubscribe => 35,
            KnownHeader.ListUnsubscribe => 36,
            KnownHeader.DkimSignature => 41,
            KnownHeader.ArcAuthenticationResults => 38,
            KnownHeader.ArcMessageSignature => 39,
            KnownHeader.ArcSeal => 40,
            KnownHeader.Dkim2Signature => 42,
            KnownHeader.MessageInstance => 43,
            KnownHeader.AcceptLanguage => 44,
            KnownHeader.AlternateRecipient => 45,
            KnownHeader.ArchivedAt => 46,
            KnownHeader.AuthenticationResults => 47,
            KnownHeader.AutoSubmitted => 48,
            KnownHeader.Autoforwarded => 49,
            KnownHeader.Autosubmitted => 50,
            KnownHeader.ContentAlternative => 51,
            KnownHeader.ContentDuration => 52,
            KnownHeader.ContentFeatures => 53,
            KnownHeader.ContentMd5 => 54,
            KnownHeader.ContentTranslationType => 55,
            KnownHeader.Conversion => 56,
            KnownHeader.ConversionWithLoss => 57,
            KnownHeader.DlExpansionHistory => 58,
            KnownHeader.DeferredDelivery => 59,
            KnownHeader.DeliveryDate => 60,
            KnownHeader.DiscardedX400IpmsExtensions => 61,
            KnownHeader.DiscardedX400MtsExtensions => 62,
            KnownHeader.DiscloseRecipients => 63,
            KnownHeader.DispositionNotificationOptions => 64,
            KnownHeader.DispositionNotificationTo => 65,
            KnownHeader.DowngradedFinalRecipient => 66,
            KnownHeader.DowngradedInReplyTo => 67,
            KnownHeader.DowngradedMessageId => 68,
            KnownHeader.DowngradedOriginalRecipient => 69,
            KnownHeader.DowngradedReferences => 70,
            KnownHeader.Encoding => 71,
            KnownHeader.Expires => 72,
            KnownHeader.GenerateDeliveryReport => 73,
            KnownHeader.HpOuter => 74,
            KnownHeader.Importance => 75,
            KnownHeader.IncompleteCopy => 76,
            KnownHeader.Language => 77,
            KnownHeader.LatestDeliveryTime => 78,
            KnownHeader.ListUnsubscribePost => 79,
            KnownHeader.MessageContext => 80,
            KnownHeader.MessageType => 81,
            KnownHeader.MmhsExemptedAddress => 82,
            KnownHeader.MmhsExtendedAuthorisationInfo => 83,
            KnownHeader.MmhsSubjectIndicatorCodes => 84,
            KnownHeader.MmhsHandlingInstructions => 85,
            KnownHeader.MmhsMessageInstructions => 86,
            KnownHeader.MmhsCodressMessageIndicator => 87,
            KnownHeader.MmhsOriginatorReference => 88,
            KnownHeader.MmhsPrimaryPrecedence => 89,
            KnownHeader.MmhsCopyPrecedence => 90,
            KnownHeader.MmhsMessageType => 91,
            KnownHeader.MmhsOtherRecipientsIndicatorTo => 92,
            KnownHeader.MmhsOtherRecipientsIndicatorCc => 93,
            KnownHeader.MmhsAcp127MessageIdentifier => 94,
            KnownHeader.MmhsOriginatorPlad => 95,
            KnownHeader.MtPriority => 96,
            KnownHeader.Organization => 97,
            KnownHeader.OriginalEncodedInformationTypes => 98,
            KnownHeader.OriginalFrom => 99,
            KnownHeader.OriginalMessageId => 100,
            KnownHeader.OriginalRecipient => 101,
            KnownHeader.OriginatorReturnAddress => 102,
            KnownHeader.OriginalSubject => 103,
            KnownHeader.PicsLabel => 104,
            KnownHeader.PreventNonDeliveryReport => 105,
            KnownHeader.Priority => 106,
            KnownHeader.ReceivedSpf => 107,
            KnownHeader.ReplyBy => 108,
            KnownHeader.RequireRecipientValidSince => 109,
            KnownHeader.Sensitivity => 110,
            KnownHeader.Solicitation => 111,
            KnownHeader.Supersedes => 112,
            KnownHeader.TlsReportDomain => 113,
            KnownHeader.TlsReportSubmitter => 114,
            KnownHeader.TlsRequired => 115,
            KnownHeader.VbrInfo => 116,
            KnownHeader.X400ContentIdentifier => 117,
            KnownHeader.X400ContentReturn => 118,
            KnownHeader.X400ContentType => 119,
            KnownHeader.X400MtsIdentifier => 120,
            KnownHeader.X400Originator => 121,
            KnownHeader.X400Received => 122,
            KnownHeader.X400Recipients => 123,
            KnownHeader.X400Trace => 124,
            KnownHeader.ApparentlyTo => 125,
            KnownHeader.Author => 126,
            KnownHeader.CfblAddress => 127,
            KnownHeader.CfblFeedbackId => 128,
            KnownHeader.DeliveredTo => 129,
            KnownHeader.EdiintFeatures => 130,
            KnownHeader.EesstVersion => 131,
            KnownHeader.ErrorsTo => 132,
            KnownHeader.Face => 133,
            KnownHeader.FormSub => 134,
            KnownHeader.JabberId => 135,
            KnownHeader.MmhsAuthorizingUsers => 136,
            KnownHeader.Privicon => 137,
            KnownHeader.SioLabel => 138,
            KnownHeader.SioLabelHistory => 139,
            KnownHeader.WrongRecipient => 140,
            _ => 37
        };
    }
}

// Rust: Header
public record Header
{
    [JsonPropertyName("name")]
    public HeaderName name { get; set; }

    [JsonPropertyName("value")]
    public HeaderValue value { get; set; } = HeaderValue.Empty;

    [JsonPropertyName("offset_field")]
    public uint offset_field { get; set; }

    [JsonPropertyName("offset_start")]
    public uint offset_start { get; set; }

    [JsonPropertyName("offset_end")]
    public uint offset_end { get; set; }

    public Header() { }

    public Header(HeaderName name, HeaderValue value, uint offset_field = 0, uint offset_start = 0, uint offset_end = 0)
    {
        this.name = name;
        this.value = value;
        this.offset_field = offset_field;
        this.offset_start = offset_start;
        this.offset_end = offset_end;
    }
}

// Rust: MessagePart
public partial record MessagePart
{
    public MessagePart() { }
    public MessagePart(List<Header> headers, Encoding encoding, PartType body, uint offset_header, uint offset_body, uint offset_end, bool is_encoding_problem)
    {
        this.headers = headers;
        this.encoding = encoding;
        this.body = body;
        this.offset_header = offset_header;
        this.offset_body = offset_body;
        this.offset_end = offset_end;
        this.is_encoding_problem = is_encoding_problem;
    }

    [JsonPropertyName("headers")]
    public List<Header> headers { get; set; } = new();

    [JsonPropertyName("is_encoding_problem")]
    public bool is_encoding_problem { get; set; }
    public int len() => body.len();

    [JsonPropertyName("body")]
    public PartType body { get; set; } = PartType.Multipart(new List<uint>());

    [JsonIgnore]
    public Encoding encoding { get; set; } = Encoding.None;

    [JsonPropertyName("offset_header")]
    public uint offset_header { get; set; }

    [JsonPropertyName("offset_body")]
    public uint offset_body { get; set; }

    [JsonPropertyName("offset_end")]
    public uint offset_end { get; set; }
}

// Rust: Message
public partial record Message
{
    [JsonPropertyName("html_body")]
    public List<uint> html_body { get; set; } = new();

    [JsonPropertyName("text_body")]
    public List<uint> text_body { get; set; } = new();

    [JsonPropertyName("attachments")]
    public List<uint> attachments { get; set; } = new();

    [JsonPropertyName("parts")]
    public List<MessagePart> parts { get; set; } = new();

    [JsonIgnore]
    public byte[]? raw_message { get; set; }
}

// Rust: MessageParser
public partial class MessageParser
{
    public Dictionary<HeaderName, Func<MessageStream, HeaderValue>> header_map { get; set; } = new();
    public Func<MessageStream, HeaderValue> def_hdr_parse_fnc { get; set; } = (s) => s.parse_raw();
}

// Rust: MimeHeaders
public interface IMimeHeaders
{
    string? content_description();
    ContentType? content_disposition();
    string? content_id();
    string? content_transfer_encoding();
    ContentType? content_type();
    HeaderValue content_language();
    string? content_location();
    string? attachment_name();
    bool is_content_type(string type_, string subtype);
}
