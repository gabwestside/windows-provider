using OtpNet;
using System;
using System.ComponentModel;
using System.Windows;
using CredentialProviderAPP.Data;

namespace CredentialProviderAPP.Views;

public partial class ResetSenhaWindow : Window
{
    private bool autenticado = false;
    private bool cancelado = false;
    private bool mostrandoDialog = false;

    public ResetSenhaWindow(string login)
    {
        InitializeComponent();
        txtLogin.Text = login;

        // ✅ Checa MFA antes de mostrar a janela
        var user = Database.GetUser(login);
        var (mfaenabled, configured, secret) = user;

        if (!configured || string.IsNullOrEmpty(secret))
        {
            autenticado = true;

            Loaded += (s, e) =>
            {
                Hide();

                NovaSenhaWindow win = new NovaSenhaWindow(login);
                win.Topmost = true;
                win.ShowDialog();

                Close();
            };
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // só foca o campo se a janela realmente vai ser usada
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

        // ✅ Sem MFA configurado → vai direto para nova senha sem validar TOTP
        if (!configured || string.IsNullOrEmpty(secret))
        {
            autenticado = true;
            Hide();

            NovaSenhaWindow win = new NovaSenhaWindow(login);
            win.Topmost = true;
            win.ShowDialog();

            Close();
            return;
        }

        // ✅ Com MFA configurado → exige código antes
        if (code.Length != 6)
        {
            mostrandoDialog = true;
            MessageBox.Show("Digite o código de 6 dígitos.");
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
            MessageBox.Show("Código inválido.");
            mostrandoDialog = false;
            txtCode.Clear();
            txtCode.Focus();
            return;
        }

        // ✅ MFA validado → abre nova senha
        autenticado = true;
        Hide();

        NovaSenhaWindow novaSenha = new NovaSenhaWindow(login);
        novaSenha.Topmost = true;
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

        var result = MessageBox.Show(
            "Deseja cancelar o processo de redefinição de senha?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
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