using CredentialProviderAPP.Enums;
using CredentialProviderAPP.Models.Api;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;
using CredentialProviderAPP.Views;
using QRCoder;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
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

Para proteger sua conta,
escaneie agora o QR Code para configurar
a autenticação em dois fatores.";

        if (string.IsNullOrWhiteSpace(otpAuthUrlAtual))
        {
            MostrarMensagem("O servidor não retornou os dados do QR Code.");
            Environment.Exit(1);
            return;
        }

        ExibirQrCode(otpAuthUrlAtual);

        btnGerarQR.Visibility = Visibility.Collapsed;
        txtCode.Visibility = Visibility.Visible;
        btnValidar.Visibility = Visibility.Visible;
        imgQR.Visibility = Visibility.Visible;
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

    private void GerarQR_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(otpAuthUrlAtual))
        {
            MostrarMensagem("QR Code ainda não disponível.");
            return;
        }

        ExibirQrCode(otpAuthUrlAtual);
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
                MostrarMensagem("Digite o código gerado pelo aplicativo autenticador.");
                return;
            }

            btnValidar.IsEnabled = false;
            txtCode.IsEnabled = false;

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
        imgQR.Visibility = Visibility.Visible;

        lblMensagem.Text =
@"Escaneie o QR Code no Google Authenticator.

Agora digite o código gerado
para confirmar a configuração.";
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