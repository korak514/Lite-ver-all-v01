// Views/AiAssistantWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Views
{
    public partial class AiAssistantWindow : Window
    {
        private AiAssistantViewModel _viewModel;

        public AiAssistantWindow(IDataRepository repository)
        {
            InitializeComponent();
            _viewModel = new AiAssistantViewModel(repository);
            this.DataContext = _viewModel;

            _viewModel.Messages.CollectionChanged += (s, e) =>
            {
                MessageScrollViewer?.Dispatcher.InvokeAsync(() =>
                {
                    MessageScrollViewer.ScrollToBottom();
                });
            };
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (_viewModel.SendCommand.CanExecute(null))
                    _viewModel.SendCommand.Execute(null);
            }
        }
    }
}
