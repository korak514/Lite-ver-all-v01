using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;

namespace SetupWizard
{
    class Program
    {
        static string pgBin, pgData, pgPort, dbName, dbPassword;
        static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm");
        static string setupDir => AppDomain.CurrentDomain.BaseDirectory;

        static void Main()
        {
            Console.Title = "WPF-LoginForm Setup Wizard";
            WriteColor("========================================", ConsoleColor.Cyan);
            WriteColor("     WPF-LoginForm Auto Installer", ConsoleColor.Cyan);
            WriteColor("========================================", ConsoleColor.Cyan);
            Console.ResetColor();
            Console.WriteLine("No sign-up needed. Uses portable PostgreSQL.");
            Console.WriteLine();

            // 1. Collect inputs
            Console.Write("Database name [MainDataDb]: ");
            dbName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(dbName)) dbName = "MainDataDb";

            Console.Write("Postgres superuser password: ");
            dbPassword = ReadPassword();

            Console.Write("Port [5432]: ");
            pgPort = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(pgPort)) pgPort = "5432";
            Console.WriteLine();

            // 2. Run steps (auto-skips if done)
            bool ok = true;
            ok &= Step("Locate PostgreSQL", FindPostgreSQL);
            ok &= Step("Database init", InitPostgreSQL);
            ok &= Step("Start PostgreSQL", StartPostgreSQL);
            ok &= Step("Create databases/users", SetupDatabases);
            ok &= Step("Configuration file", WriteConfig);

            if (!ok)
            {
                WriteColor("\nSetup finished with errors.", ConsoleColor.Red);
                PauseAndExit(); return;
            }
            LaunchApp();
        }

        static bool Step(string name, Func<bool> action)
        {
            Console.Write($"{name}... ");
            try
            {
                if (action()) { WriteColor("OK", ConsoleColor.Green); Console.WriteLine(); return true; }
                WriteColor("FAILED", ConsoleColor.Red); Console.WriteLine(); return false;
            }
            catch (Exception ex)
            {
                WriteColor("ERROR", ConsoleColor.Red); Console.WriteLine("\n  " + ex.Message); return false;
            }
        }

        static bool IsConfigDone() => File.Exists(Path.Combine(appDataPath, "general_config.json"));
        static bool IsPgReady() => pgBin != null && File.Exists(Path.Combine(pgBin, "pg_isready.exe"));
        static bool IsPgRunning() => IsPgReady() && Run(pgBin, "pg_isready", $"-p {pgPort}").ExitCode == 0;
        static bool IsDataDir() => pgData != null && Directory.Exists(pgData);

        static bool FindPostgreSQL()
        {
            if (IsPgReady()) { WriteColor("already found", ConsoleColor.Yellow); return true; }

            // Look for portable zip
            string zipPath = Path.Combine(setupDir, "postgresql-portable.zip");
            if (File.Exists(zipPath))
            {
                string extractDir = Path.Combine(setupDir, "pgsql");
                if (!Directory.Exists(extractDir))
                {
                    Console.Write("\n  Extracting PostgreSQL... ");
                    ZipFile.ExtractToDirectory(zipPath, extractDir);
                }

                // Find bin folder (handle flat or nested pgsql/pgsql structure)
                string[] candidates =
                {
                    Path.Combine(extractDir, "bin"),
                    Path.Combine(extractDir, "pgsql", "bin"),
                    Path.Combine(extractDir, "postgresql", "bin")
                };
                pgBin = candidates.FirstOrDefault(d => File.Exists(Path.Combine(d, "initdb.exe")));

                if (pgBin == null)
                {
                    var subDir = Directory.GetDirectories(extractDir).FirstOrDefault();
                    if (subDir != null)
                        pgBin = new[] { Path.Combine(subDir, "bin") }
                            .FirstOrDefault(d => File.Exists(Path.Combine(d, "initdb.exe")));
                }

                if (pgBin == null)
                {
                    WriteColor("\n  initdb.exe not found in zip.", ConsoleColor.Red);
                    WriteLineColor("\n  Download portable zip from:", ConsoleColor.Yellow);
                    WriteLineColor("  https://www.enterprisedb.com/download-postgresql-binaries", ConsoleColor.Yellow);
                    WriteLineColor("  (no sign-up required)", ConsoleColor.Yellow);
                    return false;
                }

                pgData = Path.Combine(setupDir, "pgdata");
                return true;
            }

            // Ask for path as fallback
            Console.Write("\n  PostgreSQL portable zip not found.\n  Enter PostgreSQL bin folder path (or download zip from link above): ");
            string path = Console.ReadLine();
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "initdb.exe")))
            {
                pgBin = path;
                pgData = Path.Combine(setupDir, "pgdata");
                return true;
            }

            WriteColor("  initdb.exe not found at that path.", ConsoleColor.Red);
            return false;
        }

        static bool InitPostgreSQL()
        {
            if (!IsPgReady()) return false;

            if (IsDataDir())
            {
                string pgFile = Path.Combine(pgData, "PG_VERSION");
                if (!File.Exists(pgFile))
                {
                    WriteColor("\n  pgdata exists but is corrupted, re-initializing...", ConsoleColor.Yellow);
                    try { Directory.Delete(pgData, true); }
                    catch (Exception ex) { WriteColor($"\n  Could not delete pgdata: {ex.Message}", ConsoleColor.Red); return false; }
                }
                else
                {
                    WriteColor("already exists", ConsoleColor.Yellow);
                    return true;
                }
            }

            // Try with explicit English locale first, fallback to --no-locale
            string localeArg = "--locale=\"English_United States.1252\"";
            var r = Run(pgBin, "initdb", $"-D \"{pgData}\" --username=postgres --auth=trust {localeArg} -E UTF8", 60000);

            if (r.ExitCode != 0 && r.StdErr.Contains("non-ASCII"))
            {
                WriteColor("\n  locale failed, retrying with no-locale...", ConsoleColor.Yellow);
                r = Run(pgBin, "initdb", $"-D \"{pgData}\" --username=postgres --auth=trust --no-locale -E UTF8", 60000);
            }

            if (r.ExitCode != 0) { WriteColor("\n  " + r.StdErr, ConsoleColor.Red); return false; }
            return true;
        }

        static bool StartPostgreSQL()
        {
            if (IsPgRunning()) { WriteColor("already running", ConsoleColor.Yellow); return true; }
            if (!IsDataDir()) return false;

            // Kill any stale postgres processes on our port
            Run(null, "taskkill", "/F /IM postgres.exe", 5000);

            // Try with -w (wait up to 60s), then poll for 30s more if needed
            var r = Run(pgBin, "pg_ctl", $"start -D \"{pgData}\" -l \"{pgData}\\pg.log\" -w -o \"-p {pgPort}\"", 120000, false, false);
            if (r.ExitCode == 0 || r.TimedOut)
            {
                // Poll until ready
                for (int i = 0; i < 15; i++)
                {
                    if (IsPgRunning()) return true;
                    System.Threading.Thread.Sleep(2000);
                }
            }

            WriteColor($"\n  PostgreSQL failed to start.", ConsoleColor.Red);
            if (!string.IsNullOrWhiteSpace(r.StdErr))
                WriteColor($"\n  " + r.StdErr, ConsoleColor.Red);

            // Show pg_log tail
            string logPath = pgData + "\\pg.log";
            if (File.Exists(logPath))
            {
                var lines = File.ReadAllLines(logPath);
                var tail = lines.Skip(Math.Max(0, lines.Length - 5));
                WriteLineColor("\n  Last log entries:", ConsoleColor.Yellow);
                foreach (var l in tail) Console.WriteLine("    " + l);
            }
            return false;
        }

        static bool SetupDatabases()
        {
            if (!IsPgRunning()) return false;

            string conn = $"-p {pgPort} -U postgres";

            var pw = Run(pgBin, "psql", $"{conn} -d postgres -c \"ALTER USER postgres WITH PASSWORD '{dbPassword}';\"");
            if (pw.StdErr.Contains("could not connect"))
            { WriteColor("\n  Cannot connect: " + pw.StdErr, ConsoleColor.Red); return false; }

            foreach (var db in new[] { "LoginDb", dbName })
            {
                var r = Run(pgBin, "psql", $"{conn} -d postgres -c \"CREATE DATABASE \\\"{db}\\\";\"");
                if (r.ExitCode != 0 && !r.StdErr.Contains("already exists"))
                { WriteColor($"\n  DB '{db}' failed: " + r.StdErr, ConsoleColor.Red); return false; }
            }

            var ru = Run(pgBin, "psql", $"{conn} -d postgres -c \"CREATE USER app_user WITH SUPERUSER PASSWORD '{dbPassword}';\"");
            if (ru.ExitCode != 0 && !ru.StdErr.Contains("already exists"))
            { WriteColor("\n  User failed: " + ru.StdErr, ConsoleColor.Red); return false; }

            // Create auth tables (User, Logs) in LoginDb
            foreach (var db in new[] { "LoginDb" })
            {
                Run(pgBin, "psql", $"{conn} -d \"{db}\" -c \"CREATE TABLE IF NOT EXISTS \\\"User\\\" (\\\"Id\\\" SERIAL PRIMARY KEY, \\\"Username\\\" VARCHAR(50) UNIQUE, \\\"Password\\\" VARCHAR(100), \\\"Name\\\" VARCHAR(50), \\\"LastName\\\" VARCHAR(50), \\\"Email\\\" VARCHAR(50), \\\"Role\\\" VARCHAR(50) DEFAULT 'User');\"");
                Run(pgBin, "psql", $"{conn} -d \"{db}\" -c \"CREATE TABLE IF NOT EXISTS \\\"Logs\\\" (\\\"Id\\\" SERIAL PRIMARY KEY, \\\"LogLevel\\\" VARCHAR(50), \\\"Message\\\" TEXT, \\\"Username\\\" VARCHAR(50), \\\"Exception\\\" TEXT, \\\"LogDate\\\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP);\"");

                // Insert default admin if not exists
                string adminHash = HashPassword("admin");
                Run(pgBin, "psql", $"{conn} -d \"{db}\" -c \"INSERT INTO \\\"User\\\" (\\\"Username\\\", \\\"Password\\\", \\\"Name\\\", \\\"LastName\\\", \\\"Email\\\", \\\"Role\\\") SELECT 'admin', '{adminHash}', 'System', 'Admin', 'admin@biosun.com', 'Admin' WHERE NOT EXISTS (SELECT 1 FROM \\\"User\\\" WHERE LOWER(\\\"Username\\\") = 'admin');\"");
            }

            return true;
        }

        static bool WriteConfig()
        {
            if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

            string connStr = $"Host=localhost;Port={pgPort};Username=app_user;Password={dbPassword}";
            var config = new Dictionary<string, object>
            {
                ["DbProvider"] = "PostgreSQL",
                ["PostgresAuthConnString"] = connStr + ";Database=LoginDb",
                ["PostgresDataConnString"] = connStr + $";Database={dbName}",
                ["AppLanguage"] = "en-US", ["ConnectionTimeout"] = 15, ["TrustServerCertificate"] = true,
                ["DbHost"] = "localhost", ["DbPort"] = pgPort, ["DbUser"] = "app_user",
                ["AutoImportEnabled"] = false, ["ImportIsRelative"] = true, ["ImportFileName"] = "dashboard_config.json",
                ["DefaultRowLimit"] = 500, ["ShowDashboardDateFilter"] = true, ["DashboardDateTickSize"] = 1,
                ["PureOfflineMode"] = false, ["SuppressOfflineReminder"] = false
            };
            File.WriteAllText(Path.Combine(appDataPath, "general_config.json"), new JavaScriptSerializer().Serialize(config));
            return true;
        }

        static void LaunchApp()
        {
            Console.WriteLine();
            WriteColor("========================================\n", ConsoleColor.Cyan);
            WriteColor("  Setup Complete!\n", ConsoleColor.Green);
            WriteColor("========================================\n", ConsoleColor.Cyan);
            Console.WriteLine();
            WriteLineColor("  PostgreSQL server: localhost:" + pgPort, ConsoleColor.White);
            WriteLineColor("  Databases: LoginDb, " + dbName, ConsoleColor.White);
            WriteLineColor("  User: app_user (superuser)", ConsoleColor.White);
            Console.WriteLine();
            WriteLineColor("  To connect in pgAdmin:", ConsoleColor.Yellow);
            WriteLineColor("  1. Open pgAdmin", ConsoleColor.Yellow);
            WriteLineColor("  2. Right-click 'Servers' -> Register -> Server", ConsoleColor.Yellow);
            WriteLineColor("  3. Name: PostgreSQL 16 (or any name)", ConsoleColor.Yellow);
            WriteLineColor("  4. Connection tab:", ConsoleColor.Yellow);
            WriteLineColor("     Host: localhost", ConsoleColor.Yellow);
            WriteLineColor("     Port: " + pgPort, ConsoleColor.Yellow);
            WriteLineColor("     Username: app_user", ConsoleColor.Yellow);
            WriteLineColor("     Password: (the one you entered)", ConsoleColor.Yellow);
            WriteLineColor("  5. Click Save", ConsoleColor.Yellow);
            Console.WriteLine();

            RegisterPgAdminServer();

            string exe = Path.Combine(setupDir, "WPF-LoginForm.exe");
            if (File.Exists(exe)) { Console.Write("Launching application..."); Process.Start(exe); Console.WriteLine(" OK"); }
            else WriteLineColor($"WPF-LoginForm.exe not found in:\n{setupDir}", ConsoleColor.Yellow);
            Console.WriteLine("\nPress any key to exit..."); Console.ReadKey();
        }

        static void RegisterPgAdminServer()
        {
            try
            {
                string pgAdminDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "pgAdmin", "pgadmin4.db");
                if (!File.Exists(pgAdminDb)) return;

                string sqlite = new[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "pgAdmin 4", "venv", "Scripts", "sqlite3.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "pgAdmin 4", "runtime", "sqlite3.exe"),
                    "sqlite3.exe"
                }.FirstOrDefault(File.Exists);
                if (sqlite == null) return;

                string tmpSql = Path.GetTempFileName() + ".sql";
                File.WriteAllText(tmpSql, $@"
INSERT OR IGNORE INTO server(id, user_id, servergroup_id, name, host, port, username, ssl_mode, comment)
SELECT COALESCE(MAX(id), 0) + 1, (SELECT MIN(id) FROM user_), (SELECT MIN(id) FROM servergroup), 'PostgreSQL 16', 'localhost', {pgPort}, 'app_user', 'prefer', 'Auto-created'
FROM server;
");
                Run(null, "cmd.exe", $"/c \"\"{sqlite}\" \"{pgAdminDb}\" < \"{tmpSql}\"\"", 10000);
                try { File.Delete(tmpSql); } catch { }
            }
            catch { }
        }

        static void PauseAndExit() { Console.WriteLine("\nPress any key to exit..."); Console.ReadKey(); }

        static (int ExitCode, string StdErr, bool TimedOut) Run(string folder, string exe, string args, int timeoutMs = 60000, bool envClear = false, bool redirect = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = folder != null ? Path.Combine(folder, exe) : exe,
                Arguments = args,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = redirect, RedirectStandardOutput = redirect
            };
            if (envClear)
            {
                psi.EnvironmentVariables["LC_ALL"] = "C";
                psi.EnvironmentVariables["LC_CTYPE"] = "C";
                psi.EnvironmentVariables["LC_COLLATE"] = "C";
                psi.EnvironmentVariables["LANG"] = "C";
            }
            var p = new Process { StartInfo = psi };
            
            System.Text.StringBuilder errSb = new System.Text.StringBuilder();
            if (redirect)
            {
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) errSb.AppendLine(e.Data); };
                p.OutputDataReceived += (s, e) => { }; // drain output to prevent buffer full
            }

            p.Start();

            if (redirect)
            {
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
            }

            bool exited = p.WaitForExit(timeoutMs);
            string err = "";
            if (exited) 
            { 
                if (redirect)
                {
                    p.WaitForExit(); // Wait for output streams to completely finish
                    err = errSb.ToString();
                }
                return (p.ExitCode, err, false); 
            }
            try { p.Kill(); } catch { }
            return (-1, "Timed out after " + (timeoutMs / 1000) + "s", true);
        }

        static string ReadPassword()
        {
            string pass = Console.ReadLine();
            return pass;
        }

        static void WriteColor(string m, ConsoleColor c) { var o = Console.ForegroundColor; Console.ForegroundColor = c; Console.Write(m); Console.ForegroundColor = o; }
        static void WriteLineColor(string m, ConsoleColor c) { var o = Console.ForegroundColor; Console.ForegroundColor = c; Console.WriteLine(m); Console.ForegroundColor = o; }

        static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}
