namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>Accès aux sorties réelles de yt-dlp enregistrées par <c>tools/record-fixtures.ps1</c>.</summary>
internal static class YtDlpFixtures
{
    public static readonly string[] SuccessNames = ["standard", "short", "long", "unlisted"];

    public static TheoryData<string> Successes()
    {
        var data = new TheoryData<string>();
        foreach (var name in SuccessNames)
        {
            data.Add(name);
        }

        return data;
    }

    public static string Json(string name) => File.ReadAllText(PathOf($"{name}.json"));

    /// <summary>Contenu <c>stderr</c> et code de sortie d'un cas d'échec enregistré.</summary>
    public static (int ExitCode, string StdErr) Error(string name)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(PathOf($"{name}.error.json")));
        return (doc.RootElement.GetProperty("exitCode").GetInt32(), doc.RootElement.GetProperty("stderr").GetString()!);
    }

    private static string PathOf(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ytdlp", file);
}
