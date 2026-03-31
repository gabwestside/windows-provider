using CredentialProviderAPP.Enums;
using CredentialProviderAPP.Services;
using CredentialProviderAPP.Utils;
using QRCoder;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Linq;

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

            panelQr.Visibility = Visibility.Collapsed;
            imgQR.Visibility = Visibility.Collapsed;
            btnGerarQR.Visibility = Visibility.Collapsed;
            lblCodigo.Visibility = Visibility.Visible;
            txtCode.Visibility = Visibility.Visible;
            btnValidar.Visibility = Visibility.Visible;
            txtCode.Clear();

            _ = AtualizarInfoTelefoneAsync();
        }
    }
    private bool _atualizandoTelefone = false;
    private const string MascaraTelefone = "(__) _____-____";

    private void TxtTelefone_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;

        if (string.IsNullOrWhiteSpace(tb.Text))
            tb.Text = MascaraTelefone;

        int qtdNumeros = ObterSomenteNumeros(tb.Text).Length;
        MoverCursorParaPosicaoDigitavel(tb, qtdNumeros);
    }

    private void TxtTelefone_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb)
        {
            e.Handled = true;
            return;
        }

        if (!e.Text.All(char.IsDigit))
        {
            e.Handled = true;
            return;
        }

        string numeros = ObterSomenteNumeros(tb.Text);

        if (numeros.Length >= 11)
        {
            e.Handled = true;
            return;
        }

        int indexNumero = ConverterCursorParaIndiceNumero(tb.CaretIndex);

        if (indexNumero < 0) indexNumero = 0;
        if (indexNumero > numeros.Length) indexNumero = numeros.Length;

        numeros = numeros.Insert(indexNumero, e.Text);

        if (numeros.Length > 11)
            numeros = numeros[..11];

        AtualizarTelefone(tb, numeros, indexNumero + e.Text.Length);

        e.Handled = true;
    }

    private void TxtTelefone_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;

        string numeros = ObterSomenteNumeros(tb.Text);

        if (e.Key == System.Windows.Input.Key.Space)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Back)
        {
            e.Handled = true;

            if (numeros.Length == 0)
                return;

            int indexNumero = ConverterCursorParaIndiceNumero(tb.CaretIndex);

            if (indexNumero > 0)
            {
                numeros = numeros.Remove(indexNumero - 1, 1);
                AtualizarTelefone(tb, numeros, indexNumero - 1);
            }

            return;
        }

        if (e.Key == System.Windows.Input.Key.Delete)
        {
            e.Handled = true;

            if (numeros.Length == 0)
                return;

            int indexNumero = ConverterCursorParaIndiceNumero(tb.CaretIndex);

            if (indexNumero < numeros.Length)
            {
                numeros = numeros.Remove(indexNumero, 1);
                AtualizarTelefone(tb, numeros, indexNumero);
            }

            return;
        }
    }

    private void AtualizarTelefone(System.Windows.Controls.TextBox tb, string numeros, int proximoIndiceNumero = -1)
    {
        if (_atualizandoTelefone) return;

        _atualizandoTelefone = true;

        if (numeros.Length > 11)
            numeros = numeros[..11];

        char[] mascara = MascaraTelefone.ToCharArray();
        int[] posicoesNumericas = { 1, 2, 5, 6, 7, 8, 9, 11, 12, 13, 14 };

        for (int i = 0; i < posicoesNumericas.Length; i++)
        {
            mascara[posicoesNumericas[i]] = i < numeros.Length ? numeros[i] : '_';
        }

        tb.Text = new string(mascara);

        if (proximoIndiceNumero < 0)
            proximoIndiceNumero = numeros.Length;

        MoverCursorParaPosicaoDigitavel(tb, proximoIndiceNumero);

        _atualizandoTelefone = false;
    }

    private void MoverCursorParaPosicaoDigitavel(System.Windows.Controls.TextBox tb, int quantidadeNumeros)
    {
        int[] posicoesCursor = { 1, 2, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15 };

        if (quantidadeNumeros < 0)
            quantidadeNumeros = 0;

        if (quantidadeNumeros >= posicoesCursor.Length)
            quantidadeNumeros = posicoesCursor.Length - 1;

        tb.CaretIndex = posicoesCursor[quantidadeNumeros];
    }

    private int ConverterCursorParaIndiceNumero(int caret)
    {
        int[] posicoes = { 1, 2, 5, 6, 7, 8, 9, 11, 12, 13, 14 };

        for (int i = 0; i < posicoes.Length; i++)
        {
            if (caret <= posicoes[i])
                return i;
        }

        return posicoes.Length;
    }

    private string ObterSomenteNumeros(string texto)
    {
        return new string(texto.Where(char.IsDigit).ToArray());
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

            var result = await ServerApiService.EnviarCodigoSmsAsync(loginAtual);

            if (!result.Sucesso)
            {
                MostrarMensagem(result.Erro ?? "Erro ao enviar SMS.");
                return;
            }

            MostrarMensagem("Código enviado! Verifique C:\\CredentialProvider\\sms_debug.txt");
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao enviar SMS: " + ex.Message);
        }
        finally
        {
            await AtualizarInfoTelefoneAsync();
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

            var response = await ServerApiService.ValidarCodigoMfaAsync(username, code, metodoSelecionado);

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

    private async Task AtualizarInfoTelefoneAsync()
    {
        try
        {
            var tel = await ServerApiService.ObterTelefoneAsync(loginAtual);

            if (!tel.TemTelefone)
            {
                lblMetodoInfo.Text = "Você escolheu SMS.\n\nNenhum celular cadastrado no sistema.";
                lblMetodoInfo.Visibility = Visibility.Visible;
                panelCadastroTelefone.Visibility = Visibility.Visible;
                btnEnviarSms.Visibility = Visibility.Collapsed;
            }
            else
            {
                lblMetodoInfo.Text = $"Você escolheu SMS.\n\nCódigo será enviado para: {tel.TelefoneMascarado}";
                lblMetodoInfo.Visibility = Visibility.Visible;
                panelCadastroTelefone.Visibility = Visibility.Collapsed;
                btnEnviarSms.Visibility = Visibility.Visible;

                // 🔥 NOVO: verifica status do código
                var status = await ServerApiService.ObterStatusSmsAsync(loginAtual);

                // 🔥 AQUI É O QUE TU QUERIA
                btnEnviarSms.IsEnabled = status.PodeEnviar;
            }
        }
        catch
        {
            lblMetodoInfo.Text = "Você escolheu SMS.";
            lblMetodoInfo.Visibility = Visibility.Visible;
            btnEnviarSms.Visibility = Visibility.Visible;
            panelCadastroTelefone.Visibility = Visibility.Collapsed;
        }
    }

    private async void SalvarTelefone_Click(object sender, RoutedEventArgs e)
    {
        string telefone = new string(txtTelefone.Text.Where(char.IsDigit).ToArray());

        if (telefone.Length < 10)
        {
            MostrarMensagem("Digite um número válido (ex: 85997319943).");
            return;
        }

        try
        {
            btnSalvarTelefone.IsEnabled = false;
            var result = await ServerApiService.SalvarTelefoneAsync(loginAtual, telefone);

            if (!result.Sucesso)
            {
                MostrarMensagem(result.Erro ?? "Erro ao salvar telefone.");
                return;
            }

            MostrarMensagem("Telefone cadastrado com sucesso!");
            panelCadastroTelefone.Visibility = Visibility.Collapsed;
            await AtualizarInfoTelefoneAsync();
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro: " + ex.Message);
        }
        finally
        {
            btnSalvarTelefone.IsEnabled = true;
        }
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