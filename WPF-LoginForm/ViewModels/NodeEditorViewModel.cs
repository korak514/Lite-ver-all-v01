// ViewModels/NodeEditorViewModel.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
    public class NodeEditorViewModel : ViewModelBase
    {
        private SeriesConfiguration _series;
        public Action CloseAction { get; set; }

        public List<string> AvailableColumns { get; }
        public List<string> DateFormats { get; } = new List<string> { "MMM yyyy", "MMM yy", "yyyy", "dd.MM.yyyy", "MM/dd/yyyy", "dd MMM yyyy" };

        public ObservableCollection<FlowNode> HeaderFlow { get; } = new ObservableCollection<FlowNode>();
        public ObservableCollection<FlowNode> LeftFlow { get; } = new ObservableCollection<FlowNode>();
        public ObservableCollection<FlowNode> RightFlow { get; } = new ObservableCollection<FlowNode>();

        private int _selectedZone = 3;

        public int SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (SetProperty(ref _selectedZone, value))
                {
                    OnPropertyChanged(nameof(ActiveFlow));
                    OnPropertyChanged(nameof(ActiveZoneTitle));
                }
            }
        }

        public ObservableCollection<FlowNode> ActiveFlow
        {
            get
            {
                if (SelectedZone == 1) return HeaderFlow;
                if (SelectedZone == 2) return LeftFlow;
                return RightFlow;
            }
        }

        public string ActiveZoneTitle => $"EDITING: ZONE {SelectedZone}";

        private string _previewHeader; public string PreviewHeader { get => _previewHeader; set => SetProperty(ref _previewHeader, value); }
        private string _previewLeft; public string PreviewLeft { get => _previewLeft; set => SetProperty(ref _previewLeft, value); }
        private string _previewRight; public string PreviewRight { get => _previewRight; set => SetProperty(ref _previewRight, value); }

        private int _selectedStateIndex;

        public int SelectedStateIndex
        { get => _selectedStateIndex; set { if (SetProperty(ref _selectedStateIndex, value)) LoadState(value); } }

        public string CustomChartTitle { get; set; }
        public bool ShowOnlyHoverLabels { get; set; }

        public ICommand AddDataNodeCommand { get; }
        public ICommand AddTextNodeCommand { get; }
        public ICommand AddNewLineNodeCommand { get; }
        public ICommand AddDateNodeCommand { get; }
        public ICommand AddSeriesNameNodeCommand { get; }
        public ICommand RemoveNodeCommand { get; }
        public ICommand MoveNodeLeftCommand { get; }
        public ICommand MoveNodeRightCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SetZoneCommand { get; }

        public NodeEditorViewModel(SeriesConfiguration series, List<string> availableColumns)
        {
            _series = series;
            AvailableColumns = availableColumns?.Where(c => !c.Contains("[Combined]")).ToList() ?? new List<string>();

            AddDataNodeCommand = new ViewModelCommand(ExecuteAddDataNode);
            AddTextNodeCommand = new ViewModelCommand(p => ExecuteAddGenericNode("StaticText", " - "));
            AddNewLineNodeCommand = new ViewModelCommand(p => ExecuteAddGenericNode("NewLine", "⏎"));
            AddDateNodeCommand = new ViewModelCommand(p => ExecuteAddGenericNode("XAxis", "MMM yyyy"));
            AddSeriesNameNodeCommand = new ViewModelCommand(p => ExecuteAddGenericNode("SeriesName", "Label"));

            RemoveNodeCommand = new ViewModelCommand(ExecuteRemoveNode);
            MoveNodeLeftCommand = new ViewModelCommand(ExecuteMoveNodeLeft);
            MoveNodeRightCommand = new ViewModelCommand(ExecuteMoveNodeRight);
            SaveCommand = new ViewModelCommand(ExecuteSave);
            CancelCommand = new ViewModelCommand(ExecuteCancel);
            SetZoneCommand = new ViewModelCommand(p => { if (int.TryParse(p?.ToString(), out int z)) SelectedZone = z; });

            CustomChartTitle = _series.CustomDetailTitle;
            ShowOnlyHoverLabels = _series.ShowOnlyHoverLabels;

            SelectedStateIndex = _series.ActiveStateIndex;
            LoadState(SelectedStateIndex);
        }

        private void LoadState(int index)
        {
            // Unsubscribe existing nodes to prevent memory/JSON leaks
            foreach (var node in HeaderFlow.Concat(LeftFlow).Concat(RightFlow)) node.PropertyChanged -= OnNodePropertyChanged;

            HeaderFlow.Clear(); LeftFlow.Clear(); RightFlow.Clear();
            var state = _series.SavedStates[index];
            foreach (var node in state.Nodes)
            {
                var copy = new FlowNode { NodeType = node.NodeType, Value = node.Value, Aggregation = node.Aggregation, Zone = node.Zone <= 0 ? 3 : node.Zone };
                copy.PropertyChanged += OnNodePropertyChanged;

                if (copy.Zone == 1) HeaderFlow.Add(copy);
                else if (copy.Zone == 2) LeftFlow.Add(copy);
                else RightFlow.Add(copy);
            }
            GeneratePreviews();
        }

        private void ExecuteAddDataNode(object obj)
        {
            string colName = obj?.ToString() ?? (AvailableColumns.FirstOrDefault() ?? "Unknown");
            ExecuteAddGenericNode("DataColumn", colName);
        }

        private void ExecuteAddGenericNode(string type, string val)
        {
            var newNode = new FlowNode { NodeType = type, Value = val, Zone = SelectedZone };
            newNode.PropertyChanged += OnNodePropertyChanged;
            ActiveFlow.Add(newNode);
            GeneratePreviews();
        }

        private void ExecuteRemoveNode(object obj)
        {
            if (obj is FlowNode node)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                ActiveFlow.Remove(node);
                GeneratePreviews();
            }
        }

        private void ExecuteMoveNodeLeft(object obj)
        {
            if (obj is FlowNode node)
            {
                var flow = ActiveFlow;
                int idx = flow.IndexOf(node);
                if (idx > 0)
                {
                    flow.Move(idx, idx - 1);
                    GeneratePreviews();
                }
            }
        }

        private void ExecuteMoveNodeRight(object obj)
        {
            if (obj is FlowNode node)
            {
                var flow = ActiveFlow;
                int idx = flow.IndexOf(node);
                if (idx >= 0 && idx < flow.Count - 1)
                {
                    flow.Move(idx, idx + 1);
                    GeneratePreviews();
                }
            }
        }

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FlowNode.Value) || e.PropertyName == nameof(FlowNode.Aggregation))
            {
                GeneratePreviews();
            }
        }

        private string GetNodePreviewText(FlowNode node)
        {
            switch (node.NodeType)
            {
                case "StaticText": return node.Value;
                case "NewLine": return "\n";
                case "SeriesName": return "[Series Name]";
                case "XAxis":
                    var dummyDate = new DateTime(2025, 11, 15);
                    string fmt = string.IsNullOrWhiteSpace(node.Value) ? "MMM yyyy" : node.Value;
                    try { return dummyDate.ToString(fmt, new CultureInfo("tr-TR")); }
                    catch { return dummyDate.ToString("MMM yyyy"); }
                case "DataColumn": return "150K";
                default: return "";
            }
        }

        public void GeneratePreviews()
        {
            PreviewHeader = string.Join("", HeaderFlow.Select(GetNodePreviewText));
            PreviewLeft = string.Join("", LeftFlow.Select(GetNodePreviewText));
            PreviewRight = string.Join("", RightFlow.Select(GetNodePreviewText));
        }

        private void ExecuteSave(object obj)
        {
            var state = _series.SavedStates[SelectedStateIndex];
            var allNodes = HeaderFlow.Concat(LeftFlow).Concat(RightFlow).ToList();

            // Sever UI event bindings before saving so JSON Export doesn't crash on loops
            foreach (var node in allNodes)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
            }

            state.Nodes = allNodes;
            _series.ActiveStateIndex = SelectedStateIndex;
            _series.IsCombinationLabel = state.Nodes.Any();
            _series.CustomDetailTitle = CustomChartTitle;
            _series.ShowOnlyHoverLabels = ShowOnlyHoverLabels;

            if (_series.IsCombinationLabel) _series.ColumnName = $"[Combined] State {SelectedStateIndex + 1}";

            CloseAction?.Invoke();
        }

        private void ExecuteCancel(object obj)
        {
            foreach (var node in HeaderFlow.Concat(LeftFlow).Concat(RightFlow))
            {
                node.PropertyChanged -= OnNodePropertyChanged;
            }
            CloseAction?.Invoke();
        }
    }
}