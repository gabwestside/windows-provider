using CredentialProviderAPP.Data;
using OtpNet;
using System.ComponentModel;
using System.Windows;

namespace CredentialProviderAPP.Views
{
    public partial class ResetSenhaWindow : Window
    {
        private bool autenticado = false;
        private bool cancelado = false;
        private bool mostrandoDialog = false;

        public ResetSenhaWindow(string login)
        {
            InitializeComponent();
            txtLogin.Text = login;

            var user = Database.GetUser(login);
            var (mfaenabled, configured, secret) = user;

            // 🔒 BLOQUEIA reset se não tiver MFA configurado
            if (!configured || string.IsNullOrEmpty(secret))
            {
                Loaded += (s, e) =>
                {
                    ModernMessageBox.Show(
                        "Este usuário não possui MFA configurado.\n\n" +
                        "Por segurança a redefinição de senha só é permitida para contas com MFA ativo.\n\n" +
                        "Entre em contato com o suporte de TI.",
                        "Segurança",
                        ModernMessageBox.Kind.Warning
                    );

                    Close();
                };

                return;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!autenticado)
                txtCode.Focus();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (mostrandoDialog)
                return;

            if (!autenticado && !cancelado)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Topmost = true;
                    Activate();
                }));
            }
        }

        private void Validar_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string code = txtCode.Text.Trim();

            var user = Database.GetUser(login);
            var (mfaenabled, configured, secret) = user;

            // 🔒 segurança extra
            if (!configured || string.IsNullOrEmpty(secret))
            {
                ModernMessageBox.Show(
                    "Reset de senha só é permitido para contas com MFA ativo.",
                    "Segurança",
                    ModernMessageBox.Kind.Warning
                );

                Close();
                return;
            }

            if (code.Length != 6)
            {
                mostrandoDialog = true;
                ModernMessageBox.Show("Digite o código de 6 dígitos.");
                mostrandoDialog = false;

                txtCode.Focus();
                return;
            }

            var key = Base32Encoding.ToBytes(secret);
            var totp = new Totp(key);

            bool valid = totp.VerifyTotp(
                code,
                out long step,
                new VerificationWindow(1, 1)
            );

            if (!valid)
            {
                mostrandoDialog = true;
                ModernMessageBox.Show("Código inválido.");
                mostrandoDialog = false;

                txtCode.Clear();
                txtCode.Focus();
                return;
            }

            // ✅ MFA validado
            autenticado = true;

            Hide();

            NovaSenhaWindow novaSenha = new(login)
            {
                Topmost = true
            };
            novaSenha.ShowDialog();

            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            cancelado = true;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (autenticado || cancelado)
                return;

            mostrandoDialog = true;

            var result = ModernMessageBox.ShowYesNo(
                "Deseja cancelar o processo de redefinição de senha?",
                "Cancelar",
                ModernMessageBox.Kind.Warning
            );

            mostrandoDialog = false;

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                cancelado = true;
            }
        }
    }
}