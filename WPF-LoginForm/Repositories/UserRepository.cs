using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Repositories
{
    public class UserRepository : IUserRepository
    {
        private string TableName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"User\"" : "[User]";
        private string ColId => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Id\"" : "[Id]";
        private string ColUser => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Username\"" : "[Username]";
        private string ColPass => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Password\"" : "[Password]";
        private string ColName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Name\"" : "[Name]";
        private string ColLast => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"LastName\"" : "[LastName]";
        private string ColEmail => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Email\"" : "[Email]";
        private string ColRole => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Role\"" : "[Role]";

        // Kept for interface compatibility, but forwards to Async version internally if needed
        public bool AuthenticateUser(NetworkCredential credential)
        {
            // Blocking call for legacy support
            return AuthenticateUserAsync(credential).Result;
        }

        public async Task<bool> AuthenticateUserAsync(NetworkCredential credential)
        {
            bool validUser = false;

            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    if (connection is SqlConnection sqlConn) await sqlConn.OpenAsync();
                    else if (connection is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                    else connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        // Case-insensitive username match
                        string query = DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer
                            ? $"SELECT COUNT(*) FROM {TableName} WHERE LOWER({ColUser}) = LOWER(@username) AND {ColPass} = @password"
                            : $"SELECT COUNT(*) FROM {TableName} WHERE LOWER({ColUser}) = LOWER(@username) AND {ColPass} = @password";

                        command.CommandText = query;
                        AddParameter(command, "@username", credential.UserName);
                        AddParameter(command, "@password", credential.Password);

                        object result;
                        if (command is SqlCommand sqlCmd) result = await sqlCmd.ExecuteScalarAsync();
                        else if (command is NpgsqlCommand pgCmd) result = await pgCmd.ExecuteScalarAsync();
                        else result = command.ExecuteScalar();

                        validUser = result != null && Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auth Async Failed: {ex.Message}");
                // In production, you might want to log this specifically
                validUser = false;
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
                    command.CommandText = $"INSERT INTO {TableName} ({ColUser}, {ColPass}, {ColName}, {ColLast}, {ColEmail}, {ColRole}) " +
                                          "VALUES (@username, @password, @name, @lastname, @email, @role)";

                    AddParameter(command, "@username", userModel.Username);
                    AddParameter(command, "@password", userModel.Password);
                    AddParameter(command, "@name", userModel.Name ?? DBNull.Value.ToString());
                    AddParameter(command, "@lastname", userModel.LastName ?? DBNull.Value.ToString());
                    AddParameter(command, "@email", userModel.Email ?? DBNull.Value.ToString());
                    AddParameter(command, "@role", userModel.Role ?? "User");

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
                    command.CommandText = $"UPDATE {TableName} SET {ColUser}=@username, {ColPass}=@password, " +
                                          $"{ColName}=@name, {ColLast}=@lastname, {ColEmail}=@email, {ColRole}=@role " +
                                          $"WHERE {ColId}=@id";

                    AddParameter(command, "@username", userModel.Username);
                    AddParameter(command, "@password", userModel.Password);
                    AddParameter(command, "@name", userModel.Name);
                    AddParameter(command, "@lastname", userModel.LastName);
                    AddParameter(command, "@email", userModel.Email);
                    AddParameter(command, "@role", userModel.Role ?? "User");

                    // SAFE GUID PARSING
                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";
                    if (int.TryParse(userModel.Id, out int intId))
                    {
                        paramId.Value = intId; // Support legacy Integer IDs
                    }
                    else if (Guid.TryParse(userModel.Id, out Guid guidId))
                    {
                        paramId.Value = guidId; // Support new GUID IDs
                    }
                    else
                    {
                        throw new ArgumentException("Invalid User ID format");
                    }
                    command.Parameters.Add(paramId);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void Remove(string id)
        {
            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM {TableName} WHERE {ColId}=@id";

                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";

                    if (int.TryParse(id, out int intId)) paramId.Value = intId;
                    else if (Guid.TryParse(id, out Guid guidId)) paramId.Value = guidId;
                    else return; // Fail silently or throw if ID is invalid

                    command.Parameters.Add(paramId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public UserModel GetById(string id)
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

                    if (int.TryParse(id, out int intId)) paramId.Value = intId;
                    else if (Guid.TryParse(id, out Guid guidId)) paramId.Value = guidId;
                    else return null;

                    command.Parameters.Add(paramId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = MapReaderToUser(reader);
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
                    command.CommandText = $"SELECT * FROM {TableName} WHERE LOWER({ColUser}) = LOWER(@username)";
                    AddParameter(command, "@username", username);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = MapReaderToUser(reader);
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
                            userList.Add(MapReaderToUser(reader));
                        }
                    }
                }
            }
            return userList;
        }

        // Helper to avoid repeated mapping code
        private UserModel MapReaderToUser(IDataReader reader)
        {
            return new UserModel
            {
                Id = reader["Id"].ToString(),
                Username = reader["Username"].ToString(),
                Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : string.Empty,
                LastName = reader["LastName"] != DBNull.Value ? reader["LastName"].ToString() : string.Empty,
                Email = reader["Email"] != DBNull.Value ? reader["Email"].ToString() : string.Empty,
                Password = reader["Password"].ToString(),
                Role = reader["Role"] != DBNull.Value ? reader["Role"].ToString() : "User"
            };
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