using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties; // Required for Resources

namespace WPF_LoginForm.ViewModels
{
    public class ErrorDrillDownViewModel : ViewModelBase
    {
        private string _windowTitle;
        private ObservableCollection<ErrorLogItem> _errorList;

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public ObservableCollection<ErrorLogItem> ErrorList
        {
            get => _errorList;
            set
            {
                if (SetProperty(ref _errorList, value))
                {
                    OnPropertyChanged(nameof(RecordCount));
                    OnPropertyChanged(nameof(TotalDurationText));
                }
            }
        }

        public int RecordCount => ErrorList?.Count ?? 0;

        public string TotalDurationText
        {
            get
            {
                int total = ErrorList?.Sum(x => x.DurationMinutes) ?? 0;
                // Uses localized string (min/dk)
                return $"{total} {Resources.Unit_Minutes}";
            }
        }

        public ICommand CloseCommand { get; }

        public ErrorDrillDownViewModel(ObservableCollection<ErrorLogItem> items, string title)
        {
            ErrorList = items;
            WindowTitle = title;
        }
    }
}