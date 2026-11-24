namespace MyLife.Server.Data;

/// <summary>Registrierter Passkey (WebAuthn) als zweiter Faktor.</summary>
public sealed class Passkey
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public required byte[] CredentialId { get; set; }

    /// <summary>Öffentlicher Schlüssel im COSE-Format.</summary>
    public required byte[] PublicKey { get; set; }

    public uint SignCount { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
