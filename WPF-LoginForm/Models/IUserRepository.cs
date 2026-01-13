using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace WPF_LoginForm.Models
{
    public interface IUserRepository
    {
        bool AuthenticateUser(NetworkCredential credential);

        Task<bool> AuthenticateUserAsync(NetworkCredential credential);

        void Add(UserModel userModel);

        void Edit(UserModel userModel);

        // CHANGED: int to string to support GUIDs
        void Remove(string id);

        // CHANGED: int to string to support GUIDs
        UserModel GetById(string id);

        UserModel GetByUsername(string username);

        IEnumerable<UserModel> GetByAll();
    }
}