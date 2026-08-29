/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/core/rkyv.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
//
// Intentionally out of scope (Boss decision, PARITY-AUDIT.md "PHASE 2 IN PROGRESS" /
// "rkyv scope"): rkyv is a Rust-specific zero-copy binary archival technique with no
// natural .NET equivalent. The previous partial stub types (ArchivedAddr, ArchivedGroup,
// ArchivedContentType, ArchivedReceived) had zero references anywhere in this codebase
// and have been removed. See PARITY-AUDIT.md, FILE 6, for the full analysis.
