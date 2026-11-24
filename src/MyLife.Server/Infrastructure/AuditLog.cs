using System.Net;
using System.Net.Sockets;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure;

/// <summary>
/// Schreibt Einträge ins Audit-Log (Architektur 9). Nie mit Inhalten: nur Aktion, Ziel-ID, Zeit und gekürzte IP.
/// Die Einträge werden mit dem nächsten <c>SaveChanges</c> des Aufrufers gespeichert.
/// </summary>
internal sealed class AuditLog(MyLifeDbContext db, TimeProvider time, IHttpContextAccessor http)
{
    public void Add(string action, Guid? actorId, Guid? ownerId, string? targetType = null, Guid? targetId = null, string? details = null) =>
        db.AuditLog.Add(Entry(action, actorId, ownerId, time.GetUtcNow(), http.HttpContext?.Connection.RemoteIpAddress, targetType, targetId, details));

    public static AuditEntry Entry(
        string action, Guid? actorId, Guid? ownerId, DateTimeOffset at, IPAddress? ip, string? targetType = null, Guid? targetId = null, string? details = null) =>
        new()
        {
            At = at,
            Action = action,
            ActorId = actorId,
            OwnerId = ownerId,
            TargetType = targetType,
            TargetId = targetId,
            Ip = Shorten(ip),
            Details = details is { Length: > 500 } ? details[..500] : details,
        };

    /// <summary>Eigener Eintrag für den eigenen Tresor (Akteur und Eigentümer gleich).</summary>
    public void Own(string action, Guid userId, string? targetType = null, Guid? targetId = null, string? details = null) =>
        Add(action, userId, userId, targetType, targetId, details);

    /// <summary>IPv4 auf /24, IPv6 auf /48 gekürzt (Datenschutz, Architektur 9).</summary>
    public static string? Shorten(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return new IPAddress(bytes) + "/24";
        }

        Array.Clear(bytes, 6, bytes.Length - 6);
        return new IPAddress(bytes) + "/48";
    }
}
