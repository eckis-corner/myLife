namespace MyLife.Client.Help;

/// <summary>Ein Kapitel des Tutorials: ein Schritt auf dem Weg von der Einrichtung bis zum fertigen Notfallordner.</summary>
/// <param name="Id">Kurzname für den Einsprung über <c>tutorial?chapter=…</c>; in allen Sprachen gleich.</param>
/// <param name="Intro">Der Kernsatz des Kapitels, in eingeschränktem Markdown (<see cref="HelpMarkdown"/>).</param>
/// <param name="Text">Der Rest des Kapitels, in eingeschränktem Markdown.</param>
/// <param name="Topics">Kurznamen der Themen der Anleitung, in denen es ausführlicher steht.</param>
public sealed record TutorialChapter(string Id, string Title, string Icon, string Intro, string Text, string[] Topics);

/// <summary>Ein Thema der Anleitung: ein Ablauf, so wie man ihn in myLife durchgeht.</summary>
/// <param name="Id">Kurzname für den Einsprung über <c>guide?topic=…</c>; in allen Sprachen gleich.</param>
/// <param name="Who">Wer den Ablauf ausführt, in Worten.</param>
/// <param name="Intro">Worum es geht, in eingeschränktem Markdown.</param>
/// <param name="Steps">Die Schritte in ihrer Reihenfolge, je einer in eingeschränktem Markdown.</param>
/// <param name="Notes">Was überraschen kann, in eingeschränktem Markdown.</param>
/// <param name="Page">Die Seite, auf der der Ablauf beginnt, relativ zur Basisadresse (ohne führenden Schrägstrich).</param>
/// <param name="PageName">Wie diese Seite im Menü heißt.</param>
public sealed record GuideTopic(
    string Id,
    string Title,
    string Icon,
    string Who,
    string Intro,
    string[] Steps,
    string[] Notes,
    string? Page = null,
    string? PageName = null);

/// <summary>Ein Teil der Anleitung mit seinen Themen.</summary>
/// <param name="Id">Kurzname des Teils; in allen Sprachen gleich.</param>
public sealed record GuidePart(string Id, string Title, string Description, GuideTopic[] Topics);

/// <summary>Tutorial und Anleitung in einer Sprache.</summary>
public sealed record HelpTexts(TutorialChapter[] Tutorial, GuidePart[] Guide);
