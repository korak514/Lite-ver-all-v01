// Models/SeriesConfiguration.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace WPF_LoginForm.Models
{
    public class SeriesConfiguration : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _columnName;

        public string ColumnName
        {
            get => _columnName;
            set { if (_columnName != value) { _columnName = value; OnPropertyChanged(); } }
        }

        public bool IsCombinationLabel { get; set; } = false;
        public string CustomDetailTitle { get; set; }
        public string LegendLabel { get; set; }

        [JsonIgnore]
        public List<string> PendingPreloadColumns { get; set; }
        public bool ShowOnlyHoverLabels { get; set; }
        public string TooltipDateFormat { get; set; } = "MMM yyyy";
        public bool IncludeSeriesName { get; set; } = true;
        public string SeriesColorHex { get; set; }

        // BUG FIX: Forces JSON to replace the default items instead of infinitely appending to them.
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<NodeFlowState> SavedStates { get; set; } = new List<NodeFlowState>
        {
            new NodeFlowState { StateName = "State 1" },
            new NodeFlowState { StateName = "State 2" },
            new NodeFlowState { StateName = "State 3" }
        };

        public int ActiveStateIndex { get; set; } = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NodeFlowState
    {
        public string StateName { get; set; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<FlowNode> Nodes { get; set; } = new List<FlowNode>();
    }

    public class FlowNode : INotifyPropertyChanged
    {
        public Guid NodeId { get; set; } = Guid.NewGuid();
        public string NodeType { get; set; }

        private string _value;

        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(); } }
        }

        private string _aggregation = "Sum";

        public string Aggregation
        {
            get => _aggregation;
            set { if (_aggregation != value) { _aggregation = value; OnPropertyChanged(); } }
        }

        private int _zone = 3;

        public int Zone
        {
            get => _zone;
            set { if (_zone != value) { _zone = value; OnPropertyChanged(); } }
        }

        public double X { get; set; }
        public double Y { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}