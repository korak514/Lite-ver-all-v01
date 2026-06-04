using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public enum ChatMessageType
    {
        User,
        Assistant,
        System,
        ToolCall,
        ToolResult
    }

    public class ChatMessage : ViewModelBase
    {
        private string _content;
        private ChatMessageType _type;
        private DateTime _timestamp;
        private bool _showActions;
        private string _executionStatus;

        public string Content { get => _content; set => SetProperty(ref _content, value); }
        public ChatMessageType Type { get => _type; set => SetProperty(ref _type, value); }
        public DateTime Timestamp { get => _timestamp; set => SetProperty(ref _timestamp, value); }
        public string TimestampStr => Timestamp.ToString("HH:mm:ss");
        public bool IsUser => Type == ChatMessageType.User;
        public bool IsToolCall => Type == ChatMessageType.ToolCall;
        public bool IsSystem => Type == ChatMessageType.System;
        public bool IsToolResult => Type == ChatMessageType.ToolResult;
        public bool ShowActions { get => _showActions; set => SetProperty(ref _showActions, value); }
        public string ExecutionStatus { get => _executionStatus; set { SetProperty(ref _executionStatus, value); OnPropertyChanged(nameof(StatusColor)); } }
        public string StatusColor => ExecutionStatus == "executed" ? "#1ABC9C" : ExecutionStatus == "failed" ? "#D14545" : "#F39C12";

        public string ToolName { get; set; }
        public Dictionary<string, string> ToolParameters { get; set; }
        public string ToolParamsDisplay => ToolParameters != null
            ? string.Join(", ", ToolParameters.Select(kv => $"{kv.Key}=\"{kv.Value}\""))
            : "";

        public ICommand ExecuteToolCommand { get; set; }
        public ICommand CancelToolCommand { get; set; }
        public ICommand CopyMessageCommand { get; set; }
    }

    public class AiAssistantViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        private string _inputText;
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                    (SendCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand SendCommand { get; }

        private readonly string _toolDocs;
        private string _pendingUserInput;
        private IAiService _aiService;
        private readonly List<AiMessage> _conversationHistory = new List<AiMessage>();
        private const int MaxHistoryExchanges = 20;

        public AiAssistantViewModel() : this(null) { }

        public AiAssistantViewModel(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            _toolDocs = ToolRegistry.Instance.GenerateToolDocs();

            SendCommand = new ViewModelCommand(ExecuteSend, p => !IsProcessing);

            var settings = GeneralSettingsManager.Instance.Current;
            string apiKey = settings.AiApiKey;
            string provider = settings.AiProvider;
            if (!string.IsNullOrWhiteSpace(apiKey))
                _aiService = AiServiceFactory.Create(provider, apiKey);

            string providerName = FormatProviderName(provider);
            StatusText = _aiService != null
                ? $"AI Commander ({providerName}) online • Bridge toolbox ready. " + ToolRegistry.Instance.GetCategoriesSummary()
                : $"⚠️ Set your API key in Settings to enable the AI Commander. " + ToolRegistry.Instance.GetCategoriesSummary();

            AddAssistantMessage(
                $"**AI Commander** — {providerName} online • Bridge toolbox ready\n\n" +
                "**Slash commands** (local, no AI):\n" +
                "- `/help` — available commands\n" +
                "- `/tables` — list tables\n" +
                "- `/config` — show config\n" +
                "- `/status` — system status\n" +
                "- `/read <table> [n]` — read rows locally\n" +
                "- `/schema <table>` — table schema\n" +
                "- `/clear` — reset conversation\n\n" +
                (_aiService != null
                    ? "**Online AI** is in control. Everything else goes to the AI Commander.\n" +
                      "Try: \"find the longest error in table 9\" or \"what machines had issues yesterday?\"\n"
                    : "⚠️ **Set your API key in Settings** to enable the AI Commander.\n") +
                "\nWhat do you need?");
        }

        private void ExecuteSend(object parameter)
        {
            string text = InputText?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            InputText = "";
            _pendingUserInput = text;
            AddUserMessage(text);
            ProcessWithAiBridge(text);
        }

        private async void ProcessWithAiBridge(string userInput)
        {
            IsProcessing = true;

            if (userInput.StartsWith("/"))
            {
                HandleSlashCommand(userInput);
                IsProcessing = false;
                return;
            }

            if (_aiService == null)
            {
                var settings = GeneralSettingsManager.Instance.Current;
                string apiKey = settings.AiApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    AddSystemMessage("❌ No API key configured. Go to Settings to enter one.");
                    StatusText = "API key missing.";
                    IsProcessing = false;
                    return;
                }
                _aiService = AiServiceFactory.Create(settings.AiProvider, apiKey);
                if (_aiService == null)
                {
                    AddSystemMessage($"❌ Failed to create AI service for provider '{settings.AiProvider}'.");
                    StatusText = "Provider error.";
                    IsProcessing = false;
                    return;
                }
            }

            string providerName = FormatProviderName(GeneralSettingsManager.Instance.Current.AiProvider);
            AddSystemMessage("⏳ Thinking...");
            StatusText = $"Calling {providerName} API...";

            try
            {
                var messages = new List<AiMessage>();
                messages.Add(new AiMessage("system", BuildSystemPrompt()));
                messages.AddRange(_conversationHistory);
                messages.Add(new AiMessage("user", userInput));

                _conversationHistory.Add(new AiMessage("user", userInput));

                if (_conversationHistory.Count > MaxHistoryExchanges * 2)
                    _conversationHistory.RemoveRange(0, _conversationHistory.Count - MaxHistoryExchanges * 2);

                int iteration = 0;
                const int maxIterations = 5;
                bool taskComplete = false;
                string finalResponse = null;

                while (iteration < maxIterations && !taskComplete)
                {
                    string aiResponse = await _aiService.AskAsync(messages);

                    if (aiResponse.StartsWith("API Error"))
                    {
                        AddSystemMessage($"❌ {aiResponse}");
                        StatusText = "API error.";
                        IsProcessing = false;
                        return;
                    }

                    var toolCalls = ParseToolCalls(aiResponse);
                    string naturalLanguage = StripToolCalls(aiResponse);

                    if (toolCalls.Count == 0)
                    {
                        finalResponse = naturalLanguage;
                        taskComplete = true;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(naturalLanguage))
                    {
                        if (iteration == 0)
                            AddAssistantMessage(naturalLanguage.Trim());
                        messages.Add(new AiMessage("assistant", naturalLanguage.Trim()));
                    }

                    foreach (var call in toolCalls)
                    {
                        AddToolCallMessage(call);
                        string result = await AutoExecuteToolAsync(call);
                        messages.Add(new AiMessage("assistant", $"[TOOL_RESULT: {call["_toolName"]}]\n{result}"));
                    }

                    messages.Add(new AiMessage("user", "Continue based on the tool results above. If the task is complete, prefix your response with [TASK_COMPLETE] and answer the user. If more work is needed, issue more tool commands."));
                    StatusText = $"AI processing ({iteration + 2})...";
                    iteration++;
                }

                if (!string.IsNullOrWhiteSpace(finalResponse))
                {
                    string clean = finalResponse.Trim();
                    string display = clean.Replace("[TASK_COMPLETE]", "").Trim();
                    if (!string.IsNullOrWhiteSpace(display))
                    {
                        AddAssistantMessage(display);
                        _conversationHistory.Add(new AiMessage("assistant", clean));
                    }
                }

                if (iteration >= maxIterations && !taskComplete)
                    AddSystemMessage("⚠️ Maximum iterations reached. Task may be incomplete.");

                string name = FormatProviderName(GeneralSettingsManager.Instance.Current.AiProvider);
                StatusText = taskComplete ? $"{name} completed." : $"{name} finished.";
            }
            catch (Exception ex)
            {
                string name = FormatProviderName(GeneralSettingsManager.Instance.Current.AiProvider);
                AddSystemMessage($"❌ {name} error: {ex.Message}");
                StatusText = $"{name} error.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void HandleSlashCommand(string input)
        {
            string lower = input.ToLowerInvariant().Trim();
            string[] parts = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts.Length > 0 ? parts[0] : lower;

            switch (cmd)
            {
                case "/help":
                    var sb = new StringBuilder();
                    sb.AppendLine("**Bridge Slash Commands** (local, no AI)\n");
                    sb.AppendLine("| Command | Description |");
                    sb.AppendLine("|---------|-------------|");
                    sb.AppendLine("| `/help` | Show this help |");
                    sb.AppendLine("| `/tables` | List all database tables |");
                    sb.AppendLine("| `/config` | Show current configuration |");
                    sb.AppendLine("| `/status` | Show system status |");
                    sb.AppendLine("| `/read <table> [n]` | Read first n rows from table (default 3) |");
                    sb.AppendLine("| `/schema <table>` | Show table schema |");
                    sb.AppendLine("| `/clear` | Clear conversation history |");
                    sb.AppendLine();
                    sb.AppendLine("*Everything else is sent to the online AI Commander.*");
                    AddAssistantMessage(sb.ToString());
                    StatusText = "Help shown.";
                    return;

                case "/tables":
                    var listCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["_toolName"] = "ListTables" };
                    AddToolCallMessage(listCall);
                    StatusText = "Listing tables...";
                    return;

                case "/config":
                    var configCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["_toolName"] = "ReadConfig" };
                    AddToolCallMessage(configCall);
                    StatusText = "Reading config...";
                    return;

                case "/status":
                    var statusCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["_toolName"] = "GetSystemStatus" };
                    AddToolCallMessage(statusCall);
                    StatusText = "System status...";
                    return;

                case "/read":
                    {
                        string table = parts.Length > 1 ? parts[1] : "";
                        int limit = parts.Length > 2 && int.TryParse(parts[2], out int l) ? l : 3;
                        if (string.IsNullOrWhiteSpace(table))
                        {
                            AddSystemMessage("Usage: `/read <table> [limit]` — e.g. `/read users 5`");
                            return;
                        }
                        var readCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["_toolName"] = "ReadTable",
                            ["tablename"] = table,
                            ["limit"] = limit.ToString()
                        };
                        AddToolCallMessage(readCall);
                        StatusText = $"Reading table `{table}`...";
                        return;
                    }

                case "/schema":
                    {
                        string table = parts.Length > 1 ? parts[1] : "";
                        if (string.IsNullOrWhiteSpace(table))
                        {
                            AddSystemMessage("Usage: `/schema <table>` — e.g. `/schema users`");
                            return;
                        }
                        var schemaCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["_toolName"] = "GetTableSchema",
                            ["tablename"] = table
                        };
                        AddToolCallMessage(schemaCall);
                        StatusText = $"Reading schema for `{table}`...";
                        return;
                    }

                case "/clear":
                    _conversationHistory.Clear();
                    Messages.Clear();
                    AddAssistantMessage("🧹 Conversation cleared. Start fresh!");
                    StatusText = "Conversation cleared.";
                    return;

                default:
                    AddSystemMessage($"Unknown command: `{cmd}`. Type `/help` for available commands.");
                    StatusText = "Unknown command.";
                    return;
            }
        }

        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are the AI commander of a manufacturing analytics application.");
            sb.AppendLine("The local 'Bridge' system is YOUR toolbox — it executes the tool commands you issue.");
            sb.AppendLine("You are in full control. Decide what needs to be done, then command the Bridge to do it.");
            sb.AppendLine();
            sb.AppendLine("## Architecture");
            sb.AppendLine("- You (the online AI) are the decision maker.");
            sb.AppendLine("- The Bridge is a local tool executor — it has no initiative of its own.");
            sb.AppendLine("- You analyze the user's request, plan the steps, and issue tool commands.");
            sb.AppendLine("- The Bridge carries them out and the results come back to you.");
            sb.AppendLine();
            sb.AppendLine("## Available Tools (your toolbox)");
            sb.AppendLine();
            sb.AppendLine(_toolDocs);
            sb.AppendLine();
            sb.AppendLine("## How to command the Bridge");
            sb.AppendLine("1. First, explain your plan to the user in natural language.");
            sb.AppendLine("2. Then, issue one or more tool commands wrapped in [AI_BRIDGE] tags.");
            sb.AppendLine("3. Each [AI_BRIDGE] block MUST contain valid JSON with a \"tool\" field and optional \"args\".");
            sb.AppendLine("4. You can issue multiple commands — they will be executed in order.");
            sb.AppendLine();
            sb.AppendLine("## Tool Command Format");
            sb.AppendLine("[AI_BRIDGE]");
            sb.AppendLine("{");
            sb.AppendLine("  \"tool\": \"ReadTable\",");
            sb.AppendLine("  \"args\": {");
            sb.AppendLine("    \"tableName\": \"users\",");
            sb.AppendLine("    \"limit\": \"20\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("[/AI_BRIDGE]");
            sb.AppendLine();
            sb.AppendLine("## Feedback Loop Protocol");
            sb.AppendLine("- You issue tool commands → Bridge executes them → results come back → you continue.");
            sb.AppendLine("- After the Bridge executes your tools, you will see:");
            sb.AppendLine("  `[TOOL_RESULT: ToolName]` followed by the output.");
            sb.AppendLine("- Then a message \"Continue...\" will be sent to you.");
            sb.AppendLine("- Analyze the tool result and decide:");
            sb.AppendLine("  1. Task is complete → prefix your final response with [TASK_COMPLETE] and answer the user.");
            sb.AppendLine("  2. More work is needed → issue more tool commands.");
            sb.AppendLine("- Do NOT repeat the same tool command — check what already ran.");
            sb.AppendLine("- You can issue multiple tools in one turn if multiple steps are needed.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("You are the one in control. Analyze, plan, command the Bridge, and report back to the user.");

            return sb.ToString();
        }

        private List<Dictionary<string, string>> ParseToolCalls(string response)
        {
            var results = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(response)) return results;

            var blockPattern = new Regex(@"\[AI_BRIDGE\](.*?)\[/AI_BRIDGE\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = blockPattern.Matches(response);

            foreach (Match match in matches)
            {
                string jsonBlock = match.Groups[1].Value.Trim();
                var toolCall = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    JObject obj = JObject.Parse(jsonBlock);

                    JToken toolToken = obj["tool"];
                    if (toolToken == null) continue;
                    toolCall["_toolName"] = toolToken.ToString();

                    JToken argsToken = obj["args"];
                    if (argsToken is JObject argsObj)
                    {
                        foreach (var prop in argsObj.Properties())
                        {
                            toolCall[prop.Name] = prop.Value?.ToString() ?? "";
                        }
                    }
                }
                catch (JsonException)
                {
                    continue;
                }

                if (toolCall.ContainsKey("_toolName"))
                    results.Add(toolCall);
            }

            return results;
        }

        private string StripToolCalls(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return "";
            string cleaned = Regex.Replace(response, @"\[AI_BRIDGE\].*?\[/AI_BRIDGE\]", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return Regex.Replace(cleaned, @"```(?:json)?\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        private static string GetParam(Dictionary<string, string> dict, string key)
        {
            string val;
            return dict.TryGetValue(key, out val) ? val : null;
        }

        private static string FormatProviderName(string provider)
        {
            switch ((provider ?? "").ToLowerInvariant())
            {
                case "xai": return "xAI (Grok)";
                case "openrouter": return "OpenRouter";
                default: return "Gemini";
            }
        }

        private void ExecuteToolAction(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (toolCall == null || !toolCall.ContainsKey("_toolName"))
            {
                msg.ExecutionStatus = "failed";
                msg.Content = "Error: Invalid tool call (missing tool name).";
                return;
            }

            string toolName = toolCall["_toolName"];
            msg.ExecutionStatus = "executing";

            try
            {
                switch (toolName.ToLowerInvariant())
                {
                    case "readconfig":
                        ExecuteReadConfig(msg);
                        break;
                    case "writeconfig":
                        ExecuteWriteConfig(toolCall, msg);
                        break;
                    case "listtables":
                        ExecuteListTables(msg);
                        break;
                    case "readtable":
                        ExecuteReadTable(toolCall, msg);
                        break;
                    case "getsystemstatus":
                        ExecuteGetSystemStatus(msg);
                        break;
                    case "showmessage":
                        ExecuteShowMessage(toolCall, msg);
                        break;
                    case "openview":
                        ExecuteOpenView(toolCall, msg);
                        break;
                    case "gettablechema":
                        ExecuteGetTableSchema(toolCall, msg);
                        break;
                    case "exportconfig":
                        ExecuteExportConfig(msg);
                        break;
                    case "getdashboarddata":
                        ExecuteGetDashboardData(msg);
                        break;
                    case "gettimelinedata":
                        ExecuteGetTimelineData(toolCall, msg);
                        break;
                    case "geterrorsummary":
                        ExecuteGetErrorSummary(toolCall, msg);
                        break;
                    case "generatereport":
                        ExecuteGenerateReport(toolCall, msg);
                        break;
                    case "exporttabledata":
                        ExecuteExportTableData(toolCall, msg);
                        break;
                    case "getlongesterror":
                        ExecuteGetLongestError(toolCall, msg);
                        break;
                    case "searcherrorcodes":
                        ExecuteSearchErrorCodes(toolCall, msg);
                        break;
                    case "aggregateerrordurations":
                        ExecuteAggregateErrorDurations(toolCall, msg);
                        break;
                    case "exporterrorlog":
                        ExecuteExportErrorLog(toolCall, msg);
                        break;
                    case "geterrortimeline":
                        ExecuteGetErrorTimelineV2(toolCall, msg);
                        break;
                    case "createerroralertrule":
                        ExecuteCreateErrorAlertRule(toolCall, msg);
                        break;
                    default:
                        msg.Content = $"Tool `{toolName}` is defined but not yet implemented.";
                        msg.ExecutionStatus = "pending";
                        break;
                }
            }
            catch (Exception ex)
            {
                msg.Content = $"Error executing `{toolName}`: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }

            msg.ShowActions = false;
            msg.OnPropertyChanged(nameof(msg.Content));
            msg.OnPropertyChanged(nameof(msg.ShowActions));
            msg.OnPropertyChanged(nameof(msg.ExecutionStatus));
        }

        private async Task<string> AutoExecuteToolAsync(Dictionary<string, string> toolCall)
        {
            if (toolCall == null || !toolCall.ContainsKey("_toolName"))
                return "Error: Invalid tool call (missing tool name).";

            var tempMsg = new ChatMessage();

            string toolName = toolCall["_toolName"].ToLowerInvariant();
            bool isAsync = toolName == "listtables" || toolName == "readtable" || toolName == "gettablechema"
                || toolName == "getdashboarddata" || toolName == "gettimelinedata" || toolName == "geterrorsummary" || toolName == "exporttabledata"
                || toolName == "getlongesterror" || toolName == "searcherrorcodes" || toolName == "aggregateerrordurations"
                || toolName == "exporterrorlog" || toolName == "geterrortimeline" || toolName == "createerroralertrule";

            if (isAsync)
            {
                var tcs = new TaskCompletionSource<string>();
                PropertyChangedEventHandler handler = null;
                handler = (s, e) =>
                {
                    if (e.PropertyName == nameof(ChatMessage.ExecutionStatus) && tempMsg.ExecutionStatus != "executing" && tempMsg.ExecutionStatus != null)
                    {
                        tempMsg.PropertyChanged -= handler;
                        tcs.TrySetResult(tempMsg.Content);
                    }
                };
                tempMsg.PropertyChanged += handler;
                ExecuteToolAction(toolCall, tempMsg);
                return await tcs.Task;
            }
            else
            {
                ExecuteToolAction(toolCall, tempMsg);
                return tempMsg.Content;
            }
        }

        private void ExecuteReadConfig(ChatMessage msg)
        {
            var config = GeneralSettingsManager.Instance.Current;
            string path = GeneralSettingsManager.Instance.GetResolvedConfigPath();

            var sb = new StringBuilder();
            sb.AppendLine("**Current Configuration:**\n");
            sb.AppendLine("```json");
            sb.AppendLine(JsonConvert.SerializeObject(config, Formatting.Indented));
            sb.AppendLine("```");
            sb.AppendLine($"\n*File: `{path}`*");

            msg.Content = sb.ToString();
            msg.ExecutionStatus = "executed";
        }

        private void ExecuteWriteConfig(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (!toolCall.ContainsKey("key") || !toolCall.ContainsKey("value"))
            {
                msg.Content = "Error: Missing required parameters 'key' and 'value'.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string key = toolCall["key"];
            string value = toolCall["value"];

            var config = GeneralSettingsManager.Instance.Current;
            var prop = typeof(GeneralSettings).GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
            {
                msg.Content = $"Error: Property '{key}' not found in configuration.";
                msg.ExecutionStatus = "failed";
                return;
            }

            object oldValue = prop.GetValue(config);
            object newValue;
            try
            {
                Type propType = prop.PropertyType;
                if (propType == typeof(string)) newValue = value;
                else if (propType == typeof(int)) newValue = int.Parse(value);
                else if (propType == typeof(bool)) newValue = bool.Parse(value);
                else newValue = Convert.ChangeType(value, propType);
            }
            catch
            {
                msg.Content = $"Error: Cannot convert '{value}' to type {prop.PropertyType.Name}.";
                msg.ExecutionStatus = "failed";
                return;
            }

            prop.SetValue(config, newValue);
            GeneralSettingsManager.Instance.Save();

            var sb = new StringBuilder();
            sb.AppendLine($"✅ **Configuration Updated**\n");
            sb.AppendLine($"| Property | Before | After |");
            sb.AppendLine($"|----------|--------|-------|");
            sb.AppendLine($"| `{key}` | `{oldValue ?? "(null)"}` | `{newValue}` |");
            sb.AppendLine($"\n**Important**: Please restart/refresh the application for changes to take full effect.");

            msg.Content = sb.ToString();
            msg.ExecutionStatus = "executed";
        }

        private async void ExecuteListTables(ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            try
            {
                var tables = await _dataRepository.GetTableNamesAsync(true);
                if (tables == null || tables.Count == 0)
                {
                    msg.Content = "No tables found in the database.";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"**Available Tables** ({tables.Count} total)\n");
                sb.AppendLine("| # | Table Name |");
                sb.AppendLine("|---|-----------|");
                int i = 1;
                foreach (var t in tables)
                    sb.AppendLine($"| {i++} | `{t}` |");
                sb.AppendLine("\n*Tip: reference a table by name or number in your next request.*");

                msg.Content = sb.ToString();
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error listing tables: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private async void ExecuteReadTable(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string tableName = toolCall.ContainsKey("tablename") ? toolCall["tablename"] : "unknown";
            int limit = toolCall.ContainsKey("limit") && int.TryParse(toolCall["limit"], out int l) ? l : 50;

            try
            {
                var result = await _dataRepository.GetTableDataAsync(tableName, limit);
                if (result.Data == null || result.Data.Rows.Count == 0)
                {
                    msg.Content = $"Table `{tableName}` is empty or does not exist.";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"**Table: `{tableName}`** ({result.Data.Rows.Count} rows, {result.Data.Columns.Count} cols)\n");

                sb.Append("| ");
                foreach (System.Data.DataColumn col in result.Data.Columns)
                    sb.Append($"{col.ColumnName} | ");
                sb.AppendLine();
                sb.Append("|");
                foreach (System.Data.DataColumn col in result.Data.Columns)
                    sb.Append("---|");
                sb.AppendLine();

                int rowCount = 0;
                foreach (System.Data.DataRow row in result.Data.Rows)
                {
                    if (rowCount >= 10) { sb.AppendLine("\n*... showing first 10 rows ...*"); break; }
                    sb.Append("| ");
                    foreach (System.Data.DataColumn col in result.Data.Columns)
                    {
                        string val = row[col]?.ToString() ?? "";
                        if (val.Length > 50) val = val.Substring(0, 47) + "...";
                        sb.Append($"{val} | ");
                    }
                    sb.AppendLine();
                    rowCount++;
                }

                msg.Content = sb.ToString();
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error reading table: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private void ExecuteGetSystemStatus(ChatMessage msg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**System Status**\n");
            sb.AppendLine($"| Property | Value |");
            sb.AppendLine($"|----------|-------|");

            bool isOnline = !(_dataRepository is OfflineDataRepository);
            sb.AppendLine($"| Mode | {(isOnline ? "Online" : "Offline")} |");
            sb.AppendLine($"| Database | {GeneralSettingsManager.Instance.Current.DbProvider} |");
            sb.AppendLine($"| Language | {GeneralSettingsManager.Instance.Current.AppLanguage} |");
            sb.AppendLine($"| AI Assistant | {(GeneralSettingsManager.Instance.Current.AiAssistantEnabled ? "Enabled" : "Disabled")} |");
            sb.AppendLine($"| User | {UserSessionService.CurrentUsername ?? "Unknown"} |");
            sb.AppendLine($"| Config File | `{GeneralSettingsManager.Instance.GetResolvedConfigPath()}` |");

            msg.Content = sb.ToString();
            msg.ExecutionStatus = "executed";
        }

        private void ExecuteShowMessage(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            string text = toolCall.ContainsKey("text") ? toolCall["text"] : "Hello!";
            string title = toolCall.ContainsKey("title") ? toolCall["title"] : "AI Assistant";
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
            msg.Content = $"✅ Message displayed: \"{text}\"";
            msg.ExecutionStatus = "executed";
        }

        private void ExecuteOpenView(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            string viewName = toolCall.ContainsKey("viewname") ? toolCall["viewname"].ToLowerInvariant() : "";
            msg.Content = $"📍 Navigation to `{viewName}` requested.";
            msg.ExecutionStatus = "executed";
        }

        private async void ExecuteGetTableSchema(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string tableName = toolCall.ContainsKey("tablename") ? toolCall["tablename"] : "unknown";
            try
            {
                var result = await _dataRepository.GetTableDataAsync(tableName, 1);
                if (result.Data == null)
                {
                    msg.Content = $"Table `{tableName}` does not exist.";
                    msg.ExecutionStatus = "failed";
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"**Schema: `{tableName}`**\n");
                sb.AppendLine("| # | Column | Type |");
                sb.AppendLine("|---|--------|------|");
                int i = 1;
                foreach (System.Data.DataColumn col in result.Data.Columns)
                {
                    sb.AppendLine($"| {i++} | `{col.ColumnName}` | {col.DataType.Name} |");
                }

                msg.Content = sb.ToString();
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private void ExecuteExportConfig(ChatMessage msg)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Configuration",
                Filter = "JSON files (*.json)|*.json",
                FileName = "exported_config.json"
            };

            if (dialog.ShowDialog() == true)
            {
                GeneralSettingsManager.Instance.ExportGeneralConfig(dialog.FileName);
                msg.Content = $"✅ Configuration exported to `{dialog.FileName}`";
                msg.ExecutionStatus = "executed";
            }
            else
            {
                msg.Content = "Export cancelled by user.";
                msg.ExecutionStatus = "cancelled";
            }
        }

        private async void ExecuteGetDashboardData(ChatMessage msg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Dashboard Overview**\n");
            sb.AppendLine("Available dashboard modules:\n");
            sb.AppendLine("| Module | Description |");
            sb.AppendLine("|--------|-------------|");
            sb.AppendLine("| Analytics Hub | Main dashboard portal with chart tiles |");
            sb.AppendLine("| Dashboard Module | Detailed charts with drill-down |");
            sb.AppendLine("| Advanced Chart Detail | Maximized chart view |");
            sb.AppendLine("| Error Analytics | Error management and drill-down |");
            sb.AppendLine("| Reports | Data reports with filtering |");

            sb.AppendLine($"\n**System Summary**");
            sb.AppendLine($"- Database: {GeneralSettingsManager.Instance.Current.DbProvider}");
            sb.AppendLine($"- Mode: {(_dataRepository is OfflineDataRepository ? "Offline" : "Online")}");
            sb.AppendLine($"- Language: {GeneralSettingsManager.Instance.Current.AppLanguage}");
            if (_dataRepository != null)
            {
                try
                {
                    var tables = await _dataRepository.GetTableNamesAsync();
                    sb.AppendLine($"- Available tables: {tables?.Count ?? 0}");
                }
                catch { }
            }

            sb.AppendLine("\n*Use a specific chart name filter for more detail.*");
            msg.Content = sb.ToString();
            msg.ExecutionStatus = "executed";
        }

        private async void ExecuteGetTimelineData(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string dateStr = toolCall.ContainsKey("date") ? toolCall["date"] : null;
            if (dateStr == null)
            {
                msg.Content = "Missing required parameter 'date'.";
                msg.ExecutionStatus = "failed";
                return;
            }

            if (!DateTime.TryParse(dateStr, out DateTime date))
            {
                msg.Content = $"Invalid date format: '{dateStr}'. Use yyyy-MM-dd.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string tableName = toolCall.ContainsKey("tablename") ? toolCall["tablename"] : null;

            try
            {
                if (tableName == null)
                {
                    var tables = await _dataRepository.GetTableNamesAsync();
                    tableName = tables.FirstOrDefault();
                    if (tableName == null)
                    {
                        msg.Content = "No tables found in database.";
                        msg.ExecutionStatus = "failed";
                        return;
                    }
                }

                DateTime endDate = date.Date.AddDays(1).AddSeconds(-1);
                var errors = await _dataRepository.GetErrorDataAsync(date.Date, endDate, tableName);

                if (errors == null || errors.Count == 0)
                {
                    msg.Content = $"No timeline events found for {dateStr} in table `{tableName}`.";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"**Timeline Events — {dateStr}** (table: `{tableName}`, {errors.Count} events)\n");
                sb.AppendLine("| Time | Duration | Machine | Description |");
                sb.AppendLine("|------|----------|---------|-------------|");

                foreach (var ev in errors.OrderBy(e => e.StartTime))
                {
                    string desc = ev.ErrorDescription ?? "";
                    if (desc.Length > 50) desc = desc.Substring(0, 47) + "...";
                    sb.AppendLine($"| {ev.StartTime} | {ev.DurationMinutes}m | MA-{ev.MachineCode} | {desc} |");
                }

                msg.Content = sb.ToString();
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error loading timeline: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private async void ExecuteGetErrorSummary(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string startStr = toolCall.ContainsKey("startdate") ? toolCall["startdate"] : null;
            if (startStr == null)
            {
                msg.Content = "Missing required parameter 'startDate'.";
                msg.ExecutionStatus = "failed";
                return;
            }

            if (!DateTime.TryParse(startStr, out DateTime startDate))
            {
                msg.Content = $"Invalid startDate format: '{startStr}'. Use yyyy-MM-dd.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string endStr = toolCall.ContainsKey("enddate") ? toolCall["enddate"] : null;
            if (!DateTime.TryParse(endStr, out DateTime endDate))
                endDate = startDate;

            try
            {
                var tables = await _dataRepository.GetTableNamesAsync();
                var allErrors = new List<ErrorEventModel>();

                foreach (var table in tables)
                {
                    try
                    {
                        var tableErrors = await _dataRepository.GetErrorDataAsync(startDate.Date, endDate.Date.AddDays(1).AddSeconds(-1), table);
                        if (tableErrors != null)
                            allErrors.AddRange(tableErrors);
                    }
                    catch { }
                }

                if (allErrors.Count == 0)
                {
                    msg.Content = $"No errors found between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}.";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                var byMachine = allErrors.GroupBy(e => e.MachineCode ?? "??")
                    .ToDictionary(g => g.Key, g => g.Count());
                var byDescription = allErrors.GroupBy(e => e.ErrorDescription ?? "Unknown")
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());
                double totalDuration = allErrors.Sum(e => (double)e.DurationMinutes);

                var sb = new StringBuilder();
                sb.AppendLine($"**Error Summary** ({startDate:yyyy-MM-dd} → {endDate:yyyy-MM-dd})\n");
                sb.AppendLine($"| Metric | Value |");
                sb.AppendLine($"|--------|-------|");
                sb.AppendLine($"| Total Events | {allErrors.Count} |");
                sb.AppendLine($"| Total Duration | {totalDuration:F0} min |");
                sb.AppendLine($"| Unique Machines | {byMachine.Count} |");

                sb.AppendLine($"\n**By Machine**");
                sb.AppendLine("| Machine | Count |");
                sb.AppendLine("|---------|-------|");
                foreach (var kvp in byMachine.OrderByDescending(k => k.Value))
                    sb.AppendLine($"| MA-{kvp.Key} | {kvp.Value} |");

                sb.AppendLine($"\n**Top Error Descriptions**");
                sb.AppendLine("| Description | Count |");
                sb.AppendLine("|-------------|-------|");
                foreach (var kvp in byDescription)
                {
                    string desc = kvp.Key;
                    if (desc.Length > 50) desc = desc.Substring(0, 47) + "...";
                    sb.AppendLine($"| {desc} | {kvp.Value} |");
                }

                msg.Content = sb.ToString();
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error generating summary: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private void ExecuteGenerateReport(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            string type = toolCall.ContainsKey("type") ? toolCall["type"] : "unknown";
            string dateFrom = toolCall.ContainsKey("datefrom") ? toolCall["datefrom"] : "";
            string dateTo = toolCall.ContainsKey("dateto") ? toolCall["dateto"] : dateFrom;

            var sb = new StringBuilder();
            sb.AppendLine($"**Generate Report**\n");
            sb.AppendLine($"| Setting | Value |");
            sb.AppendLine($"|---------|-------|");
            sb.AppendLine($"| Type | `{type}` |");
            sb.AppendLine($"| Date From | `{dateFrom}` |");
            sb.AppendLine($"| Date To | `{dateTo}` |");
            sb.AppendLine();
            sb.AppendLine("To generate this report:");
            sb.AppendLine("1. Navigate to **Reports** view from the main navigation");
            sb.AppendLine("2. Select the appropriate table and date range");
            sb.AppendLine("3. Use the export or print functionality");
            sb.AppendLine();
            sb.AppendLine("*Report generation dialog will open automatically in a future update.*");

            msg.Content = sb.ToString();
            msg.ExecutionStatus = "executed";
        }

        private async void ExecuteExportTableData(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null)
            {
                msg.Content = "No database connection available.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string tableName = toolCall.ContainsKey("tablename") ? toolCall["tablename"] : null;
            if (tableName == null)
            {
                msg.Content = "Missing required parameter 'tableName'.";
                msg.ExecutionStatus = "failed";
                return;
            }

            string format = toolCall.ContainsKey("format") ? toolCall["format"].ToLowerInvariant() : "csv";
            if (format != "csv" && format != "xlsx")
            {
                msg.Content = $"Unsupported format '{format}'. Use 'csv' or 'xlsx'.";
                msg.ExecutionStatus = "failed";
                return;
            }

            try
            {
                string filter = format == "csv" ? "CSV files (*.csv)|*.csv" : "Excel files (*.xlsx)|*.xlsx";
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Export {tableName}",
                    Filter = filter,
                    FileName = $"{tableName}.{format}"
                };

                if (dialog.ShowDialog() != true)
                {
                    msg.Content = "Export cancelled by user.";
                    msg.ExecutionStatus = "cancelled";
                    return;
                }

                var result = await _dataRepository.GetTableDataAsync(tableName, 0);
                if (result.Data == null || result.Data.Rows.Count == 0)
                {
                    msg.Content = $"Table `{tableName}` is empty or does not exist.";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                if (format == "csv")
                {
                    var sb = new StringBuilder();
                    var columns = result.Data.Columns;
                    sb.AppendLine(string.Join(",", columns.Cast<System.Data.DataColumn>().Select(c => $"\"{c.ColumnName}\"")));

                    foreach (System.Data.DataRow row in result.Data.Rows)
                    {
                        var values = new List<string>();
                        foreach (System.Data.DataColumn col in columns)
                        {
                            string val = row[col]?.ToString() ?? "";
                            val = val.Replace("\"", "\"\"");
                            values.Add($"\"{val}\"");
                        }
                        sb.AppendLine(string.Join(",", values));
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                }
                else
                {
                    // xlsx not directly supported — save as CSV as fallback
                    string fallback = dialog.FileName.Replace(".xlsx", ".csv");
                    var sb = new StringBuilder();
                    var columns = result.Data.Columns;
                    sb.AppendLine(string.Join(",", columns.Cast<System.Data.DataColumn>().Select(c => $"\"{c.ColumnName}\"")));

                    foreach (System.Data.DataRow row in result.Data.Rows)
                    {
                        var values = new List<string>();
                        foreach (System.Data.DataColumn col in columns)
                        {
                            string val = row[col]?.ToString() ?? "";
                            val = val.Replace("\"", "\"\"");
                            values.Add($"\"{val}\"");
                        }
                        sb.AppendLine(string.Join(",", values));
                    }

                    File.WriteAllText(fallback, sb.ToString(), Encoding.UTF8);
                    msg.Content = $"⚠️ XLSX export not available. Saved as CSV: `{fallback}`";
                    msg.ExecutionStatus = "executed";
                    return;
                }

                msg.Content = $"✅ Table `{tableName}` exported to `{dialog.FileName}` ({result.Data.Rows.Count} rows).";
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex)
            {
                msg.Content = $"Error exporting table: {ex.Message}";
                msg.ExecutionStatus = "failed";
            }
        }

        private async void ExecuteGetLongestError(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null) { msg.Content = "No database connection."; msg.ExecutionStatus = "failed"; return; }
            string tableName = GetParam(toolCall, "tablename") ?? GetParam(toolCall, "tableName");
            string startStr = GetParam(toolCall, "startdate") ?? GetParam(toolCall, "startDate");
            string endStr = GetParam(toolCall, "enddate") ?? GetParam(toolCall, "endDate");
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(startStr)) { msg.Content = "Missing parameters: tableName, startDate."; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(startStr, out DateTime startDate)) { msg.Content = $"Invalid startDate: {startStr}"; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(endStr, out DateTime endDate)) endDate = startDate;
            string sectionFilter = GetParam(toolCall, "section");

            try
            {
                var errors = await _dataRepository.GetErrorDataAsync(startDate.Date, endDate.Date.AddDays(1).AddSeconds(-1), tableName);
                if (errors == null || errors.Count == 0) { msg.Content = $"No errors found between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}."; msg.ExecutionStatus = "executed"; return; }

                if (!string.IsNullOrWhiteSpace(sectionFilter))
                    errors = errors.Where(e => e.SectionCode?.Equals(sectionFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();

                if (errors.Count == 0) { msg.Content = $"No errors found in section '{sectionFilter}'."; msg.ExecutionStatus = "executed"; return; }

                var longest = errors.OrderByDescending(e => e.DurationMinutes).First();
                var sb = new StringBuilder();
                sb.AppendLine("**Longest Error Found**\n");
                sb.AppendLine($"| Field | Value |");
                sb.AppendLine($"|-------|-------|");
                sb.AppendLine($"| Raw Code | `{longest.RawData}` |");
                sb.AppendLine($"| Duration | {longest.DurationMinutes} min ({longest.DisplayDuration}) |");
                sb.AppendLine($"| Time | {longest.StartTime} ➝ {longest.EndTime} |");
                sb.AppendLine($"| Date | {longest.Date:yyyy-MM-dd} |");
                sb.AppendLine($"| Shift | {longest.Shift} |");
                sb.AppendLine($"| Section | {longest.SectionCode} |");
                sb.AppendLine($"| Description | {longest.ErrorDescription} |");
                sb.AppendLine($"| Stop Total | {longest.RowTotalStopMinutes} min |");
                sb.AppendLine($"| Actual Work | {longest.RowActualWorkingMinutes} min |");
                msg.Content = sb.ToString(); msg.ExecutionStatus = "executed";
            }
            catch (Exception ex) { msg.Content = $"Error: {ex.Message}"; msg.ExecutionStatus = "failed"; }
        }

        private async void ExecuteSearchErrorCodes(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null) { msg.Content = "No database connection."; msg.ExecutionStatus = "failed"; return; }
            string tableName = GetParam(toolCall, "tablename") ?? GetParam(toolCall, "tableName");
            string query = GetParam(toolCall, "query");
            string startStr = GetParam(toolCall, "startdate") ?? GetParam(toolCall, "startDate");
            string endStr = GetParam(toolCall, "enddate") ?? GetParam(toolCall, "endDate");
            int limit = int.TryParse(GetParam(toolCall, "limit"), out int l) ? l : 20;
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(startStr)) { msg.Content = "Missing parameters: tableName, query, startDate."; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(startStr, out DateTime startDate)) { msg.Content = $"Invalid startDate: {startStr}"; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(endStr, out DateTime endDate)) endDate = startDate;

            try
            {
                var errors = await _dataRepository.GetErrorDataAsync(startDate.Date, endDate.Date.AddDays(1).AddSeconds(-1), tableName);
                if (errors == null || errors.Count == 0) { msg.Content = $"No errors found in date range."; msg.ExecutionStatus = "executed"; return; }

                var matches = errors.Where(e => e.ErrorDescription?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || e.RawData?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).Take(limit).ToList();

                if (matches.Count == 0) { msg.Content = $"No errors matching '{query}' found."; msg.ExecutionStatus = "executed"; return; }

                var sb = new StringBuilder();
                sb.AppendLine($"**Search Results: \"{query}\"** ({matches.Count} matches)\n");
                sb.AppendLine("| # | Date | Shift | Time | Duration | Section | Description |");
                sb.AppendLine("|---|------|-------|------|----------|---------|-------------|");
                int i = 1;
                foreach (var e in matches.OrderByDescending(x => x.DurationMinutes))
                {
                    string desc = (e.ErrorDescription ?? "").Length > 40 ? e.ErrorDescription.Substring(0, 37) + "..." : (e.ErrorDescription ?? "");
                    sb.AppendLine($"| {i++} | {e.Date:yyyy-MM-dd} | {e.Shift} | {e.StartTime} | {e.DurationMinutes}m | {e.SectionCode} | {desc} |");
                }
                msg.Content = sb.ToString(); msg.ExecutionStatus = "executed";
            }
            catch (Exception ex) { msg.Content = $"Error: {ex.Message}"; msg.ExecutionStatus = "failed"; }
        }

        private async void ExecuteAggregateErrorDurations(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null) { msg.Content = "No database connection."; msg.ExecutionStatus = "failed"; return; }
            string tableName = GetParam(toolCall, "tablename") ?? GetParam(toolCall, "tableName");
            string startStr = GetParam(toolCall, "startdate") ?? GetParam(toolCall, "startDate");
            string endStr = GetParam(toolCall, "enddate") ?? GetParam(toolCall, "endDate");
            string groupBy = (GetParam(toolCall, "groupby") ?? GetParam(toolCall, "groupBy") ?? "description").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(startStr)) { msg.Content = "Missing parameters: tableName, startDate."; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(startStr, out DateTime startDate)) { msg.Content = $"Invalid startDate: {startStr}"; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(endStr, out DateTime endDate)) endDate = startDate;

            try
            {
                var errors = await _dataRepository.GetErrorDataAsync(startDate.Date, endDate.Date.AddDays(1).AddSeconds(-1), tableName);
                if (errors == null || errors.Count == 0) { msg.Content = $"No errors found in date range."; msg.ExecutionStatus = "executed"; return; }

                Func<ErrorEventModel, string> keySelector;
                if (groupBy == "section") keySelector = e => e.SectionCode ?? "?";
                else if (groupBy == "code") keySelector = e => e.MachineCode ?? "?";
                else keySelector = e => e.ErrorDescription ?? "?";
                string groupLabel = groupBy == "section" ? "Section" : groupBy == "code" ? "Code" : "Description";

                var groups = errors.GroupBy(keySelector)
                    .Select(g => new
                    {
                        Key = string.IsNullOrWhiteSpace(g.Key) ? "(empty)" : g.Key,
                        Count = g.Count(),
                        TotalMinutes = g.Sum(x => (double)x.DurationMinutes),
                        MaxMinutes = g.Max(x => x.DurationMinutes),
                        AvgMinutes = g.Average(x => (double)x.DurationMinutes)
                    })
                    .OrderByDescending(g => g.TotalMinutes).ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"**Error Duration Summary** ({errors.Count} events, {groups.Count} groups)\n");
                sb.AppendLine($"| # | {groupLabel} | Occurrences | Total (min) | Max (min) | Avg (min) |");
                sb.AppendLine($"|---|{new string('-', groupLabel.Length)}|------------|-------------|-----------|-----------|");
                int i = 1;
                foreach (var g in groups)
                {
                    sb.AppendLine($"| {i++} | {g.Key} | {g.Count} | {g.TotalMinutes:F0} | {g.MaxMinutes} | {g.AvgMinutes:F0} |");
                }
                msg.Content = sb.ToString(); msg.ExecutionStatus = "executed";
            }
            catch (Exception ex) { msg.Content = $"Error: {ex.Message}"; msg.ExecutionStatus = "failed"; }
        }

        private async void ExecuteExportErrorLog(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null) { msg.Content = "No database connection."; msg.ExecutionStatus = "failed"; return; }
            string tableName = GetParam(toolCall, "tablename") ?? GetParam(toolCall, "tableName");
            string startStr = GetParam(toolCall, "startdate") ?? GetParam(toolCall, "startDate");
            string endStr = GetParam(toolCall, "enddate") ?? GetParam(toolCall, "endDate");
            string format = (GetParam(toolCall, "format") ?? "csv").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(startStr)) { msg.Content = "Missing parameters: tableName, startDate."; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(startStr, out DateTime startDate)) { msg.Content = $"Invalid startDate: {startStr}"; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(endStr, out DateTime endDate)) endDate = startDate;

            try
            {
                var errors = await _dataRepository.GetErrorDataAsync(startDate.Date, endDate.Date.AddDays(1).AddSeconds(-1), tableName);
                if (errors == null || errors.Count == 0) { msg.Content = $"No errors found in date range."; msg.ExecutionStatus = "executed"; return; }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Export Error Log ({startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})",
                    Filter = format == "csv" ? "CSV files (*.csv)|*.csv" : "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"error_log_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.{format}"
                };

                if (dialog.ShowDialog() != true) { msg.Content = "Export cancelled."; msg.ExecutionStatus = "cancelled"; return; }

                var sb = new StringBuilder();
                sb.AppendLine("Date,Shift,Time,DurationMinutes,Section,Code,Description,RawCode");
                foreach (var e in errors.OrderBy(x => x.Date).ThenBy(x => x.StartTime))
                {
                    string desc = (e.ErrorDescription ?? "").Replace("\"", "\"\"");
                    string raw = (e.RawData ?? "").Replace("\"", "\"\"");
                    sb.AppendLine($"\"{e.Date:yyyy-MM-dd}\",\"{e.Shift}\",\"{e.StartTime}\",{e.DurationMinutes},\"{e.SectionCode}\",\"{e.MachineCode}\",\"{desc}\",\"{raw}\"");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                msg.Content = $"✅ Error log exported to `{dialog.FileName}` ({errors.Count} rows).";
                msg.ExecutionStatus = "executed";
            }
            catch (Exception ex) { msg.Content = $"Error: {ex.Message}"; msg.ExecutionStatus = "failed"; }
        }

        private async void ExecuteGetErrorTimelineV2(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            if (_dataRepository == null) { msg.Content = "No database connection."; msg.ExecutionStatus = "failed"; return; }
            string tableName = GetParam(toolCall, "tablename") ?? GetParam(toolCall, "tableName");
            string dateStr = GetParam(toolCall, "date");
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(dateStr)) { msg.Content = "Missing parameters: tableName, date."; msg.ExecutionStatus = "failed"; return; }
            if (!DateTime.TryParse(dateStr, out DateTime date)) { msg.Content = $"Invalid date: {dateStr}"; msg.ExecutionStatus = "failed"; return; }
            string sectionFilter = GetParam(toolCall, "section");

            try
            {
                var errors = await _dataRepository.GetErrorDataAsync(date.Date, date.Date.AddDays(1).AddSeconds(-1), tableName);
                if (errors == null || errors.Count == 0) { msg.Content = $"No errors found on {date:yyyy-MM-dd}."; msg.ExecutionStatus = "executed"; return; }

                if (!string.IsNullOrWhiteSpace(sectionFilter))
                    errors = errors.Where(e => e.SectionCode?.Equals(sectionFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();

                if (errors.Count == 0) { msg.Content = $"No errors in section '{sectionFilter}' on {date:yyyy-MM-dd}."; msg.ExecutionStatus = "executed"; return; }

                var timeline = errors.OrderBy(e => e.StartTime).ToList();
                var sb = new StringBuilder();
                sb.AppendLine($"**Error Timeline — {date:yyyy-MM-dd}** ({timeline.Count} events, shift: {timeline[0].Shift})\n");
                sb.AppendLine("| Time | Duration | Section | Code | Description |");
                sb.AppendLine("|------|----------|---------|------|-------------|");
                foreach (var e in timeline)
                {
                    string desc = (e.ErrorDescription ?? "").Length > 45 ? e.ErrorDescription.Substring(0, 42) + "..." : (e.ErrorDescription ?? "");
                    sb.AppendLine($"| {e.StartTime} ➝ {e.EndTime} | {e.DurationMinutes}m | {e.SectionCode} | {e.MachineCode} | {desc} |");
                }

                double totalStop = timeline.Sum(e => e.RowTotalStopMinutes);
                double totalWork = timeline.Sum(e => e.RowActualWorkingMinutes);
                sb.AppendLine($"\n**Summary**: {timeline.Count} events, max {timeline.Max(e => e.DurationMinutes)} min, total stop {totalStop:F0} min, work {totalWork:F0} min.");

                msg.Content = sb.ToString(); msg.ExecutionStatus = "executed";
            }
            catch (Exception ex) { msg.Content = $"Error: {ex.Message}"; msg.ExecutionStatus = "failed"; }
        }

        private void ExecuteCreateErrorAlertRule(Dictionary<string, string> toolCall, ChatMessage msg)
        {
            string section = GetParam(toolCall, "section");
            string thresholdStr = GetParam(toolCall, "durationthreshold") ?? GetParam(toolCall, "durationThreshold");
            string email = GetParam(toolCall, "notifyemail") ?? GetParam(toolCall, "notifyEmail");
            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(thresholdStr)) { msg.Content = "Missing parameters: section, durationThreshold."; msg.ExecutionStatus = "failed"; return; }
            if (!int.TryParse(thresholdStr, out int threshold)) { msg.Content = $"Invalid durationThreshold: {thresholdStr}"; msg.ExecutionStatus = "failed"; return; }

            var config = GeneralSettingsManager.Instance.Current;
            config.ErrorAlertSection = section;
            config.ErrorAlertThreshold = threshold;
            config.ErrorAlertEmail = email ?? "";
            GeneralSettingsManager.Instance.Save();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Alert Rule Created**\n");
            sb.AppendLine("| Setting | Value |");
            sb.AppendLine("|---------|-------|");
            sb.AppendLine($"| Section | `{section}` |");
            sb.AppendLine($"| Duration Threshold | {threshold} min |");
            sb.AppendLine($"| Notify Email | `{(string.IsNullOrWhiteSpace(email) ? "(not set)" : email)}` |");
            sb.AppendLine();
            sb.AppendLine($"The system will alert when any error in section **{section}** exceeds **{threshold} minutes**.");
            msg.Content = sb.ToString(); msg.ExecutionStatus = "executed";
        }

        private void AddUserMessage(string text)
        {
            Messages.Add(MakeMessage(text, ChatMessageType.User));
        }

        private void AddAssistantMessage(string text)
        {
            Messages.Add(MakeMessage(text, ChatMessageType.Assistant));
        }

        private void AddSystemMessage(string text)
        {
            Messages.Add(MakeMessage(text, ChatMessageType.System));
        }

        private ChatMessage MakeMessage(string content, ChatMessageType type)
        {
            var msg = new ChatMessage
            {
                Content = content,
                Type = type,
                Timestamp = DateTime.Now
            };
            msg.CopyMessageCommand = new ViewModelCommand(p =>
            {
                Clipboard.SetText(msg.Content);
                StatusText = "Message copied.";
            });
            return msg;
        }

        private void AddToolCallMessage(Dictionary<string, string> toolCall)
        {
            if (toolCall == null || !toolCall.ContainsKey("_toolName")) return;

            string toolName = toolCall["_toolName"];
            var parameters = new Dictionary<string, string>();
            foreach (var kvp in toolCall)
            {
                if (kvp.Key != "_toolName")
                    parameters[kvp.Key] = kvp.Value;
            }

            var msg = new ChatMessage
            {
                Content = $"Ready to execute `{toolName}` with parameters: {string.Join(", ", parameters.Select(p => $"`{p.Key}={p.Value}`"))}",
                Type = ChatMessageType.ToolCall,
                Timestamp = DateTime.Now,
                ToolName = toolName,
                ToolParameters = parameters,
                ShowActions = true,
                ExecutionStatus = "pending"
            };

            msg.ExecuteToolCommand = new ViewModelCommand(p =>
            {
                msg.ShowActions = false;
                msg.Content = $"⏳ Executing `{toolName}`...";
                msg.OnPropertyChanged(nameof(msg.Content));
                msg.OnPropertyChanged(nameof(msg.ShowActions));
                StatusText = $"Executing tool: {toolName}";
                ExecuteToolAction(toolCall, msg);
                StatusText = $"Tool `{toolName}` {msg.ExecutionStatus}.";
            });

            msg.CancelToolCommand = new ViewModelCommand(p =>
            {
                msg.ShowActions = false;
                msg.Content = $"❌ Tool `{toolName}` cancelled.";
                msg.ExecutionStatus = "cancelled";
                msg.OnPropertyChanged(nameof(msg.Content));
                msg.OnPropertyChanged(nameof(msg.ShowActions));
                msg.OnPropertyChanged(nameof(msg.ExecutionStatus));
                StatusText = "Tool call cancelled.";
            });

            msg.CopyMessageCommand = new ViewModelCommand(p =>
            {
                Clipboard.SetText(msg.Content);
                StatusText = "Message copied.";
            });

            Messages.Add(msg);
        }
    }
}
