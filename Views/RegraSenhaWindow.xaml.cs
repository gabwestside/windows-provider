using CredentialProviderAPP.Models;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Views
{
    public partial class RegraSenhaWindow : Window
    {
        private readonly string policyPath =
            Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty,
                "password_policy.txt");

        private bool policyExists = false;

        public RegraSenhaWindow()
        {
            InitializeComponent();
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
            if (File.Exists(policyPath))
            {
                policyExists = true;

                ModernMessageBox.Show(
                    "Regra de senha encontrada. Visualize ou edite abaixo.",
                    "Política encontrada",
                    ModernMessageBox.Kind.Info,
                    this);

                lblBtnSalvar.Text = "Atualizar";

                LoadPolicy();
            }
            else
            {
                var r = ModernMessageBox.ShowYesNo(
                    "Nenhuma regra de senha foi encontrada.\nDeseja criar uma agora?",
                    "Política de senha",
                    ModernMessageBox.Kind.Warning,
                    this);

                if (r != MessageBoxResult.Yes)
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
                    ModernMessageBox.Show(
                        "O arquivo de política está inválido ou corrompido.",
                        "Erro", ModernMessageBox.Kind.Error, this);
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
                ModernMessageBox.Show(
                    "Erro ao carregar a política de senha.",
                    "Erro", ModernMessageBox.Kind.Error, this);
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Valida tamanho mínimo
                if (!int.TryParse(txtTamanhoSenha.Text, out int tamanho) || tamanho <= 0)
                {
                    ModernMessageBox.Show(
                        "Informe um tamanho mínimo válido (número maior que zero).",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtTamanhoSenha.Focus();
                    return;
                }

                // Valida quantidade de especiais
                int especiais = 0;
                if (!string.IsNullOrWhiteSpace(txtQtdEspecial.Text))
                {
                    if (!int.TryParse(txtQtdEspecial.Text, out especiais) || especiais < 0)
                    {
                        ModernMessageBox.Show(
                            "Quantidade de caracteres especiais inválida.",
                            "Atenção", ModernMessageBox.Kind.Warning, this);
                        txtQtdEspecial.Focus();
                        return;
                    }
                }

                string caracteres = txtCaracteres.Text;

                if (string.IsNullOrWhiteSpace(caracteres) && especiais > 0)
                {
                    ModernMessageBox.Show(
                        "Informe os caracteres especiais permitidos.",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtCaracteres.Focus();
                    return;
                }

                if (caracteres.Any(char.IsLetterOrDigit))
                {
                    ModernMessageBox.Show(
                        "Caracteres especiais não podem conter letras ou números.",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtCaracteres.Focus();
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