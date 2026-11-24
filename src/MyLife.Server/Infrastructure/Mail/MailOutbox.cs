using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Shared.Vault;

namespace MyLife.Server.Infrastructure.Mail;

/// <summary>Weckt den Versanddienst, sobald eine neue Mail gespeichert ist.</summary>
internal sealed class MailSignal : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Schon geweckt.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _signal.Dispose();
}

/// <summary>
/// Legt Mails in die Outbox (Architektur 6.1). Gespeichert wird mit dem nächsten <c>SaveChanges</c> des
/// Aufrufers, also in derselben Transaktion wie die Änderung, die die Mail auslöst; danach startet der Versand.
/// </summary>
internal sealed class MailOutbox
{
    internal const string ParametersPurpose = "MyLife.Mail.Outbox.v1";
    private readonly MyLifeDbContext _db;
    private readonly IDataProtectionProvider _protection;
    private readonly TimeProvider _time;
    private bool _pending;

    public MailOutbox(MyLifeDbContext db, IDataProtectionProvider protection, TimeProvider time, MailSignal signal)
    {
        _db = db;
        _protection = protection;
        _time = time;
        db.SavedChanges += (_, _) =>
        {
            if (_pending)
            {
                _pending = false;
                signal.Notify();
            }
        };
    }

    /// <summary>Legt eine Mail in die Outbox; liefert ihre ID, etwa um den Versand zu verfolgen.</summary>
    public Guid Enqueue(string recipient, string language, string template, IReadOnlyDictionary<string, string> parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        var id = Guid.CreateVersion7();
        _db.MailOutbox.Add(new MailOutboxEntry
        {
            Id = id,
            Recipient = recipient,
            Language = language,
            Template = template,
            Parameters = _protection.CreateProtector(ParametersPurpose).Protect(json),
            CreatedAt = _time.GetUtcNow(),
            NextAttemptAt = _time.GetUtcNow(),
        });
        _pending = true;
        return id;
    }

    public Guid Enqueue(User user, string template, IReadOnlyDictionary<string, string>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        var all = new Dictionary<string, string>(parameters ?? new Dictionary<string, string>(), StringComparer.Ordinal) { ["name"] = user.DisplayName };
        return Enqueue(user.Email, user.Language, template, all);
    }
}

/// <summary>
/// Verschickt die Outbox im Hintergrund: sofort nach neuen Mails, sonst alle 30 Sekunden. Fehlgeschlagene Mails
/// werden mit wachsendem Abstand wiederholt (1 Minute bis 1 Tag, gut zwei Tage lang) und danach aufgegeben.
/// Ohne SMTP-Einstellungen bleiben Mails liegen, bis der Admin den Versand einrichtet.
/// </summary>
internal sealed partial class MailOutboxService(
    IServiceScopeFactory scopes, IMailTransport transport, MailSignal signal, IDataProtectionProvider protection,
    TimeProvider time, ILogger<MailOutboxService> logger) : BackgroundService
{
    internal const string NotConfigured = "not-configured";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly int[] BackoffMinutes = [1, 5, 15, 30, 60, 120, 240, 480, 720, 1440];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public static int MaxAttempts => BackoffMinutes.Length;

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
            {
                LogProcessingFailed(logger, exception);
            }

            try
            {
                await signal.WaitAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Verschickt alle fälligen Mails einmal; liefert die Zahl der verschickten.</summary>
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var composer = scope.ServiceProvider.GetRequiredService<MailComposer>();
            var now = time.GetUtcNow();
            var due = await db.MailOutbox
                .Where(m => m.SentAt == null && m.FailedAt == null && m.NextAttemptAt <= now)
                .OrderBy(m => m.NextAttemptAt).Take(20).ToListAsync(cancellationToken);
            if (due.Count == 0)
            {
                return 0;
            }

            var config = await composer.ConfigAsync(cancellationToken);
            if (config is null)
            {
                foreach (var entry in due)
                {
                    entry.LastError = NotConfigured;
                    entry.NextAttemptAt = now.AddMinutes(5);
                }

                await db.SaveChangesAsync(cancellationToken);
                return 0;
            }

            var sent = 0;
            foreach (var entry in due)
            {
                try
                {
                    var parameters = await composer.WithInstanceAsync(ReadParameters(entry), cancellationToken);
                    var mail = MailTemplates.Render(entry.Template, entry.Language, parameters);
                    await transport.SendAsync(config, MailComposer.Build(config, entry.Recipient, mail), cancellationToken);
                    entry.SentAt = time.GetUtcNow();
                    entry.Parameters = null;
                    entry.LastError = null;
                    sent++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Failed(db, entry, exception);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return sent;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static string Short(string message) => message.Length > 300 ? message[..300] : message;

    private Dictionary<string, string> ReadParameters(MailOutboxEntry entry)
    {
        if (entry.Parameters is null)
        {
            return [];
        }

        var json = protection.CreateProtector(MailOutbox.ParametersPurpose).Unprotect(entry.Parameters);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    private void Failed(MyLifeDbContext db, MailOutboxEntry entry, Exception exception)
    {
        entry.Attempts++;
        entry.LastError = Short(exception.Message);
        // Ohne Schlüsselring lassen sich die Parameter nie mehr lesen: sofort aufgeben.
        if (entry.Attempts >= MaxAttempts || exception is CryptographicException)
        {
            entry.FailedAt = time.GetUtcNow();
            entry.Parameters = null;
            db.AuditLog.Add(new AuditEntry { At = time.GetUtcNow(), Action = AuditActions.MailFailed, TargetType = "mail", TargetId = entry.Id, Details = entry.Template });
        }
        else
        {
            entry.NextAttemptAt = time.GetUtcNow().AddMinutes(BackoffMinutes[entry.Attempts - 1]);
        }

        LogSendFailed(logger, entry.Id, entry.Attempts, exception.GetType().Name);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail-Outbox konnte nicht verarbeitet werden")]
    private static partial void LogProcessingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail {MailId} nicht verschickt (Versuch {Attempt}, {Error})")]
    private static partial void LogSendFailed(ILogger logger, Guid mailId, int attempt, string error);
}
