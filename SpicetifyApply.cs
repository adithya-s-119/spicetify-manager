using System;
using System.Diagnostics;
using System.IO;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.Title = "Spicetify - backup apply";

        string spicetify = FindSpicetify();
        if (spicetify == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: spicetify.exe was not found.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Looked in:");
            Console.WriteLine("  - your PATH");
            Console.WriteLine("  - %LOCALAPPDATA%\\spicetify\\spicetify.exe");
            Console.WriteLine("  - %APPDATA%\\spicetify\\spicetify.exe");
            Console.WriteLine();
            Console.WriteLine("Install it from https://spicetify.app and try again.");
            return Pause(1, args);
        }

        string arguments = JoinArgs(SplitControlFlags(args));
        if (arguments.Length == 0)
        {
            arguments = "backup apply";
        }

        Console.WriteLine("spicetify " + arguments);
        Console.WriteLine(new string('-', 60));

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(spicetify, arguments);
            startInfo.UseShellExecute = false;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: could not start spicetify.");
                    Console.ResetColor();
                    return Pause(1, args);
                }

                process.WaitForExit();

                Console.WriteLine(new string('-', 60));
                if (process.ExitCode == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (arguments == "backup apply")
                    {
                        Console.WriteLine("Done. Spotify theme applied successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Done. spicetify " + arguments + " completed successfully.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("spicetify exited with code " + process.ExitCode + ".");
                    Console.WriteLine("Scroll up to see the error message.");
                }

                Console.ResetColor();
                return Pause(process.ExitCode, args);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + ex.Message);
            Console.ResetColor();
            return Pause(1, args);
        }
    }

    private static int Pause(int exitCode, string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.Equals("--no-pause", StringComparison.OrdinalIgnoreCase))
            {
                return exitCode;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to close this window...");
        try
        {
            Console.ReadKey(true);
        }
        catch (InvalidOperationException)
        {
            // No console input available (e.g. piped output); just exit.
        }

        return exitCode;
    }

    private static string[] SplitControlFlags(string[] args)
    {
        System.Collections.Generic.List<string> forwarded = new System.Collections.Generic.List<string>();
        foreach (string arg in args)
        {
            // Consumed by this launcher, not passed on to spicetify.
            if (arg.Equals("--no-pause", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            forwarded.Add(arg);
        }

        return forwarded.ToArray();
    }

    private static string JoinArgs(string[] args)
    {
        string[] quoted = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            quoted[i] = args[i].IndexOf(' ') >= 0 ? "\"" + args[i] + "\"" : args[i];
        }

        return string.Join(" ", quoted);
    }

    private static string FindSpicetify()
    {
        string fromPath = SearchPath("spicetify.exe");
        if (fromPath != null)
        {
            return fromPath;
        }

        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "spicetify",
            "spicetify.exe");
        if (File.Exists(local))
        {
            return local;
        }

        string roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "spicetify",
            "spicetify.exe");
        if (File.Exists(roaming))
        {
            return roaming;
        }

        return null;
    }

    private static string SearchPath(string fileName)
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string dir in path.Split(Path.PathSeparator))
        {
            if (dir.Trim().Length == 0)
            {
                continue;
            }

            try
            {
                string candidate = Path.Combine(dir.Trim().Trim('"'), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Skip malformed PATH entries.
            }
        }

        return null;
    }
}
