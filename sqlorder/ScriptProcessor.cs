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
        // Separate migrations
        var scriptList = scripts.ToList();
        var migrationScripts = scriptList
            .Where(s => MigrationScript().IsMatch(s.Name))
            .OrderBy(s => s.Name)
            .ToList();
        var otherScripts = scriptList.Where(s => !MigrationScript().IsMatch(s.Name)).ToList();

        var orderedScripts = SortNonMigrationScripts(otherScripts);

        return migrationScripts.Concat(orderedScripts);
    }

    private static List<Script> SortNonMigrationScripts(List<Script> scripts)
    {
        // Compare script s1 with every other script s2
        // If s1 contains any mentions of s2, add s2 as a dependency
        var dependencies = new Dictionary<Script, HashSet<Script>>();
        foreach (var s1 in scripts)
        {
            foreach (var s2 in scripts.Where(s2 => s1 != s2 && s1.Contents.Contains(FilenameWithoutExtension(s2.Name))))
            {
                // Throw if dependency is cyclic
                if (dependencies.TryGetValue(s2, out var s2deps)
                    && s2deps.Select(s => s.Name).Contains(s1.Name))
                {
                    throw new Exception($"Cyclic dependency detected between {s1.Name} and {s2.Name}");
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
                var depsNotVisited = deps.Where(d => !visited.Contains(d.Name)).ToList();
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
            if (!visited.Contains(s.Name))
            {
                orderedScripts.Add(s);
                visited.Add(s.Name);
            }

            scriptsToVisit.Pop();
        }

        return orderedScripts;
    }

    private static Script ScriptFromPath(string path)
    {
        var contents = File.ReadAllText(path);
        return new Script(Path.GetFileName(path), contents);
    }

    private static string FilenameWithoutExtension(string filename)
    {
        return Path.GetFileNameWithoutExtension(filename);
    }

    [GeneratedRegex(@"^\d+.*.sql$")]
    private static partial Regex MigrationScript();
}