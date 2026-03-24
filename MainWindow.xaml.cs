using CredentialProviderAPP.Views;
using CredentialProviderAPP.Utils;
using OtpNet;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.DirectoryServices;

namespace CredentialProviderAPP
{
    public partial class MainWindow : Window
    {
        private byte[]? currentKey;
        private string? currentSecret;

        private bool autenticado = false;
        private bool mostrandoDialog = false;

        private readonly string loginAtual = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(string login)
        {
            InitializeComponent();

            if (string.IsNullOrEmpty(login))
                return;

            loginAtual = login;
            txtUser.Text = login;
            txtUser.IsReadOnly = true;
            btnBuscar.Visibility = Visibility.Collapsed;

            string? valorInfo = ObterInfoAD(login);

            // vazio → MFA não habilitado pelo admin → deixa passar
            if (string.IsNullOrWhiteSpace(valorInfo))
            {
                Environment.Exit(0);
                return;
            }

            // "setup" → habilitado mas ainda não configurado → mostrar QR
            if (valorInfo.Equals("setup", StringComparison.OrdinalIgnoreCase))
            {
                lblMensagem.Text =
    $@"Bem-vindo {login}

Para proteger sua conta,
gere agora o QR Code para configurar
a autenticação em dois fatores.";

                btnGerarQR.Visibility = Visibility.Visible;
                return;
            }

            // qualquer outro valor → é o secret → validar código
            VerificarCodigoWindow win = new VerificarCodigoWindow(valorInfo);
            win.ShowDialog();
            Environment.Exit(0);
        }

        private string? ObterInfoAD(string login)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
                using var root = CriarDirectoryEntry(ldap);

                var user = LdapHelper.Escape(LdapHelper.NormalizeLogin(login));
                using var searcher = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(samAccountName={user}))"
                };
                searcher.PropertiesToLoad.Add("info");

                var result = searcher.FindOne();
                if (result == null) return null;

                return result.Properties["info"].Count > 0
                    ? result.Properties["info"][0].ToString()
                    : null;
            }
            catch { return null; }
        }
        // ✅ direto com credenciais — sem tentativa anônima
        private DirectoryEntry CriarDirectoryEntry(string ldap)
        {
            string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
            string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");
            return new DirectoryEntry(ldap, adUser, adSenha, AuthenticationTypes.Secure);
        }

        private void SalvarSecretAD(string login, string secret)
        {
            string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
            string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
            string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");

            using var root = CriarDirectoryEntry(ldap);

            var user = LdapHelper.Escape(LdapHelper.NormalizeLogin(login));
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(samAccountName={user}))"
            };

            var result = searcher.FindOne();
            if (result == null) throw new Exception("Usuário não encontrado no AD.");

            // ✅ entry também com credenciais explícitas
            using var entry = new DirectoryEntry(result.Path, adUser, adSenha, AuthenticationTypes.Secure);
            entry.Properties["info"].Value = secret;
            entry.CommitChanges();
        }

        private void BuscarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUser.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                MostrarMensagem("Digite um usuário.");
                return;
            }

            txtUser.IsReadOnly = true;
            btnBuscar.Visibility = Visibility.Collapsed;

            string? valorInfo = ObterInfoAD(username);

            if (string.IsNullOrWhiteSpace(valorInfo))
            {
                Environment.Exit(0);
                return;
            }

            if (valorInfo.Equals("setup", StringComparison.OrdinalIgnoreCase))
            {
                lblMensagem.Text =
    $@"Bem-vindo {username}

Para proteger sua conta,
gere agora o QR Code para configurar
a autenticação em dois fatores.";

                btnGerarQR.Visibility = Visibility.Visible;
                return;
            }

            // secret → validar
            VerificarCodigoWindow win = new VerificarCodigoWindow(valorInfo);
            win.ShowDialog();
        }

        private void GerarQR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = txtUser.Text.Trim();

                currentKey = KeyGeneration.GenerateRandomKey(20);
                currentSecret = Base32Encoding.ToString(currentKey);

                string issuer = "CredentialProvider";
                string url = $"otpauth://totp/{issuer}:{username}?secret={currentSecret}&issuer={issuer}";

                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                Bitmap qrBitmap = qrCode.GetGraphic(20);

                imgQR.Source = BitmapToImageSource(qrBitmap);
                imgQR.Visibility = Visibility.Visible;

                lblMensagem.Text =
    @"Escaneie o QR Code no Google Authenticator.

Agora valide o código gerado
para confirmar a configuração.";

                txtCode.Visibility = Visibility.Visible;
                btnValidar.Visibility = Visibility.Visible;
                btnGerarQR.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MostrarMensagem(ex.ToString());
            }
        }

        private void ValidarCodigo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = txtUser.Text.Trim();
                string code = txtCode.Text.Trim();

                if (currentKey == null || currentSecret == null)
                    return;

                var totp = new Totp(currentKey);
                bool valid = totp.VerifyTotp(code, out long _, new VerificationWindow(1, 1));

                if (!valid)
                {
                    MostrarMensagem("Código inválido.");
                    return;
                }

                // ✅ grava secret no AD em vez do banco
                SalvarSecretAD(username, currentSecret);

                autenticado = true;
                MostrarMensagem("MFA configurado com sucesso!");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MostrarMensagem(ex.ToString());
            }
        }

        private void MostrarMensagem(string msg)
        {
            mostrandoDialog = true;
            ModernMessageBox.Show(msg);
            mostrandoDialog = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!autenticado) Environment.Exit(1);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (mostrandoDialog) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Topmost = true;
                Activate();
            }));
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            return bitmapImage;
        }

    }
}