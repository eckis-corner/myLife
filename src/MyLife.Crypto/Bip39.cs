using System.Security.Cryptography;

namespace MyLife.Crypto;

/// <summary>
/// Recovery-Key als 24 Wörter nach BIP-39 (englische Standardliste, Entscheidung 50): 256 Bit Zufall plus
/// 8 Bit Prüfsumme aus SHA-256 ergeben 24 × 11 Bit. Beim Einlesen genügen die ersten vier Buchstaben jedes Worts,
/// weil sie in der englischen Liste eindeutig sind.
/// </summary>
public static class Bip39
{
    public const int WordCount = 24;
    public const int EntropyBytes = 32;

    private static readonly Lazy<string[]> WordList = new(LoadWordList);
    private static readonly Lazy<Dictionary<string, int>> ByPrefix = new(() =>
        WordList.Value.Select((word, index) => (Prefix: word.Length > 4 ? word[..4] : word, index))
            .ToDictionary(x => x.Prefix, x => x.index, StringComparer.Ordinal));

    public static IReadOnlyList<string> Words => WordList.Value;

    /// <summary>Wandelt 32 Byte Entropie in 24 Wörter.</summary>
    public static IReadOnlyList<string> Encode(byte[] entropy)
    {
        if (entropy is not { Length: EntropyBytes })
        {
            throw new ArgumentException("Es werden genau 32 Byte erwartet.", nameof(entropy));
        }

        byte[] bits = [.. entropy, SHA256.HashData(entropy)[0]];
        var words = new string[WordCount];
        for (var i = 0; i < WordCount; i++)
        {
            words[i] = WordList.Value[ReadBits(bits, i * 11, 11)];
        }

        return words;
    }

    /// <summary>
    /// Liest 24 Wörter (durch Leerzeichen, Zeilenumbrüche oder Kommas getrennt, Groß- und Kleinschreibung egal,
    /// vier Buchstaben genügen) und prüft die Prüfsumme.
    /// </summary>
    public static bool TryDecode(string? phrase, out byte[] entropy)
    {
        entropy = [];
        var words = (phrase ?? string.Empty).Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != WordCount)
        {
            return false;
        }

        var bits = new byte[EntropyBytes + 1];
        for (var i = 0; i < WordCount; i++)
        {
            var index = IndexOf(words[i]);
            if (index < 0)
            {
                return false;
            }

            WriteBits(bits, i * 11, 11, index);
        }

        var candidate = bits[..EntropyBytes];
        if (SHA256.HashData(candidate)[0] != bits[EntropyBytes])
        {
            return false;
        }

        entropy = candidate;
        return true;
    }

    /// <summary>Index eines Worts oder seiner ersten vier Buchstaben, sonst -1.</summary>
    public static int IndexOf(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var normalized = word.Trim().ToLowerInvariant();
        if (normalized.Length < 3)
        {
            return -1;
        }

        var prefix = normalized.Length > 4 ? normalized[..4] : normalized;
        if (!ByPrefix.Value.TryGetValue(prefix, out var index))
        {
            return -1;
        }

        // Längere Eingaben müssen zum vollständigen Wort passen, damit Tippfehler nach dem vierten Buchstaben auffallen.
        return WordList.Value[index].StartsWith(normalized, StringComparison.Ordinal) ? index : -1;
    }

    private static int ReadBits(byte[] data, int offset, int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            var bit = offset + i;
            value = (value << 1) | ((data[bit / 8] >> (7 - (bit % 8))) & 1);
        }

        return value;
    }

    private static void WriteBits(byte[] data, int offset, int count, int value)
    {
        for (var i = 0; i < count; i++)
        {
            var bit = offset + i;
            if (((value >> (count - 1 - i)) & 1) == 1)
            {
                data[bit / 8] |= (byte)(1 << (7 - (bit % 8)));
            }
        }
    }

    private static string[] LoadWordList()
    {
        using var stream = typeof(Bip39).Assembly.GetManifestResourceStream("MyLife.Crypto.bip39-english.txt")
                           ?? throw new InvalidOperationException("BIP-39-Wortliste fehlt.");
        using var reader = new StreamReader(stream);
        var words = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 2048 ? words : throw new InvalidOperationException("BIP-39-Wortliste ist unvollständig.");
    }
}
