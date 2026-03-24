using OtpNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Views;

public partial class VerificarCodigoWindow : Window
{
    private byte[] key;
    private bool autenticado = false;
    private bool mostrandoDialog = false;
    private bool _forcandoFoco = false; // ✅ evita loop de reentrada

    public VerificarCodigoWindow(string secret)
    {
        InitializeComponent();

        key = Base32Encoding.ToBytes(secret);

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        this.StateChanged += (s, e) =>
        {
            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ForcarFoco();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog || _forcandoFoco)
            return;

        ForcarFoco();
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
            Environment.Exit(1);
    }
}