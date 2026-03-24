using OtpNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CredentialProviderAPP.Views
{
    public partial class VerificarCodigoWindow : Window
    {
        private readonly byte[] _key;
        private bool _autenticado = false;
        private bool _mostrandoDialog = false;

        // ── Timer regressivo de 30 segundos (ciclo TOTP) ──
        private readonly DispatcherTimer _timer = new();

        private int _segundosRestantes = 30;

        public VerificarCodigoWindow(string secret)
        {
            InitializeComponent();
            _key = Base32Encoding.ToBytes(secret);

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
        //  TIMER REGRESSIVO — sincronizado com o ciclo TOTP (30 s)
        // ══════════════════════════════════════════════════════════════
        private void InicializarTimer()
        {
            // Calcula os segundos restantes no ciclo TOTP atual
            long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _segundosRestantes = 30 - (int)(epoch % 30);

            AtualizarLabelTimer();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _segundosRestantes--;

            if (_segundosRestantes <= 0)
                _segundosRestantes = 30; // reinicia ciclo

            AtualizarLabelTimer();
        }

        private void AtualizarLabelTimer()
        {
            lblTimer.Text = $"{_segundosRestantes:D2}:{0:D2}"; // ex: 24:00
            // Nos últimos 5 segundos muda de cor para alertar
            lblTimer.Foreground = _segundosRestantes <= 5
                ? new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44))
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));
        }

        // ══════════════════════════════════════════════════════════════
        //  FOCO
        // ══════════════════════════════════════════════════════════════
        private void Window_Loaded(object sender, RoutedEventArgs e) => ForcarFoco();

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_mostrandoDialog) return;
            ForcarFoco();
        }

        private void ForcarFoco()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Topmost = true;
                Activate();
                Focus();
                Keyboard.Focus(txtCode);
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
        //  VERIFICAR
        // ══════════════════════════════════════════════════════════════
        private void Verificar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string code = txtCode.Text.Trim();

                if (string.IsNullOrEmpty(code))
                {
                    MostrarAviso("Digite o código de verificação.");
                    return;
                }

                var totp = new Totp(_key);
                bool valid = totp.VerifyTotp(code, out long _, new VerificationWindow(1, 1));

                _timer.Stop();

                if (valid)
                {
                    _autenticado = true;
                    MostrarSucesso("Código válido. Acesso autorizado.");
                    Environment.Exit(0);
                }
                else
                {
                    MostrarErro("Código inválido. Tente novamente.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                MostrarErro("Erro ao verificar: " + ex.Message);
                Environment.Exit(1);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CANCELAR
        // ══════════════════════════════════════════════════════════════
        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _autenticado = true; // evita o OnClosing disparar segundo modal
            Environment.Exit(1);
        }

        // ══════════════════════════════════════════════════════════════
        //  FECHAR
        // ══════════════════════════════════════════════════════════════
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_autenticado)
                Environment.Exit(1);
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS DE MENSAGEM
        // ══════════════════════════════════════════════════════════════
        private void MostrarAviso(string msg)
        {
            _mostrandoDialog = true;
            ModernMessageBox.Show(msg, "Atenção", ModernMessageBox.Kind.Warning, this);
            _mostrandoDialog = false;
        }

        private void MostrarErro(string msg)
        {
            _mostrandoDialog = true;
            ModernMessageBox.Show(msg, "Erro", ModernMessageBox.Kind.Error, this);
            _mostrandoDialog = false;
        }

        private void MostrarSucesso(string msg)
        {
            _mostrandoDialog = true;
            ModernMessageBox.Show(msg, "Sucesso", ModernMessageBox.Kind.Success, this);
            _mostrandoDialog = false;
        }
    }
}