using System.Windows.Input;
namespace Polka.Desktop.ViewModels;
public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    public RelayCommand(Func<Task> execute) : this(_ => execute())
    {
    }
    public RelayCommand(Func<object?, Task> execute) => _execute = execute;
    event EventHandler? ICommand.CanExecuteChanged
    {
        add { }
        remove { }
    }
    public bool CanExecute(object? parameter) => true;
    public async void Execute(object? parameter)
    {
        try
        {
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}
