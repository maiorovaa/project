using System.Windows.Input;
namespace Polka.Desktop.ViewModels;
public sealed class RowItem
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Aside { get; init; } = "";
    public string Mark { get; init; } = "";
}
public sealed class ChoiceChip : ObservableObject
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public ICommand? Command { get; set; }
    private bool _active;
    public bool Active
    {
        get => _active;
        set => Set(ref _active, value);
    }
}
public sealed class RatingChip : ObservableObject
{
    public int Value { get; init; }
    public ICommand? Command { get; set; }
    private bool _active;
    public bool Active
    {
        get => _active;
        set => Set(ref _active, value);
    }
}
public sealed class EpisodeItem
{
    public required string Label { get; init; }
    public bool Done { get; init; }
    public string Tick => Done ? "✓" : "·";
    public bool CanOpen { get; init; }
    public ICommand? ToggleCommand { get; set; }
    public ICommand? OpenCommand { get; set; }
}
public sealed class TrackItem
{
    public required string Label { get; init; }
    public bool CanOpen { get; init; }
    public ICommand? OpenCommand { get; set; }
}
public sealed class ShelfEntryItem
{
    public required string Title { get; init; }
    public bool CanOpen { get; init; }
    public ICommand? OpenCommand { get; set; }
    public ICommand? RemoveCommand { get; set; }
}
public sealed class ScanLine
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string Path { get; init; }
}
