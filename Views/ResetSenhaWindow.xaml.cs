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
    private string metodoMfa = "app"; // "app" ou "sms"

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

    private void FecharComResultado(bool? resultado)
    {
        try
        {
            DialogResult = resultado;
        }
        catch
        {
        }

        Close();
    }

    private async void ResetSenhaWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowFocusHelper.ForcarFoco(this, txtCode);

        try
        {
            var response = await ServerApiService.ObterStatusMfaAsync(login);

            if (!response.Sucesso)
            {
                Mostrar(response.Erro ?? "Erro ao consultar MFA.");
                FecharComResultado(false);
                return;
            }

            // aceita Configured e Trusted
            if (!string.Equals(response.Status, "Configured", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(response.Status, "Trusted", StringComparison.OrdinalIgnoreCase))
            {
                Mostrar("MFA precisa estar configurado.");
                FecharComResultado(false);
                return;
            }

            metodoMfa = string.IsNullOrWhiteSpace(response.Metodo)
                ? "app"
                : response.Metodo.Trim().ToLowerInvariant();

            if (metodoMfa == "sms")
            {
                lblCodigo.Text = "Código enviado por SMS";
                lblMetodoInfo.Text = "Enviando código por SMS para o telefone cadastrado...";
                lblMetodoInfo.Visibility = Visibility.Visible;

                try
                {
                    var statusSms = await ServerApiService.ObterStatusSmsAsync(login);

                    if (statusSms.PodeEnviar)
                    {
                        var envio = await ServerApiService.EnviarCodigoSmsAsync(login);

                        if (!envio.Sucesso)
                        {
                            lblMetodoInfo.Text = "Não foi possível enviar o código por SMS agora.";
                        }
                        else
                        {
                            lblMetodoInfo.Text = "Código enviado por SMS. Digite o código recebido para continuar.";
                        }
                    }
                    else
                    {
                        lblMetodoInfo.Text = "Já existe um código SMS válido. Digite o código recebido para continuar.";
                    }
                }
                catch
                {
                    lblMetodoInfo.Text = "Não foi possível enviar o SMS agora. Tente novamente.";
                }
            }
            else
            {
                metodoMfa = "app";
                lblCodigo.Text = "Código do aplicativo";
                lblMetodoInfo.Text = "Abra o aplicativo autenticador e digite o código de 6 dígitos.";
                lblMetodoInfo.Visibility = Visibility.Visible;
            }

            WindowFocusHelper.ForcarFoco(this, txtCode);
        }
        catch
        {
            Mostrar("Erro ao conectar com o servidor.");
            FecharComResultado(false);
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

            var response = await ServerApiService.ValidarCodigoMfaAsync(login, code, metodoMfa);

            if (!response.Sucesso)
            {
                Mostrar(response.Erro ?? "Erro MFA");
                txtCode.Focus();
                txtCode.SelectAll();
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
            fluxoConcluido = true;
            FecharComResultado(true);
            return;
        }
        catch (Exception ex)
        {
            Mostrar("Erro no fluxo de redefinição: " + ex.Message);
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
        FecharComResultado(false);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (fluxoConcluido || cancelado)
            return;

        mostrandoDialog = true;

        var result = MessageBox.Show(
            "Deseja cancelar?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        mostrandoDialog = false;

        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
            return;
        }

        cancelado = true;

        try
        {
            DialogResult = false;
        }
        catch
        {
        }

        // não chama Close() aqui
        // a janela já está fechando por causa do X
    }
}