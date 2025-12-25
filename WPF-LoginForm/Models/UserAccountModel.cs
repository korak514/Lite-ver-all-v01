namespace WPF_LoginForm.Models
{
    public class UserAccountModel
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public byte[] ProfilePicture { get; set; }

        // NEW: Role Property for UI Binding
        public string Role { get; set; }

        public bool IsAdmin => Role == "Admin";
    }
}