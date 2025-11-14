// In WPF_LoginForm.ViewModels/ColumnSchemaViewModel.cs
using System.Collections.Generic;

namespace WPF_LoginForm.ViewModels
{
    public class ColumnSchemaViewModel : ViewModelBase
    {
        private string _sourceColumnName;
        public string SourceColumnName
        {
            get => _sourceColumnName;
            set => SetProperty(ref _sourceColumnName, value);
        }

        private string _destinationColumnName;
        public string DestinationColumnName
        {
            get => _destinationColumnName;
            set => SetProperty(ref _destinationColumnName, value);
        }

        private string _selectedDataType;
        public string SelectedDataType
        {
            get => _selectedDataType;
            set => SetProperty(ref _selectedDataType, value);
        }

        private bool _isPrimaryKey;
        public bool IsPrimaryKey
        {
            get => _isPrimaryKey;
            set => SetProperty(ref _isPrimaryKey, value);
        }

        public List<string> AvailableDataTypes { get; } = new List<string>
        {
            "Text (string)",
            "Number (int)",
            "Decimal (decimal)",
            "Date (datetime)"
        };
    }
}