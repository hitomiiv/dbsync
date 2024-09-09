using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace sqlorder;

public partial class ScriptProcessor
{
    public static string CreateScriptList(DirectoryInfo prefix, IDbConnection connection, bool tracked)
    {
        var orderedScripts = OrderedScriptsInDirectory(prefix, connection, tracked);

        var builder = new StringBuilder();
        var count = 1;
        foreach (var script in orderedScripts)
        {
            builder.AppendLine($"{count}. {script.Name}");
            count++;
        }

        if (count == 1)
        {
            builder.AppendLine("No scripts to run");
        }

        return builder.ToString();
    }

    public static string CreateDeployScript(DirectoryInfo prefix, IDbConnection connection, bool tracked)
    {
        var orderedScripts = OrderedScriptsInDirectory(prefix, connection, tracked);
        var deployScript = ConcatScripts(orderedScripts, tracked);
        return deployScript;
    }

    private static IEnumerable<Script> OrderedScriptsInDirectory(DirectoryInfo prefix, IDbConnection connection,
        bool tracked)
    {
        var scripts = ScriptsInDirectory(prefix);
        var filteredScripts = connection != null && tracked ? FilterByNotRun(scripts, connection) : scripts;
        return ScriptOrder(filteredScripts);
    }

    public static IEnumerable<Script> ScriptOrder(IEnumerable<Script> scripts)
    {
        // Separate migrations
        var scriptList = scripts.ToList();
        var migrationScripts = scriptList
            .Where(s => MigrationScript().IsMatch(s.Name))
            .OrderBy(s => s.Name)
            .ToList();
        var otherScripts = scriptList.Where(s => !MigrationScript().IsMatch(s.Name)).ToList();

        // Compare script s1 with every other script s2
        // If s1 contains any mentions of s2, add s2 as a dependency
        var dependencies = new Dictionary<Script, HashSet<Script>>();
        foreach (var s1 in otherScripts)
        {
            foreach (var s2 in otherScripts.Where(s2 => s1 != s2 && s1.Contents.Contains((string)WithoutExtension(s2.Name))))
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
        var scriptsToVisit = new Stack<Script>(otherScripts);
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

        return migrationScripts.Concat(orderedScripts);
    }

    private static string ConcatScripts(IEnumerable<Script> scripts, bool tracked)
    {
        var builder = new StringBuilder();

        // Create migrations table if needed
        if (tracked)
        {
            builder.AppendLine("""
                               CREATE TABLE IF NOT EXISTS dbsync_migrations 
                               (
                                   filename TEXT PRIMARY KEY, 
                                   hash TEXT, 
                                   timestamp DATETIME
                               );
                               """);
        }

        // Concat scripts
        foreach (var script in scripts)
        {
            builder.AppendLine($"-- {script.Name}");
            builder.AppendLine(script.Contents);

            if (tracked)
            {
                builder.AppendLine($"""
                                    INSERT INTO dbsync_migrations (filename, hash, timestamp) 
                                    VALUES ('{script.Name}', '{script.Hash}', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}');
                                    """);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IEnumerable<Script> FilterByNotRun(IEnumerable<Script> scripts, IDbConnection connection)
    {
        var command = connection.CreateCommand();
        foreach (var script in scripts)
        {
            command.CommandText = $"EXISTS SELECT 1 FROM dbsync_migrations WHERE hash = {script.Hash}";
            if ((bool)(command.ExecuteScalar() ?? false))
            {
                yield return script;
            }
        }
    }

    private static IEnumerable<Script> ScriptsInDirectory(DirectoryInfo prefix)
    {
        return from script in prefix.EnumerateFiles("*.sql", SearchOption.TopDirectoryOnly)
               let contents = File.ReadAllText(script.FullName)
               let hash = SHA1.HashData(Encoding.Default.GetBytes(contents))
               select new Script(script.Name, contents, Encoding.Default.GetString(hash));
    }

    private static string WithoutExtension(string filename)
    {
        return Path.GetFileNameWithoutExtension(filename);
    }

    [GeneratedRegex(@"^\d+.*.sql$")]
    private static partial Regex MigrationScript();
}