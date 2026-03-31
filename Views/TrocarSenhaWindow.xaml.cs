using CredentialProviderAPP.Config;
using CredentialProviderAPP.Models;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CredentialProviderAPP.Views
{
    /// <summary>
    /// Modal de confirmação "Trocar senha no próximo login".
    /// Gera senha aleatória respeitando a política, exibe validador em tempo real,
    /// e envia e-mail ao(s) usuário(s) ao confirmar.
    /// </summary>
    public partial class TrocarSenhaWindow : Window
    {
        // ── resultado público ─────────────────────────────────────────
        public bool Confirmado { get; private set; } = false;

        public string SenhaGerada { get; private set; } = "";
        public bool SomenteForcarTrocaNoProximoLogin { get; private set; } = false;

        // ── política carregada ────────────────────────────────────────
        private int _minLength = 8;

        private int _minEspeciais = 1;
        private string _especiaisPermitidos = "!@#$%&*";
        private bool _exigirMaiuscula = true;
        private bool _exigirMinuscula = true;
        private bool _exigirNumero = true;

        // ── estado ────────────────────────────────────────────────────
        private bool _senhaVisivel = false;

        private readonly List<UsuarioViewModel> _usuarios;
        private readonly Random _rng = new();

        // ── caminho da política ───────────────────────────────────────
        private static readonly string[] _candidatos = new[]
        {
            @"C:\Dev\windows-provider\bin\Debug\net9.0-windows\password_policy.txt",
            System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "password_policy.txt")
        };

        // ─────────────────────────────────────────────────────────────
        public TrocarSenhaWindow(List<UsuarioViewModel> usuarios)
        {
            InitializeComponent();
            _usuarios = usuarios;
            CarregarPolitica();
            InicializarUI();
            GerarSenha();
        }

        // ══════════════════════════════════════════════════════════════
        //  INICIALIZAÇÃO
        // ══════════════════════════════════════════════════════════════
        private void CarregarPolitica()
        {
            foreach (var path in _candidatos)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var linhas = File.ReadAllLines(path);
                    if (linhas.Length >= 6)
                    {
                        if (int.TryParse(linhas[0].Trim(), out int ml)) _minLength = ml;
                        if (int.TryParse(linhas[1].Trim(), out int me)) _minEspeciais = me;
                        if (!string.IsNullOrWhiteSpace(linhas[2])) _especiaisPermitidos = linhas[2].Trim();
                        if (bool.TryParse(linhas[3].Trim(), out bool mau)) _exigirMaiuscula = mau;
                        if (bool.TryParse(linhas[4].Trim(), out bool mmi)) _exigirMinuscula = mmi;
                        if (bool.TryParse(linhas[5].Trim(), out bool mn)) _exigirNumero = mn;
                    }
                    break; // achou e leu — para
                }
                catch { /* usa padrão */ }
            }
        }

        private void InicializarUI()
        {
            // Subtítulo
            int n = _usuarios.Count;
            lblSubtitle.Text = n == 1
                ? $"Definindo para: {_usuarios[0].Login}"
                : $"Definindo para {n} usuários selecionados";

            // Labels dos requisitos
            lblLength.Text = $"Mínimo {_minLength} caracteres";
            lblSpecial.Text = $"Caractere especial ({_minEspeciais}): {_especiaisPermitidos}";

            // Ocultar linhas que não se aplicam
            rowUpper.Visibility = _exigirMaiuscula ? Visibility.Visible : Visibility.Collapsed;
            rowLower.Visibility = _exigirMinuscula ? Visibility.Visible : Visibility.Collapsed;
            rowNumber.Visibility = _exigirNumero ? Visibility.Visible : Visibility.Collapsed;
            rowSpecial.Visibility = _minEspeciais > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Banner de múltiplos usuários
            if (n > 1)
            {
                panelMultiplos.Visibility = Visibility.Visible;
                lblMultiplos.Text =
                    $"A mesma senha temporária será enviada para todos os {n} usuários. " +
                    "Cada um deverá alterá-la no próximo acesso.";
            }

            // Pré-preencher e-mail se for apenas 1 usuário (se o modelo tiver Email)
            if (n == 1)
                txtEmail.Text = !string.IsNullOrWhiteSpace(_usuarios[0].Email)
                    ? _usuarios[0].Email
                    : "";
        }

        // ══════════════════════════════════════════════════════════════
        //  GERAÇÃO DE SENHA
        // ══════════════════════════════════════════════════════════════
        private void GerarSenha()
        {
            string senha = GerarSenhaAleatoria();
            AplicarSenhaNoCampo(senha);
        }

        private string GerarSenhaAleatoria()
        {
            const string maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string minusculas = "abcdefghjkmnpqrstuvwxyz";
            const string numeros = "23456789";

            var chars = new StringBuilder();

            // Garante obrigatórios
            if (_exigirMaiuscula) chars.Append(maiusculas[_rng.Next(maiusculas.Length)]);
            if (_exigirMinuscula) chars.Append(minusculas[_rng.Next(minusculas.Length)]);
            if (_exigirNumero) chars.Append(numeros[_rng.Next(numeros.Length)]);

            for (int i = 0; i < _minEspeciais; i++)
                chars.Append(_especiaisPermitidos[_rng.Next(_especiaisPermitidos.Length)]);

            // Completa até o comprimento mínimo (com margem de +4)
            string pool = maiusculas + minusculas + numeros + _especiaisPermitidos;
            int alvo = Math.Max(_minLength + 4, chars.Length);
            while (chars.Length < alvo)
                chars.Append(pool[_rng.Next(pool.Length)]);

            // Embaralha
            return new string(chars.ToString().OrderBy(_ => _rng.Next()).ToArray());
        }

        private void AplicarSenhaNoCampo(string senha)
        {
            SenhaGerada = senha;

            if (_senhaVisivel)
            {
                txtSenhaVisivel.Text = senha;
            }
            else
            {
                // Usa helper para setar PasswordBox sem disparar PasswordChanged em loop
                _ignorarChanged = true;
                pwdSenha.Password = senha;
                _ignorarChanged = false;
            }

            AtualizarValidador(senha);
        }

        // ══════════════════════════════════════════════════════════════
        //  VALIDADOR EM TEMPO REAL
        // ══════════════════════════════════════════════════════════════
        private bool _ignorarChanged = false;

        private void PwdSenha_Changed(object sender, RoutedEventArgs e)
        {
            if (_ignorarChanged) return;
            SenhaGerada = pwdSenha.Password;
            AtualizarValidador(SenhaGerada);
        }

        private void TxtSenhaVisivel_Changed(object sender, TextChangedEventArgs e)
        {
            SenhaGerada = txtSenhaVisivel.Text;
            AtualizarValidador(SenhaGerada);
        }

        private void AtualizarValidador(string senha)
        {
            bool okLen = senha.Length >= _minLength;
            bool okUpper = !_exigirMaiuscula || senha.Any(char.IsUpper);
            bool okLower = !_exigirMinuscula || senha.Any(char.IsLower);
            bool okNum = !_exigirNumero || senha.Any(char.IsDigit);
            bool okEsp = _minEspeciais == 0 || senha.Count(c => _especiaisPermitidos.Contains(c)) >= _minEspeciais;

            SetRule(dotLength, lblLength, okLen, $"Mínimo {_minLength} caracteres", senha.Length > 0);
            SetRule(dotUpper, lblUpper, okUpper, "Letra maiúscula", senha.Length > 0);
            SetRule(dotLower, lblLower, okLower, "Letra minúscula", senha.Length > 0);
            SetRule(dotNumber, lblNumber, okNum, "Número", senha.Length > 0);
            SetRule(dotSpecial, lblSpecial, okEsp, $"Caractere especial ({_minEspeciais}): {_especiaisPermitidos}", senha.Length > 0);

            // Barra de força
            int score = new[] { okLen, okUpper, okLower, okNum, okEsp }.Count(x => x);
            AtualizarBarraForca(score, senha.Length);

            // Habilitar botão confirmar
            bool tudo = okLen && okUpper && okLower && okNum && okEsp;
            bool temEmail = !string.IsNullOrWhiteSpace(txtEmail?.Text);
            btnConfirmar.IsEnabled = tudo && temEmail;
        }

        private static readonly SolidColorBrush _brushOk = new(Color.FromRgb(22, 163, 74));
        private static readonly SolidColorBrush _brushFail = new(Color.FromRgb(220, 38, 38));
        private static readonly SolidColorBrush _brushNeutral = new(Color.FromRgb(209, 213, 219));

        private static void SetRule(Ellipse dot, TextBlock label, bool ok, string text, bool active)
        {
            label.Text = text;
            if (!active)
            {
                dot.Fill = _brushNeutral;
                label.Foreground = _brushNeutral;
            }
            else if (ok)
            {
                dot.Fill = _brushOk;
                label.Foreground = _brushOk;
            }
            else
            {
                dot.Fill = _brushFail;
                label.Foreground = _brushFail;
            }
        }

        private void AtualizarBarraForca(int score, int len)
        {
            // Penaliza senha muito curta
            if (len < _minLength) score = Math.Min(score, 1);

            var (c1, c2, c3, c4, label, cor) = score switch
            {
                0 => ("#E8ECF0", "#E8ECF0", "#E8ECF0", "#E8ECF0", "", "#9CA3AF"),
                1 => ("#EF4444", "#E8ECF0", "#E8ECF0", "#E8ECF0", "Fraca", "#EF4444"),
                2 => ("#F59E0B", "#F59E0B", "#E8ECF0", "#E8ECF0", "Regular", "#F59E0B"),
                3 => ("#3B82F6", "#3B82F6", "#3B82F6", "#E8ECF0", "Boa", "#3B82F6"),
                _ => ("#10B981", "#10B981", "#10B981", "#10B981", "Forte", "#10B981"),
            };

            SetBar(barForca1, c1);
            SetBar(barForca2, c2);
            SetBar(barForca3, c3);
            SetBar(barForca4, c4);

            lblForca.Text = label;
            lblForca.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cor));
        }

        private static void SetBar(Border bar, string hex)
        {
            bar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        // ══════════════════════════════════════════════════════════════
        //  MOSTRAR / OCULTAR SENHA
        // ══════════════════════════════════════════════════════════════
        private void BtnMostrarSenha_Click(object sender, RoutedEventArgs e)
        {
            _senhaVisivel = !_senhaVisivel;

            if (_senhaVisivel)
            {
                txtSenhaVisivel.Text = pwdSenha.Password;
                txtSenhaVisivel.Visibility = Visibility.Visible;
                pwdSenha.Visibility = Visibility.Collapsed;
                lblOlho.Text = "🙈";
            }
            else
            {
                _ignorarChanged = true;
                pwdSenha.Password = txtSenhaVisivel.Text;
                _ignorarChanged = false;
                pwdSenha.Visibility = Visibility.Visible;
                txtSenhaVisivel.Visibility = Visibility.Collapsed;
                lblOlho.Text = "👁";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  BOTÕES
        // ══════════════════════════════════════════════════════════════
        private void BtnRegerar_Click(object sender, RoutedEventArgs e) => GerarSenha();

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = false;
            Close();
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = false;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void TxtEmail_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Re-avaliar botão quando e-mail muda
            AtualizarValidador(SenhaGerada);
        }

        private async void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            btnConfirmar.IsEnabled = false;

            if (SomenteForcarTrocaNoProximoLogin)
            {
                Confirmado = true;
                SenhaGerada = "";
                Close();
                return;
            }

            txtBtnConfirmar.Text = "Enviando...";

            string email = txtEmail.Text.Trim();

            try
            {
                await Task.Run(() => EnviarEmails(email, SenhaGerada));

                Confirmado = true;

                ModernMessageBox.Show(
                    $"✅ Senha enviada com sucesso para: {email}\n\n" +
                    $"O(s) usuário(s) deve(m) alterar a senha no próximo acesso.",
                    "Confirmado",
                    ModernMessageBox.Kind.Info);

                Close();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(
                    $"⚠️ Erro ao enviar e-mail:\n{ex.Message}\n\n" +
                    "A troca de senha foi configurada no AD, mas o e-mail não foi entregue.",
                    "Atenção",
                    ModernMessageBox.Kind.Warning);

                btnConfirmar.IsEnabled = true;
                txtBtnConfirmar.Text = "Enviar e confirmar";
            }
        }

        private void ChkSomenteForcarTroca_CheckedChanged(object sender, RoutedEventArgs e)
        {
            SomenteForcarTrocaNoProximoLogin = chkSomenteForcarTroca.IsChecked == true;

            bool modoSenha = !SomenteForcarTrocaNoProximoLogin;

            // Campos principais
            txtEmail.IsEnabled = modoSenha;
            pwdSenha.IsEnabled = modoSenha;
            txtSenhaVisivel.IsEnabled = modoSenha;
            btnRegerar.IsEnabled = modoSenha;
            btnMostrarSenha.IsEnabled = modoSenha;

            // Requisitos visuais
            rowLength.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;
            rowUpper.Visibility = modoSenha && _exigirMaiuscula ? Visibility.Visible : Visibility.Collapsed;
            rowLower.Visibility = modoSenha && _exigirMinuscula ? Visibility.Visible : Visibility.Collapsed;
            rowNumber.Visibility = modoSenha && _exigirNumero ? Visibility.Visible : Visibility.Collapsed;
            rowSpecial.Visibility = modoSenha && _minEspeciais > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Barra de força
            barForca1.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;
            barForca2.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;
            barForca3.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;
            barForca4.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;
            lblForca.Visibility = modoSenha ? Visibility.Visible : Visibility.Collapsed;

            if (SomenteForcarTrocaNoProximoLogin)
            {
                txtBtnConfirmar.Text = "Confirmar";
                btnConfirmar.IsEnabled = true;
            }
            else
            {
                txtBtnConfirmar.Text = "Enviar e confirmar";
                AtualizarValidador(SenhaGerada);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ENVIO DE E-MAIL (SMTP)
        //  Configurações lidas de appsettings.json → seção "Email"
        // ══════════════════════════════════════════════════════════════

        private void EnviarEmails(string emailDestino, string senha)
        {
            string smtpHost = ConfigHelper.Get("Email:SmtpHost");
            int smtpPort = ConfigHelper.GetInt("Email:SmtpPort", 587);
            bool enableSsl = ConfigHelper.GetBool("Email:EnableSsl", true);
            string smtpUser = ConfigHelper.Get("Email:Usuario");
            string smtpPassword = ConfigHelper.Get("Email:Senha");
            string remetenteName = ConfigHelper.GetOptional("Email:NomeRemetente", "CredentialProviderAPP");

            if (string.IsNullOrWhiteSpace(smtpHost))
                throw new InvalidOperationException("Email:SmtpHost não configurado.");

            if (string.IsNullOrWhiteSpace(smtpUser))
                throw new InvalidOperationException("Email:Usuario não configurado.");

            if (string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("Email:Senha não configurada.");

            var destinatarios = emailDestino
                .Split(',', ';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!destinatarios.Any())
                throw new InvalidOperationException("Nenhum destinatário de e-mail válido foi informado.");

            string logins = string.Join(", ", _usuarios.Select(u => u.Login));
            string plural = _usuarios.Count > 1 ? "s" : "";

            string assunto = "Sua senha temporária — CredentialProviderAPP";
            string corpo = $@"
<html>
<body style='font-family:Segoe UI,Arial,sans-serif;color:#1A1D23;max-width:560px;margin:auto'>
  <div style='background:#2563EB;padding:28px 32px;border-radius:12px 12px 0 0'>
    <h2 style='color:white;margin:0;font-size:20px'>CredentialProviderAPP</h2>
    <p style='color:#BFD4FF;margin:6px 0 0;font-size:13px'>Sistema de Gestão de Credenciais</p>
  </div>

  <div style='background:white;padding:32px;border:1px solid #E8ECF0;border-top:none;border-radius:0 0 12px 12px'>
    <h3 style='margin:0 0 16px;font-size:17px'>Senha temporária definida</h3>

    <p style='color:#6B7280;font-size:13px;line-height:1.6'>
      O administrador do sistema definiu uma senha temporária para o{plural} usuário{plural}
      <strong>{logins}</strong>. Use-a no próximo acesso e altere imediatamente.
    </p>

    <div style='background:#F8FAFC;border:1px solid #E8ECF0;border-radius:8px;padding:16px 20px;margin:20px 0;text-align:center'>
      <p style='margin:0 0 4px;font-size:11px;color:#9CA3AF;letter-spacing:1px'>SENHA TEMPORÁRIA</p>
      <p style='margin:0;font-size:22px;font-weight:bold;font-family:Consolas,monospace;letter-spacing:4px;color:#1A1D23'>{senha}</p>
    </div>

    <p style='color:#EF4444;font-size:12px'>
      ⚠️ Por segurança, você <strong>deve alterar esta senha</strong> imediatamente após o login.
    </p>

    <hr style='border:none;border-top:1px solid #E8ECF0;margin:24px 0'/>
    <p style='color:#9CA3AF;font-size:11px;margin:0'>
      Este é um e-mail automático gerado pelo CredentialProviderAPP.<br/>
      Não responda a este e-mail.
    </p>
  </div>
</body>
</html>";

            using var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(smtpUser, remetenteName),
                Subject = assunto,
                Body = corpo,
                IsBodyHtml = true
            };

            foreach (var addr in destinatarios)
                msg.To.Add(addr);

            smtp.Send(msg);
        }
    }
}