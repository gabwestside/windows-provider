using CredentialProviderAPP.Config;
using CredentialProviderAPP.Utils;
using Microsoft.Data.Sqlite;

namespace CredentialProviderAPP.Services
{
    /// <summary>
    /// Gerencia as credenciais do administrador do sistema (tabela admin_credentials).
    /// Usa o mesmo banco MFA do projeto via AppConfig.DatabasePath.
    /// A criptografia é delegada ao CryptoHelper (AES-256-GCM).
    /// </summary>
    public static class AdminCredentialService
    {
        // ── usa o mesmo banco MFA configurado no AppConfig ────────────────────
        private static string ConnectionString =>
            $"Data Source={AppConfig.DatabasePath};";

        // ══════════════════════════════════════════════════════════════════════
        //  INICIALIZAÇÃO — cria tabela se não existir
        // ══════════════════════════════════════════════════════════════════════
        public static void InicializarBanco()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS admin_credentials (
                    id           INTEGER PRIMARY KEY,
                    login        TEXT    NOT NULL,
                    senha_enc    TEXT    NOT NULL,   -- AES-GCM base64(nonce + ciphertext + tag)
                    updated_at   TEXT    NOT NULL DEFAULT (datetime('now'))
                );

                -- Garante que sempre haverá no máximo 1 registro (id = 1)
                INSERT OR IGNORE INTO admin_credentials (id, login, senha_enc)
                VALUES (1, 'admin', '');
            ";
            cmd.ExecuteNonQuery();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CARREGAR — retorna (login, senhaDecifrada)
        // ══════════════════════════════════════════════════════════════════════
        public static (string login, string senha) Carregar()
        {
            InicializarBanco();

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT login, senha_enc FROM admin_credentials WHERE id = 1";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return ("admin", "");

            string login = reader.GetString(0);
            string senhaEnc = reader.GetString(1);

            string senha = string.IsNullOrWhiteSpace(senhaEnc)
                ? ""
                : CryptoHelper.Descriptografar(senhaEnc);

            return (login, senha);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SALVAR — persiste login + senha criptografada
        // ══════════════════════════════════════════════════════════════════════
        /// <param name="login">Login do administrador.</param>
        /// <param name="novaSenha">
        ///   Nova senha em texto puro. Se vazio, mantém a senha atual no banco.
        /// </param>
        public static void Salvar(string login, string novaSenha)
        {
            InicializarBanco();

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            // Se novaSenha for vazio, preserva o valor atual
            string senhaEnc;
            if (string.IsNullOrWhiteSpace(novaSenha))
            {
                var getCmdText = conn.CreateCommand();
                getCmdText.CommandText = "SELECT senha_enc FROM admin_credentials WHERE id = 1";
                var existing = getCmdText.ExecuteScalar() as string ?? "";
                senhaEnc = existing;
            }
            else
            {
                senhaEnc = CryptoHelper.Criptografar(novaSenha);
            }

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE admin_credentials
                SET login      = @login,
                    senha_enc  = @senha,
                    updated_at = datetime('now')
                WHERE id = 1;
            ";
            cmd.Parameters.AddWithValue("@login", login.Trim());
            cmd.Parameters.AddWithValue("@senha", senhaEnc);
            cmd.ExecuteNonQuery();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VERIFICAR — valida login + senha (para o fluxo "esqueci minha senha")
        // ══════════════════════════════════════════════════════════════════════
        public static bool Verificar(string login, string senha)
        {
            var (loginSalvo, senhaSalva) = Carregar();
            return string.Equals(loginSalvo, login, StringComparison.OrdinalIgnoreCase)
                && senhaSalva == senha;
        }
    }
}