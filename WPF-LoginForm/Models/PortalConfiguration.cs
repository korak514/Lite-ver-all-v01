using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class PortalButtonConfig
    {
        public int Id { get; set; }
        public string Title { get; set; } = "Unassigned";

        // The specific dashboard .json file this button will load
        public string DashboardFileName { get; set; } = "";

        public string Description { get; set; } = "Click to configure...";
    }

    public class PortalConfiguration
    {
        public List<PortalButtonConfig> Buttons { get; set; } = new List<PortalButtonConfig>();
    }
}