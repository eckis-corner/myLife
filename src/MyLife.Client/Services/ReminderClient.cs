using System.Net.Http.Json;
using MyLife.Shared.Reminders;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/reminders</c> (M3.1).</summary>
public sealed class ReminderClient(HttpClient http)
{
    public async Task<ReminderStatusDto> GetAsync(CancellationToken cancellationToken = default) =>
        (await http.GetFromJsonAsync<ReminderStatusDto>("api/reminders", cancellationToken))!;

    public async Task<ReminderStatusDto> SaveSettingsAsync(ReminderSettingsDto settings, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync("api/reminders/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReminderStatusDto>(cancellationToken))!;
    }

    public async Task SetNextDueAsync(DateOnly? nextDueOn, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync("api/reminders/next", new NextDueRequest(nextDueOn), cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Erinnerungen im Browser (M3.1): berechnet aus Ablaufdaten (mit Vorlauf) und Wiedervorlagen den nächsten Termin
/// und meldet dem Server nur dieses Datum, ohne Zusammenhang (Entscheidung 39b). Dazu die Prüffälligkeit der Bereiche
/// für die Übersicht.
/// </summary>
public sealed class ReminderSync(ReminderClient api, VaultService vault, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private ReminderStatusDto? _status;
    private bool _reported;

    public ReminderSettingsDto Settings => _status?.Settings ?? ReminderSettingsDto.Default;

    /// <summary>Das Datum, das der Server zuletzt kennt.</summary>
    public DateOnly? ServerNextDueOn => _status?.NextDueOn;

    public async Task<ReminderSettingsDto> LoadAsync()
    {
        _status ??= await api.GetAsync();
        return _status.Settings;
    }

    public async Task SaveAsync(ReminderSettingsDto settings)
    {
        _status = await api.SaveSettingsAsync(settings);
        _reported = false;
        await SyncAsync();
    }

    /// <summary>Meldet den nächsten Termin, wenn er sich gegenüber dem Stand des Servers geändert hat.</summary>
    public async Task SyncAsync()
    {
        if (!vault.IsLoaded)
        {
            return;
        }

        await LoadAsync();
        var next = NextDue(vault.Current, Settings.LeadDays, DateOnly.FromDateTime(_time.GetLocalNow().DateTime));
        if (_reported && next == _status!.NextDueOn)
        {
            return;
        }

        if (next != _status!.NextDueOn)
        {
            await api.SetNextDueAsync(next);
            _status = _status with { NextDueOn = next };
        }

        _reported = true;
    }

    /// <summary>
    /// Frühester künftiger Termin (heute eingeschlossen) aktiver Einträge: Ablaufdatum minus Vorlauf oder Wiedervorlage.
    /// Was schon im Vorlauf liegt, steht auf der Übersicht und braucht keine Mail mehr.
    /// </summary>
    public static DateOnly? NextDue(IEnumerable<VaultItem> items, int leadDays, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(i => !i.IsDeleted && i.Status == MyLife.Shared.Vault.ObjectStatus.Active)
            .SelectMany(i => new[] { i.Payload.ExpiresOn?.AddDays(-leadDays), i.Payload.FollowUpOn })
            .Where(d => d is { } date && date >= today)
            .Min();
    }

    /// <summary>Bereiche mit aktiven Einträgen, die länger als das Prüfintervall nicht geprüft wurden.</summary>
    public IReadOnlySet<int> DueAreas(DateTimeOffset now)
    {
        if (Settings.ReviewMonths <= 0 || !vault.IsLoaded)
        {
            return new HashSet<int>();
        }

        var limit = now.AddMonths(-Settings.ReviewMonths);
        var reviewed = vault.Areas.ToDictionary(a => a.Area, a => a.LastReviewedAt);
        return vault.Current.Where(i => i.Status == MyLife.Shared.Vault.ObjectStatus.Active).GroupBy(i => i.Area)
            .Where(g => (reviewed.GetValueOrDefault(g.Key) ?? g.Min(i => i.CreatedAt)) < limit)
            .Select(g => g.Key).ToHashSet();
    }
}
