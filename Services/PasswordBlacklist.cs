using CredentialProviderAPP.Config;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class PasswordBlacklist
{
    private static HashSet<string>? cache;

    public static HashSet<string> LoadWords()
    {
        if (cache != null)
            return cache;

        var path = AppConfig.PasswordBlacklistPath;

        if (!File.Exists(path))
        {
            cache = new HashSet<string>();
            return cache;
        }

        cache = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim().ToLower())
            .ToHashSet();

        return cache;
    }

    public static bool ContainsForbiddenWord(string password)
    {
        return GetForbiddenWord(password) != null;
    }

    public static string? GetForbiddenWord(string password)
    {
        var words = LoadWords();

        string normalized = password.ToLower();

        foreach (var word in words)
        {
            if (normalized.Contains(word))
                return word;
        }

        return null;
    }
}