using CredentialProviderAPP.Models;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Views
{
    public partial class RegraSenhaWindow : Window
    {
        private string policyPath =
            Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty,
                "password_policy.txt");

        private bool policyExists = false;

        public RegraSenhaWindow()
        {
            InitializeComponent();
            CheckExistingPolicy();
        }

        private void CheckExistingPolicy()
        {
            if (File.Exists(policyPath))
            {
                policyExists = true;

                MessageBox.Show(
                    "Regra de senha encontrada.\nClique OK para visualizar ou editar.",
                    "Política encontrada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                btnSalvar.Content = "Atualizar";

                LoadPolicy();
            }
            else
            {
                var r = MessageBox.Show(
                    "Regra de senha não encontrada.\nCriar agora?",
                    "Política de senha",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (r == MessageBoxResult.No)
                    Close();
            }
        }

        private void LoadPolicy()
        {
            try
            {
                var lines = File.ReadAllLines(policyPath);

                if (lines.Length < 6)
                {
                    MessageBox.Show("Arquivo de política inválido.");
                    return;
                }

                var policy = new PasswordPolicy
                {
                    MinLength = int.Parse(lines[0]),
                    MinSpecialChars = int.Parse(lines[1]),
                    AllowedSpecialChars = lines[2],
                    RequireUppercase = bool.Parse(lines[3]),
                    RequireLowercase = bool.Parse(lines[4]),
                    RequireNumber = bool.Parse(lines[5])
                };

                txtTamanhoSenha.Text = policy.MinLength.ToString();
                txtQtdEspecial.Text = policy.MinSpecialChars.ToString();
                txtCaracteres.Text = policy.AllowedSpecialChars;

                chkMaiuscula.IsChecked = policy.RequireUppercase;
                chkMinuscula.IsChecked = policy.RequireLowercase;
                chkNumero.IsChecked = policy.RequireNumber;
            }
            catch
            {
                MessageBox.Show("Erro ao carregar a política.");
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtTamanhoSenha.Text, out int tamanho) || tamanho <= 0)
                {
                    MessageBox.Show("Informe um tamanho mínimo válido.");
                    return;
                }

                int especiais = 0;

                if (!string.IsNullOrWhiteSpace(txtQtdEspecial.Text))
                {
                    if (!int.TryParse(txtQtdEspecial.Text, out especiais) || especiais < 0)
                    {
                        MessageBox.Show("Quantidade de caracteres especiais inválida.");
                        return;
                    }
                }

                string caracteres = txtCaracteres.Text;

                if (string.IsNullOrWhiteSpace(caracteres) && especiais > 0)
                {
                    MessageBox.Show("Informe os caracteres especiais permitidos.");
                    return;
                }

                if (caracteres.Any(char.IsLetterOrDigit))
                {
                    MessageBox.Show("Caracteres especiais não podem conter letras ou números.");
                    return;
                }

                var policy = new PasswordPolicy
                {
                    MinLength = tamanho,
                    MinSpecialChars = especiais,
                    AllowedSpecialChars = caracteres,
                    RequireUppercase = chkMaiuscula.IsChecked == true,
                    RequireLowercase = chkMinuscula.IsChecked == true,
                    RequireNumber = chkNumero.IsChecked == true
                };

                string[] data =
                {
                    policy.MinLength.ToString(),
                    policy.MinSpecialChars.ToString(),
                    policy.AllowedSpecialChars,
                    policy.RequireUppercase.ToString(),
                    policy.RequireLowercase.ToString(),
                    policy.RequireNumber.ToString()
                };

                File.WriteAllLines(policyPath, data);

                policyExists = true;

                MessageBox.Show(
                    "Política de senha salva com sucesso.",
                    "Senha",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar política: " + ex.Message);
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NumeroOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }
    }
}