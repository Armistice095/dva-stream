using System.Globalization;
using System.Text;

// Modes : echo <args...> | stderr <texte...> | exit <code> | sleep <ms> | flood <lignes> | unicode | cwd | env <nom> | replay <out> <err|-> <code>
// Sorties en UTF-8 sur tous les OS (sinon Windows utilise la page de code console pour stderr et stdout).
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });

var mode = args.Length > 0 ? args[0] : string.Empty;

switch (mode)
{
    case "echo":
        Console.Out.Write(string.Join("|", args.Skip(1)));
        return 0;

    case "stderr":
        Console.Error.Write(string.Join(" ", args.Skip(1)));
        return 0;

    case "exit":
        return int.Parse(args[1], CultureInfo.InvariantCulture);

    case "sleep":
        await Task.Delay(int.Parse(args[1], CultureInfo.InvariantCulture));
        return 0;

    case "flood":
    {
        // Beaucoup de texte sur les deux sorties : un lecteur synchrone se bloquerait ici.
        var count = int.Parse(args[1], CultureInfo.InvariantCulture);
        var line = new string('x', 99);
        for (var i = 0; i < count; i++)
        {
            Console.Out.WriteLine(line);
            Console.Error.WriteLine(line);
        }

        return 0;
    }

    case "unicode":
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.Out.Write("é✓日本");
        return 0;

    case "env":
        Console.Out.Write(Environment.GetEnvironmentVariable(args[1]) ?? "<absente>");
        return 0;

    case "replay":
    {
        // replay <fichier stdout> <fichier stderr ou -> <code> : rejoue une sortie enregistrée (fixtures yt-dlp).
        Console.Out.Write(File.ReadAllText(args[1]));
        if (args[2] != "-")
        {
            Console.Error.Write(File.ReadAllText(args[2]));
        }

        return int.Parse(args[3], CultureInfo.InvariantCulture);
    }

    case "cwd":
        Console.Out.Write(Directory.GetCurrentDirectory());
        return 0;

    default:
        Console.Error.WriteLine("mode inconnu : " + mode);
        return 64;
}
