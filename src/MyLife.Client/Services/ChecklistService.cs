using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Localization;
using MyLife.Client.Resources;
using MyLife.Shared.Checklist;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/checklist</c>: wer welche Aufgabe abgehakt hat (M3.2).</summary>
public sealed class ChecklistClient(HttpClient http)
{
    public async Task<IReadOnlyList<ChecklistProgressDto>> GetAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/checklist/{ownerId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChecklistProgressDto>>(cancellationToken) ?? [];
    }

    public async Task<ChecklistProgressDto> DoneAsync(Guid ownerId, Guid taskId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/checklist/{ownerId}/tasks/{taskId}") { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChecklistProgressDto>(cancellationToken))!;
    }

    public async Task UndoneAsync(Guid ownerId, Guid taskId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/checklist/{ownerId}/tasks/{taskId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Checkliste (Bereich 12, M3.2): Aufgaben nach Phasen, die Regel für verknüpfte Einträge (Bereich und Wunsch,
/// etwa alle Verträge mit „kündigen“) und das Übernehmen der Vorlage für Deutschland in den eigenen Tresor.
/// </summary>
public sealed class ChecklistService(VaultService vault, IStringLocalizer<SharedResource> l)
{
    public static IReadOnlyList<string> Phases => ObjectCatalog.OptionSets["checklistPhase"];

    public static bool IsTask(VaultItem item) => item?.Type.Key == "task";

    /// <summary>Phase einer Aufgabe; ohne Angabe „später“.</summary>
    public static string PhaseOf(VaultItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var phase = task.Payload.Get("checklistPhase");
        return Phases.Contains(phase) ? phase : "later";
    }

    /// <summary>Bereich aus der Regel der Aufgabe, falls gesetzt.</summary>
    public static int? LinkedArea(VaultItem task) => ObjectCatalog.AreaByKey(task?.Payload.Get("linkedArea"));

    public static string? LinkedWish(VaultItem task)
    {
        var wish = task?.Payload.Get("linkedWish");
        return string.IsNullOrEmpty(wish) ? null : wish;
    }

    /// <summary>
    /// Verknüpfte Einträge: alle aktiven Einträge im Bereich der Regel (mit passendem Wunsch, wenn einer gesetzt ist),
    /// dazu die einzeln verknüpften. Gesucht wird nur unter <paramref name="items"/>; was jemand nicht sehen darf, fehlt.
    /// </summary>
    public static IReadOnlyList<VaultItem> Linked(VaultItem task, IEnumerable<VaultItem> items)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(items);
        var area = LinkedArea(task);
        var wish = LinkedWish(task);
        var explicitLinks = task.Payload.Links.ToHashSet();
        return
        [
            .. items.Where(i => !i.IsDeleted && i.Status == ObjectStatus.Active && !IsTask(i)
                    && ((area is { } a && i.Area == a && (wish is null || i.Payload.Wish == wish)) || explicitLinks.Contains(i.Id)))
                .OrderBy(i => i.Area).ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    /// <summary>Aufgaben in der Reihenfolge der Phasen, innerhalb einer Phase in der Reihenfolge des Anlegens.</summary>
    public static IEnumerable<IGrouping<string, VaultItem>> ByPhase(IEnumerable<VaultItem> tasks) =>
        tasks.Where(t => !t.IsDeleted && IsTask(t))
            .OrderBy(t => Phases.ToList().IndexOf(PhaseOf(t))).ThenBy(t => t.CreatedAt).ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
            .GroupBy(PhaseOf);

    /// <summary>Übernimmt die Vorlage für Deutschland als Aufgaben in den eigenen Tresor (in der Sprache der Oberfläche).</summary>
    public async Task<int> AdoptTemplateAsync()
    {
        await vault.EnsureLoadedAsync();
        var created = 0;
        foreach (var template in ChecklistTemplate.Tasks)
        {
            var payload = new VaultPayload { Title = l[$"ChecklistTemplate_{template.Id}_title"] };
            payload.Fields["checklistPhase"] = template.Phase;
            payload.Fields["taskText"] = l[$"ChecklistTemplate_{template.Id}_text"];
            if (template.LinkedArea is { } area)
            {
                payload.Fields["linkedArea"] = area;
            }

            if (template.LinkedWish is { } wish)
            {
                payload.Fields["linkedWish"] = wish;
            }

            await vault.CreateAsync("task", payload);
            created++;
        }

        return created;
    }
}
