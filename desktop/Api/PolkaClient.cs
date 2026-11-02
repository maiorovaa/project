using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Polka.Desktop.Models;
namespace Polka.Desktop.Api;
public sealed class PolkaException : Exception
{
    public int Status { get; }
    public PolkaException(int status, string message) : base(message) => Status = status;
}
public sealed class PolkaClient : IDisposable
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(40) };
    private string _baseUrl;
    public PolkaClient(string baseUrl)
    {
        _baseUrl = Normalize(baseUrl);
    }
    public string BaseUrl => _baseUrl;
    public void SetBaseUrl(string baseUrl) => _baseUrl = Normalize(baseUrl);
    public void SetToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }
    public Task<AuthDto> LoginAsync(string login, string password) =>
        SendAsync<AuthDto>(HttpMethod.Post, "/api/auth/login", new { Login = login, Password = password });
    public Task<UserDto> MeAsync() => GetAsync<UserDto>("/api/auth/me");
    public Task<PageDto<MovieDto>> MoviesAsync() => GetAsync<PageDto<MovieDto>>("/api/movies?page_size=48&sort=title");
    public Task<PageDto<SeriesDto>> SeriesAsync() => GetAsync<PageDto<SeriesDto>>("/api/series?page_size=48&sort=title");
    public Task<PageDto<AlbumDto>> AlbumsAsync() => GetAsync<PageDto<AlbumDto>>("/api/albums?page_size=48");
    public async Task<List<ShelfDto>> ShelvesAsync()
    {
        var page = await GetAsync<PageDto<ShelfDto>>("/api/shelves");
        return page.Items;
    }
    public Task<ShelfDto> ShelfAsync(string id) => GetAsync<ShelfDto>($"/api/shelves/{id}");
    public Task<RecommendationsDto> RecommendationsAsync() => GetAsync<RecommendationsDto>("/api/recommendations");
    public Task<SearchDto> SearchAsync(string query) =>
        GetAsync<SearchDto>("/api/search?q=" + Uri.EscapeDataString(query));
    public Task<MovieDto> MovieAsync(string id) => GetAsync<MovieDto>($"/api/movies/{id}");
    public Task<SeriesDto> SeriesDetailAsync(string id) => GetAsync<SeriesDto>($"/api/series/{id}");
    public Task<AlbumDto> AlbumAsync(string id) => GetAsync<AlbumDto>($"/api/albums/{id}");
    public Task PatchAsync(string path, object body) => SendAsync(HttpMethod.Patch, path, body);
    public Task CreateShelfAsync(string title) =>
        SendAsync(HttpMethod.Post, "/api/shelves", new { Title = title, Description = "" });
    public Task AddShelfItemAsync(string shelfId, string mediaType, string mediaId) =>
        SendAsync(HttpMethod.Post, $"/api/shelves/{shelfId}/items", new { MediaType = mediaType, MediaId = mediaId });
    public Task RemoveShelfItemAsync(string shelfId, string itemId) =>
        SendAsync(HttpMethod.Delete, $"/api/shelves/{shelfId}/items/{itemId}");
    public Task<SyncHeadDto> HeadAsync() => GetAsync<SyncHeadDto>("/api/sync/head");
    public Task<SyncDto> SinceAsync(int revision) => GetAsync<SyncDto>($"/api/sync?since={revision}");
    public Task<ScanReportDto> ScanAsync(string path, bool dryRun) =>
        SendAsync<ScanReportDto>(HttpMethod.Post, "/api/library/scan", new { Path = path, DryRun = dryRun });
    public void Dispose() => _http.Dispose();
    private Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path);
    private async Task SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var response = await SendRawAsync(method, path, body);
        await EnsureSuccess(response);
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        using var response = await SendRawAsync(method, path, body);
        await EnsureSuccess(response);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default!;
        await using var stream = await response.Content.ReadAsStreamAsync();
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, Json);
        return payload ?? throw new PolkaException((int)response.StatusCode, "Пустой ответ сервера");
    }
    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, _baseUrl + path);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, Json);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        try
        {
            return await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new PolkaException(0, "Сервер «Полки» не отвечает по адресу " + _baseUrl + ". " + ex.Message);
        }
    }
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var text = await response.Content.ReadAsStringAsync();
        var detail = text;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("detail", out var node) && node.ValueKind == JsonValueKind.String)
                detail = node.GetString() ?? detail;
        }
        catch (JsonException)
        {
            /* ответ не JSON */
        }
        throw new PolkaException((int)response.StatusCode, string.IsNullOrWhiteSpace(detail) ? "Сервер отклонил запрос" : detail);
    }
    private static string Normalize(string baseUrl)
    {
        var value = (baseUrl ?? "").Trim().TrimEnd('/');
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            value = "http://" + value;
        return value;
    }
}
