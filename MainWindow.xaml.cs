using System;
using System.Windows;
using OtpNet;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace CredentialProviderAPP;

public partial class MainWindow : Window
{
    private byte[]? currentKey;
    private string? currentSecret;

    private bool usuarioTravado = false;

    public MainWindow()
    {
        InitializeComponent();
        EsconderTudo();
    }

    private void EsconderTudo()
    {
        btnGerarQR.Visibility = Visibility.Collapsed;
        txtSecret.Visibility = Visibility.Collapsed;
        imgQR.Visibility = Visibility.Collapsed;
        txtCode.Visibility = Visibility.Collapsed;
        btnValidar.Visibility = Visibility.Collapsed;
        lblValidacao.Visibility = Visibility.Collapsed;
        btnVerificarCodigo.Visibility = Visibility.Collapsed;
    }

    private void BuscarUsuario_Click(object sender, RoutedEventArgs e)
    {
        if (usuarioTravado)
        {
            txtUser.IsReadOnly = false;
            txtUser.Text = "";
            btnBuscar.Content = "Buscar";
            usuarioTravado = false;

            EsconderTudo();
            return;
        }

        string username = txtUser.Text.Trim();

        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Digite um usuário.");
            return;
        }

        var user = Database.GetUser(username);

        txtUser.IsReadOnly = true;
        btnBuscar.Content = "Limpar";
        usuarioTravado = true;

        if (user.configured)
        {
            MostrarTelaVerificacao();
        }
        else
        {
            MostrarTelaConfiguracao();
        }
    }

    private void MostrarTelaConfiguracao()
    {
        btnGerarQR.Visibility = Visibility.Visible;
        txtSecret.Visibility = Visibility.Visible;
        imgQR.Visibility = Visibility.Visible;
        txtCode.Visibility = Visibility.Visible;
        btnValidar.Visibility = Visibility.Visible;
        lblValidacao.Visibility = Visibility.Visible;

        btnVerificarCodigo.Visibility = Visibility.Collapsed;
    }

    private void MostrarTelaVerificacao()
    {
        btnGerarQR.Visibility = Visibility.Collapsed;
        txtSecret.Visibility = Visibility.Collapsed;
        imgQR.Visibility = Visibility.Collapsed;
        txtCode.Visibility = Visibility.Collapsed;
        btnValidar.Visibility = Visibility.Collapsed;
        lblValidacao.Visibility = Visibility.Collapsed;

        btnVerificarCodigo.Visibility = Visibility.Visible;
    }

    private void GerarQR_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string username = txtUser.Text.Trim();

            var user = Database.GetUser(username);

            if (user.configured)
            {
                MessageBox.Show("Usuário já possui MFA configurado.");
                return;
            }

            string issuer = "CredentialProvider";

            currentKey = KeyGeneration.GenerateRandomKey(20);
            currentSecret = Base32Encoding.ToString(currentKey);

            txtSecret.Text = currentSecret;

            string url =
                $"otpauth://totp/{issuer}:{username}?secret={currentSecret}&issuer={issuer}";

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrBitmap = qrCode.GetGraphic(20);

            imgQR.Source = BitmapToImageSource(qrBitmap);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }

    private void ValidarCodigo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string username = txtUser.Text.Trim();
            string code = txtCode.Text.Trim();

            if (currentKey == null)
            {
                MessageBox.Show("Primeiro gere o QR Code.");
                return;
            }

            var totp = new Totp(currentKey);

            bool valid = totp.VerifyTotp(
                code,
                out long step,
                new VerificationWindow(2, 2)
            );

            if (!valid)
            {
                MessageBox.Show("Código inválido.");
                return;
            }

            if (currentSecret == null)
                return;

            Database.SaveSecret(username, currentSecret);

            MessageBox.Show("MFA configurado com sucesso!");

            txtSecret.Text = "";
            txtCode.Text = "";
            imgQR.Source = null;

            MostrarTelaVerificacao();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }

    private void VerificarCodigo_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUser.Text.Trim();

        var user = Database.GetUser(username);

        if (!user.configured || string.IsNullOrEmpty(user.secret))
        {
            MessageBox.Show("Usuário ainda não configurou MFA.");
            return;
        }

        VerificarCodigoWindow win = new VerificarCodigoWindow(user.secret);
        win.ShowDialog();
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