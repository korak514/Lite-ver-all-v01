using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Repositories
{
    public class UserRepository : IUserRepository
    {
        private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");
        private static string NormalizeForComparison(string s) =>
            s.ToLower(TurkishCulture).Replace('ı', 'i');

        private static bool TurkishIgnoreCaseEquals(string a, string b) =>
            NormalizeForComparison(a ?? "") == NormalizeForComparison(b ?? "");

        private string TableName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"User\"" : "[User]";
        private string ColId => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Id\"" : "[Id]";
        private string ColUser => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Username\"" : "[Username]";
        private string ColPass => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Password\"" : "[Password]";
        private string ColName => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Name\"" : "[Name]";
        private string ColLast => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"LastName\"" : "[LastName]";
        private string ColEmail => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Email\"" : "[Email]";
        private string ColRole => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "\"Role\"" : "[Role]";
        private string ColUserRaw => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "Username" : "Username";
        private string ColPassRaw => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql ? "Password" : "Password";

        public bool AuthenticateUser(NetworkCredential credential)
        {
            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    connection.Open();
                    string dbUsername = credential.UserName;
                    string storedPass = LookupPasswordHash(connection, credential.UserName, out dbUsername);
                    if (storedPass == null) return false;

                    if (PasswordHelper.VerifyPassword(credential.Password, storedPass))
                    {
                        return true;
                    }
                    if (storedPass == credential.Password)
                    {
                        UpgradeUserPassword(dbUsername, credential.Password);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auth Sync Failed: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> AuthenticateUserAsync(NetworkCredential credential)
        {
            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    if (connection is SqlConnection sqlConn) await sqlConn.OpenAsync();
                    else if (connection is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                    else connection.Open();

                    string dbUsername = credential.UserName;
                    string storedPass = LookupPasswordHash(connection, credential.UserName, out dbUsername);
                    if (storedPass == null) return false;

                    if (PasswordHelper.VerifyPassword(credential.Password, storedPass))
                    {
                        return true;
                    }
                    if (storedPass == credential.Password)
                    {
                        await UpgradeUserPasswordAsync(dbUsername, credential.Password);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auth Async Failed: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Finds the user by username (Turkish-aware for SQL Server) and returns the stored password hash.
        /// </summary>
        private string LookupPasswordHash(IDbConnection connection, string inputUsername, out string matchedUsername)
        {
            matchedUsername = inputUsername;

            // Step 1: try LOWER() for standard case-insensitive (handles most cases)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT {ColUser}, {ColPass} FROM {TableName} WHERE LOWER({ColUser}) = LOWER(@username)";
                AddParameter(command, "@username", inputUsername);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        matchedUsername = reader[ColUserRaw].ToString();
                        return reader[ColPassRaw].ToString();
                    }
                }
            }

            // Step 2: LOWER() doesn't handle Turkish İ→i.
            // Fetch all users and compare in C# using Turkish culture.
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT {ColUser}, {ColPass} FROM {TableName}";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string dbUser = reader[ColUserRaw].ToString();
                        if (TurkishIgnoreCaseEquals(dbUser, inputUsername))
                        {
                            matchedUsername = dbUser;
                            return reader[ColPassRaw].ToString();
                        }
                    }
                }
            }

            return null;
        }

        private string LookupPasswordHashAsync(IDbConnection connection, string inputUsername, out string matchedUsername)
        {
            return LookupPasswordHash(connection, inputUsername, out matchedUsername);
        }

        private void UpgradeUserPassword(string username, string plainPassword)
        {
            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    connection.Open();
                    string matchedUser;
                    string hash = LookupPasswordHash(connection, username, out matchedUser);
                    if (hash == null) return;

                    string newHash = PasswordHelper.HashPassword(plainPassword);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"UPDATE {TableName} SET {ColPass} = @hash WHERE LOWER({ColUser}) = LOWER(@username)";
                        AddParameter(command, "@hash", newHash);
                        AddParameter(command, "@username", matchedUser);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        private async Task UpgradeUserPasswordAsync(string username, string plainPassword)
        {
            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    if (connection is SqlConnection sqlConn) await sqlConn.OpenAsync();
                    else if (connection is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                    else connection.Open();

                    string matchedUser;
                    string hash = LookupPasswordHash(connection, username, out matchedUser);
                    if (hash == null) return;

                    string newHash = PasswordHelper.HashPassword(plainPassword);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"UPDATE {TableName} SET {ColPass} = @hash WHERE LOWER({ColUser}) = LOWER(@username)";
                        AddParameter(command, "@hash", newHash);
                        AddParameter(command, "@username", matchedUser);
                        if (command is SqlCommand sqlCmd) await sqlCmd.ExecuteNonQueryAsync();
                        else if (command is NpgsqlCommand pgCmd) await pgCmd.ExecuteNonQueryAsync();
                        else command.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
        {
            try
            {
                using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    if (connection is SqlConnection sqlConn) await sqlConn.OpenAsync();
                    else if (connection is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                    else connection.Open();

                    string matchedUser;
                    string storedHash = LookupPasswordHash(connection, username, out matchedUser);
                    if (storedHash == null) return false;

                    if (!PasswordHelper.VerifyPassword(oldPassword, storedHash) && storedHash != oldPassword)
                        return false;

                    string newHash = PasswordHelper.HashPassword(newPassword);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"UPDATE {TableName} SET {ColPass} = @hash WHERE LOWER({ColUser}) = LOWER(@username)";
                        AddParameter(command, "@hash", newHash);
                        AddParameter(command, "@username", matchedUser);
                        if (command is SqlCommand sqlCmd) await sqlCmd.ExecuteNonQueryAsync();
                        else if (command is NpgsqlCommand pgCmd) await pgCmd.ExecuteNonQueryAsync();
                        else command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
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
                    AddParameter(command, "@password", PasswordHelper.HashPassword(userModel.Password));
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
                    string passClause = string.IsNullOrEmpty(userModel.Password) ? "" : $", {ColPass}=@password";

                    command.CommandText = $"UPDATE {TableName} SET {ColUser}=@username, " +
                                          $"{ColName}=@name, {ColLast}=@lastname, {ColEmail}=@email, {ColRole}=@role " +
                                          $"{passClause} WHERE {ColId}=@id";

                    AddParameter(command, "@username", userModel.Username);
                    if (!string.IsNullOrEmpty(userModel.Password))
                    {
                        AddParameter(command, "@password", PasswordHelper.HashPassword(userModel.Password));
                    }
                    AddParameter(command, "@name", userModel.Name);
                    AddParameter(command, "@lastname", userModel.LastName);
                    AddParameter(command, "@email", userModel.Email);
                    AddParameter(command, "@role", userModel.Role ?? "User");

                    var paramId = command.CreateParameter();
                    paramId.ParameterName = "@id";
                    if (int.TryParse(userModel.Id, out int intId)) paramId.Value = intId;
                    else if (Guid.TryParse(userModel.Id, out Guid guidId)) paramId.Value = guidId;
                    else throw new ArgumentException("Invalid User ID format");
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
                    else return;
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
                        if (reader.Read()) user = MapReaderToUser(reader);
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

                // Step 1: try LOWER()
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

                // Step 2: Turkish fallback - full scan with C# comparison
                if (user == null)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"SELECT * FROM {TableName}";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var candidate = MapReaderToUser(reader);
                                if (TurkishIgnoreCaseEquals(candidate.Username, username))
                                {
                                    user = candidate;
                                    break;
                                }
                            }
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
                        while (reader.Read()) userList.Add(MapReaderToUser(reader));
                    }
                }
            }
            return userList;
        }

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
