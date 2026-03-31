using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;
using System.ComponentModel;
using System.Windows;

namespace CredentialProviderAPP.Views;

public partial class ResetSenhaWindow : Window
{
    private bool autenticado = false;
    private bool cancelado = false;
    private bool mostrandoDialog = false;
    private bool fluxoConcluido = false;

    private readonly string login;

    public ResetSenhaWindow(string login)
    {
        InitializeComponent();
        this.login = login;

        txtLogin.Text = login;

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Loaded += ResetSenhaWindow_Loaded;

        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        };
    }

    private async void ResetSenhaWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowFocusHelper.ForcarFoco(this, txtCode);

        try
        {
            var response = await ServerApiService.ObterStatusMfaAsync(login);

            if (!response.Sucesso)
            {
                MessageBox.Show(response.Erro ?? "Erro ao consultar MFA.");
                DialogResult = false;
                Close();
                return;
            }

            if (response.Status != "Configured")
            {
                MessageBox.Show("MFA precisa estar configurado.");
                DialogResult = false;
                Close();
                return;
            }
        }
        catch
        {
            MessageBox.Show("Erro ao conectar com o servidor.");
            DialogResult = false;
            Close();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog || !IsVisible || WindowState == WindowState.Minimized)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!mostrandoDialog && IsVisible && !IsActive)
                WindowFocusHelper.ForcarFoco(this, txtCode);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private async void Validar_Click(object sender, RoutedEventArgs e)
    {
        string code = txtCode.Text.Trim();

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            Mostrar("Digite 6 dígitos.");
            return;
        }

        try
        {
            ToggleValidacao(false);

            var response = await ServerApiService.ValidarCodigoMfaAsync(login, code);

            if (!response.Sucesso)
            {
                Mostrar(response.Erro ?? "Erro MFA");
                txtCode.Focus();
                return;
            }

            if (!response.Valido)
            {
                Mostrar("Código inválido.");
                txtCode.Clear();
                txtCode.Focus();
                return;
            }

            autenticado = true;

            Hide();

            var novaSenha = new NovaSenhaWindow(login);
            bool? ok = novaSenha.ShowDialog();

            if (ok == true)
            {
                fluxoConcluido = true;
                DialogResult = true;
                Close();
                return;
            }

            // se o usuário fechou/cancelou a troca de senha, volta pra tela MFA
            Show();
            Activate();
            WindowFocusHelper.ForcarFoco(this, txtCode);
        }
        catch (Exception ex)
        {
            Mostrar("Erro no fluxo de redefinição: " + ex.Message);
            Show();
            Activate();
            txtCode.Focus();
        }
        finally
        {
            ToggleValidacao(true);
        }
    }

    private void ToggleValidacao(bool enabled)
    {
        btnValidar.IsEnabled = enabled;
        btnCancelar.IsEnabled = enabled;
        txtCode.IsEnabled = enabled;
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
        DialogResult = false;
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (fluxoConcluido || cancelado)
            return;

        mostrandoDialog = true;

        var result = MessageBox.Show("Deseja cancelar?", "Cancelar", MessageBoxButton.YesNo);

        mostrandoDialog = false;

        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
            return;
        }

        cancelado = true;
        DialogResult = false;
    }
}