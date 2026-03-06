using OtpNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CredentialProviderAPP.Data;

namespace CredentialProviderAPP.Views;

public partial class ResetSenhaWindow : Window
{
    private bool autenticado = false;
    private bool mostrandoDialog = false;

    public ResetSenhaWindow(string login)
    {
        InitializeComponent();

        txtLogin.Text = login;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtCode.Focus();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Topmost = true;
            Activate();
        }));
    }

    private void Validar_Click(object sender, RoutedEventArgs e)
    {
        string login = txtLogin.Text.Trim();
        string code = txtCode.Text.Trim();

        if (string.IsNullOrEmpty(login))
        {
            MessageBox.Show("Digite o login.");
            return;
        }

        var user = Database.GetUser(login);

        var (mfaenabled, configured, secret) = user;

        if (!configured || secret == null)
        {
            MessageBox.Show("Usuário não configurado para MFA.");
            return;
        }

        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);

        bool valid = totp.VerifyTotp(
            code,
            out long step,
            new VerificationWindow(1,1)
        );

        if (!valid)
        {
            MessageBox.Show("Código inválido.");
            return;
        }

        autenticado = true;

        MessageBox.Show("MFA validado.");

        // aqui você abre tela de nova senha depois
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!autenticado)
        {
            e.Cancel = true;
        }
    }
}