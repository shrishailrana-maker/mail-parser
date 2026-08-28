/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/parsers/fields/mod.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 4bf7c4677fcf29096a529953274ec735c9bf520f20e88b56fb363c1fafe09165
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public class TestField<T>
{
    [JsonPropertyName("header")]
    public string header { get; set; } = "";

    [JsonPropertyName("expected")]
    public T expected { get; set; } = default!;
}

public static class FieldTestUtils
{
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new AttributeJsonConverter(),
            new IPAddressJsonConverter(),
            new AddressJsonConverter(),
            new HostJsonConverter(),
            new PartTypeJsonConverter(),
            new HeaderValueJsonConverter(),
            new HeaderNameJsonConverter()
        }
    };

    public static List<TestField<T>> load_tests<T>(string test_name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "resources", $"{test_name}.json");
        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        return JsonSerializer.Deserialize<List<TestField<T>>>(json, JsonOptions)!;
    }
}
