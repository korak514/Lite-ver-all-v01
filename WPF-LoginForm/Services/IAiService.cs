using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPF_LoginForm.Services
{
    public class AiMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public AiMessage() { }

        public AiMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    public interface IAiService
    {
        Task<string> AskAsync(List<AiMessage> messages);
    }
}
