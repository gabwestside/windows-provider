using System.ComponentModel;
using System.DirectoryServices;
using System.Windows;
using System.Windows.Input;
using OtpNet;
using CredentialProviderAPP.Utils;

namespace CredentialProviderAPP.Views;

public partial class ResetSenhaWindow : Window
{
    private bool autenticado = false;
    private bool cancelado = false;
    private bool mostrandoDialog = false;
    private bool _forcandoFoco = false; // ✅ evita loop

    private string secret = "";
    private string login;

    public ResetSenhaWindow(string login)
    {
        InitializeComponent();
        this.login = login;

        txtLogin.Text = login;

        string? secretAd = ObterSecretMFA(login);

        if (string.IsNullOrEmpty(secretAd))
        {
            Loaded += (s, e) =>
            {
                MessageBox.Show(
                    "Este usuário não possui MFA configurado no Active Directory.",
                    "Segurança",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                Close();
            };
            return;
        }

        secret = secretAd;
    }

    private string? ObterSecretMFA(string login)
    {
        try
        {
            string ldap   = ConfigHelper.Get("ActiveDirectory:LDAP");
            string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
            string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");

            using var root = new DirectoryEntry(ldap, adUser, adSenha, AuthenticationTypes.Secure);

            var user = LdapHelper.Escape(LdapHelper.NormalizeLogin(login));

            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(samAccountName={user}))"
            };
            searcher.PropertiesToLoad.Add("info");

            var result = searcher.FindOne();
            if (result == null) return null;

            string? valor = result.Properties["info"].Count > 0
                ? result.Properties["info"][0].ToString()
                : null;

            if (string.IsNullOrWhiteSpace(valor) ||
                valor.Equals("setup", StringComparison.OrdinalIgnoreCase))
                return null;

            return valor;
        }
        catch { return null; }
    }

    private void ForcarFoco()
    {
        if (_forcandoFoco) return;

        _forcandoFoco = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Topmost = true;
                Activate();
                Focus();
                Keyboard.Focus(txtCode);
            }
            finally
            {
                _forcandoFoco = false;
            }
        }));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ForcarFoco();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // ✅ não força foco se já está autenticado (NovaSenhaWindow está aberto)
        if (mostrandoDialog || _forcandoFoco || autenticado)
            return;

        ForcarFoco();
    }

    private void Validar_Click(object sender, RoutedEventArgs e)
    {
        string code = txtCode.Text.Trim();

        if (code.Length != 6)
        {
            Mostrar("Digite o código de 6 dígitos.");
            return;
        }

        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);

        bool valid = totp.VerifyTotp(
            code,
            out long _,
            new VerificationWindow(1, 1)
        );

        if (!valid)
        {
            Mostrar("Código inválido.");
            txtCode.Clear();
            txtCode.Focus();
            return;
        }

        autenticado = true; // ✅ seta ANTES de abrir NovaSenhaWindow
        Hide();

        var novaSenha = new NovaSenhaWindow(login);
        novaSenha.ShowDialog(); // ✅ sem Topmost aqui — NovaSenhaWindow controla o próprio foco

        Close();
    }

    private void Mostrar(string msg)
    {
        mostrandoDialog = true;
        MessageBox.Show(msg);
        mostrandoDialog = false;
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

        var result = MessageBox.Show(
            "Deseja cancelar o processo de redefinição de senha?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        mostrandoDialog = false;

        if (result == MessageBoxResult.No)
            e.Cancel = true;
        else
            cancelado = true;
    }
}