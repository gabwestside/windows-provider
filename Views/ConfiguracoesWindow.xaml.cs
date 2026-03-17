using CredentialProviderAPP.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CredentialProviderAPP.Views
{
    public partial class ConfiguracoesWindow : Window
    {
        // ── controle de visibilidade das senhas ───────────────────────
        private bool _novaSenhaVisivel = false;
        private bool _confirmarSenhaVisivel = false;

        // ── flag para evitar loop no TextChanged ──────────────────────
        private bool _sincronizando = false;

        public ConfiguracoesWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ══════════════════════════════════════════════════════════════
        //  INICIALIZAÇÃO
        // ══════════════════════════════════════════════════════════════
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Garante que o banco e a tabela existem
                AdminCredentialService.InicializarBanco();

                // Carrega o login atual (nunca expõe a senha)
                var (login, _) = AdminCredentialService.Carregar();
                txtLogin.Text = login;

                // Mostra dica de "deixe em branco para manter"
                panelSenhaVazia.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MostrarStatus(false, $"Erro ao carregar configurações: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  MOSTRAR / OCULTAR SENHAS
        // ══════════════════════════════════════════════════════════════
        private void BtnMostrarNova_Click(object sender, RoutedEventArgs e)
        {
            _novaSenhaVisivel = !_novaSenhaVisivel;
            ToggleSenhaVisivel(
                _novaSenhaVisivel,
                pwdNovaSenha, txtNovaSenhaVisivel, lblOlhoNova);
        }

        private void BtnMostrarConfirmar_Click(object sender, RoutedEventArgs e)
        {
            _confirmarSenhaVisivel = !_confirmarSenhaVisivel;
            ToggleSenhaVisivel(
                _confirmarSenhaVisivel,
                pwdConfirmarSenha, txtConfirmarVisivel, lblOlhoConfirmar);
        }

        private static void ToggleSenhaVisivel(
            bool visivel,
            PasswordBox pwdBox, TextBox txtBox, TextBlock olho)
        {
            if (visivel)
            {
                txtBox.Text = pwdBox.Password;
                txtBox.Visibility = Visibility.Visible;
                pwdBox.Visibility = Visibility.Collapsed;
                olho.Text = "🙈";
            }
            else
            {
                pwdBox.Password = txtBox.Text;
                pwdBox.Visibility = Visibility.Visible;
                txtBox.Visibility = Visibility.Collapsed;
                olho.Text = "👁";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  EVENTOS DE SENHA — validação em tempo real
        // ══════════════════════════════════════════════════════════════
        private void Pwd_Changed(object sender, RoutedEventArgs e)
        {
            if (_sincronizando) return;
            ValidarSenhas();
        }

        private void TxtSenha_Changed(object sender, TextChangedEventArgs e)
        {
            if (_sincronizando) return;

            // Sincroniza o TextBox visível com o PasswordBox correspondente
            _sincronizando = true;
            try
            {
                if (sender == txtNovaSenhaVisivel)
                    pwdNovaSenha.Password = txtNovaSenhaVisivel.Text;
                else if (sender == txtConfirmarVisivel)
                    pwdConfirmarSenha.Password = txtConfirmarVisivel.Text;
            }
            finally
            {
                _sincronizando = false;
            }

            ValidarSenhas();
        }

        private void ValidarSenhas()
        {
            string nova = ObterSenha(pwdNovaSenha, txtNovaSenhaVisivel);
            string confirmar = ObterSenha(pwdConfirmarSenha, txtConfirmarVisivel);

            bool ambosVazios = string.IsNullOrEmpty(nova) && string.IsNullOrEmpty(confirmar);
            bool conferem = nova == confirmar;

            // Painel "deixe em branco para manter"
            panelSenhaVazia.Visibility =
                ambosVazios ? Visibility.Visible : Visibility.Collapsed;

            // Painel "não conferem"
            panelSenhasNaoConferem.Visibility =
                (!ambosVazios && !conferem) ? Visibility.Visible : Visibility.Collapsed;

            // Habilitar salvar: login preenchido + senhas conferem (ou ambas vazias)
            AtualizarBotaoSalvar();
        }

        private void AtualizarBotaoSalvar()
        {
            string nova = ObterSenha(pwdNovaSenha, txtNovaSenhaVisivel);
            string confirmar = ObterSenha(pwdConfirmarSenha, txtConfirmarVisivel);
            bool loginOk = !string.IsNullOrWhiteSpace(txtLogin.Text);
            bool senhasOk = nova == confirmar; // vazio == vazio também é ok

            btnSalvar.IsEnabled = loginOk && senhasOk;
        }

        // ══════════════════════════════════════════════════════════════
        //  BOTÕES
        // ══════════════════════════════════════════════════════════════
        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string novaSenha = ObterSenha(pwdNovaSenha, txtNovaSenhaVisivel);

            if (string.IsNullOrWhiteSpace(login))
            {
                MostrarStatus(false, "O login do administrador não pode ser vazio.");
                return;
            }

            try
            {
                // Salva (senha vazia = mantém a atual)
                AdminCredentialService.Salvar(login, novaSenha);

                MostrarStatus(true, "Configurações salvas com sucesso!");

                // Limpa os campos de senha por segurança
                LimparCamposSenha();
            }
            catch (Exception ex)
            {
                MostrarStatus(false, $"Erro ao salvar: {ex.Message}");
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Retorna a senha do campo ativo (PasswordBox ou TextBox visível).</summary>
        private static string ObterSenha(PasswordBox pwdBox, TextBox txtBox)
            => txtBox.Visibility == Visibility.Visible
               ? txtBox.Text
               : pwdBox.Password;

        private void LimparCamposSenha()
        {
            pwdNovaSenha.Password = "";
            pwdConfirmarSenha.Password = "";
            txtNovaSenhaVisivel.Text = "";
            txtConfirmarVisivel.Text = "";
            panelSenhasNaoConferem.Visibility = Visibility.Collapsed;
            panelSenhaVazia.Visibility = Visibility.Visible;
        }

        private void MostrarStatus(bool sucesso, string mensagem)
        {
            panelStatus.Visibility = Visibility.Visible;

            if (sucesso)
            {
                panelStatus.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4
                panelStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)); // #BBF7D0
                lblStatusIcon.Text = "✅";
                lblStatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));   // #16A34A
            }
            else
            {
                panelStatus.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
                panelStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202)); // #FECACA
                lblStatusIcon.Text = "❌";
                lblStatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));   // #DC2626
            }

            lblStatusMsg.Text = mensagem;
        }
    }
}