namespace PesterDash.Cli.Utilities;

internal static class AlternateScreen
{
    private const string Enter = "\x1b[?1049h\x1b[H";
    private const string Leave = "\x1b[?1049l";

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        Console.Write(Enter);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Console.Write(Leave);
        }
    }
}
