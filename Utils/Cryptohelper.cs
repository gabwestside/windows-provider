using System.Security.Cryptography;
using System.Text;

namespace CredentialProviderAPP.Utils
{
    /// <summary>
    /// Utilitário de criptografia AES-256-GCM.
    /// Formato armazenado: Base64(nonce[12] + ciphertext + tag[16])
    /// A chave é derivada do MachineName, garantindo que os dados
    /// só possam ser descriptografados na mesma máquina.
    /// </summary>
    public static class CryptoHelper
    {
        private static readonly byte[] _key = DeriveKey();

        private static byte[] DeriveKey()
        {
            string entropy = Environment.MachineName + "CredentialProviderAPP_v1_AdminKey";
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(entropy)); // 32 bytes → AES-256
        }

        /// <summary>
        /// Criptografa um texto puro e retorna uma string Base64 segura para armazenar.
        /// </summary>
        public static string Criptografar(string texto)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(texto);
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];   // 16 bytes
            byte[] cipher = new byte[plaintext.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            // Layout: nonce[12] + ciphertext[n] + tag[16]
            byte[] resultado = new byte[nonce.Length + cipher.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, resultado, 0, nonce.Length);
            Buffer.BlockCopy(cipher, 0, resultado, nonce.Length, cipher.Length);
            Buffer.BlockCopy(tag, 0, resultado, nonce.Length + cipher.Length, tag.Length);

            return Convert.ToBase64String(resultado);
        }

        /// <summary>
        /// Descriptografa uma string Base64 gerada por <see cref="Criptografar"/>.
        /// Lança <see cref="CryptographicException"/> se os dados forem inválidos ou adulterados.
        /// </summary>
        public static string Descriptografar(string base64)
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

            using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
    }
}