// Models/ToolDefinition.cs
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Example { get; set; }
        public List<ToolParameter> Parameters { get; set; } = new List<ToolParameter>();
        public bool RequiresConfirmation { get; set; } = true;
    }

    public class ToolParameter
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; } = true;
        public string DefaultValue { get; set; }
    }
}
