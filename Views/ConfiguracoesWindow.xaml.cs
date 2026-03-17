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
                MessageBox.Show("Erro ao carregar configurações: " + ex.Message,
                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("O login do administrador não pode ser vazio.",
                    "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                AdminCredentialService.Salvar(login, novaSenha);
                MessageBox.Show("Configurações salvas com sucesso!",
                    "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message,
                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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