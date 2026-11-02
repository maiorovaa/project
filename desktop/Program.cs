using Avalonia;
using Polka.Desktop.Api;
namespace Polka.Desktop;
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--check"))
            return CheckApi(args).GetAwaiter().GetResult();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    private static async Task<int> CheckApi(string[] args)
    {
        var baseUrl = "http://127.0.0.1:8741";
        var index = Array.IndexOf(args, "--api");
        if (index >= 0 && index + 1 < args.Length)
            baseUrl = args[index + 1];
        try
        {
            var api = new PolkaClient(baseUrl);
            var auth = await api.LoginAsync("anastasia", "polka-demo");
            api.SetToken(auth.AccessToken);
            var movies = await api.MoviesAsync();
            var series = await api.SeriesAsync();
            var albums = await api.AlbumsAsync();
            var picks = await api.RecommendationsAsync();
            var scan = await api.ScanAsync("sample-library", dryRun: true);
            Console.WriteLine(
                $"ok user={auth.User.DisplayName} movies={movies.Total} series={series.Total} albums={albums.Total} picks={picks.Movies.Count} scan={scan.Examined}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
