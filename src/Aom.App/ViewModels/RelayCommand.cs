using System.Windows.Input;

namespace Aom.App.ViewModels;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;
    private readonly Predicate<T?>? canExecute;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (canExecute is null)
        {
            return true;
        }

        return canExecute((T?)parameter);
    }

    public void Execute(object? parameter)
    {
        execute((T?)parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}