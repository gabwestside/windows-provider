using Microsoft.Data.Sqlite;
using CredentialProviderAPP.Config;
using System.IO;

namespace CredentialProviderAPP.Data;

public static class Database
{
    public static SqliteConnection GetConnection()
    {
        var path = AppConfig.DatabasePath;

        var folder = Path.GetDirectoryName(path);

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder!);

        return new SqliteConnection($"Data Source={path}");
    }


    public static (bool mfaenabled, bool configured, string? secret) GetUser(string username)
    {
        username = Normalize(username);

        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText =
        @"SELECT mfaenabled, configured, totpsecret
          FROM users
          WHERE lower(username) = lower($user)";

        cmd.Parameters.AddWithValue("$user", username);

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            bool mfaenabled = reader.GetInt32(0) == 1;
            bool configured = reader.GetInt32(1) == 1;
            string? secret = reader.IsDBNull(2) ? null : reader.GetString(2);

            return (mfaenabled, configured, secret);
        }

        return (false, false, null);
    }

    public static void SaveSecret(string username, string secret)
    {
        username = Normalize(username);

        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();

        cmd.CommandText =
        @"UPDATE users
          SET totpsecret = $secret,
              configured = 1
          WHERE lower(username) = lower($user)";

        cmd.Parameters.AddWithValue("$secret", secret);
        cmd.Parameters.AddWithValue("$user", username);

        int rows = cmd.ExecuteNonQuery();

        if (rows == 0)
        {
            throw new Exception("Usuário não encontrado ao salvar MFA.");
        }
    }

    public static string Normalize(string user)
    {
        user = user.Trim();

        if (user.Contains("\\"))
            user = user.Split('\\')[1];

        if (user.Contains("@"))
            user = user.Split('@')[0];

        return user; // NÃO altera maiúsculo/minúsculo
    }
}