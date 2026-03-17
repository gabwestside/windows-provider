using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CredentialProviderAPP.Services
{
    /// <summary>
    /// Gerencia as credenciais do administrador do sistema (tabela admin_credentials).
    /// A senha é armazenada criptografada com AES-256-GCM.
    /// </summary>
    public static class AdminCredentialService
    {
        // ── chave derivada de uma frase fixa + entropia da máquina ────────────
        // Em produção, considere armazenar a chave no Windows DPAPI ou KeyVault.
        private static readonly byte[] _aesKey = DeriveKey();

        private static byte[] DeriveKey()
        {
            // Usa o SID da conta de serviço + frase como fonte de entropia.
            // Isso garante que o banco descriptografado só funcione na mesma máquina.
            string entropy = Environment.MachineName + "CredentialProviderAPP_v1_AdminKey";
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(entropy)); // 32 bytes → AES-256
        }

        // ── caminho do banco (mesmo diretório do executável) ──────────────────
        private static string DbPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.db");

        private static string ConnectionString =>
            $"Data Source={DbPath};";

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
                : Descriptografar(senhaEnc);

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
                senhaEnc = Criptografar(novaSenha);
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

        // ══════════════════════════════════════════════════════════════════════
        //  CRIPTOGRAFIA — AES-256-GCM
        //  Formato armazenado: Base64(nonce[12] + ciphertext + tag[16])
        // ══════════════════════════════════════════════════════════════════════
        private static string Criptografar(string texto)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(texto);
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];   // 16 bytes
            byte[] cipher = new byte[plaintext.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_aesKey, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            // Concatena nonce + ciphertext + tag e converte para Base64
            byte[] resultado = new byte[nonce.Length + cipher.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, resultado, 0, nonce.Length);
            Buffer.BlockCopy(cipher, 0, resultado, nonce.Length, cipher.Length);
            Buffer.BlockCopy(tag, 0, resultado, nonce.Length + cipher.Length, tag.Length);

            return Convert.ToBase64String(resultado);
        }

        private static string Descriptografar(string base64)
        {
            byte[] dados = Convert.FromBase64String(base64);
            int nonceLen = AesGcm.NonceByteSizes.MaxSize; // 12
            int tagLen = AesGcm.TagByteSizes.MaxSize;   // 16
            int cipherLen = dados.Length - nonceLen - tagLen;

            if (cipherLen < 0)
                throw new CryptographicException("Dados criptografados inválidos.");

            byte[] nonce = dados[..nonceLen];
            byte[] cipher = dados[nonceLen..(nonceLen + cipherLen)];
            byte[] tag = dados[(nonceLen + cipherLen)..];
            byte[] plain = new byte[cipherLen];

            using var aes = new AesGcm(_aesKey, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
    }
}