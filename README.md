# mail-parser

A .NET 10 port of [Stalwart mail-parser](https://github.com/stalwartlabs/mail-parser) v0.11.8.

The port keeps the upstream source attribution in each translated C# file. Stalwart Labs LLC owns the upstream copyright. Shrishail Rana owns the C# port copyright. The code is available under Apache-2.0 or MIT, at your option.

## Build

Install the .NET 10 SDK, then build the standalone solution:

```powershell
dotnet build Stalwart.MailParser.Port.sln -c Release
```

Run the full ported test project and its public fixture corpus:

```powershell
dotnet test tests/Stalwart.MailParser.Port.Tests.csproj -c Release
```

## Use

```csharp
using System.Text;
using Stalwart.MailParser.Port;

byte[] source = Encoding.UTF8.GetBytes("Subject: hello\r\n\r\nbody");
var message = new MessageParser().parse(source);

Console.WriteLine(message?.subject());
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | .NET 10 parsing library |
| `tests/` | MSTest port of the upstream test suite |
| `examples/` | C# ports of the upstream examples |
| `fuzz/` | C# port of the upstream fuzz entry point |
| `resources/` | Public upstream fixtures, preserved byte-for-byte |
| `LICENSES/` | Full Apache-2.0 and MIT license texts |

`resources/` is marked as non-text in `.gitattributes`. Mail and JSON fixtures keep their original bytes even when a Git checkout uses automatic line-ending conversion.

## Provenance and licensing

This repository tracks the Stalwart mail-parser v0.11.8 source snapshot pinned by the translated file headers. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for attribution and [LICENSE](LICENSE) for the dual-license terms.

`CHANGELOG.md` starts with this C# port’s 0.11.8 entry, then preserves the upstream Rust history unchanged.
