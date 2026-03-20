using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CredentialProviderAPP.Views
{
    /// <summary>
    /// Modal de mensagem moderno que substitui o MessageBox.Show padrão do sistema.
    ///
    /// Uso simples (OK apenas):
    ///   ModernMessageBox.Show("Texto da mensagem.");
    ///   ModernMessageBox.Show("Texto", "Título personalizado");
    ///   ModernMessageBox.Show("Texto", "Título", ModernMessageBox.Kind.Error);
    ///
    /// Uso com Sim/Não:
    ///   var result = ModernMessageBox.ShowYesNo("Deseja continuar?");
    ///   if (result == MessageBoxResult.Yes) { ... }
    /// </summary>
    public partial class ModernMessageBox : Window
    {
        // ── Tipos de mensagem ──────────────────────────────────────────
        public enum Kind { Info, Success, Warning, Error }

        // ── Resultado público ──────────────────────────────────────────
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        // ── Construtor privado — use os métodos estáticos ──────────────
        private ModernMessageBox() => InitializeComponent();

        // ══════════════════════════════════════════════════════════════
        //  MÉTODOS ESTÁTICOS PÚBLICOS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Exibe um modal com botão OK.</summary>
        public static void Show(
            string mensagem,
            string titulo = "Atenção",
            Kind kind = Kind.Info,
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    Show(mensagem, titulo, kind, owner));
                return;
            }

            var dlg = Criar(mensagem, titulo, kind, owner, yesNo: false);
            AplicarBlur(dlg.Owner, true);
            dlg.ShowDialog();
            AplicarBlur(dlg.Owner, false);
        }

        /// <summary>Exibe um modal com botões Sim / Cancelar e retorna o resultado.</summary>
        public static MessageBoxResult ShowYesNo(
            string mensagem,
            string titulo = "Confirmação",
            Kind kind = Kind.Warning,
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.Invoke(() =>
                    ShowYesNo(mensagem, titulo, kind, owner));
            }

            var dlg = Criar(mensagem, titulo, kind, owner, yesNo: true);
            AplicarBlur(dlg.Owner, true);
            dlg.ShowDialog();
            AplicarBlur(dlg.Owner, false);
            return dlg.Result;
        }

        // ══════════════════════════════════════════════════════════════
        //  FACTORY INTERNO
        // ══════════════════════════════════════════════════════════════
        private static ModernMessageBox Criar(
            string mensagem,
            string titulo,
            Kind kind,
            Window? owner,
            bool yesNo)
        {
            var dlg = new ModernMessageBox();

            // Owner — tenta usar a janela ativa se não informada
            dlg.Owner = owner
                ?? Application.Current?.Windows.OfType<Window>()
                               .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.MainWindow;

            // Texto
            dlg.lblTitulo.Text = titulo;
            dlg.lblMensagem.Text = mensagem;

            // Ícone e cores por tipo
            switch (kind)
            {
                case Kind.Success:
                    dlg.iconText.Text = "✓";
                    dlg.iconText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                    dlg.iconBorder.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    dlg.lblTitulo.Text = titulo == "Atenção" ? "Sucesso" : titulo;
                    break;

                case Kind.Warning:
                    dlg.iconText.Text = "⚠";
                    dlg.iconText.FontSize = 18;
                    dlg.iconText.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                    dlg.iconBorder.Background = new SolidColorBrush(Color.FromRgb(255, 251, 235));
                    dlg.lblTitulo.Text = titulo == "Atenção" ? "Atenção" : titulo;
                    break;

                case Kind.Error:
                    dlg.iconText.Text = "✕";
                    dlg.iconText.FontSize = 18;
                    dlg.iconText.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    dlg.iconBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                    dlg.lblTitulo.Text = titulo == "Atenção" ? "Erro" : titulo;
                    break;

                default: // Info
                    dlg.iconText.Text = "ℹ";
                    dlg.iconText.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    dlg.iconBorder.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                    break;
            }

            // Botões Sim / Cancelar
            if (yesNo)
            {
                dlg.btnOk.Content = "Sim";
                dlg.btnCancelar.Visibility = Visibility.Visible;
            }

            return dlg;
        }

        // ══════════════════════════════════════════════════════════════
        //  EFEITO DE DESFOQUE NA JANELA PAI
        //  Aplica blur suave na janela owner para dar a sensação de overlay
        // ══════════════════════════════════════════════════════════════
        private static void AplicarBlur(Window? owner, bool ativar)
        {
            if (owner == null) return;

            if (ativar)
            {
                owner.Effect = new BlurEffect { Radius = 6, KernelType = KernelType.Gaussian };
                owner.IsHitTestVisible = false; // bloqueia interação com o owner
            }
            else
            {
                owner.Effect = null;
                owner.IsHitTestVisible = true;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  EVENTOS DOS BOTÕES
        // ══════════════════════════════════════════════════════════════
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        // Fecha com Escape
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Result = MessageBoxResult.Cancel;
                Close();
            }
        }
    }
}
