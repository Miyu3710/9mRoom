using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NineMRoom.Server;

public sealed record AdminRecord(long Id, string Username, bool IsSuper);

public sealed record RoomRecord(
    string Code,
    string Name,
    string Kind,
    long? OwnerAdminId,
    string CreatedAt,
    string LastActiveAt);

public sealed class ServerDatabase
{
    private readonly string _connectionString;

    public ServerDatabase(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Initialize();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS admins (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL,
                is_super INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS rooms (
                code TEXT PRIMARY KEY COLLATE NOCASE,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                owner_admin_id INTEGER NULL,
                created_at TEXT NOT NULL,
                last_active_at TEXT NOT NULL,
                expires_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS sessions (
                token TEXT PRIMARY KEY,
                admin_id INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL
            );

            INSERT OR IGNORE INTO rooms(code, name, kind, owner_admin_id, created_at, last_active_at)
            VALUES ('demo', '演示房间', 'server', NULL, datetime('now'), datetime('now'));
            """;
        command.ExecuteNonQuery();

        // 旧数据库没有 is_super：补列后把最早的管理员提升为超级管理员。
        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "PRAGMA table_info(admins)";
        var hasSuper = false;
        using (var reader = columnCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "is_super", StringComparison.OrdinalIgnoreCase))
                {
                    hasSuper = true;
                    break;
                }
            }
        }

        if (!hasSuper)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE admins ADD COLUMN is_super INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }

        using var promote = connection.CreateCommand();
        promote.CommandText = """
            UPDATE admins
            SET is_super = 1
            WHERE id = (SELECT id FROM admins ORDER BY id LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM admins WHERE is_super = 1)
            """;
        promote.ExecuteNonQuery();
    }

    public bool HasAdmins()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM admins";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    public AdminRecord? CreateAdmin(string username, string password, bool isSuper = false)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO admins(username, password_hash, created_at, is_super)
            VALUES ($username, $hash, $created, $super)
            """;
        command.Parameters.AddWithValue("$username", username.Trim());
        command.Parameters.AddWithValue("$hash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("$created", Now());
        command.Parameters.AddWithValue("$super", isSuper ? 1 : 0);
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return null;
        }

        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid()";
        var id = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        return new AdminRecord(id, username.Trim(), isSuper);
    }

    public AdminRecord? Authenticate(string username, string password)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, password_hash, is_super FROM admins WHERE username = $username COLLATE NOCASE";
        command.Parameters.AddWithValue("$username", username.Trim());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var hash = reader.GetString(2);
        return PasswordHasher.Verify(password, hash)
            ? new AdminRecord(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(3) != 0)
            : null;
    }

    public AdminRecord? AdminById(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, is_super FROM admins WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AdminRecord(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2) != 0)
            : null;
    }

    public IReadOnlyList<AdminRecord> ListAdmins()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, is_super FROM admins ORDER BY is_super DESC, id";
        using var reader = command.ExecuteReader();
        var admins = new List<AdminRecord>();
        while (reader.Read())
        {
            admins.Add(new AdminRecord(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2) != 0));
        }

        return admins;
    }

    public bool SetAdminPassword(long id, string password)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE admins SET password_hash = $hash WHERE id = $id";
        command.Parameters.AddWithValue("$hash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteAdmin(long id)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM admins WHERE id = $id AND is_super = 0";
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }

            using var sessions = connection.CreateCommand();
            sessions.Transaction = transaction;
            sessions.CommandText = "DELETE FROM sessions WHERE admin_id = $id";
            sessions.Parameters.AddWithValue("$id", id);
            sessions.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public string CreateSession(long adminId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions(token, admin_id, created_at, expires_at)
            VALUES ($token, $admin, $created, $expires)
            """;
        command.Parameters.AddWithValue("$token", token);
        command.Parameters.AddWithValue("$admin", adminId);
        command.Parameters.AddWithValue("$created", Now());
        command.Parameters.AddWithValue("$expires", DateTimeOffset.UtcNow.AddHours(12).ToString("O"));
        command.ExecuteNonQuery();
        return token;
    }

    public AdminRecord? SessionAdmin(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.id, a.username, a.is_super, s.expires_at
            FROM sessions s JOIN admins a ON a.id = s.admin_id
            WHERE s.token = $token
            """;
        command.Parameters.AddWithValue("$token", token);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(reader.GetString(3), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expires) || expires <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new AdminRecord(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2) != 0);
    }

    public void DeleteSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE token = $token";
        command.Parameters.AddWithValue("$token", token);
        command.ExecuteNonQuery();
    }

    public RoomRecord? GetRoom(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT code, name, kind, owner_admin_id, created_at, last_active_at
            FROM rooms WHERE code = $code COLLATE NOCASE
            """;
        command.Parameters.AddWithValue("$code", code.Trim());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RoomRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt64(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    public List<RoomRecord> ListRooms()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT code, name, kind, owner_admin_id, created_at, last_active_at
            FROM rooms ORDER BY created_at DESC
            """;
        using var reader = command.ExecuteReader();
        var rooms = new List<RoomRecord>();
        while (reader.Read())
        {
            rooms.Add(new RoomRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return rooms;
    }

    public bool CreateRoom(string code, string name, string kind, long? ownerAdminId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rooms(code, name, kind, owner_admin_id, created_at, last_active_at)
            VALUES ($code, $name, $kind, $owner, $created, $active)
            """;
        command.Parameters.AddWithValue("$code", code.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$owner", (object?)ownerAdminId ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", Now());
        command.Parameters.AddWithValue("$active", Now());
        try
        {
            command.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public bool DeleteRoom(string code)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rooms WHERE code = $code COLLATE NOCASE";
        command.Parameters.AddWithValue("$code", code.Trim());
        return command.ExecuteNonQuery() > 0;
    }

    public void TouchRoom(string code)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE rooms SET last_active_at = $active WHERE code = $code COLLATE NOCASE";
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$active", Now());
        command.ExecuteNonQuery();
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("O");
}

public static class PasswordHasher
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
