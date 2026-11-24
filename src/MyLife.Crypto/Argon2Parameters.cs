using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace MyLife.Crypto;

/// <summary>
/// Parameter für Argon2id. Sie werden mit dem Nutzer gespeichert, damit sie später angehoben werden können.
/// Die Untergrenzen schützen davor, dass ein manipulierter Server schwache Parameter unterschiebt;
/// die Obergrenzen schützen den Browser vor Überlast.
/// </summary>
public sealed record Argon2Parameters(int MemoryKiB, int Iterations, int Parallelism)
{
    public const int MinMemoryKiB = 65536;
    public const int MaxMemoryKiB = 1048576;
    public const int MinIterations = 3;
    public const int MaxIterations = 20;
    public const int MinParallelism = 1;
    public const int MaxParallelism = 8;

    /// <summary>Standard laut Architektur 4.1: m = 64 MiB, t = 3, p = 1.</summary>
    public static Argon2Parameters Default { get; } = new(65536, 3, 1);

    [JsonIgnore]
    public bool IsValid =>
        MemoryKiB is >= MinMemoryKiB and <= MaxMemoryKiB
        && Iterations is >= MinIterations and <= MaxIterations
        && Parallelism is >= MinParallelism and <= MaxParallelism;

    internal void EnsureValid()
    {
        if (!IsValid)
        {
            throw new CryptographicException("mylife:invalid-argon2-parameters");
        }
    }
}
