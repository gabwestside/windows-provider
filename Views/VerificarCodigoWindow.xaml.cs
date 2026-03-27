using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;

namespace CredentialProviderAPP.Views;

public partial class VerificarCodigoWindow : Window
{
    private readonly string login;
    private bool autenticado = false;
    private bool mostrandoDialog = false;

    public VerificarCodigoWindow(string login)
    {
        InitializeComponent();

        this.login = login;

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowFocusHelper.ForcarFoco(this, txtCode);
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

    private async void Verificar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string code = txtCode.Text.Trim();

            if (code.Length != 6 || !code.All(char.IsDigit))
            {
                MostrarMensagem("Digite um código de 6 dígitos.", "Validação", MessageBoxImage.Warning);
                return;
            }

            btnVerificar.IsEnabled = false;
            txtCode.IsEnabled = false;

            var response = await ServerApiService.ValidarCodigoMfaAsync(login, code);

            if (!response.Sucesso)
            {
                MostrarMensagem(response.Erro ?? "Erro ao validar MFA.", "Erro", MessageBoxImage.Error);
                txtCode.Focus();
                return;
            }

            if (response.Valido)
            {
                autenticado = true;
                Environment.Exit(0);
                return;
            }

            MostrarMensagem("Código inválido.", "Validação", MessageBoxImage.Warning);
            txtCode.Clear();
            txtCode.Focus();
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro: " + ex.Message, "Erro", MessageBoxImage.Error);
            Environment.Exit(1);
        }
        finally
        {
            btnVerificar.IsEnabled = true;
            txtCode.IsEnabled = true;
        }
    }

    private void MostrarMensagem(string msg, string titulo = "Aviso", MessageBoxImage image = MessageBoxImage.Information)
    {
        mostrandoDialog = true;
        MessageBox.Show(msg, titulo, MessageBoxButton.OK, image);
        mostrandoDialog = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!autenticado)
            Environment.Exit(1);
    }
}