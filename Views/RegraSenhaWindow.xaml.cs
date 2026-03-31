using CredentialProviderAPP.Models.Api;
using CredentialProviderAPP.Utils;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Views
{
    public partial class RegraSenhaWindow : Window
    {
        private bool policyExists = false;

        public RegraSenhaWindow()
        {
            InitializeComponent();
            Loaded += RegraSenhaWindow_Loaded;
        }

        private void RegraSenhaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckExistingPolicy();
        }

        // ── Arrastar janela ──
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CheckExistingPolicy()
        {
            try
            {
                var policy = PasswordPolicyFileHelper.Load();

                if (policy != null)
                {
                    policyExists = true;

                    MessageBox.Show(
                        "Regra de senha encontrada.\nClique OK para visualizar ou editar.",
                        "Política encontrada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    btnSalvar.Content = "Atualizar";
                    LoadPolicy(policy);
                    return;
                }

                policyExists = false;
                btnSalvar.Content = "Salvar";

                MessageBox.Show(
                    "Regra de senha não encontrada.\nA tela será aberta em branco para criar uma nova.",
                    "Política de senha",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearFields();
            }
            catch (Exception ex)
            {
                policyExists = false;
                btnSalvar.Content = "Salvar";
                ClearFields();

                MessageBox.Show(
                    "Erro ao carregar política: " + ex.Message + "\nA tela será aberta em branco.",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LoadPolicy(PasswordPolicyConfig policy)
        {
            try
            {
                txtTamanhoSenha.Text = policy.MinLength.ToString();
                txtQtdEspecial.Text = policy.MinSpecialChars.ToString();
                txtCaracteres.Text = policy.AllowedSpecialChars ?? string.Empty;

                chkMaiuscula.IsChecked = policy.RequireUppercase;
                chkMinuscula.IsChecked = policy.RequireLowercase;
                chkNumero.IsChecked = policy.RequireNumber;
            }
            catch (Exception ex)
            {
                ClearFields();
                MessageBox.Show("Erro ao aplicar a política na tela: " + ex.Message);
            }
        }

        private void ClearFields()
        {
            txtTamanhoSenha.Text = string.Empty;
            txtQtdEspecial.Text = "0";
            txtCaracteres.Text = string.Empty;

            chkMaiuscula.IsChecked = false;
            chkMinuscula.IsChecked = false;
            chkNumero.IsChecked = false;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtTamanhoSenha.Text?.Trim(), out int tamanho) || tamanho <= 0)
                {
                    MessageBox.Show("Informe um tamanho mínimo válido.");
                    txtTamanhoSenha.Focus();
                    return;
                }

                // Valida quantidade de especiais
                int especiais = 0;
                if (!string.IsNullOrWhiteSpace(txtQtdEspecial.Text))
                {
                    if (!int.TryParse(txtQtdEspecial.Text.Trim(), out especiais) || especiais < 0)
                    {
                        MessageBox.Show("Quantidade de caracteres especiais inválida.");
                        txtQtdEspecial.Focus();
                        return;
                    }
                }

                string caracteres = txtCaracteres.Text?.Trim() ?? string.Empty;

                if (especiais > 0 && string.IsNullOrWhiteSpace(caracteres))
                {
                    MessageBox.Show("Informe os caracteres especiais permitidos.");
                    txtCaracteres.Focus();
                    return;
                }

                if (!string.IsNullOrEmpty(caracteres) && caracteres.Any(char.IsLetterOrDigit))
                {
                    MessageBox.Show("Caracteres especiais não podem conter letras ou números.");
                    txtCaracteres.Focus();
                    return;
                }

                var policy = new PasswordPolicyConfig
                {
                    MinLength = tamanho,
                    MinSpecialChars = especiais,
                    AllowedSpecialChars = caracteres,
                    RequireUppercase = chkMaiuscula.IsChecked == true,
                    RequireLowercase = chkMinuscula.IsChecked == true,
                    RequireNumber = chkNumero.IsChecked == true
                };

                PasswordPolicyFileHelper.Save(policy);

                policyExists = true;
                btnSalvar.Content = "Atualizar";

                ModernMessageBox.Show(
                    "Política de senha salva com sucesso.",
                    "Sucesso", ModernMessageBox.Kind.Success, this);

                Close();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(
                    "Erro ao salvar política:\n" + ex.Message,
                    "Erro", ModernMessageBox.Kind.Error, this);
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void NumeroOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }
    }
}