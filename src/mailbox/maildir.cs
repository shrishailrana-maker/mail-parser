/*
 * SPDX-FileCopyrightText: 2020 Stalwart Labs LLC <hello@stalw.art>
 * SPDX-FileCopyrightText: 2026 Shrishail Rana (C# port)
 *
 * Ported from Stalwart mail-parser v0.11.8.
 * SPDX-License-Identifier: Apache-2.0 OR MIT
 */

// Port of: src/mailbox/maildir.rs
// Upstream commit: 499ae0f2ff649af84c921af4b008f7c617b0bf87
// Source SHA-256: 1a8de7a7becf2644adc27a6d7fc879d7339fa0ba8e3953c897d6d088598fc797
// This file must remain 1:1 with the Rust source file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if STALWART_PORT_TESTS
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Stalwart.MailParser.Port;

public enum MaildirFlag
{
    Passed,
    Replied,
    Seen,
    Trashed,
    Draft,
    Flagged,
}

public class MaildirMessage : IComparable<MaildirMessage>, IEquatable<MaildirMessage>
{
    public ulong internal_date { get; set; }
    public List<MaildirFlag> flags { get; set; } = new();
    public byte[] contents { get; set; } = Array.Empty<byte>();
    public string path { get; set; } = "";

    public ulong internal_date_sec() => internal_date;
    public IReadOnlyList<MaildirFlag> flags_list() => flags;
    public string path_str() => path;
    public byte[] contents_bytes() => contents;
    public byte[] unwrap_contents() => contents;

    public int CompareTo(MaildirMessage? other)
    {
        if (other == null) return 1;
        int c = internal_date.CompareTo(other.internal_date);
        if (c != 0) return c;
        c = flags.Count.CompareTo(other.flags.Count);
        if (c != 0) return c;
        for (int i = 0; i < flags.Count; i++)
        {
            c = flags[i].CompareTo(other.flags[i]);
            if (c != 0) return c;
        }
        c = string.CompareOrdinal(path, other.path);
        if (c != 0) return c;
        return StructuralComparisons.StructuralComparer.Compare(contents, other.contents);
    }

    public bool Equals(MaildirMessage? other)
    {
        if (other == null) return false;
        if (internal_date != other.internal_date) return false;
        if (flags.Count != other.flags.Count) return false;
        for (int i = 0; i < flags.Count; i++)
        {
            if (flags[i] != other.flags[i]) return false;
        }
        if (path != other.path) return false;
        return StructuralComparisons.StructuralEqualityComparer.Equals(contents, other.contents);
    }

    public override bool Equals(object? obj) => Equals(obj as MaildirMessage);
    public override int GetHashCode() => HashCode.Combine(internal_date, flags.Count, path, contents.Length);
}

public class MaildirFolder : IEnumerable<MaildirMessage>
{
    public string? name { get; set; }
    public string path { get; set; }

    public MaildirFolder(string folderPath, string? folderName)
    {
        path = folderPath;
        name = folderName;
    }

    public string? folder_name() => name;

    public IEnumerator<MaildirMessage> GetEnumerator()
    {
        var curDir = Path.Combine(path, "cur");
        var newDir = Path.Combine(path, "new");

        var files = new List<string>();
        if (Directory.Exists(curDir)) files.AddRange(Directory.GetFiles(curDir));
        if (Directory.Exists(newDir)) files.AddRange(Directory.GetFiles(newDir));

        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            if (fileName.StartsWith(".")) continue;

            var fi = new FileInfo(file);
            ulong modTime = (ulong)new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds();
            byte[] bytes = File.ReadAllBytes(file);

            var flags = new List<MaildirFlag>();
            int idx = fileName.LastIndexOf("2,");
            if (idx >= 0)
            {
                string flagPart = fileName.Substring(idx + 2);
                foreach (char ch in flagPart)
                {
                    switch (ch)
                    {
                        case 'P': flags.Add(MaildirFlag.Passed); break;
                        case 'R': flags.Add(MaildirFlag.Replied); break;
                        case 'S': flags.Add(MaildirFlag.Seen); break;
                        case 'T': flags.Add(MaildirFlag.Trashed); break;
                        case 'D': flags.Add(MaildirFlag.Draft); break;
                        case 'F': flags.Add(MaildirFlag.Flagged); break;
                        default:
                            // Rust iterates filename BYTES and checks ch.is_ascii_alphanumeric()
                            // -- ASCII only (0-9, A-Z, a-z), stops at the first non-ASCII-
                            // alphanumeric byte. char.IsLetterOrDigit() is Unicode-aware and
                            // returns true for non-ASCII letters (e.g. 'é'), so a suffix like
                            // "2,XéF" continued past 'é' to incorrectly pick up 'F' (Flagged)
                            // where Rust stops at 'é' and never sees the 'F' (PARITY-AUDIT.md;
                            // Boss's own review caught this in the maildir.cs rewrite).
                            if (!((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))) goto flagDone;
                            break;
                    }
                }
            flagDone: ;
            }

            yield return new MaildirMessage
            {
                internal_date = modTime,
                flags = flags,
                contents = bytes,
                path = file,
            };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MaildirFolderIterator : IEnumerable<MaildirFolder>
{
    private readonly string _rootPath;
    private readonly string? _prefix;

    public MaildirFolderIterator(string rootPath, string? prefix)
    {
        _rootPath = rootPath;
        _prefix = prefix;
    }

    // Rust's real algorithm (maildir.rs:112-177), not a flat single-level scan: a
    // depth-first walk using an explicit directory-handle stack + name-segment stack. At
    // EVERY level (root included): a directory named "cur"/"new"/"tmp" is skipped. If a
    // prefix is configured (Maildir++ mode), a directory whose name does NOT start with
    // that prefix is skipped ENTIRELY -- not recursed into, not checked for cur/new, not
    // yielded (`name.strip_prefix(prefix)` returns None, the guard fails). If no prefix
    // is configured (Dovecot LAYOUT=fs), every directory name is used as-is with no
    // filtering, and recursion is unconditional. A directory that qualifies by name is
    // ALWAYS recursed into, whether or not it itself turns out to have valid cur+new --
    // Rust pushes the read_dir handle before attempting MessageIterator::new_, so a
    // maildir folder can be nested inside a non-maildir directory. The previous C#
    // implementation used SearchOption.AllDirectories (recurses everywhere regardless of
    // naming convention) and only used the prefix to STRIP an already-found folder's
    // name, never to decide whether to look at a directory at all -- confirmed wrong via
    // a concrete failing input (PARITY-AUDIT.md: a non-prefixed sibling directory with
    // its own cur/new was wrongly yielded in Maildir++ mode).
    // Rust: FolderIterator::new() does `fs::read_dir(path)?` -- a missing or inaccessible
    // root is a hard error at construction, not an empty result; read failures mid-
    // traversal are likewise `Err`, not silently skipped (`Some(Err(err)) => return
    // Some(Err(err))` in next()). This previously swallowed both cases into a silently
    // empty enumeration (`if (!Directory.Exists(_rootPath)) yield break;` and a try/catch
    // around the read in Walk()), making an inaccessible mailbox indistinguishable from
    // "just empty" (PARITY-AUDIT.md; Boss's own review caught this in the maildir.cs
    // rewrite). Fixed by removing both: a missing/inaccessible root or subdirectory now
    // throws naturally (DirectoryNotFoundException / UnauthorizedAccessException) from
    // Directory.EnumerateFileSystemEntries, the C#-idiomatic equivalent of Rust
    // propagating a real error to the caller instead of yielding nothing.
    public IEnumerator<MaildirFolder> GetEnumerator()
    {
        if (Directory.Exists(_rootPath) && HasCurAndNew(_rootPath))
        {
            yield return new MaildirFolder(_rootPath, null);
        }

        foreach (var folder in Walk(_rootPath, new List<string>()))
        {
            yield return folder;
        }
    }

    private IEnumerable<MaildirFolder> Walk(string dirPath, List<string> nameStack)
    {
        var entries = Directory.EnumerateFileSystemEntries(dirPath);

        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry)) continue; // Rust: path.is_dir()

            string name = Path.GetFileName(entry);
            if (name is "cur" or "new" or "tmp") continue;

            string segment;
            if (_prefix != null)
            {
                if (!name.StartsWith(_prefix, StringComparison.Ordinal)) continue; // Rust: strip_prefix -> None -> skip entirely
                segment = name.Substring(_prefix.Length);
            }
            else
            {
                segment = name;
            }

            nameStack.Add(segment);

            if (HasCurAndNew(entry))
            {
                string sep = _prefix ?? "/";
                yield return new MaildirFolder(entry, string.Join(sep, nameStack));
            }

            foreach (var sub in Walk(entry, nameStack))
            {
                yield return sub;
            }

            nameStack.RemoveAt(nameStack.Count - 1);
        }
    }

    // Rust: MessageIterator::new_ requires BOTH 'cur' and 'new' to exist (each missing
    // one independently returns Err(NotFound)) -- this used to accept either one alone.
    private static bool HasCurAndNew(string path) =>
        Directory.Exists(Path.Combine(path, "cur")) && Directory.Exists(Path.Combine(path, "new"));

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

#if STALWART_PORT_TESTS
[TestClass]
public class maildir_tests
{
    [TestMethod]
    public void parse_maildir()
    {
        string maildirPath = Path.Combine(AppContext.BaseDirectory, "resources", "maildir");
        var it = new MaildirFolderIterator(maildirPath, ".");
        var messages = new List<(string folder, MaildirMessage msg)>();

        var expected_messages = new List<(string folder, MaildirMessage msg)>
        {
            (
                "INBOX",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag> { MaildirFlag.Seen },
                    contents = new byte[] { 98, 10 },
                    path = "unknown",
                }
            ),
            (
                "INBOX",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag> { MaildirFlag.Seen, MaildirFlag.Trashed },
                    contents = new byte[] { 97, 10 },
                    path = "unknown",
                }
            ),
            (
                "My Folder",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag>(),
                    contents = new byte[] { 100, 10 },
                    path = "unknown",
                }
            ),
            (
                "My Folder",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag> { MaildirFlag.Trashed, MaildirFlag.Draft, MaildirFlag.Replied },
                    contents = new byte[] { 99, 10 },
                    path = "unknown",
                }
            ),
            (
                "My Folder.Nested Folder",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag> { MaildirFlag.Replied, MaildirFlag.Draft, MaildirFlag.Flagged },
                    contents = new byte[] { 102, 10 },
                    path = "unknown",
                }
            ),
            (
                "My Folder.Nested Folder",
                new MaildirMessage
                {
                    internal_date = 0,
                    flags = new List<MaildirFlag> { MaildirFlag.Flagged, MaildirFlag.Passed },
                    contents = new byte[] { 101, 10 },
                    path = "unknown",
                }
            ),
        };

        foreach (var folder in it)
        {
            string folderName = folder.name ?? "INBOX";
            foreach (var message in folder)
            {
                Assert.AreNotEqual(0UL, message.internal_date);
                Assert.IsTrue(File.Exists(message.path));
                message.internal_date = 0;
                message.path = "unknown";
                messages.Add((folderName, message));
            }
        }

        messages.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.folder, b.folder);
            if (c != 0) return c;
            return a.msg.CompareTo(b.msg);
        });

        expected_messages.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.folder, b.folder);
            if (c != 0) return c;
            return a.msg.CompareTo(b.msg);
        });

        Assert.AreEqual(expected_messages.Count, messages.Count);
        for (int i = 0; i < expected_messages.Count; i++)
        {
            Assert.AreEqual(expected_messages[i].folder, messages[i].folder, $"Folder mismatch at {i}");
            Assert.AreEqual(expected_messages[i].msg, messages[i].msg, $"Message mismatch at {i}");
        }
    }

    // Regression tests for Phase 2 fixes -- each pins a Rust-verified expected value.

    [TestMethod]
    public void cur_and_new_both_required_matches_rust()
    {
        // Rust: MessageIterator::new_ requires BOTH 'cur' and 'new' to exist. A folder
        // with only 'cur' (no 'new') must be silently skipped, not accepted
        // (PARITY-AUDIT.md FILE 13).
        string root = Path.Combine(Path.GetTempPath(), "maildir_test_" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "cur"));
            Directory.CreateDirectory(Path.Combine(root, "new"));
            Directory.CreateDirectory(Path.Combine(root, ".HalfFolder", "cur")); // no 'new'
            Directory.CreateDirectory(Path.Combine(root, ".FullFolder", "cur"));
            Directory.CreateDirectory(Path.Combine(root, ".FullFolder", "new"));

            var it = new MaildirFolderIterator(root, ".");
            var names = new List<string?>();
            foreach (var folder in it) names.Add(folder.name);

            Assert.IsTrue(names.Contains("FullFolder"));
            Assert.IsFalse(names.Contains("HalfFolder"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void separator_uses_prefix_or_slash_matches_rust()
    {
        // Rust: self.name_stack.join(self.prefix.unwrap_or("/")) -- with no prefix
        // (the documented LAYOUT=fs mode), nested folder names join with '/', not '.'
        // (PARITY-AUDIT.md FILE 13).
        string root = Path.Combine(Path.GetTempPath(), "maildir_test_" + Guid.NewGuid());
        try
        {
            string nested = Path.Combine(root, "Work", "Projects");
            Directory.CreateDirectory(Path.Combine(nested, "cur"));
            Directory.CreateDirectory(Path.Combine(nested, "new"));

            var it = new MaildirFolderIterator(root, null);
            var names = new List<string?>();
            foreach (var folder in it) names.Add(folder.name);

            Assert.IsTrue(names.Contains("Work/Projects"), $"Expected 'Work/Projects', got: {string.Join(", ", names)}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void non_prefixed_sibling_skipped_entirely_in_maildir_plusplus_mode_matches_rust()
    {
        // Rust (maildir.rs:136-152): when a prefix IS configured (Maildir++ mode), a
        // directory name that does NOT start with the prefix causes `strip_prefix` to
        // return None -- the `if let Some(name) = ...` guard fails, so that directory is
        // skipped ENTIRELY: not recursed into, not checked for cur/new, not yielded.
        // C#'s SearchOption.AllDirectories recursive walk finds cur/new anywhere in the
        // tree regardless of naming convention, only using the prefix to STRIP text from
        // an already-found folder's name -- it doesn't use the prefix to decide whether
        // to look at a directory at all. This is the real architectural divergence Phase 1
        // flagged as "needs a concrete failing input" -- this is that input.
        string root = Path.Combine(Path.GetTempPath(), "maildir_test_" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "cur"));
            Directory.CreateDirectory(Path.Combine(root, "new"));
            // A real Maildir++ sibling (dot-prefixed) -- must be found.
            Directory.CreateDirectory(Path.Combine(root, ".Sent", "cur"));
            Directory.CreateDirectory(Path.Combine(root, ".Sent", "new"));
            // A directory with a valid cur/new shape but WITHOUT the "." prefix -- Rust
            // skips this entirely in Maildir++ mode; it is not a Maildir++ sibling.
            Directory.CreateDirectory(Path.Combine(root, "NotPrefixed", "cur"));
            Directory.CreateDirectory(Path.Combine(root, "NotPrefixed", "new"));

            var it = new MaildirFolderIterator(root, ".");
            var names = new List<string?>();
            foreach (var folder in it) names.Add(folder.name);

            Assert.IsTrue(names.Contains("Sent"), $"Expected 'Sent' to be found, got: {string.Join(", ", names)}");
            Assert.IsFalse(names.Contains("NotPrefixed"), $"'NotPrefixed' must be skipped entirely in Maildir++ mode, got: {string.Join(", ", names)}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void missing_root_throws_instead_of_silently_empty_matches_rust()
    {
        // Rust: FolderIterator::new() does fs::read_dir(path)? -- a missing/inaccessible
        // root is a hard error, not an empty result (PARITY-AUDIT.md; Boss's own review
        // caught this in the maildir.cs rewrite -- the root-existence check and a
        // try/catch in Walk() were silently swallowing this into an empty enumeration).
        string missingRoot = Path.Combine(Path.GetTempPath(), "maildir_does_not_exist_" + Guid.NewGuid());
        var it = new MaildirFolderIterator(missingRoot, ".");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
        {
            foreach (var _ in it) { }
        });
    }

    [TestMethod]
    public void flag_parsing_stops_at_first_non_ascii_alphanumeric_byte_matches_rust()
    {
        // Rust iterates filename BYTES and checks ch.is_ascii_alphanumeric() -- ASCII
        // only, stops at the first non-ASCII-alphanumeric byte. char.IsLetterOrDigit() is
        // Unicode-aware and returns true for 'é', so a suffix like "2,XéF" continued past
        // 'é' to incorrectly pick up the trailing 'F' (Flagged), where Rust stops parsing
        // at 'é' and never reaches 'F' (PARITY-AUDIT.md; Boss's own review caught this in
        // the maildir.cs rewrite -- exact case Boss traced by hand).
        string root = Path.Combine(Path.GetTempPath(), "maildir_test_" + Guid.NewGuid());
        try
        {
            string curDir = Path.Combine(root, "cur");
            Directory.CreateDirectory(curDir);
            Directory.CreateDirectory(Path.Combine(root, "new"));
            File.WriteAllBytes(Path.Combine(curDir, "1234567890.host:2,XéF"), new byte[] { 98, 10 });

            var folder = new MaildirFolder(root, null);
            var messages = new List<MaildirMessage>();
            foreach (var msg in folder) messages.Add(msg);

            Assert.AreEqual(1, messages.Count);
            Assert.IsFalse(messages[0].flags.Contains(MaildirFlag.Flagged), $"Flagged must NOT be set -- Rust stops at 'é', never reaches 'F'. Got: {string.Join(",", messages[0].flags)}");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
#endif
