using CredentialProviderAPP.Enums;
using CredentialProviderAPP.Services;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CredentialProviderAPP;

public partial class MainWindow : Window
{
    private bool autenticado = false;
    private bool mostrandoDialog = false;

    private string loginAtual = "";
    private string? otpAuthUrlAtual = null;
    private readonly AppMode _modo;

    private string metodoSelecionado = ""; // "app" ou "sms"

    public MainWindow()
    {
        InitializeComponent();
        _modo = AppMode.Default;
    }

    public MainWindow(string login, AppMode modo)
    {
        InitializeComponent();

        _modo = modo;

        if (string.IsNullOrWhiteSpace(login))
        {
            MostrarMensagem("Login não informado.");
            Environment.Exit(1);
            return;
        }

        loginAtual = login;
        txtUser.Text = login;
        txtUser.IsReadOnly = true;
        btnBuscar.Visibility = Visibility.Collapsed;

        Loaded += async (_, __) => await ProcessarFluxoUsuarioAsync(login);
    }

    private async Task ProcessarFluxoUsuarioAsync(string login)
    {
        try
        {
            var statusResponse = await ServerApiService.ObterStatusMfaAsync(login);

            if (!statusResponse.Sucesso)
            {
                MostrarMensagem(statusResponse.Erro ?? "Erro ao consultar status do MFA.");
                Environment.Exit(1);
                return;
            }

            if (_modo == AppMode.Setup)
            {
                await AbrirFluxoSetupAsync(login, statusResponse.Status);
                return;
            }

            MostrarMensagem("Modo de operação inválido.");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao processar MFA: " + ex.Message);
            Environment.Exit(1);
        }
    }

    private async Task AbrirFluxoSetupAsync(string login, string status)
    {
        if (!status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            MostrarMensagem("MFA não está pendente para este usuário.");
            Environment.Exit(1);
            return;
        }

        var setupResponse = await ServerApiService.ObterSetupMfaAsync(login);

        if (!setupResponse.Sucesso)
        {
            MostrarMensagem(setupResponse.Erro ?? "Erro ao preparar configuração do MFA.");
            Environment.Exit(1);
            return;
        }

        loginAtual = setupResponse.Login;
        txtUser.Text = setupResponse.Login;
        otpAuthUrlAtual = setupResponse.OtpAuthUrl;

        lblMensagem.Text =
$@"Bem-vindo {setupResponse.Nome}

Escolha como deseja configurar
a autenticação em dois fatores.";

        panelMetodo.Visibility = Visibility.Visible;
        lblMetodoInfo.Visibility = Visibility.Visible;

        rbAuthenticator.IsChecked = true;
    }

    private async void BuscarUsuario_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUser.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            MostrarMensagem("Digite um usuário.");
            return;
        }

        loginAtual = username;
        txtUser.IsReadOnly = true;
        btnBuscar.Visibility = Visibility.Collapsed;

        await ProcessarFluxoUsuarioAsync(username);
    }

    private void MetodoMfa_Checked(object sender, RoutedEventArgs e)
    {
        if (rbAuthenticator.IsChecked == true)
        {
            metodoSelecionado = "app";

            lblMetodoInfo.Text =
    @"Você escolheu aplicativo autenticador.

Escaneie o QR Code e depois informe
o código gerado no aplicativo.";
            lblMetodoInfo.Visibility = Visibility.Visible;

            panelQr.Visibility = Visibility.Visible;
            imgQR.Visibility = Visibility.Visible;

            btnGerarQR.Visibility = Visibility.Visible;
            btnEnviarSms.Visibility = Visibility.Collapsed;

            lblCodigo.Visibility = Visibility.Visible;
            txtCode.Visibility = Visibility.Visible;
            btnValidar.Visibility = Visibility.Visible;

            txtCode.Clear();

            if (!string.IsNullOrWhiteSpace(otpAuthUrlAtual))
                ExibirQrCode(otpAuthUrlAtual);
        }
        else if (rbSms.IsChecked == true)
        {
            metodoSelecionado = "sms";

            lblMetodoInfo.Text =
    @"Você escolheu SMS.

Envie um código para o telefone cadastrado
e informe o código recebido.";
            lblMetodoInfo.Visibility = Visibility.Visible;

            panelQr.Visibility = Visibility.Collapsed;
            imgQR.Visibility = Visibility.Collapsed;

            btnGerarQR.Visibility = Visibility.Collapsed;
            btnEnviarSms.Visibility = Visibility.Visible;

            lblCodigo.Visibility = Visibility.Visible;
            txtCode.Visibility = Visibility.Visible;
            btnValidar.Visibility = Visibility.Visible;

            txtCode.Clear();
        }
    }

    private void GerarQR_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(otpAuthUrlAtual))
        {
            MostrarMensagem("QR Code ainda não disponível.");
            return;
        }

        ExibirQrCode(otpAuthUrlAtual);
    }

    private async void EnviarSms_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(loginAtual))
            {
                MostrarMensagem("Usuário não informado.");
                return;
            }

            btnEnviarSms.IsEnabled = false;

            // TODO:
            // aqui depois vamos chamar a API real de SMS
            // ex: await ServerApiService.EnviarCodigoSmsAsync(loginAtual);

            await Task.Delay(500);

            MostrarMensagem("Código SMS enviado com sucesso.");
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao enviar SMS: " + ex.Message);
        }
        finally
        {
            btnEnviarSms.IsEnabled = true;
        }
    }

    private async void ValidarCodigo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string username = txtUser.Text.Trim();
            string code = txtCode.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                MostrarMensagem("Login inválido.");
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                MostrarMensagem("Digite o código de validação.");
                return;
            }

            btnValidar.IsEnabled = false;
            txtCode.IsEnabled = false;

            // Por enquanto mantém a validação existente.
            // Depois, no backend, você pode separar:
            // - ValidarCodigoMfaAsync para app autenticador
            // - ValidarCodigoSmsAsync para SMS

            var response = await ServerApiService.ValidarCodigoMfaAsync(username, code);

            if (!response.Sucesso)
            {
                MostrarMensagem(response.Erro ?? "Erro ao validar MFA.");
                return;
            }

            if (!response.Valido)
            {
                MostrarMensagem("Código inválido.");
                return;
            }

            autenticado = true;
            MostrarMensagem("MFA configurado com sucesso!");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao validar MFA: " + ex.Message);
        }
        finally
        {
            btnValidar.IsEnabled = true;
            txtCode.IsEnabled = true;
        }
    }

    private void ExibirQrCode(string otpAuthUrl)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q);
        QRCode qrCode = new QRCode(qrCodeData);
        Bitmap qrBitmap = qrCode.GetGraphic(20);

        imgQR.Source = BitmapToImageSource(qrBitmap);
        panelQr.Visibility = Visibility.Visible;
        imgQR.Visibility = Visibility.Visible;
    }

    private void MostrarMensagem(string msg)
    {
        mostrandoDialog = true;
        MessageBox.Show(msg);
        mostrandoDialog = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!autenticado && _modo == AppMode.Setup)
            Environment.Exit(1);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog || _modo != AppMode.Setup)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Topmost = true;
            Activate();
        }));
    }

    private BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;

        BitmapImage bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();

        return bitmapImage;
    }
}