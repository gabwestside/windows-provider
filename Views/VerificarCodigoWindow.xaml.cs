using OtpNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CredentialProviderAPP.Views;

public partial class VerificarCodigoWindow : Window
{
    private readonly byte[] key;
    private bool autenticado = false;
    private bool mostrandoDialog = false;
    private bool _forcandoFoco = false;

    private readonly DispatcherTimer _timer = new();
    private int _segundosRestantes = 30;

    public VerificarCodigoWindow(string secret)
    {
        InitializeComponent();
        key = Base32Encoding.ToBytes(secret);

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // Bloqueia minimizar
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        };

        InicializarTimer();
    }

    // ══════════════════════════════════════════════════════════════
    //  TIMER — sincronizado com o ciclo TOTP real
    // ══════════════════════════════════════════════════════════════
    private void InicializarTimer()
    {
        long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _segundosRestantes = 30 - (int)(epoch % 30);

        AtualizarLabelTimer();

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) =>
        {
            _segundosRestantes--;
            if (_segundosRestantes <= 0)
                _segundosRestantes = 30;

            AtualizarLabelTimer();
        };
        _timer.Start();
    }

    private void AtualizarLabelTimer()
    {
        lblTimer.Text = $"0{_segundosRestantes / 60}:{_segundosRestantes % 60:D2}";

        // Últimos 5 segundos → vermelho para alertar
        lblTimer.Foreground = _segundosRestantes <= 5
            ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
            : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
    }

    // ══════════════════════════════════════════════════════════════
    //  FOCO — mantido igual ao original com flag anti-reentrada
    // ══════════════════════════════════════════════════════════════
    private void Window_Loaded(object sender, RoutedEventArgs e) => ForcarFoco();

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (mostrandoDialog || _forcandoFoco) return;
        ForcarFoco();
    }

    private void ForcarFoco()
    {
        if (_forcandoFoco) return;
        _forcandoFoco = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Topmost = true;
                Activate();
                Focus();
                Keyboard.Focus(txtCode);
            }
            finally
            {
                _forcandoFoco = false;
            }
        }));
    }

    // ══════════════════════════════════════════════════════════════
    //  ENTER no campo dispara verificação
    // ══════════════════════════════════════════════════════════════
    private void TxtCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Verificar_Click(sender, e);
    }

    // ══════════════════════════════════════════════════════════════
    //  VERIFICAR — lógica original preservada
    // ══════════════════════════════════════════════════════════════
    private void Verificar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string code = txtCode.Text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                MostrarAviso("Digite o código.");
                return;
            }

            var totp = new Totp(key);
            bool valid = totp.VerifyTotp(code, out long _, new VerificationWindow(1, 1));

            _timer.Stop();

            if (valid)
            {
                autenticado = true;
                MostrarSucesso("Código válido ✔");
                Environment.Exit(0);
            }
            else
            {
                MostrarErro("Código inválido ❌");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            MostrarErro("Erro: " + ex.Message);
            Environment.Exit(1);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  CANCELAR
    // ══════════════════════════════════════════════════════════════
    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        autenticado = true; // evita OnClosing disparar Exit(1) duplo
        Environment.Exit(1);
    }

    // ══════════════════════════════════════════════════════════════
    //  FECHAR — original preservado
    // ══════════════════════════════════════════════════════════════
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!autenticado)
            Environment.Exit(1);
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS DE MENSAGEM — substituem o MessageBox.Show original
    // ══════════════════════════════════════════════════════════════
    private void MostrarAviso(string msg)
    {
        mostrandoDialog = true;
        ModernMessageBox.Show(msg, "Atenção", ModernMessageBox.Kind.Warning, this);
        mostrandoDialog = false;
    }

    private void MostrarErro(string msg)
    {
        mostrandoDialog = true;
        ModernMessageBox.Show(msg, "Erro", ModernMessageBox.Kind.Error, this);
        mostrandoDialog = false;
    }

    private void MostrarSucesso(string msg)
    {
        mostrandoDialog = true;
        ModernMessageBox.Show(msg, "Sucesso", ModernMessageBox.Kind.Success, this);
        mostrandoDialog = false;
    }
}