mail-parser 0.11.8 (C# port)
================================
- Initial .NET 10 port of Stalwart mail-parser 0.11.8.
- Preserved the public upstream fixtures and source attribution.
- The upstream 0.11.8 entry and its full prior history follow unchanged.
- Full Rust-to-C# source parity audit (all 44 file pairs, symbol-by-symbol) found and fixed ~30 behavioral divergences from upstream; see `PARITY-AUDIT.md` for complete findings and rationale.
- Fix: `core/header.cs` was not actually a port of `header.rs` (held unrelated date-utility code instead); reorganized to hold the real `Header`/`HeaderValue`/`HeaderName`/`ContentType`/`Received`/`Host` API.
- Fix: `as_text()`/`as_text_list()` returned the wrong `TextList` element (first instead of last), which affected `message_id()`, `content_id()`, and `return_address()`.
- Fix: `return_address()` never actually fell back to the `From` header when `Return-Path` was absent.
- Fix: several ASCII-only-vs-Unicode divergences in whitespace classification and case folding (7+ call sites).
- Fix: `mbox` parsing silently stripped CRLF line endings from message content; date validity checking was too weak.
- Fix: `maildir` accepted a folder missing either `cur/` or `new/`, and always used the wrong path separator.
- Fix: `content_type` parsing fabricated a result on truncated input with no trailing newline.
- Fix: quoted-printable decoding silently inserted recovery bytes on malformed input instead of signaling failure.
- Fix: `Received` header parsing silently accepted IPv4 octets with leading zeros (a known octal-interpretation/SSRF-bypass ambiguity); now rejected.
- Fix: `HeaderName.resent_cc()` intentionally kept reading the correct header, documented as a deliberate deviation from a real bug in pinned upstream Rust.
- Removed unused `rkyv` zero-copy stub classes (no .NET equivalent to Rust's zero-copy technique; confirmed unused).
- Wired up previously dead-code `BodyPartIterator`/`AttachmentIterator` to the actual public iteration API.
- Fix: `remove_header()` used order-preserving removal instead of Rust's `swap_remove` (remaining header order could differ from upstream).
- Fix: `header_raw()`/`headers_raw()` silently substituted replacement characters for invalid UTF-8 instead of matching Rust's strict decode-or-reject behavior.
- Fix: `header_as(HeaderForm.URLs)` fell through to the wrong parser instead of routing through address parsing; `HeaderForm.Raw` now matches Rust's actual inline decode/trim logic instead of delegating to the wrong parser.
- Fix: `DateTime.ToString()` returned an RFC822-formatted string; Rust's `Display` returns RFC3339.
- Fix: `HeaderName` equality/hashing used broader Unicode case-folding instead of Rust's ASCII-only comparison.
- Fix: `HeaderValue.len()` counted UTF-16 characters instead of UTF-8 bytes for address and content-type values.
- Fix: `maildir` folder traversal did not match Rust's actual recursive, prefix-gated discovery algorithm.

mail-parser 0.11.8
================================
- Fix: `HeaderName` breaks rkyv serialization from <= 0.11.6.

mail-parser 0.11.7
================================
- Added more IANA headers.
- Fix: `DateTime::to_timezone` corrupting non-whole-hour offsets by storing leftover seconds in `tz_minute` (#158)

mail-parser 0.11.6
================================
- Fix: Missing whitespace between a quoted name and a following encoded word (#150)
- Fix: `panic` when a `Received` header ends with a folded line (#155)
- Fix: `Received` header tokens losing their last character at the end of the input, retaining folding characters, and failing to parse a clause folded before its value (#155)
- Fix: Multi-word display names followed by a comment no longer produce a fabricated address (#153)

mail-parser 0.11.5
================================
- Fix: Address names containing LF are not parsed correctly (#149)
- Fix: Decode the `iso-8859-1` label as `windows-1252` per the WHATWG Encoding Standard (#131)
- Fix: `panic` with messages containing corrupted eml attachments (#120).
- Recognize additional charset labels supported by `encoding_rs` (#123)
- Added `Dkim2Signature` and `MessageInstance` to support DKIM2 headers.

mail-parser 0.11.4
================================
- Add `Message::received_all()` to iterate over all Received header fields (#146)
- Reject dates with invalid month names.
- `parse_date()` doesn't handle UTC+12 and greater (#148)
- Leniently decode quoted-printable bodies with invalid `=` escapes (#144)

mail-parser 0.11.3
================================
- Fix panic with messages containing corrupted attachments (#145)

mail-parser 0.11.2
================================
- Do not return invalid mime parts when parsing broken nested messages.
- Fix broken receive header date parsing when tab is used in long header syntax (#130)

mail-parser 0.11.1
================================
- Fix `DateTime::from_timestamp` to handle negative timestamps correctly.

mail-parser 0.11.0
================================
- `rkyv` zero-copy deserialization support.
- Changed `usize` to `u32` types.
- Renamed `serde_support` feature to `serde`.
- Fix: Parsing of headers without LFs (#102) 

mail-parser 0.10.2
================================
- Fixed `HeaderName` enum order to avoid breaking bincode serialization.

mail-parser 0.10.1
================================
- Fixed `HeaderName::parse` function.

mail-parser 0.10.0
================================
- Perfect hashing using `hashify` crate rather than static `gperf` generated code.
- Added `DkimSignature`, `ArcAuthenticationResults`, `ArcMessageSignature` and `ArcSeal` headers. 
- `HeaderName` is non-exhaustive.
- Parse obsolete timezones (#95).
- Fix: Folding ws between "Content-Type:" and "plain/text" leads to empty header (#96).
- Fix: Multiline quoted continuations (closes #92).
- Fix: Deserialize (#93).
- Retain mbox IO errors (#91).
- Hide concrete type behind impl type (#94).
- Removed `ludicrous` feature, the Rust compiler is smart enough to optimize array lookups.

mail-parser 0.9.4
================================
- Flexible parsing of charset names (#85).

mail-parser 0.9.3
================================
- Fixed parsing of address names containing @ (#80)

mail-parser 0.9.2
================================
- Fixed `quoted_printable_decode` external function (not used by mail-parser directly).
- Fix `Received` header serialization for bincode compatibility.

mail-parser 0.9.1
================================
- Fixed panic when Content-Disposition is empty (#63)
- Removed `content_type()` and `address()` functions that could `panic!`. Use `as_content_type()` and `as_address()` instead.
- Updated Rust edition to 2021.

mail-parser 0.9.0
================================
This version introduces multiple breaking changes. Please read the following notes carefully.

- Parsing is now done using `MessageParser`, which allows to customize the parsing process.
- Added parser for `Received` headers.
- Added `MessageParser::parse_headers` function to parse only the headers of a message.
- Removed `RfcHeader` enum, now all headers are represented using `HeaderName`.
- All address types are now stored in the `HeaderValue::Address` variant using the `Address` enum.
- Renamed the `as_` prefix to `to_` in some functions.

mail-parser 0.8.2
================================
- Fix: Parsing address name with \ characters (#41) 
- Fix: Missing space when folded header begins with RFC2047 word (#43) 

mail-parser 0.8.1
================================
- Added `raw_message()` function.

mail-parser 0.8.0
================================
- Removed get_() prefixes (#31).
- Maildir import: Use modified time instead of created time (#32)

mail-parser 0.7.0
================================
- Base64/QuotedPrintable decoding optimizations.
- Automatic parsing of base64/qp encoded nested messages.
- Refactoring or ``MessageStream`` to use iterators more efficiently.
- Added "ludicrous mode" Cargo option to use some unsafe code for additional performance.
- Fixed support for empty messages.
- Fixed raw offsets of multipart/* parts to include MIME epilogue.
- Fixed values of non-RFC headers.

mail-parser 0.6.1
================================
- Support for malformed unstructured fields containing encoded words (#29).
- Add support for gb2312 charsets (#30).

mail-parser 0.6.0
================================
- Maildir parsing support.
- Headers and attributes are now stored in a `Vec` instead of a `HashMap` for a tiny performance enhancement.
- Support for Content-Type attributes spanning multiple lines.
- Support for malformed Thunderbird messages (#27). 
- Fixed raw offset range for body parts.

mail-parser 0.5.0
================================
- `Message` headers are now stored as a `MessagePart` with index 0.
- Improved `MessagePart` API.
- Nested base64/quoted-printable encoded message/rfc822 parts are automatically parsed when calling `get_message`.
- Better handling of malformed MIME messages.
- Added raw offsets to MIME parts.

mail-parser 0.4.8
================================
- get_bytes_to_boundary fix (#21)

mail-parser 0.4.7
================================
- Retrieving message headers in order (#19)
- Added `get_raw_headers` and `get_header` methods.
- Added `get_return_address` method to obtain the return address from the Return-Path or From headers.
- Support for malformed Return-Path headers.
- Support for ks_c_5601 charsets (#20)

mail-parser 0.4.6
================================
- DateTime is_valid() fix (#15)
  
mail-parser 0.4.5
================================
- DateTime to UNIX timestamp conversion.
- Ord, PartialOrd support for DateTime (#13).
- Fixed Message::parse() panic on duplicate Content-Type headers (#14).

mail-parser 0.4.4
================================
- Support for multi-line headers.
- Text and HTML message body preview.
- Improved support for raw headers.

mail-parser 0.4.3
================================
- Mbox file parsing support (issue #11) conforming to the [QMail specification](http://qmail.org/qmail-manual-html/man5/mbox.html).
- Support for bincode serialize/deserialize.

mail-parser 0.4.2
================================
- Added `Message::get_thread_name()` to obtain the base subject of a message as defined in [RFC 5957 - Internet Message Access Protocol - SORT and THREAD Extensions (Section 2.1)](https://datatracker.ietf.org/doc/html/rfc5256#section-2.1).
- Added `MimeHeader::get_attachment_name` for simplified access to a MIME attachment file name.

mail-parser 0.4.1
================================
- Lazy parsing of nested e-mail messages.
- Support for base64/quoted-printable nested messages.

mail-parser 0.4.0
================================
- Lazy conversion to/from HTML an plain text parts.
- Improved API.
- Parts are now generics.

mail-parser 0.3.1
================================
- Support for non-standard headers.
- Raw message offsets are stored in the message object.
- Message body structure is now stored in the message object.

mail-parser 0.3
================================
- Improved API, now `Message::parse` returns `Option<Message>` to indicate when parsing was successful.
- Headers are now stored internally in a `HashMap` instead of `struct` fields.
- Added support for new RFCs:
  - [RFC 2557 - MIME Encapsulation of Aggregate Documents, such as HTML (MHTML)](https://datatracker.ietf.org/doc/html/rfc2557)
  - [RFC 2392 - Content-ID and Message-ID Uniform Resource Locators](https://datatracker.ietf.org/doc/html/rfc2392)
  - [RFC 3282 - Content Language Headers](https://datatracker.ietf.org/doc/html/rfc3282)
  - [RFC 3339 - Date and Time on the Internet: Timestamps](https://datatracker.ietf.org/doc/html/rfc3339)

mail-parser 0.2.1
================================
- Performance enhacements, now *mail-parser* is almost as fast as the `unsafe` 0.1 version.

mail-parser 0.2
================================
- Re-factoring to use **100% safe** Rust after a [discussion on Reddit](https://www.reddit.com/r/rust/comments/qkc5rk/fast_and_robust_email_parsing_library_for_rust/).
- Added `Message::is_empty`.

mail-parser 0.1.1
================================
- Bug-fixing after **fuzzing** the library.

mail-parser 0.1
================================
- Initial release with plenty of `unsafe` code to speed things up.






