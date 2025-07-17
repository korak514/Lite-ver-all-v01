using System;
using System.Windows.Input;

namespace WPF_LoginForm.ViewModels
{
    public class ViewModelCommand : ICommand
    {
        //Fields
        private readonly Action<object> _executeAction;
        private readonly Predicate<object> _canExecuteAction;

        //Constructors
        public ViewModelCommand(Action<object> executeAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = null;
        }

        public ViewModelCommand(Action<object> executeAction, Predicate<object> canExecuteAction)
        {
            _executeAction = executeAction;
            _canExecuteAction = canExecuteAction;
        }

        //Events
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        //Methods
        public bool CanExecute(object parameter)
        {
            return _canExecuteAction == null ? true : _canExecuteAction(parameter);
        }

        public void Execute(object parameter)
        {
            _executeAction(parameter);
        }

        // --- NEW METHOD ---
        /// <summary>
        /// Raises the CanExecuteChanged event to indicate that the CanExecute status has changed.
        /// UI elements bound to this command will re-evaluate their IsEnabled state.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // The CanExecuteChanged event is wired up to CommandManager.RequerySuggested.
            // Forcing a RequerySuggested event will trigger any subscribers.
            // A more direct way if not relying on CommandManager's global requery would be:
            // CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            // However, since WPF buttons and other command-aware controls subscribe
            // via CommandManager, forcing a requery is a common WPF pattern.
            // If you want to be absolutely sure without CommandManager, you'd invoke directly.
            // For now, let's stick to what CommandManager provides. If a direct invocation is needed
            // the event would need to be declared like: public event EventHandler ActualCanExecuteChanged;
            // and then Invoke(ActualCanExecuteChanged, ...)

            // Forcing a requery is the standard way when CanExecuteChanged is implemented as above.
            CommandManager.InvalidateRequerySuggested();
        }
    }
}