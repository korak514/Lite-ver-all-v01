using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks; // Required for Task

namespace WPF_LoginForm.Models
{
    public interface IUserRepository
    {
        bool AuthenticateUser(NetworkCredential credential);

        // --- NEW: Async Authentication ---
        Task<bool> AuthenticateUserAsync(NetworkCredential credential);

        void Add(UserModel userModel);

        void Edit(UserModel userModel);

        void Remove(int id);

        UserModel GetById(int id);

        UserModel GetByUsername(string username);

        IEnumerable<UserModel> GetByAll();
    }
}