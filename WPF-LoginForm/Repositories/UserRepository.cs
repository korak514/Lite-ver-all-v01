using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using Npgsql;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Repositories
{
    public class UserRepository : IUserRepository
    {
        // Helper to quote tables based on DB Type
        private string TableName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"User\"" : "[User]";

        private string ColId => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Id\"" : "[Id]";
        private string ColUser => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Username\"" : "[Username]";
        private string ColPass => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Password\"" : "[Password]";
        private string ColName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Name\"" : "[Name]";
        private string ColLast => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"LastName\"" : "[LastName]";
        private string ColEmail => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Email\"" : "[Email]";

        // NEW: Role Column
        private string ColRole => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Role\"" : "[Role]";

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
                        command.CommandText = $"SELECT COUNT(*) FROM {TableName} WHERE {ColUser}=@username AND {ColPass}=@password";
                        AddParameter(command, "@username", credential.UserName);
                        AddParameter(command, "@password", credential.Password);

                        var result = command.ExecuteScalar();
                        validUser = result != null && Convert.ToInt32(result) > 0;
                    }
                }
                catch (Exception)
                {
                    validUser = false;
                }
            }
            return validUser;
        }

        public void Add(UserModel userModel)
        {
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // UPDATED: Include Role
                    command.CommandText = $"INSERT INTO {TableName} ({ColUser}, {ColPass}, {ColName}, {ColLast}, {ColEmail}, {ColRole}) " +
                                          "VALUES (@username, @password, @name, @lastname, @email, @role)";

                    AddParameter(command, "@username", userModel.Username);
                    AddParameter(command, "@password", userModel.Password);
                    AddParameter(command, "@name", userModel.Name);
                    AddParameter(command, "@lastname", userModel.LastName);
                    AddParameter(command, "@email", userModel.Email);
                    AddParameter(command, "@role", userModel.Role ?? "User"); // Default to User

                    command.ExecuteNonQuery();
                }
            }
        }

        public void Edit(UserModel userModel)
        {
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // UPDATED: Include Role
                    command.CommandText = $"UPDATE {TableName} SET {ColUser}=@username, {ColPass}=@password, " +
                                          $"{ColName}=@name, {ColLast}=@lastname, {ColEmail}=@email, {ColRole}=@role " +
                                          $"WHERE {ColId}=@id";

                    AddParameter(command, "@username", userModel.Username);
                    AddParameter(command, "@password", userModel.Password);
                    AddParameter(command, "@name", userModel.Name);
                    AddParameter(command, "@lastname", userModel.LastName);
                    AddParameter(command, "@email", userModel.Email);
                    AddParameter(command, "@role", userModel.Role ?? "User");

                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";
                    paramId.Value = int.Parse(userModel.Id);
                    command.Parameters.Add(paramId);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void Remove(int id)
        {
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM {TableName} WHERE {ColId}=@id";

                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";
                    paramId.Value = id;
                    command.Parameters.Add(paramId);

                    command.ExecuteNonQuery();
                }
            }
        }

        public UserModel GetById(int id)
        {
            UserModel user = null;
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {TableName} WHERE {ColId}=@id";

                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";
                    paramId.Value = id;
                    command.Parameters.Add(paramId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new UserModel
                            {
                                Id = reader["Id"].ToString(),
                                Username = reader["Username"].ToString(),
                                Name = reader["Name"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Password = reader["Password"].ToString(),
                                // UPDATED: Read Role
                                Role = reader["Role"] != DBNull.Value ? reader["Role"].ToString() : "User"
                            };
                        }
                    }
                }
            }
            return user;
        }

        public UserModel GetByUsername(string username)
        {
            UserModel user = null;
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {TableName} WHERE {ColUser}=@username";
                    AddParameter(command, "@username", username);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new UserModel
                            {
                                Id = reader["Id"].ToString(),
                                Username = reader["Username"].ToString(),
                                Name = reader["Name"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Password = reader["Password"].ToString(),
                                // UPDATED: Read Role
                                Role = reader["Role"] != DBNull.Value ? reader["Role"].ToString() : "User"
                            };
                        }
                    }
                }
            }
            return user;
        }

        public IEnumerable<UserModel> GetByAll()
        {
            var userList = new List<UserModel>();
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {TableName} ORDER BY {ColUser}";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var user = new UserModel
                            {
                                Id = reader["Id"].ToString(),
                                Username = reader["Username"].ToString(),
                                Name = reader["Name"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Password = reader["Password"].ToString(),
                                // UPDATED: Read Role
                                Role = reader["Role"] != DBNull.Value ? reader["Role"].ToString() : "User"
                            };
                            userList.Add(user);
                        }
                    }
                }
            }
            return userList;
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}