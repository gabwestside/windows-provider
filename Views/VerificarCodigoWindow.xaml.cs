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
    private readonly string metodo; // "sms" ou "app"
    private bool autenticado = false;
    private bool mostrandoDialog = false;

    public bool CodigoValidado => autenticado;

    public VerificarCodigoWindow(string login, string metodo = "app")
    {
        InitializeComponent();

        this.login = login;
        this.metodo = string.IsNullOrWhiteSpace(metodo)
            ? "app"
            : metodo.Trim().ToLowerInvariant();

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
        if (metodo == "sms")
        {
            lblTitulo.Text = "Digite o código enviado por SMS";
            lblSubtitulo.Text = "Enviamos um código de 6 dígitos para o telefone cadastrado.";
        }
        else
        {
            lblTitulo.Text = "Digite o código do aplicativo autenticador";
            lblSubtitulo.Text = "Abra seu aplicativo autenticador e informe o código de 6 dígitos.";
        }

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

            // =========================
            // MODO VISUAL / TESTE DE TELA
            // =========================
            // Aqui você pode testar a UI sem backend SMS.
            // Por enquanto:
            // - app continua chamando backend real
            // - sms só simula sucesso visual

            if (metodo == "sms")
            {
                // simulação visual
                await System.Threading.Tasks.Task.Delay(400);

                autenticado = true;
                DialogResult = true;
                Close();
                return;
            }

            // app / authenticator
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
                DialogResult = true;
                Close();
                return;
            }

            MostrarMensagem("Código inválido.", "Validação", MessageBoxImage.Warning);
            txtCode.Clear();
            txtCode.Focus();
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro: " + ex.Message, "Erro", MessageBoxImage.Error);
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
            DialogResult = false;
    }
}