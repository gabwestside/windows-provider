using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using OtpNet;

namespace CredentialProviderAPP;

public partial class VerificarCodigoWindow : Window
{
    private byte[] key;
    private bool autenticado = false;

    // controla se um dialog está aberto
    private bool mostrandoDialog = false;

    public VerificarCodigoWindow(string secret)
    {
        InitializeComponent();

        key = Base32Encoding.ToBytes(secret);

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ForcarFoco();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ForcarFoco();
        }));
    }

    private void ForcarFoco()
    {
        Topmost = true;
        Activate();
        Focus();
        Keyboard.Focus(txtCode);
    }

    private void Verificar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string code = txtCode.Text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                MostrarMensagem("Digite o código.");
                return;
            }

            var totp = new Totp(key);

            bool valid = totp.VerifyTotp(
                code,
                out long step,
                new VerificationWindow(1, 1)
            );

            if (valid)
            {
                autenticado = true;

                MostrarMensagem("Código válido ✔");

                Environment.Exit(0);
            }
            else
            {
                MostrarMensagem("Código inválido ❌");

                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro: " + ex.Message);

            Environment.Exit(1);
        }
    }

    private void MostrarMensagem(string msg)
    {
        mostrandoDialog = true;
        MessageBox.Show(msg);
        mostrandoDialog = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!autenticado)
        {
            Environment.Exit(1);
        }
    }
}