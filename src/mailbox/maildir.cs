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
                            if (!char.IsLetterOrDigit(ch)) goto flagDone;
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

    public IEnumerator<MaildirFolder> GetEnumerator()
    {
        yield return new MaildirFolder(_rootPath, null);

        if (!Directory.Exists(_rootPath)) yield break;

        var subdirs = Directory.GetDirectories(_rootPath, "*", SearchOption.AllDirectories);
        foreach (var dir in subdirs)
        {
            string dirName = Path.GetFileName(dir);
            if (dirName is "cur" or "new" or "tmp") continue;

            if (Directory.Exists(Path.Combine(dir, "cur")) || Directory.Exists(Path.Combine(dir, "new")))
            {
                string rel = Path.GetRelativePath(_rootPath, dir);
                if (_prefix != null && rel.StartsWith(_prefix))
                {
                    rel = rel.Substring(_prefix.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                string folderName = rel.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');
                if (folderName.StartsWith(".")) folderName = folderName.Substring(1);
                yield return new MaildirFolder(dir, folderName);
            }
        }
    }

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
}
#endif
