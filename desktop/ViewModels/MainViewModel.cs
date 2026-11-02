using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using Polka.Desktop.Api;
using Polka.Desktop.Models;
namespace Polka.Desktop.ViewModels;
public sealed class MainViewModel : ObservableObject
{
    private readonly PolkaClient _api = new("http://127.0.0.1:8741");
    private readonly string _sessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "polka",
        "desktop-session.json");
    private DispatcherTimer? _timer;
    private int _revision;
    private bool _armed;
    private int _loadGen;
    private int _queryGen;
    private bool _suppressOpen;
    private string? _openKind;
    private string? _openId;
    private string? _openShelf;
    private string? _token;
    public MainViewModel()
    {
        LoginCommand = new RelayCommand(LoginAsync);
        LogoutCommand = new RelayCommand(LogoutAsync);
        GoMoviesCommand = new RelayCommand(() => GoAsync("movies"));
        GoSeriesCommand = new RelayCommand(() => GoAsync("series"));
        GoAlbumsCommand = new RelayCommand(() => GoAsync("albums"));
        GoShelvesCommand = new RelayCommand(() => GoAsync("shelves"));
        GoPicksCommand = new RelayCommand(() => GoAsync("picks"));
        GoScanCommand = new RelayCommand(() => GoAsync("scan"));
        CreateShelfCommand = new RelayCommand(CreateShelfAsync);
        ScanCommand = new RelayCommand(ScanAsync);
        BrowseCommand = new RelayCommand(BrowseAsync);
        SaveNotesCommand = new RelayCommand(SaveNotesAsync);
        FavoriteCommand = new RelayCommand(ToggleFavoriteAsync);
        AddToShelfCommand = new RelayCommand(AddToShelfAsync);
        OpenFileCommand = new RelayCommand(() => OpenPathAsync(FilePath));
        foreach (var (key, label) in new[]
        {
            ("planned", "В планах"),
            ("in_progress", "Сейчас"),
            ("completed", "Готово"),
            ("on_hold", "Пауза"),
            ("dropped", "Брошено"),
        })
        {
            var chip = new ChoiceChip { Key = key, Label = label };
            chip.Command = new RelayCommand(() => PatchStatusAsync(chip.Key));
            Statuses.Add(chip);
        }
        for (var value = 1; value <= 10; value++)
        {
            var chip = new RatingChip { Value = value };
            var rating = value;
            chip.Command = new RelayCommand(() => PatchRatingAsync(rating));
            Ratings.Add(chip);
        }
    }
    public Func<Task<string?>>? BrowseFolder { get; set; }
    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand GoMoviesCommand { get; }
    public ICommand GoSeriesCommand { get; }
    public ICommand GoAlbumsCommand { get; }
    public ICommand GoShelvesCommand { get; }
    public ICommand GoPicksCommand { get; }
    public ICommand GoScanCommand { get; }
    public ICommand CreateShelfCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand SaveNotesCommand { get; }
    public ICommand FavoriteCommand { get; }
    public ICommand AddToShelfCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ObservableCollection<RowItem> Rows { get; } = new();
    public ObservableCollection<ChoiceChip> Statuses { get; } = new();
    public ObservableCollection<RatingChip> Ratings { get; } = new();
    public ObservableCollection<EpisodeItem> Episodes { get; } = new();
    public ObservableCollection<TrackItem> Tracks { get; } = new();
    public ObservableCollection<ShelfDto> ShelfChoices { get; } = new();
    public ObservableCollection<ShelfEntryItem> ShelfEntries { get; } = new();
    public ObservableCollection<ScanLine> ScanLines { get; } = new();
    private bool _isBooting = true;
    public bool IsBooting
    {
        get => _isBooting;
        private set
        {
            if (Set(ref _isBooting, value))
                Raise(nameof(ShowLogin));
        }
    }
    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (Set(ref _isAuthenticated, value))
                Raise(nameof(ShowLogin));
        }
    }
    public bool ShowLogin => !IsBooting && !IsAuthenticated;
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    private string _loginName = "anastasia";
    public string LoginName { get => _loginName; set => Set(ref _loginName, value); }
    private string _password = "polka-demo";
    public string Password { get => _password; set => Set(ref _password, value); }
    private string _host = "http://127.0.0.1:8741";
    public string Host { get => _host; set => Set(ref _host, value); }
    private string _loginError = "";
    public string LoginError
    {
        get => _loginError;
        private set
        {
            if (Set(ref _loginError, value))
                Raise(nameof(HasLoginError));
        }
    }
    public bool HasLoginError => LoginError.Length > 0;
    private string _displayName = "";
    public string DisplayName { get => _displayName; private set => Set(ref _displayName, value); }
    private string _roleLabel = "";
    public string RoleLabel { get => _roleLabel; private set => Set(ref _roleLabel, value); }
    private string _section = "movies";
    public bool SectionIsMovies => _section == "movies";
    public bool SectionIsSeries => _section == "series";
    public bool SectionIsAlbums => _section == "albums";
    public bool SectionIsShelves => _section == "shelves";
    public bool SectionIsPicks => _section == "picks";
    public bool SectionIsScan => _section == "scan";
    private string _query = "";
    public string Query
    {
        get => _query;
        set
        {
            if (!Set(ref _query, value))
                return;
            RefreshChrome();
            _ = DebouncedLoadAsync();
        }
    }
    private string _kicker = "Каталог";
    public string Kicker { get => _kicker; private set => Set(ref _kicker, value); }
    private string _heading = "Кино на полке";
    public string Heading { get => _heading; private set => Set(ref _heading, value); }
    private string _intro = "";
    public string Intro
    {
        get => _intro;
        private set
        {
            if (Set(ref _intro, value))
                Raise(nameof(HasIntro));
        }
    }
    public bool HasIntro => Intro.Length > 0;
    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public bool ShowEmptyList => ShowRows && !IsLoading && !HasRows;
    private string _listError = "";
    public string ListError { get => _listError; private set => Set(ref _listError, value); }
    private bool _showRows = true;
    public bool ShowRows { get => _showRows; private set => Set(ref _showRows, value); }
    private bool _showScan;
    public bool ShowScan { get => _showScan; private set => Set(ref _showScan, value); }
    private bool _showShelfComposer;
    public bool ShowShelfComposer { get => _showShelfComposer; private set => Set(ref _showShelfComposer, value); }
    private bool _hasRows;
    public bool HasRows { get => _hasRows; private set => Set(ref _hasRows, value); }
    private RowItem? _selectedRow;
    public RowItem? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!Set(ref _selectedRow, value))
                return;
            if (_suppressOpen || value is null)
                return;
            _ = OpenRowAsync(value);
        }
    }
    private string _newShelfTitle = "";
    public string NewShelfTitle { get => _newShelfTitle; set => Set(ref _newShelfTitle, value); }
    private string _scanPath = "sample-library";
    public string ScanPath { get => _scanPath; set => Set(ref _scanPath, value); }
    private bool _dryRun = true;
    public bool DryRun { get => _dryRun; set => Set(ref _dryRun, value); }
    private string _scanSummary = "";
    public string ScanSummary { get => _scanSummary; private set => Set(ref _scanSummary, value); }
    private bool _hasMedia;
    public bool HasMedia { get => _hasMedia; private set => Set(ref _hasMedia, value); }
    private bool _hasShelf;
    public bool HasShelf { get => _hasShelf; private set => Set(ref _hasShelf, value); }
    public bool ShowEmpty => !HasMedia && !HasShelf;
    private string _detailKicker = "";
    public string DetailKicker { get => _detailKicker; private set => Set(ref _detailKicker, value); }
    private string _detailTitle = "";
    public string DetailTitle { get => _detailTitle; private set => Set(ref _detailTitle, value); }
    private string _detailMeta = "";
    public string DetailMeta { get => _detailMeta; private set => Set(ref _detailMeta, value); }
    private string _detailGenres = "";
    public string DetailGenres { get => _detailGenres; private set => Set(ref _detailGenres, value); }
    private string _detailBlurb = "";
    public string DetailBlurb { get => _detailBlurb; private set => Set(ref _detailBlurb, value); }
    private string _detailProgress = "";
    public string DetailProgress { get => _detailProgress; private set => Set(ref _detailProgress, value); }
    public bool ShowProgress => DetailProgress.Length > 0;
    private string _notes = "";
    public string Notes { get => _notes; set => Set(ref _notes, value); }
    private bool _favorite;
    public bool IsFavorite => _favorite;
    public string FavoriteLabel => _favorite ? "В избранном" : "В избранное";
    private string _filePath = "";
    public string FilePath { get => _filePath; private set => Set(ref _filePath, value); }
    public bool HasFile => FilePath.Length > 0;
    public bool HasEpisodes => Episodes.Count > 0;
    public bool HasTracks => Tracks.Count > 0;
    private ShelfDto? _shelfTarget;
    public ShelfDto? ShelfTarget { get => _shelfTarget; set => Set(ref _shelfTarget, value); }
    public bool ShowShelfPicker => ShelfChoices.Count > 0 && HasMedia;
    private string _shelfTitle = "";
    public string ShelfTitle { get => _shelfTitle; private set => Set(ref _shelfTitle, value); }
    private string _shelfBlurb = "";
    public string ShelfBlurb { get => _shelfBlurb; private set => Set(ref _shelfBlurb, value); }
    private string _revisionText = "—";
    public string RevisionText { get => _revisionText; private set => Set(ref _revisionText, value); }
    private string _syncNote = "Ждём журнал";
    public string SyncNote { get => _syncNote; private set => Set(ref _syncNote, value); }
    public string ApiLabel => _api.BaseUrl;
    public string RuntimeLabel => ".NET 8";
    public async Task StartAsync()
    {
        try
        {
            LoadSession();
            if (!string.IsNullOrWhiteSpace(_token))
            {
                _api.SetBaseUrl(Host);
                _api.SetToken(_token);
                var user = await _api.MeAsync();
                ApplyUser(user);
                IsAuthenticated = true;
                await LoadSectionAsync();
                ArmSync();
            }
        }
        catch
        {
            _token = null;
            _api.SetToken(null);
        }
        finally
        {
            IsBooting = false;
        }
    }
    private async Task LoginAsync()
    {
        LoginError = "";
        IsBusy = true;
        try
        {
            _api.SetBaseUrl(Host);
            var auth = await _api.LoginAsync(LoginName.Trim(), Password);
            _token = auth.AccessToken;
            _api.SetToken(_token);
            SaveSession();
            ApplyUser(auth.User);
            IsAuthenticated = true;
            Raise(nameof(ApiLabel));
            await LoadSectionAsync();
            ArmSync();
        }
        catch (Exception ex)
        {
            LoginError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
    private Task LogoutAsync()
    {
        _timer?.Stop();
        _armed = false;
        _token = null;
        _api.SetToken(null);
        TryDeleteSession();
        IsAuthenticated = false;
        HasMedia = false;
        HasShelf = false;
        Raise(nameof(ShowEmpty));
        Rows.Clear();
        return Task.CompletedTask;
    }
    private Task GoAsync(string section)
    {
        _queryGen++;
        _query = "";
        Raise(nameof(Query));
        if (_section == section)
        {
            RefreshChrome();
            return LoadSectionAsync();
        }
        _section = section;
        Raise(nameof(SectionIsMovies));
        Raise(nameof(SectionIsSeries));
        Raise(nameof(SectionIsAlbums));
        Raise(nameof(SectionIsShelves));
        Raise(nameof(SectionIsPicks));
        Raise(nameof(SectionIsScan));
        RefreshChrome();
        return LoadSectionAsync();
    }
    private async Task DebouncedLoadAsync()
    {
        var gen = ++_queryGen;
        await Task.Delay(280);
        if (gen == _queryGen)
            await LoadSectionAsync();
    }
    private async Task LoadSectionAsync()
    {
        var gen = ++_loadGen;
        ListError = "";
        IsLoading = true;
        try
        {
            var query = Query.Trim();
            List<RowItem> rows;
            var intro = "";
            if (query.Length > 0)
            {
                var found = await _api.SearchAsync(query);
                rows = [];
                rows.AddRange(found.Movies.Select(item => MediaRow("movie", item.Id, item.Title, item.Year?.ToString() ?? "", item.State)));
                rows.AddRange(found.Series.Select(item => MediaRow("series", item.Id, item.Title, YearSpan(item.YearStart, item.YearEnd), item.State)));
                rows.AddRange(found.Albums.Select(item => MediaRow("album", item.Id, item.Title, item.Artist.Name, item.State)));
            }
            else if (_section == "movies")
            {
                var page = await _api.MoviesAsync();
                rows = page.Items.Select(item => MediaRow("movie", item.Id, item.Title, item.Year?.ToString() ?? "", item.State)).ToList();
            }
            else if (_section == "series")
            {
                var page = await _api.SeriesAsync();
                rows = page.Items.Select(item => MediaRow("series", item.Id, item.Title, $"{item.Progress.Percent}%", item.State)).ToList();
            }
            else if (_section == "albums")
            {
                var page = await _api.AlbumsAsync();
                rows = page.Items.Select(item => MediaRow("album", item.Id, item.Title, item.Artist.Name, item.State)).ToList();
            }
            else if (_section == "shelves")
            {
                var shelves = await _api.ShelvesAsync();
                rows = shelves.Select(item => new RowItem
                {
                    Kind = "shelf",
                    Id = item.Id,
                    Title = item.Title,
                    Aside = item.ItemsCount.ToString(),
                    Mark = "полка",
                }).ToList();
                ReplaceShelves(shelves);
            }
            else if (_section == "picks")
            {
                var data = await _api.RecommendationsAsync();
                intro = data.Intro;
                rows = [];
                rows.AddRange(data.Movies.Select(item => MediaRow("movie", item.Item.Id, item.Item.Title, item.Score.ToString(), item.Item.State, item.Reasons.FirstOrDefault())));
                rows.AddRange(data.Series.Select(item => MediaRow("series", item.Item.Id, item.Item.Title, item.Score.ToString(), item.Item.State, item.Reasons.FirstOrDefault())));
                rows.AddRange(data.Albums.Select(item => MediaRow("album", item.Item.Id, item.Item.Title, item.Score.ToString(), item.Item.State, item.Reasons.FirstOrDefault())));
            }
            else
            {
                rows = [];
            }
            if (gen != _loadGen)
                return;
            if (_section != "shelves")
                _ = RefreshShelvesQuietlyAsync();
            Intro = intro;
            Fill(rows);
        }
        catch (Exception ex)
        {
            if (gen == _loadGen)
                ListError = ex.Message;
        }
        finally
        {
            if (gen == _loadGen)
            {
                IsLoading = false;
                Raise(nameof(ShowEmptyList));
            }
        }
    }
    private async Task RefreshShelvesQuietlyAsync()
    {
        try
        {
            var shelves = await _api.ShelvesAsync();
            await Dispatcher.UIThread.InvokeAsync(() => ReplaceShelves(shelves));
        }
        catch
        {
            /* полки для «добавить» необязательны, если список временно недоступен */
        }
    }
    private void ReplaceShelves(List<ShelfDto> shelves)
    {
        var selected = ShelfTarget?.Id;
        ShelfChoices.Clear();
        foreach (var shelf in shelves)
            ShelfChoices.Add(shelf);
        ShelfTarget = ShelfChoices.FirstOrDefault(item => item.Id == selected) ?? ShelfChoices.FirstOrDefault();
        Raise(nameof(ShowShelfPicker));
    }
    private void Fill(List<RowItem> rows)
    {
        var keepId = SelectedRow?.Id;
        var keepKind = SelectedRow?.Kind;
        _suppressOpen = true;
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
        SelectedRow = Rows.FirstOrDefault(item => item.Id == keepId && item.Kind == keepKind);
        _suppressOpen = false;
        HasRows = Rows.Count > 0;
        Raise(nameof(ShowEmptyList));
    }
    private async Task OpenRowAsync(RowItem row)
    {
        try
        {
            if (row.Kind == "shelf")
                await ShowShelfAsync(row.Id);
            else
                await ShowMediaAsync(row.Kind, row.Id);
        }
        catch (Exception ex)
        {
            ListError = ex.Message;
        }
    }
    private async Task ShowMediaAsync(string kind, string id)
    {
        _openKind = kind;
        _openId = id;
        _openShelf = null;
        HasShelf = false;
        if (kind == "movie")
        {
            var movie = await _api.MovieAsync(id);
            ApplyState(movie.State);
            DetailKicker = "Фильм";
            DetailTitle = movie.Title;
            DetailMeta = Join(movie.Year?.ToString(), movie.Country, string.Join(", ", movie.Directors));
            DetailGenres = string.Join(" · ", movie.Genres);
            DetailBlurb = string.IsNullOrWhiteSpace(movie.Description) ? "Описания пока нет." : movie.Description;
            DetailProgress = "";
            FilePath = movie.FilePath;
            Episodes.Clear();
            Tracks.Clear();
        }
        else if (kind == "series")
        {
            var series = await _api.SeriesDetailAsync(id);
            ApplyState(series.State);
            DetailKicker = "Сериал";
            DetailTitle = series.Title;
            DetailMeta = Join(YearSpan(series.YearStart, series.YearEnd), string.Join(", ", series.Creators));
            DetailGenres = string.Join(" · ", series.Genres);
            DetailBlurb = string.IsNullOrWhiteSpace(series.Description) ? "Описания пока нет." : series.Description;
            DetailProgress = $"Серии: {series.Progress.EpisodesCompleted} из {series.Progress.EpisodesTotal} · {series.Progress.Percent}%";
            FilePath = "";
            Tracks.Clear();
            Episodes.Clear();
            foreach (var season in series.Seasons)
            {
                foreach (var episode in season.Episodes)
                {
                    var done = episode.State?.Status == "completed";
                    var item = new EpisodeItem
                    {
                        Label = $"{season.Title} · {episode.Number}. {episode.Title}",
                        Done = done,
                        CanOpen = episode.FilePath.Length > 0,
                    };
                    var episodeId = episode.Id;
                    var path = episode.FilePath;
                    item.ToggleCommand = new RelayCommand(() => ToggleEpisodeAsync(episodeId, done));
                    item.OpenCommand = new RelayCommand(() => OpenPathAsync(path));
                    Episodes.Add(item);
                }
            }
        }
        else
        {
            var album = await _api.AlbumAsync(id);
            ApplyState(album.State);
            DetailKicker = "Альбом";
            DetailTitle = album.Title;
            DetailMeta = Join(album.Artist.Name, album.Year?.ToString());
            DetailGenres = string.Join(" · ", album.Genres);
            DetailBlurb = string.IsNullOrWhiteSpace(album.Description) ? "Описания пока нет." : album.Description;
            DetailProgress = "";
            FilePath = "";
            Episodes.Clear();
            Tracks.Clear();
            foreach (var track in album.Tracks)
            {
                var number = track.TrackNumber is int n ? $"{n}. " : "";
                var item = new TrackItem
                {
                    Label = number + track.Title,
                    CanOpen = track.FilePath.Length > 0,
                };
                var path = track.FilePath;
                item.OpenCommand = new RelayCommand(() => OpenPathAsync(path));
                Tracks.Add(item);
            }
        }
        HasMedia = true;
        RaiseInspectorFlags();
    }
    private async Task ShowShelfAsync(string id)
    {
        var shelf = await _api.ShelfAsync(id);
        _openShelf = id;
        _openKind = null;
        _openId = null;
        HasMedia = false;
        HasShelf = true;
        ShelfTitle = shelf.Title;
        ShelfBlurb = string.IsNullOrWhiteSpace(shelf.Description)
            ? "Личный список. Другие участники дома его не видят."
            : shelf.Description;
        ShelfEntries.Clear();
        foreach (var entry in shelf.Items)
        {
            var item = new ShelfEntryItem
            {
                Title = entry.Title,
                CanOpen = !entry.Missing && entry.MediaType is "movie" or "series" or "album",
            };
            var mediaType = entry.MediaType;
            var mediaId = entry.MediaId;
            var itemId = entry.Id;
            item.OpenCommand = new RelayCommand(() => ShowMediaAsync(mediaType, mediaId));
            item.RemoveCommand = new RelayCommand(() => RemoveShelfItemAsync(id, itemId));
            ShelfEntries.Add(item);
        }
        RaiseInspectorFlags();
    }
    private void ApplyState(MediaStateDto? state)
    {
        var status = state?.Status;
        var rating = state?.Rating;
        _favorite = state?.Favorite ?? false;
        Notes = state?.Notes ?? "";
        foreach (var chip in Statuses)
            chip.Active = chip.Key == status;
        foreach (var chip in Ratings)
            chip.Active = chip.Value == rating;
        Raise(nameof(FavoriteLabel));
    }
    private async Task PatchStatusAsync(string status)
    {
        if (_openKind is null || _openId is null)
            return;
        await Guard(async () =>
        {
            await _api.PatchAsync(StatePath(), new { Status = status });
            await ShowMediaAsync(_openKind, _openId);
            await LoadSectionAsync();
        });
    }
    private async Task PatchRatingAsync(int rating)
    {
        if (_openKind is null || _openId is null)
            return;
        var clear = Ratings.FirstOrDefault(chip => chip.Value == rating)?.Active == true;
        await Guard(async () =>
        {
            await _api.PatchAsync(StatePath(), new { Rating = clear ? (int?)null : rating });
            await ShowMediaAsync(_openKind, _openId);
            await LoadSectionAsync();
        });
    }
    private async Task ToggleFavoriteAsync()
    {
        if (_openKind is null || _openId is null)
            return;
        await Guard(async () =>
        {
            await _api.PatchAsync(StatePath(), new { Favorite = !_favorite });
            await ShowMediaAsync(_openKind, _openId);
            await LoadSectionAsync();
        });
    }
    private async Task SaveNotesAsync()
    {
        if (_openKind is null || _openId is null)
            return;
        await Guard(async () =>
        {
            await _api.PatchAsync(StatePath(), new { Notes });
            ListError = "";
            SyncNote = "Заметка сохранена";
        });
    }
    private async Task ToggleEpisodeAsync(string episodeId, bool done)
    {
        await Guard(async () =>
        {
            await _api.PatchAsync($"/api/episodes/{episodeId}/state", new { Status = done ? "planned" : "completed" });
            if (_openKind == "series" && _openId is not null)
                await ShowMediaAsync("series", _openId);
            await LoadSectionAsync();
        });
    }
    private async Task AddToShelfAsync()
    {
        if (ShelfTarget is null || _openKind is null || _openId is null)
            return;
        await Guard(async () =>
        {
            await _api.AddShelfItemAsync(ShelfTarget.Id, _openKind, _openId);
            SyncNote = "Добавлено на полку «" + ShelfTarget.Title + "»";
            if (_section == "shelves")
                await LoadSectionAsync();
        });
    }
    private async Task RemoveShelfItemAsync(string shelfId, string itemId)
    {
        await Guard(async () =>
        {
            await _api.RemoveShelfItemAsync(shelfId, itemId);
            await ShowShelfAsync(shelfId);
            await LoadSectionAsync();
        });
    }
    private async Task CreateShelfAsync()
    {
        var title = NewShelfTitle.Trim();
        if (title.Length == 0)
            return;
        await Guard(async () =>
        {
            await _api.CreateShelfAsync(title);
            NewShelfTitle = "";
            await LoadSectionAsync();
        });
    }
    private async Task BrowseAsync()
    {
        if (BrowseFolder is null)
            return;
        var path = await BrowseFolder();
        if (!string.IsNullOrWhiteSpace(path))
            ScanPath = path;
    }
    private async Task ScanAsync()
    {
        IsBusy = true;
        ListError = "";
        try
        {
            var report = await _api.ScanAsync(ScanPath, DryRun);
            ScanSummary =
                $"Просмотрено {report.Examined}. Новых {report.Created}. Связано {report.Linked}. Пропущено {report.Skipped}."
                + (report.DryRun ? " Запись не выполнялась." : " Каталог обновлён.")
                + (report.Truncated ? " Достигнут предел 5000 файлов." : "");
            ScanLines.Clear();
            foreach (var item in report.Items.Take(80))
            {
                ScanLines.Add(new ScanLine
                {
                    Title = item.Title,
                    Detail = ActionLabel(item.Action) + (string.IsNullOrWhiteSpace(item.Detail) ? "" : " · " + item.Detail),
                    Path = item.Path,
                });
            }
            if (!report.DryRun)
                SyncNote = ScanSummary;
        }
        catch (Exception ex)
        {
            ListError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
    private async Task OpenPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ListError = "Файл не открылся: " + ex.Message;
        }
        await Task.CompletedTask;
    }
    private void ArmSync()
    {
        _timer?.Stop();
        _armed = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += async (_, _) => await SyncTickAsync();
        _timer.Start();
        _ = SyncTickAsync();
    }
    private async Task SyncTickAsync()
    {
        try
        {
            if (!_armed)
            {
                var head = await _api.HeadAsync();
                _revision = head.Revision;
                _armed = true;
                RevisionText = _revision.ToString();
                SyncNote = "Журнал подключён";
                return;
            }
            var data = await _api.SinceAsync(_revision);
            _revision = data.Revision;
            RevisionText = _revision.ToString();
            if (!data.Reset && data.Changes.Count == 0)
                return;
            SyncNote = data.Changes.LastOrDefault()?.Summary ?? "Каталог обновлён";
            await LoadSectionAsync();
            if (_openKind is not null && _openId is not null)
                await ShowMediaAsync(_openKind, _openId);
            else if (_openShelf is not null)
                await ShowShelfAsync(_openShelf);
        }
        catch
        {
            SyncNote = "Сервер недоступен";
        }
    }
    private async Task Guard(Func<Task> action)
    {
        try
        {
            ListError = "";
            await action();
        }
        catch (Exception ex)
        {
            ListError = ex.Message;
        }
    }
    private string StatePath() => _openKind switch
    {
        "movie" => $"/api/movies/{_openId}/state",
        "series" => $"/api/series/{_openId}/state",
        _ => $"/api/albums/{_openId}/state",
    };
    private void RefreshChrome()
    {
        var query = Query.Trim();
        if (query.Length > 0)
        {
            Kicker = "Поиск";
            Heading = "«" + query + "»";
        }
        else
        {
            (Kicker, Heading) = _section switch
            {
                "series" => ("Каталог", "Сериалы по сериям"),
                "albums" => ("Каталог", "Музыка дома"),
                "shelves" => ("Личное", "Личные подборки"),
                "picks" => ("Подбор", "Что смотреть дальше"),
                "scan" => ("Сканер", "Разобрать папку"),
                _ => ("Каталог", "Кино на полке"),
            };
        }
        ShowScan = _section == "scan" && query.Length == 0;
        ShowRows = !ShowScan;
        ShowShelfComposer = _section == "shelves" && query.Length == 0;
        if (query.Length > 0)
            Intro = "";
        Raise(nameof(ShowEmptyList));
    }
    private void RaiseInspectorFlags()
    {
        Raise(nameof(ShowEmpty));
        Raise(nameof(ShowProgress));
        Raise(nameof(HasFile));
        Raise(nameof(HasEpisodes));
        Raise(nameof(HasTracks));
        Raise(nameof(ShowShelfPicker));
        Raise(nameof(FavoriteLabel));
        Raise(nameof(IsFavorite));
    }
    private void ApplyUser(UserDto user)
    {
        DisplayName = user.DisplayName;
        RoleLabel = user.Role == "owner" ? "Владелец" : "Участник";
    }
    private static RowItem MediaRow(string kind, string id, string title, string aside, MediaStateDto? state, string? mark = null) =>
        new()
        {
            Kind = kind,
            Id = id,
            Title = title,
            Aside = aside,
            Mark = mark ?? Mark(state),
        };
    private static string Mark(MediaStateDto? state)
    {
        if (state is null)
            return "";
        var bits = new List<string>();
        if (!string.IsNullOrEmpty(state.Status))
            bits.Add(StatusLabel(state.Status));
        if (state.Rating is int rating)
            bits.Add($"{rating}/10");
        if (state.Favorite)
            bits.Add("★");
        return string.Join(" · ", bits);
    }
    private static string StatusLabel(string? status) => status switch
    {
        "planned" => "В планах",
        "in_progress" => "В процессе",
        "completed" => "Готово",
        "dropped" => "Брошено",
        "on_hold" => "Пауза",
        _ => "",
    };
    private static string YearSpan(int? start, int? end)
    {
        if (start is null && end is null)
            return "";
        if (start is not null && end is not null && start != end)
            return $"{start}–{end}";
        return (start ?? end)?.ToString() ?? "";
    }
    private static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    private static string ActionLabel(string action) => action switch
    {
        "create" => "новая карточка",
        "link" => "привязан файл",
        "duplicate" => "уже в каталоге",
        "skip" => "пропуск",
        _ => action,
    };
    private void LoadSession()
    {
        if (!File.Exists(_sessionPath))
            return;
        var session = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(_sessionPath));
        if (session is null)
            return;
        if (!string.IsNullOrWhiteSpace(session.BaseUrl))
            Host = session.BaseUrl;
        _token = session.Token;
    }
    private void SaveSession()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);
        var json = JsonSerializer.Serialize(new SessionFile { BaseUrl = _api.BaseUrl, Token = _token ?? "" });
        File.WriteAllText(_sessionPath, json);
    }
    private void TryDeleteSession()
    {
        try
        {
            if (File.Exists(_sessionPath))
                File.Delete(_sessionPath);
        }
        catch
        {
            /* сессия останется до следующего входа */
        }
    }
    private sealed class SessionFile
    {
        public string BaseUrl { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
