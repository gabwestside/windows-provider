using Microsoft.Data.Sqlite;

namespace CredentialProviderAPP.Data;

public static class Database
{
    private static string dbPath =
        @"C:\CredentialProvider\mfa.db";

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection($"Data Source={dbPath}");
    }

    public static (bool mfaenabled, bool configured, string? secret) GetUser(string username)
    {
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
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();

        cmd.CommandText =
        @"UPDATE users
      SET totpsecret = $secret,
          configured = 1
      WHERE username = $user";

        cmd.Parameters.AddWithValue("$secret", secret);
        cmd.Parameters.AddWithValue("$user", username);

        cmd.ExecuteNonQuery();
    }

    public static void SetConfigured(string username, bool configured)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();

        cmd.CommandText =
        @"UPDATE users
      SET configured = $configured
      WHERE username = $user";

        cmd.Parameters.AddWithValue("$configured", configured ? 1 : 0);
        cmd.Parameters.AddWithValue("$user", username);

        cmd.ExecuteNonQuery();
    }
}