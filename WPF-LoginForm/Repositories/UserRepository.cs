// using System.Data.SqlClient; // Remove or comment out this
// using System.Data;           // Remove or comment out this if only used for SqlDbType/SqlConnection
using Microsoft.Data.SqlClient; // Add this
using System.Data;             // Keep this if needed for other things like DataTable/DataSet (not used here)
using System.Net;
using WPF_LoginForm.Models;
using System.Collections.Generic; // Keep this if GetByAll is implemented later
using System; // Keep this for NotImplementedException

namespace WPF_LoginForm.Repositories
{
    // No changes to class definition or base class
    public class UserRepository : RepositoryBase, IUserRepository
    {
        public void Add(UserModel userModel)
        {
            throw new NotImplementedException();
        }

        public bool AuthenticateUser(NetworkCredential credential)
        {
            bool validUser;
            // GetConnection() now returns Microsoft.Data.SqlClient.SqlConnection
            using (var connection = GetConnection())
            // SqlCommand is in Microsoft.Data.SqlClient
            using (var command = new SqlCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = "select *from [User] where username=@username and [password]=@password";
                // Parameters use Microsoft.Data.SqlClient types implicitly here
                // Or explicitly: command.Parameters.Add("@username", SqlDbType.NVarChar).Value = credential.UserName;
                command.Parameters.AddWithValue("@username", credential.UserName); // AddWithValue often simpler
                command.Parameters.AddWithValue("@password", credential.Password); // Use AddWithValue
                // command.Parameters.Add("@password", SqlDbType.NVarChar).Value = credential.Password; // SqlDbType is fine too

                validUser = command.ExecuteScalar() == null ? false : true;
            }
            return validUser;
        }

        public void Edit(UserModel userModel)
        {
            throw new NotImplementedException();
        }
        public IEnumerable<UserModel> GetByAll()
        {
            throw new NotImplementedException();
        }
        public UserModel GetById(int id)
        {
            throw new NotImplementedException();
        }
        public UserModel GetByUsername(string username)
        {
            UserModel user = null;
            // GetConnection() returns Microsoft.Data.SqlClient.SqlConnection
            using (var connection = GetConnection())
            // SqlCommand is in Microsoft.Data.SqlClient
            using (var command = new SqlCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = "select *from [User] where username=@username";
                // Parameters use Microsoft.Data.SqlClient types implicitly here
                command.Parameters.AddWithValue("@username", username); // Use AddWithValue
                // command.Parameters.Add("@username", SqlDbType.NVarChar).Value = username; // SqlDbType is fine too

                // SqlDataReader is in Microsoft.Data.SqlClient
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        user = new UserModel()
                        {
                            Id = reader[0].ToString(),
                            Username = reader[1].ToString(),
                            Password = string.Empty, // Keep password empty
                            Name = reader[3].ToString(),
                            LastName = reader[4].ToString(),
                            Email = reader[5].ToString(),
                        };
                    }
                }
            }
            return user;
        }
        public void Remove(int id)
        {
            throw new NotImplementedException();
        }
    }
}