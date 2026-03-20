using CredentialProviderAPP.Services;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Views
{
    public partial class ConfiguracoesWindow : Window
    {
        public ConfiguracoesWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AdminCredentialService.InicializarBanco();

                var (login, senha) = AdminCredentialService.Carregar();

                txtLogin.Text = login ?? string.Empty;
                pwdSenha.Password = senha ?? string.Empty;

                btnSalvar.IsEnabled = !string.IsNullOrWhiteSpace(txtLogin.Text);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao carregar configurações: " + ex.Message,
                    "Erro", ModernMessageBox.Kind.Error);
            }
        }

        private void TxtLogin_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            btnSalvar.IsEnabled = !string.IsNullOrWhiteSpace(txtLogin.Text);
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string novaSenha = pwdSenha.Password;

            if (string.IsNullOrWhiteSpace(login))
            {
                ModernMessageBox.Show("O login do administrador não pode ser vazio.",
                    "Validação", ModernMessageBox.Kind.Warning);
                return;
            }

            try
            {
                AdminCredentialService.Salvar(login, novaSenha);
                ModernMessageBox.Show("Configurações salvas com sucesso!",
                    "Sucesso", ModernMessageBox.Kind.Info);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao salvar: " + ex.Message,
                    "Erro", ModernMessageBox.Kind.Info);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}