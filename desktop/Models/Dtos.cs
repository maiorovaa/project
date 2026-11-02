namespace Polka.Desktop.Models;
public sealed class UserDto
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}
public sealed class AuthDto
{
    public string AccessToken { get; set; } = "";
    public UserDto User { get; set; } = new();
}
public sealed class MediaStateDto
{
    public string? Status { get; set; }
    public int? Rating { get; set; }
    public int ProgressSec { get; set; }
    public bool Favorite { get; set; }
    public string Notes { get; set; } = "";
}
public sealed class MovieDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public string Description { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public List<string> Directors { get; set; } = [];
    public string Country { get; set; } = "";
    public string FilePath { get; set; } = "";
    public MediaStateDto? State { get; set; }
}
public sealed class ProgressDto
{
    public int EpisodesTotal { get; set; }
    public int EpisodesCompleted { get; set; }
    public int Percent { get; set; }
}
public sealed class EpisodeDto
{
    public string Id { get; set; } = "";
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public int? RuntimeMin { get; set; }
    public string FilePath { get; set; } = "";
    public MediaStateDto? State { get; set; }
}
public sealed class SeasonDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<EpisodeDto> Episodes { get; set; } = [];
}
public sealed class SeriesDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int? YearStart { get; set; }
    public int? YearEnd { get; set; }
    public string Description { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public List<string> Creators { get; set; } = [];
    public ProgressDto Progress { get; set; } = new();
    public MediaStateDto? State { get; set; }
    public List<SeasonDto> Seasons { get; set; } = [];
}
public sealed class ArtistBriefDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class TrackDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int? TrackNumber { get; set; }
    public int? DurationSec { get; set; }
    public string FilePath { get; set; } = "";
}
public sealed class AlbumDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public string Description { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public ArtistBriefDto Artist { get; set; } = new();
    public MediaStateDto? State { get; set; }
    public List<TrackDto> Tracks { get; set; } = [];
}
public sealed class PageDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }
}
public sealed class ShelfItemDto
{
    public string Id { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string MediaId { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Missing { get; set; }
}
public sealed class ShelfDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int ItemsCount { get; set; }
    public List<ShelfItemDto> Items { get; set; } = [];
}
public sealed class ScoredDto<T>
{
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = [];
    public T Item { get; set; } = default!;
}
public sealed class RecommendationsDto
{
    public string Intro { get; set; } = "";
    public List<ScoredDto<MovieDto>> Movies { get; set; } = [];
    public List<ScoredDto<SeriesDto>> Series { get; set; } = [];
    public List<ScoredDto<AlbumDto>> Albums { get; set; } = [];
}
public sealed class SearchDto
{
    public List<MovieDto> Movies { get; set; } = [];
    public List<SeriesDto> Series { get; set; } = [];
    public List<AlbumDto> Albums { get; set; } = [];
}
public sealed class SyncHeadDto
{
    public int Revision { get; set; }
}
public sealed class SyncChangeDto
{
    public string Summary { get; set; } = "";
}
public sealed class SyncDto
{
    public int Revision { get; set; }
    public bool Reset { get; set; }
    public List<SyncChangeDto> Changes { get; set; } = [];
}
public sealed class ScanItemDto
{
    public string Path { get; set; } = "";
    public string Action { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
}
public sealed class ScanReportDto
{
    public bool DryRun { get; set; }
    public int Examined { get; set; }
    public int Created { get; set; }
    public int Linked { get; set; }
    public int Skipped { get; set; }
    public bool Truncated { get; set; }
    public List<ScanItemDto> Items { get; set; } = [];
}
