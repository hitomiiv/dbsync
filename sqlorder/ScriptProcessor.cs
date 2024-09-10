using System.Text.RegularExpressions;

namespace sqlorder;

public static partial class ScriptProcessor
{
    public static IEnumerable<Script> OrderScripts(IEnumerable<string> paths)
    {
        return OrderScripts(paths.Select(ScriptFromPath));
    }

    public static IEnumerable<Script> OrderScripts(IEnumerable<Script> scripts)
    {
        var scriptList = scripts.ToList();

        // Misc scripts (tables, types, etc.)
        var miscScripts = scriptList.Where(IsOtherScript).ToList();
        var orderedMiscScripts = SortNonMigrationScripts(miscScripts);

        // Migration scripts
        var orderedMigrationScripts = scriptList
            .Where(IsMigrationScript)
            .OrderBy(s => s.Path)
            .ToList();

        // Function and procedure scripts
        var functionAndProcedureScripts = scriptList.Where(IsFunctionOrProcedureScript).ToList();
        var orderedFunctionAndProcedureScripts = SortNonMigrationScripts(functionAndProcedureScripts);

        return orderedMiscScripts
            .Concat(orderedMigrationScripts)
            .Concat(orderedFunctionAndProcedureScripts);
    }

    private static List<Script> SortNonMigrationScripts(List<Script> scripts)
    {
        // Compare script s1 with every other script s2
        // If s1 contains any mentions of s2, add s2 as a dependency
        var dependencies = new Dictionary<Script, HashSet<Script>>();
        foreach (var s1 in scripts)
        {
            foreach (var s2 in scripts.Where(s2 => s1 != s2 && s1.Contents.Contains(ScriptName(s2))))
            {
                // Throw if dependency is cyclic
                if (dependencies.TryGetValue(s2, out var s2deps)
                    && s2deps.Select(s => s.Path).Contains(s1.Path))
                {
                    throw new Exception($"Cyclic dependency detected between {s1.Path} and {s2.Path}");
                }

                // Add s2 as a dependency
                if (dependencies.TryGetValue(s1, out var s1deps))
                {
                    s1deps.Add(s2);
                }
                else
                {
                    dependencies[s1] = [s2];
                }
            }
        }

        // Order by dependencies (visit each node post-order)
        var orderedScripts = new List<Script>();
        var scriptsToVisit = new Stack<Script>(scripts);
        var visited = new HashSet<string>();
        while (scriptsToVisit.Count > 0)
        {
            var s = scriptsToVisit.Peek();

            // If unvisited dependencies exist, add them to the stack
            if (dependencies.TryGetValue(s, out var deps))
            {
                var depsNotVisited = deps.Where(d => !visited.Contains(d.Path)).ToList();
                if (depsNotVisited.Count > 0)
                {
                    foreach (var dep in depsNotVisited)
                    {
                        scriptsToVisit.Push(dep);
                    }

                    continue;
                }
            }

            // Leaf node or all dependencies visited
            if (!visited.Contains(s.Path))
            {
                orderedScripts.Add(s);
                visited.Add(s.Path);
            }

            scriptsToVisit.Pop();
        }

        return orderedScripts;
    }

    private static bool IsOtherScript(Script script)
    {
        return !IsMigrationScript(script) && !IsFunctionOrProcedureScript(script);
    }

    private static bool IsFunctionOrProcedureScript(Script script)
    {
        return !IsMigrationScript(script)
               && (FunctionScript().IsMatch(script.Contents)
                   || ProcedureScript().IsMatch(script.Contents));
    }

    private static bool IsMigrationScript(Script script)
    {
        return MigrationScript().IsMatch(ScriptName(script));
    }

    private static string ScriptName(Script script)
    {
        return Path.GetFileNameWithoutExtension(script.Path);
    }

    private static Script ScriptFromPath(string path)
    {
        var contents = File.ReadAllText(path);
        return new Script(Path.GetFileName(path), contents);
    }

    [GeneratedRegex(@"^\d+.*$")]
    private static partial Regex MigrationScript();

    [GeneratedRegex(@"create|alter|replace\s+function\s+$")]
    private static partial Regex FunctionScript();

    [GeneratedRegex(@"create|alter|replace\s+procedure\s+$")]
    private static partial Regex ProcedureScript();
}