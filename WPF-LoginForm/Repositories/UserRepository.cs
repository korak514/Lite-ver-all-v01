using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Repositories
{
    public class UserRepository : IUserRepository
    {
        // --- MODIFIED: Uses ConnectionTarget.Auth ---
        public bool AuthenticateUser(NetworkCredential credential)
        {
            bool validUser;
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                try
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        string tableName = DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql
                            ? "\"User\"" // Postgres
                            : "[User]";  // SQL Server

                        command.CommandText = $"SELECT * FROM {tableName} WHERE Username=@username AND Password=@password";

                        AddParameter(command, "@username", credential.UserName);
                        AddParameter(command, "@password", credential.Password);

                        validUser = command.ExecuteScalar() != null;
                    }
                }
                catch (Exception)
                {
                    validUser = false;
                }
            }
            return validUser;
        }

        public UserModel GetByUsername(string username)
        {
            UserModel user = null;
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    string tableName = DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql
                        ? "\"User\""
                        : "[User]";

                    command.CommandText = $"SELECT * FROM {tableName} WHERE Username=@username";
                    AddParameter(command, "@username", username);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new UserModel()
                            {
                                Id = reader["Id"].ToString(),
                                Username = reader["Username"].ToString(),
                                Name = reader["Name"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                            };
                        }
                    }
                }
            }
            return user;
        }

        // Stubbed methods
        public void Add(UserModel userModel) { throw new NotImplementedException(); }
        public void Edit(UserModel userModel) { throw new NotImplementedException(); }
        public void Remove(int id) { throw new NotImplementedException(); }
        public UserModel GetById(int id) { throw new NotImplementedException(); }
        public IEnumerable<UserModel> GetByAll() { throw new NotImplementedException(); }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}