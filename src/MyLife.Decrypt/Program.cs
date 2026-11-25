namespace MyLife.Decrypt;

/// <summary>mylife-decrypt – öffnet JSON-Exporte und Notfallpakete von myLife ohne Server (M3.5).</summary>
internal static class DecryptProgram
{
    public static Task<int> Main(string[] args) =>
        DecryptTool.RunAsync(args, Console.In, Console.Out, Console.Error, Environment.GetEnvironmentVariable("MYLIFE_SECRET"));
}
