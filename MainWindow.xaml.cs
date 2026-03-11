using CredentialProviderAPP.Data;
using CredentialProviderAPP.Views;
using OtpNet;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CredentialProviderAPP;

public partial class MainWindow : Window
{
    private byte[]? currentKey;
    private string? currentSecret;

    private bool autenticado = false;
    private bool mostrandoDialog = false;

    private string loginAtual = "";

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(string login)
    {
        InitializeComponent();

        if (string.IsNullOrEmpty(login))
            return;

        loginAtual = login;

        var user = Database.GetUser(login);

        txtUser.Text = login;
        txtUser.IsReadOnly = true;
        btnBuscar.Visibility = Visibility.Collapsed;

        //
        // 🚨 MFA DESATIVADO → NÃO FAZ NADA
        //
        if (!user.mfaenabled)
        {
            Environment.Exit(0);
            return;
        }

        //
        // 🔐 MFA habilitado mas NÃO configurado → gerar QR
        //
        if (user.mfaenabled && !user.configured)
        {
            lblMensagem.Text =
$@"Bem-vindo {login}

Para proteger sua conta,
gere agora o QR Code para configurar
a autenticação em dois fatores.";

            btnGerarQR.Visibility = Visibility.Visible;
            return;
        }

        //
        // 🔐 MFA habilitado e configurado → validar código
        //
        if (user.mfaenabled && user.configured && !string.IsNullOrEmpty(user.secret))
        {
            VerificarCodigoWindow win = new VerificarCodigoWindow(user.secret);
            win.ShowDialog();
            Environment.Exit(0);
            return;
        }

        Environment.Exit(1);
    }

    private void BuscarUsuario_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUser.Text.Trim();

        if (string.IsNullOrEmpty(username))
        {
            MostrarMensagem("Digite um usuário.");
            return;
        }

        var user = Database.GetUser(username);

        txtUser.IsReadOnly = true;
        btnBuscar.Visibility = Visibility.Collapsed;

        //
        // MFA DESATIVADO
        //
        if (!user.mfaenabled)
        {
            Environment.Exit(0);
            return;
        }

        //
        // MFA CONFIGURADO
        //
        if (user.mfaenabled && user.configured && !string.IsNullOrEmpty(user.secret))
        {
            VerificarCodigoWindow win = new VerificarCodigoWindow(user.secret);
            win.ShowDialog();
            return;
        }

        //
        // MFA NÃO CONFIGURADO
        //
        lblMensagem.Text =
$@"Bem-vindo {username}

Para proteger sua conta,
gere agora o QR Code para configurar
a autenticação em dois fatores.";

        btnGerarQR.Visibility = Visibility.Visible;
    }

    private void GerarQR_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string username = txtUser.Text.Trim();

            currentKey = KeyGeneration.GenerateRandomKey(20);
            currentSecret = Base32Encoding.ToString(currentKey);

            string issuer = "CredentialProvider";

            string url =
                $"otpauth://totp/{issuer}:{username}?secret={currentSecret}&issuer={issuer}";

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrBitmap = qrCode.GetGraphic(20);

            imgQR.Source = BitmapToImageSource(qrBitmap);
            imgQR.Visibility = Visibility.Visible;

            lblMensagem.Text =
@"Escaneie o QR Code no Google Authenticator.

Agora valide o código gerado
para confirmar a configuração.";

            txtCode.Visibility = Visibility.Visible;
            btnValidar.Visibility = Visibility.Visible;

            btnGerarQR.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MostrarMensagem(ex.ToString());
        }
    }

    private void ValidarCodigo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string username = txtUser.Text.Trim();
            string code = txtCode.Text.Trim();

            if (currentKey == null)
                return;

            var totp = new Totp(currentKey);

            bool valid = totp.VerifyTotp(
                code,
                out long step,
                new VerificationWindow(1, 1)
            );

            if (!valid)
            {
                MostrarMensagem("Código inválido.");
                return;
            }

            if (currentSecret == null)
                return;

            Database.SaveSecret(username, currentSecret);

            autenticado = true;

            MostrarMensagem("MFA configurado com sucesso!");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MostrarMensagem(ex.ToString());
        }
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

        if (!autenticado)
        {
            Environment.Exit(1);
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog)
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