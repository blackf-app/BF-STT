using System.Windows.Input;

namespace BF_STT.ViewModels
{
    /// <summary>
    /// Avalonia-friendly RelayCommand. Avalonia has no `CommandManager.RequerySuggested`,
    /// so callers raise the static <see cref="RaiseCanExecuteChanged"/> when they want
    /// every live command to re-query its CanExecute.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private static event EventHandler? RequerySuggested;

        public static void RaiseCanExecuteChanged()
        {
            RequerySuggested?.Invoke(null, EventArgs.Empty);
        }

        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { RequerySuggested += value; }
            remove { RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);
    }
}
