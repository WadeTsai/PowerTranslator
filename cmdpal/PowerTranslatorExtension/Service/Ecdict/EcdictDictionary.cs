using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PowerTranslatorExtension.Service.Ecdict;

/// <summary>One ECDICT record (English headword with Traditional Chinese translations and English definitions).</summary>
public sealed class EcdictEntry
{
    public string Word { get; init; } = string.Empty;
    public string Phonetic { get; init; } = string.Empty;
    public IReadOnlyList<string> Translations { get; init; } = [];
    /// <summary>English (WordNet) definitions such as <c>n. an expression of greeting</c>; empty for older data files.</summary>
    public IReadOnlyList<string> Definitions { get; init; } = [];
    /// <summary>Raw ECDICT exchange field, e.g. <c>p:went/d:gone/i:going/3:goes</c> or <c>0:go/1:p</c>.</summary>
    public string Exchange { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    /// <summary>Corpus frequency rank; 0 when unknown. Lower is more common.</summary>
    public int Frequency { get; init; }

    /// <summary>The base form (<c>0:</c> exchange entry) when this word is an inflection, otherwise null.</summary>
    public string? Lemma => ExchangeValue('0') is { Length: > 0 } lemma
        && !string.Equals(lemma, Word, StringComparison.OrdinalIgnoreCase) ? lemma : null;

    /// <summary>The inflection types of this word relative to its lemma (<c>1:</c> exchange entry), e.g. "pd".</summary>
    public string InflectionTypes => ExchangeValue('1') ?? string.Empty;

    public string Summary => string.Join("；", Translations);

    private string? ExchangeValue(char type)
    {
        foreach (var part in Exchange.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length > 2 && part[0] == type && part[1] == ':')
                return part[2..];
        }
        return null;
    }
}

/// <summary>
/// Read-only access to the bundled ECDICT index produced by <c>tools/ecdict/build_ecdict.py</c>.
/// Records are sorted by key, so lookups binary-search the file with positional reads instead
/// of loading the ~60 MB dictionary into memory. All members are thread-safe.
/// </summary>
public sealed partial class EcdictDictionary : IDisposable
{
    public const string FileName = "ecdict.bin";
    private static readonly byte[] Magic = "PTECD001"u8.ToArray();
    private const int HeaderSize = 12;
    private const char FieldSeparator = '\x1f';

    private static readonly Lazy<EcdictDictionary?> shared = new(() => TryOpen(DefaultPath));

    private readonly SafeFileHandle handle;
    private readonly long dataStart;

    public int Count { get; }

    /// <summary>The dictionary shipped next to the executable, or null when the data file is missing.</summary>
    public static EcdictDictionary? Shared => shared.Value;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", FileName);

    private EcdictDictionary(SafeFileHandle handle, int count)
    {
        this.handle = handle;
        Count = count;
        dataStart = HeaderSize + 4L * (count + 1);
    }

    public static EcdictDictionary Open(string path)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        try
        {
            Span<byte> header = stackalloc byte[HeaderSize];
            if (RandomAccess.Read(handle, header, 0) != HeaderSize || !header[..8].SequenceEqual(Magic))
                throw new InvalidDataException($"{path} is not a PowerTranslator ECDICT file");
            var count = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
            if (count < 0 || HeaderSize + 4L * (count + 1) > RandomAccess.GetLength(handle))
                throw new InvalidDataException($"{path} is truncated");
            return new EcdictDictionary(handle, count);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public static EcdictDictionary? TryOpen(string path)
    {
        try
        {
            return File.Exists(path) ? Open(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Lower-cases ASCII letters and collapses whitespace, matching the key built by the script.</summary>
    public static string NormalizeKey(string word)
    {
        var sb = new StringBuilder(word.Length);
        var pendingSpace = false;
        foreach (var c in word.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c is >= 'A' and <= 'Z' ? (char)(c + 32) : c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// All entries whose key equals the normalized word; the entry with the exact spelling comes first,
    /// followed by the lower-case spelling, e.g. "US" returns US (美國) before us (我們).
    /// </summary>
    public IReadOnlyList<EcdictEntry> Lookup(string word)
    {
        var key = NormalizeKey(word);
        if (key.Length == 0)
            return [];
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var res = new List<EcdictEntry>();
        for (var i = LowerBound(keyBytes); i < Count; i++)
        {
            var fields = ReadFields(i);
            if (!string.Equals(fields[0], key, StringComparison.Ordinal))
                break;
            res.Add(ToEntry(fields));
        }
        var trimmed = word.Trim();
        return res
            .OrderBy(e => string.Equals(e.Word, trimmed, StringComparison.Ordinal) ? 0
                : string.Equals(e.Word, key, StringComparison.Ordinal) ? 1 : 2)
            .ToList();
    }

    public EcdictEntry? Find(string word) => Lookup(word) is { Count: > 0 } entries ? entries[0] : null;

    /// <summary>
    /// Words starting with <paramref name="prefix"/>, most frequent first.
    /// Only the first <paramref name="scanLimit"/> matching records are ranked to keep the lookup cheap.
    /// </summary>
    public IReadOnlyList<EcdictEntry> Suggest(string prefix, int limit = 5, int scanLimit = 4000)
    {
        var key = NormalizeKey(prefix);
        if (key.Length == 0 || limit <= 0)
            return [];
        var candidates = new List<EcdictEntry>();
        for (var i = LowerBound(Encoding.UTF8.GetBytes(key)); i < Count && candidates.Count < scanLimit; i++)
        {
            var fields = ReadFields(i);
            if (!fields[0].StartsWith(key, StringComparison.Ordinal))
                break;
            candidates.Add(ToEntry(fields));
        }
        return candidates
            .OrderBy(e => e.Frequency > 0 ? e.Frequency : int.MaxValue)
            .ThenBy(e => e.Word.Length)
            .ThenBy(e => e.Word, StringComparer.Ordinal)
            .DistinctBy(e => NormalizeKey(e.Word))
            .Take(limit)
            .ToList();
    }

    private int LowerBound(byte[] key)
    {
        int lo = 0, hi = Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (CompareKey(mid, key) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private int CompareKey(int index, byte[] key)
    {
        var record = ReadRecord(index);
        var end = record.AsSpan().IndexOf((byte)FieldSeparator);
        var recordKey = end < 0 ? record.AsSpan() : record.AsSpan(0, end);
        return recordKey.SequenceCompareTo(key);
    }

    private byte[] ReadRecord(int index)
    {
        Span<byte> offsets = stackalloc byte[8];
        RandomAccess.Read(handle, offsets, HeaderSize + (4L * index));
        var start = BinaryPrimitives.ReadUInt32LittleEndian(offsets);
        var end = BinaryPrimitives.ReadUInt32LittleEndian(offsets[4..]);
        var buffer = new byte[end - start];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = RandomAccess.Read(handle, buffer.AsSpan(read), dataStart + start + read);
            if (n <= 0)
                throw new InvalidDataException("ECDICT file is truncated");
            read += n;
        }
        return buffer;
    }

    private string[] ReadFields(int index)
    {
        var fields = Encoding.UTF8.GetString(ReadRecord(index)).Split(FieldSeparator);
        if (fields.Length < 8)
            Array.Resize(ref fields, 8);
        for (var i = 0; i < fields.Length; i++)
            fields[i] ??= string.Empty;
        return fields;
    }

    private static EcdictEntry ToEntry(string[] fields)
    {
        return new EcdictEntry
        {
            Word = fields[1],
            Phonetic = fields[2],
            Translations = fields[3]
                .Split("\\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Exchange = fields[4],
            Tag = fields[5],
            Frequency = int.TryParse(fields[6], out var frq) ? frq : 0,
            Definitions = fields[7]
                .Split("\\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };
    }

    public void Dispose()
    {
        handle.Dispose();
    }
}
