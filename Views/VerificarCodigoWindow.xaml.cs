using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;

namespace CredentialProviderAPP.Views;

public partial class VerificarCodigoWindow : Window
{
    private readonly string login;
    private readonly string metodo; // "sms" ou "app"
    private readonly string clientMachine;
    private bool autenticado = false;
    private bool mostrandoDialog = false;
    private bool _verificando = false;

    public bool CodigoValidado => autenticado;

    public VerificarCodigoWindow(string login, string metodo = "app", string clientMachine = "")
    {
        InitializeComponent();

        this.login = login;
        this.metodo = string.IsNullOrWhiteSpace(metodo)
            ? "app"
            : metodo.Trim().ToLowerInvariant();

        this.clientMachine = clientMachine?.Trim().Trim('"') ?? "";

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        btnVerificar.IsEnabled = false;

        if (metodo == "sms")
        {
            lblTitulo.Text = "Digite o código enviado por SMS";
            lblSubtitulo.Text = "Informe o código para continuar.";

            try
            {
                var status = await ServerApiService.ObterStatusSmsAsync(login);

                if (status.PodeEnviar)
                {
                    var envio = await ServerApiService.EnviarCodigoSmsAsync(login);

                    if (!envio.Sucesso)
                        lblSubtitulo.Text = "Não foi possível enviar o código agora.";
                }
            }
            catch
            {
                lblSubtitulo.Text = "Informe o código para continuar.";
            }
        }
        else
        {
            lblTitulo.Text = "Digite o código do aplicativo autenticador";
            lblSubtitulo.Text = "Abra seu aplicativo autenticador e informe o código de 6 dígitos.";
        }

        WindowFocusHelper.ForcarFoco(this, txtCode);
        txtCode.Focus();
        txtCode.SelectAll();
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

    private void TxtCode_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void TxtCode_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        string texto = (e.DataObject.GetData(DataFormats.Text) as string ?? "").Trim();
        string numeros = new string(texto.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(numeros))
        {
            e.CancelCommand();
            return;
        }

        txtCode.Text = numeros.Length > 6 ? numeros[..6] : numeros;
        txtCode.CaretIndex = txtCode.Text.Length;
        e.CancelCommand();
    }

    private void TxtCode_TextChanged(object sender, TextChangedEventArgs e)
    {
        string numeros = new string(txtCode.Text.Where(char.IsDigit).ToArray());

        if (txtCode.Text != numeros)
        {
            int caret = numeros.Length;
            txtCode.Text = numeros;
            txtCode.CaretIndex = Math.Min(caret, txtCode.Text.Length);
        }

        btnVerificar.IsEnabled = !_verificando && txtCode.Text.Length == 6;
    }

    private async void Verificar_Click(object sender, RoutedEventArgs e)
    {
        await VerificarCodigoAsync();
    }

    private async System.Threading.Tasks.Task VerificarCodigoAsync()
    {
        if (_verificando)
            return;

        try
        {
            string code = txtCode.Text.Trim();

            if (code.Length != 6 || !code.All(char.IsDigit))
            {
                btnVerificar.IsEnabled = false;
                return;
            }

            _verificando = true;
            btnVerificar.IsEnabled = false;
            txtCode.IsEnabled = false;

            var metodoValidacao = metodo == "sms" ? "sms" : "app";
            var response = await ServerApiService.ValidarCodigoMfaAsync(
                login,
                code,
                metodoValidacao,
                clientMachine);

            if (!response.Sucesso)
            {
                MostrarMensagem(response.Erro ?? "Erro ao validar MFA.", "Erro", MessageBoxImage.Error);
                txtCode.Focus();
                txtCode.SelectAll();
                return;
            }

            if (response.Valido)
            {
                autenticado = true;
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
            _verificando = false;
            txtCode.IsEnabled = true;
            btnVerificar.IsEnabled = txtCode.Text.Length == 6;

            if (IsVisible)
            {
                txtCode.Focus();
                txtCode.SelectAll();
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!autenticado && this.IsLoaded)
        {
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void MostrarMensagem(string msg, string titulo = "Aviso", MessageBoxImage image = MessageBoxImage.Information)
    {
        mostrandoDialog = true;
        MessageBox.Show(msg, titulo, MessageBoxButton.OK, image);
        mostrandoDialog = false;
    }
}