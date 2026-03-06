using Microsoft.Data.Sqlite;

namespace CredentialProviderAPP;

public static class Database
{
    private static string dbPath =
        @"C:\Users\diego.viana\Documents\DEV\scriptdll\credentialProviderAPP.db";

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection($"Data Source={dbPath}");
    }

    public static (bool configured, string? secret) GetUser(string username)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT configured, totpsecret FROM users WHERE username = $user";
        cmd.Parameters.AddWithValue("$user", username);

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            bool configured = reader.GetInt32(0) == 1;
            string? secret = reader.IsDBNull(1) ? null : reader.GetString(1);

            return (configured, secret);
        }

        return (false, null);
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
}