namespace sqlorder;

internal static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            if (args.Length >= 1 && args[0] == "--concat")
            {
                // Print ordered script contents
                var scriptPaths = GetScriptPaths();
                var orderedScripts = ScriptProcessor.OrderScripts(scriptPaths);
                foreach (var script in orderedScripts)
                {
                    Console.WriteLine(script.Contents);
                }
            }
            else if (args.Length == 0)
            {
                // Print ordered script paths
                var scriptPaths = GetScriptPaths();
                var orderedScripts = ScriptProcessor.OrderScripts(scriptPaths);
                foreach (var script in orderedScripts)
                {
                    Console.WriteLine(script.Path);
                }
            }
            else
            {
                // Print usage
                Console.WriteLine("Usage: sqlorder [--concat]");
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            Environment.Exit(1);
        }
    }

    private static List<string> GetScriptPaths()
    {
        // Read paths from stdin
        var scriptPaths = new List<string>();
        while (Console.ReadLine() is { } path)
        {
            scriptPaths.Add(path);
        }

        return scriptPaths;
    }
}