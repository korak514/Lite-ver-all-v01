// Services/ToolRegistry.cs
using System.Collections.Generic;
using System.Text;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public class ToolRegistry
    {
        private static ToolRegistry _instance;
        public static ToolRegistry Instance => _instance ?? (_instance = new ToolRegistry());

        public List<ToolDefinition> Tools { get; }

        private ToolRegistry()
        {
            Tools = new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "ReadConfig",
                    Description = "Read the current application configuration. Returns all settings (DB connection, language, feature toggles, dashboard preferences, offline paths, AI settings) as JSON.",
                    Category = "Configuration",
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ReadConfig\"\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "WriteConfig",
                    Description = "Update a configuration setting. Shows the current value, the new value, and asks for confirmation before applying. After applying, tells the user to restart the app.",
                    Category = "Configuration",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "key", Type = "string", Description = "Setting name (e.g. DbHost, AppLanguage, AiAssistantEnabled, DefaultRowLimit)", Required = true },
                        new ToolParameter { Name = "value", Type = "string", Description = "New value for the setting", Required = true }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"WriteConfig\",\n  \"args\": {\n    \"key\": \"DbHost\",\n    \"value\": \"192.168.1.100\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                },
                new ToolDefinition
                {
                    Name = "ExportConfig",
                    Description = "Export the current configuration to a JSON file at a chosen location.",
                    Category = "Configuration",
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ExportConfig\"\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                },
                new ToolDefinition
                {
                    Name = "ListTables",
                    Description = "List all available database tables. Returns table names and row counts if available.",
                    Category = "Database",
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ListTables\"\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "ReadTable",
                    Description = "Query a database table and return its data. Shows column names and up to 50 rows.",
                    Category = "Database",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Name of the table to query", Required = true },
                        new ToolParameter { Name = "limit", Type = "int", Description = "Maximum rows to return (default: 50)", Required = false, DefaultValue = "50" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ReadTable\",\n  \"args\": {\n    \"tableName\": \"users\",\n    \"limit\": \"20\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GetTableSchema",
                    Description = "Get column names, types, and nullable info for a database table.",
                    Category = "Database",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Name of the table", Required = true }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetTableSchema\",\n  \"args\": {\n    \"tableName\": \"events\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GetDashboardData",
                    Description = "Get dashboard chart data. Returns available series, date ranges, and latest values.",
                    Category = "Dashboard",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "chartName", Type = "string", Description = "Optional chart name to filter", Required = false, DefaultValue = "all" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetDashboardData\",\n  \"args\": {\n    \"chartName\": \"production\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GetTimelineData",
                    Description = "Get timeline event data for a specific date. Returns events with time, duration, machine code, and description.",
                    Category = "Timeline",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "date", Type = "string", Description = "Date in yyyy-MM-dd format", Required = true },
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Timeline ruler/table name", Required = false, DefaultValue = "default" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetTimelineData\",\n  \"args\": {\n    \"date\": \"2025-01-15\",\n    \"tableName\": \"ruler_a\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GetErrorSummary",
                    Description = "Get a summary of errors for a date range. Returns error counts by category, machine, and type.",
                    Category = "Timeline",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "startDate", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "endDate", Type = "string", Description = "End date (yyyy-MM-dd), defaults to startDate", Required = false }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetErrorSummary\",\n  \"args\": {\n    \"startDate\": \"2025-01-01\",\n    \"endDate\": \"2025-01-31\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GenerateReport",
                    Description = "Generate a timeline or error report. Opens the report setup dialog or exports to file.",
                    Category = "Reports",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "type", Type = "string", Description = "Report type: 'timeline', 'error_summary', 'monthly'", Required = true },
                        new ToolParameter { Name = "dateFrom", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "dateTo", Type = "string", Description = "End date (yyyy-MM-dd)", Required = false }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GenerateReport\",\n  \"args\": {\n    \"type\": \"monthly\",\n    \"dateFrom\": \"2025-01-01\",\n    \"dateTo\": \"2025-01-31\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                },
                new ToolDefinition
                {
                    Name = "OpenView",
                    Description = "Open a specific view or window in the application.",
                    Category = "Navigation",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "viewName", Type = "string", Description = "View name: 'settings', 'timeline', 'dashboard', 'users', 'help'", Required = true }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"OpenView\",\n  \"args\": {\n    \"viewName\": \"settings\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "GetSystemStatus",
                    Description = "Get current system status: online/offline mode, database type, connected user, app version.",
                    Category = "System",
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetSystemStatus\"\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "ShowMessage",
                    Description = "Show a message box to the user with the given text and optional title.",
                    Category = "Utility",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "text", Type = "string", Description = "Message text to display", Required = true },
                        new ToolParameter { Name = "title", Type = "string", Description = "Message box title", Required = false, DefaultValue = "AI Assistant" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ShowMessage\",\n  \"args\": {\n    \"text\": \"Report generated successfully!\",\n    \"title\": \"Success\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "ExportTableData",
                    Description = "Export a database table to CSV or Excel format.",
                    Category = "Database",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Name of the table to export", Required = true },
                        new ToolParameter { Name = "format", Type = "string", Description = "Export format: 'csv' or 'xlsx'", Required = false, DefaultValue = "csv" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ExportTableData\",\n  \"args\": {\n    \"tableName\": \"production_log\",\n    \"format\": \"xlsx\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                },
                new ToolDefinition
                {
                    Name = "GetLongestError",
                    Description = "Find the error with the longest duration in a date range. Returns the full error-code string, duration in minutes, timestamp, and description.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Main production table name", Required = true },
                        new ToolParameter { Name = "startDate", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "endDate", Type = "string", Description = "End date (yyyy-MM-dd), defaults to startDate", Required = false },
                        new ToolParameter { Name = "section", Type = "string", Description = "Optional section filter (e.g. MA, MB)", Required = false }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetLongestError\",\n  \"args\": {\n    \"tableName\": \"AnaTablo\",\n    \"startDate\": \"2026-01-01\",\n    \"endDate\": \"2026-01-31\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "SearchErrorCodes",
                    Description = "Search all error codes in a date range by free-text query. Returns matching error codes with durations and timestamps.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Main production table name", Required = true },
                        new ToolParameter { Name = "query", Type = "string", Description = "Free-text search (e.g. TIKANMA, TEMIZLIK, ISKELET)", Required = true },
                        new ToolParameter { Name = "startDate", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "endDate", Type = "string", Description = "End date (yyyy-MM-dd), defaults to startDate", Required = false },
                        new ToolParameter { Name = "limit", Type = "int", Description = "Max results to return", Required = false, DefaultValue = "20" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"SearchErrorCodes\",\n  \"args\": {\n    \"tableName\": \"AnaTablo\",\n    \"query\": \"TIKANMA\",\n    \"startDate\": \"2026-01-01\",\n    \"endDate\": \"2026-01-31\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "AggregateErrorDurations",
                    Description = "Aggregate errors by type or section in a date range. Returns max duration, total duration, and occurrence count per group.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Main production table name", Required = true },
                        new ToolParameter { Name = "startDate", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "endDate", Type = "string", Description = "End date (yyyy-MM-dd), defaults to startDate", Required = false },
                        new ToolParameter { Name = "groupBy", Type = "string", Description = "Grouping: 'description' (default), 'section', or 'code'", Required = false, DefaultValue = "description" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"AggregateErrorDurations\",\n  \"args\": {\n    \"tableName\": \"AnaTablo\",\n    \"startDate\": \"2026-01-01\",\n    \"endDate\": \"2026-01-31\",\n    \"groupBy\": \"description\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "ExportErrorLog",
                    Description = "Export parsed error data to a CSV file with columns: Date, Shift, Time, DurationMinutes, Section, Code, Description.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Main production table name", Required = true },
                        new ToolParameter { Name = "startDate", Type = "string", Description = "Start date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "endDate", Type = "string", Description = "End date (yyyy-MM-dd), defaults to startDate", Required = false },
                        new ToolParameter { Name = "format", Type = "string", Description = "Export format: 'csv' (default) or 'xlsx'", Required = false, DefaultValue = "csv" }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"ExportErrorLog\",\n  \"args\": {\n    \"tableName\": \"AnaTablo\",\n    \"startDate\": \"2026-01-01\",\n    \"endDate\": \"2026-01-31\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                },
                new ToolDefinition
                {
                    Name = "GetErrorTimeline",
                    Description = "Get a chronological timeline of all errors for a specific day. Each entry shows time, duration, section, code, and description.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "tableName", Type = "string", Description = "Main production table name", Required = true },
                        new ToolParameter { Name = "date", Type = "string", Description = "Date (yyyy-MM-dd)", Required = true },
                        new ToolParameter { Name = "section", Type = "string", Description = "Optional section filter (e.g. MA, MB)", Required = false }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"GetErrorTimeline\",\n  \"args\": {\n    \"tableName\": \"AnaTablo\",\n    \"date\": \"2026-01-15\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = false
                },
                new ToolDefinition
                {
                    Name = "CreateErrorAlertRule",
                    Description = "Register an alert rule that watches for errors exceeding a duration threshold in a section. The system will surface an alert when triggered.",
                    Category = "Error Analytics",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "section", Type = "string", Description = "Section to monitor (e.g. MA, MB, or 'all')", Required = true },
                        new ToolParameter { Name = "durationThreshold", Type = "int", Description = "Duration in minutes — errors longer than this trigger the alert", Required = true },
                        new ToolParameter { Name = "notifyEmail", Type = "string", Description = "Email to notify when alert fires", Required = false }
                    },
                    Example = "[AI_BRIDGE]\n{\n  \"tool\": \"CreateErrorAlertRule\",\n  \"args\": {\n    \"section\": \"MA\",\n    \"durationThreshold\": \"120\",\n    \"notifyEmail\": \"operator@factory.com\"\n  }\n}\n[/AI_BRIDGE]",
                    RequiresConfirmation = true
                }
            };
        }

        public string GenerateToolDocs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# AI Bridge Tool Documentation");
            sb.AppendLine();
            sb.AppendLine("You are an AI assistant integrated into a manufacturing analytics application. When the user asks you to perform an action, respond with natural language AND a tool command using the format below.");
            sb.AppendLine();
            sb.AppendLine("## Tool Command Format");
            sb.AppendLine("Every tool call must be wrapped in [AI_BRIDGE] tags with **valid JSON** inside:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("[AI_BRIDGE]");
            sb.AppendLine("{");
            sb.AppendLine("  \"tool\": \"ToolName\",");
            sb.AppendLine("  \"args\": {");
            sb.AppendLine("    \"param1\": \"value1\",");
            sb.AppendLine("    \"param2\": \"value2\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("[/AI_BRIDGE]");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("If the tool takes no parameters, omit `args`:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("[AI_BRIDGE]");
            sb.AppendLine("{");
            sb.AppendLine("  \"tool\": \"ToolName\"");
            sb.AppendLine("}");
            sb.AppendLine("[/AI_BRIDGE]");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("You can include multiple tool calls in one response. Always explain in natural language what you're doing.");
            sb.AppendLine();
            sb.AppendLine("## Available Tools");
            sb.AppendLine();

            string currentCategory = null;
            foreach (var tool in Tools)
            {
                if (tool.Category != currentCategory)
                {
                    currentCategory = tool.Category;
                    sb.AppendLine($"### {currentCategory}");
                    sb.AppendLine();
                }

                sb.AppendLine($"**{tool.Name}**");
                sb.AppendLine($"- {tool.Description}");
                if (tool.RequiresConfirmation)
                    sb.AppendLine("- ⚠️ Requires user confirmation before executing");

                if (tool.Parameters.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("| Parameter | Type | Required | Default | Description |");
                    sb.AppendLine("|-----------|------|----------|---------|-------------|");
                    foreach (var param in tool.Parameters)
                    {
                        string req = param.Required ? "Yes" : "No";
                        string def = string.IsNullOrEmpty(param.DefaultValue) ? "-" : param.DefaultValue;
                        sb.AppendLine($"| `{param.Name}` | {param.Type} | {req} | {def} | {param.Description} |");
                    }
                }
                sb.AppendLine();
                sb.AppendLine("Example:");
                sb.AppendLine($"```");
                sb.AppendLine(tool.Example);
                sb.AppendLine($"```");
                sb.AppendLine();
            }

            sb.AppendLine("## Rules");
            sb.AppendLine("1. **Only use tools listed above.** Do not invent new tool names.");
            sb.AppendLine("2. Always explain what you are doing in natural language before the tool call.");
            sb.AppendLine("3. For destructive actions (write, delete, modify), always wait for user confirmation.");
            sb.AppendLine("4. If you need a parameter value, ask the user for it.");
            sb.AppendLine("5. You can chain multiple tools in one response if they are related.");
            sb.AppendLine("6. The content inside [AI_BRIDGE] tags must be **valid JSON**.");
            sb.AppendLine("7. Tool names are case-sensitive. Use exact names from the table above.");
            sb.AppendLine("8. When configuration is changed, always tell the user to restart or refresh the application.");
            sb.AppendLine("9. All parameter values must be strings, even numbers.");

            return sb.ToString();
        }

        public ToolDefinition FindTool(string name)
        {
            return Tools.Find(t => t.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        }

        public string GetCategoriesSummary()
        {
            var categories = new Dictionary<string, List<string>>();
            foreach (var tool in Tools)
            {
                if (!categories.ContainsKey(tool.Category))
                    categories[tool.Category] = new List<string>();
                categories[tool.Category].Add(tool.Name);
            }

            var sb = new StringBuilder();
            sb.AppendLine("**Available Tool Categories:**");
            foreach (var kvp in categories)
            {
                sb.AppendLine($"- **{kvp.Key}**: {string.Join(", ", kvp.Value)}");
            }
            return sb.ToString();
        }
    }
}
