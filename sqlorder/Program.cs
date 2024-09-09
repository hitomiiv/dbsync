using System.CommandLine;
using System.Data.SqlClient;

namespace sqlorder;

internal static class Program
{
    public static void Main(string[] args)
    {
        var connectionStringOption = new Option<string>(["--connection", "-c"],
            "The connection string to the database. If none is specified, diffs will include all scripts");

        var prefixOption = new Option<DirectoryInfo>(["--prefix", "-p"],
            "The directory to look for scripts in. Defaults to the current directory");

        var untrackedOption = new Option<bool>("--untracked",
            "Disables the use of a migrations table. All scripts will run without being tracked");

        var filenameArgument = new Argument<FileInfo>("filename", _ => null, isDefault: true,
            "The file to write the sql scripts to. If none is specified, the scripts to run will be printed to the console");

        var diffCommand = new Command("diff", "Print the sql scripts to be run in order");
        diffCommand.AddArgument(filenameArgument);
        diffCommand.SetHandler(DiffHandler, filenameArgument, connectionStringOption, prefixOption, untrackedOption);

        var pushCommand = new Command("push", "Push changes to the database. A connection string must be provided");
        pushCommand.SetHandler(PushHandler, connectionStringOption, prefixOption, untrackedOption);

        var rootCommand = new RootCommand
        {
            diffCommand,
            pushCommand
        };
        rootCommand.AddGlobalOption(connectionStringOption);
        rootCommand.AddGlobalOption(prefixOption);
        rootCommand.AddGlobalOption(untrackedOption);
        rootCommand.Invoke(args);
    }

    private static void DiffHandler(FileInfo filename, string connectionString, DirectoryInfo prefix,
        bool untracked)
    {
        prefix ??= new DirectoryInfo(Directory.GetCurrentDirectory());
        var connection = connectionString != null ? new SqlConnection(connectionString) : null;

        if (filename != null)
        {
            File.WriteAllText(filename.FullName, ScriptProcessor.CreateDeployScript(prefix, connection, !untracked));
            Console.WriteLine($"Deploy script created: {filename.FullName}");
        }
        else
        {
            Console.WriteLine("Scripts to run:");
            Console.WriteLine(ScriptProcessor.CreateScriptList(prefix, connection, !untracked));
        }
    }

    private static void PushHandler(string connectionString, DirectoryInfo prefix, bool untracked)
    {
        prefix ??= new DirectoryInfo(Directory.GetCurrentDirectory());

        using var connection = new SqlConnection(connectionString);
        using var transaction = connection.BeginTransaction();

        try
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = ScriptProcessor.CreateDeployScript(prefix, connection, !untracked);
            command.ExecuteNonQuery();

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine("Deploy requires manual intervention. Transaction rolled back.");
            Console.WriteLine(ex.Message);
        }
    }
}