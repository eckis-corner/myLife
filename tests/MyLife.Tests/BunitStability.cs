using System.Runtime.CompilerServices;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;

namespace MyLife.Tests;

/// <summary>
/// Seitentests auch auf langsamen CI-Rechnern stabil: längere Wartezeit für Serveraufrufe und Klicks, die einen
/// zwischenzeitlich neu gezeichneten Knopf noch einmal suchen.
/// </summary>
internal static class BunitStability
{
    /// <summary>Statt der Standard-Sekunde: Auf dem CI-Rechner brauchen Serveraufrufe samt Neuzeichnen länger.</summary>
    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    [ModuleInitializer]
    internal static void Initialize() => BunitContext.DefaultWaitTimeout = WaitTimeout;

    /// <summary>
    /// Klickt auf das Element, sobald es da ist. Zeichnet die Seite zwischen Suchen und Klicken neu (etwa nach einer
    /// Antwort des Servers), gehört der alte Klick-Handler nicht mehr zum Baum; dann wird neu gesucht.
    /// </summary>
    public static void ClickWhenReady<TComponent>(this IRenderedComponent<TComponent> component, string selector)
        where TComponent : IComponent
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                component.WaitForElement(selector).Click();
                return;
            }
            catch (UnknownEventHandlerIdException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Wie <see cref="ClickWhenReady{TComponent}"/>, für Eingaben (Text, Haken).</summary>
    public static void ChangeWhenReady<TComponent>(this IRenderedComponent<TComponent> component, string selector, object value)
        where TComponent : IComponent
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                component.WaitForElement(selector).Change(value);
                return;
            }
            catch (UnknownEventHandlerIdException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }
}
