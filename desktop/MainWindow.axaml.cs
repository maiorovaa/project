using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Polka.Desktop.ViewModels;
namespace Polka.Desktop;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var model = new MainViewModel { BrowseFolder = BrowseFolderAsync };
        DataContext = model;
        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
                await viewModel.StartAsync();
        };
    }
    private async Task<string?> BrowseFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Папка с фильмами, сериалами и музыкой",
            AllowMultiple = false,
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
