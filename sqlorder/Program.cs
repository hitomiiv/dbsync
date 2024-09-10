namespace sqlorder;

internal static class Program
{
    public static void Main(string[] args)
    {
        // Read paths from stdin
        var scriptPaths = new List<string>();
        while (Console.ReadLine() is { } path)
        {
            if (path.EndsWith(".sql"))
                scriptPaths.Add(path);
        }

        // Output ordered scripts
        var orderedScripts = ScriptProcessor.OrderScripts(scriptPaths);
        if (args.Length >= 2 && args[1] == "--concat")
        {
            // Print contents
            foreach (var script in orderedScripts)
            {
                Console.WriteLine(script.Contents);
            }
        }
        else
        {
            // Print filenames
            foreach (var script in orderedScripts)
            {
                Console.WriteLine(script.Path);
            }
        }
    }
}