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

        // ── Arrastar janela ──
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void RegraSenhaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckExistingPolicy();
        }

        private void ChkExpiracao_Changed(object sender, RoutedEventArgs e)
        {
            if (txtDiasExpiracao == null) return;

            bool ativa = chkExpiracaoAtiva.IsChecked == true;
            txtDiasExpiracao.IsEnabled = ativa;

            // Limpa o campo ao desativar para não salvar valor residual
            if (!ativa)
                txtDiasExpiracao.Text = string.Empty;
        }

        private void CheckExistingPolicy()
        {
            try
            {
                var policy = PasswordPolicyFileHelper.Load();

                if (policy != null)
                {
                    policyExists = true;

                    ModernMessageBox.Show(
                        "Regra de senha encontrada. Visualize ou edite abaixo.",
                        "Política encontrada",
                        ModernMessageBox.Kind.Info,
                        this);

                    lblBtnSalvar.Text = "Atualizar";
                    LoadPolicy(policy);
                    return;
                }

                policyExists = false;
                lblBtnSalvar.Text = "Salvar Alterações";

                ModernMessageBox.Show(
                    "Nenhuma regra de senha encontrada.\nA tela será aberta em branco para criar uma nova.",
                    "Política de senha",
                    ModernMessageBox.Kind.Warning,
                    this);

                ClearFields();
            }
            catch (Exception ex)
            {
                policyExists = false;
                lblBtnSalvar.Text = "Salvar Alterações";
                ClearFields();

                ModernMessageBox.Show(
                    "Erro ao carregar política:\n" + ex.Message + "\n\nA tela será aberta em branco.",
                    "Erro",
                    ModernMessageBox.Kind.Error,
                    this);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CARREGAR POLÍTICA NOS CAMPOS
        // ══════════════════════════════════════════════════════════════
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

                // Campos de expiração
                chkExpiracaoAtiva.IsChecked = policy.ExpiracaoAtiva;
                txtDiasExpiracao.IsEnabled = policy.ExpiracaoAtiva;
                txtDiasExpiracao.Text = policy.ExpiracaoAtiva && policy.DiasExpiracao > 0
                    ? policy.DiasExpiracao.ToString()
                    : string.Empty;
            }
            catch (Exception ex)
            {
                ClearFields();
                ModernMessageBox.Show(
                    "Erro ao aplicar a política na tela:\n" + ex.Message,
                    "Erro", ModernMessageBox.Kind.Error, this);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  LIMPAR CAMPOS
        // ══════════════════════════════════════════════════════════════
        private void ClearFields()
        {
            txtTamanhoSenha.Text = string.Empty;
            txtQtdEspecial.Text = "0";
            txtCaracteres.Text = string.Empty;

            chkMaiuscula.IsChecked = false;
            chkMinuscula.IsChecked = false;
            chkNumero.IsChecked = false;
            chkExpiracaoAtiva.IsChecked = false;

            txtDiasExpiracao.Text = string.Empty;
            txtDiasExpiracao.IsEnabled = false;
        }

        // ══════════════════════════════════════════════════════════════
        //  SALVAR
        // ══════════════════════════════════════════════════════════════
        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Tamanho mínimo
                if (!int.TryParse(txtTamanhoSenha.Text?.Trim(), out int tamanho) || tamanho <= 0)
                {
                    ModernMessageBox.Show(
                        "Informe um tamanho mínimo válido (número maior que zero).",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtTamanhoSenha.Focus();
                    return;
                }

                // Quantidade de especiais
                int especiais = 0;
                if (!string.IsNullOrWhiteSpace(txtQtdEspecial.Text))
                {
                    if (!int.TryParse(txtQtdEspecial.Text.Trim(), out especiais) || especiais < 0)
                    {
                        ModernMessageBox.Show(
                            "Quantidade de caracteres especiais inválida.",
                            "Atenção", ModernMessageBox.Kind.Warning, this);
                        txtQtdEspecial.Focus();
                        return;
                    }
                }

                string caracteres = txtCaracteres.Text?.Trim() ?? string.Empty;

                if (especiais > 0 && string.IsNullOrWhiteSpace(caracteres))
                {
                    ModernMessageBox.Show(
                        "Informe os caracteres especiais permitidos.",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtCaracteres.Focus();
                    return;
                }

                if (!string.IsNullOrEmpty(caracteres) && caracteres.Any(char.IsLetterOrDigit))
                {
                    ModernMessageBox.Show(
                        "Caracteres especiais não podem conter letras ou números.",
                        "Atenção", ModernMessageBox.Kind.Warning, this);
                    txtCaracteres.Focus();
                    return;
                }

                // Expiração de senha
                bool expiracaoAtiva = chkExpiracaoAtiva.IsChecked == true;
                int diasExpiracao = 0;

                if (expiracaoAtiva)
                {
                    if (!int.TryParse(txtDiasExpiracao.Text?.Trim(), out diasExpiracao) || diasExpiracao <= 0)
                    {
                        ModernMessageBox.Show(
                            "Informe um número válido de dias para expiração (maior que zero).",
                            "Atenção", ModernMessageBox.Kind.Warning, this);
                        txtDiasExpiracao.Focus();
                        return;
                    }
                }

                var policy = new PasswordPolicyConfig
                {
                    MinLength = tamanho,
                    MinSpecialChars = especiais,
                    AllowedSpecialChars = caracteres,
                    RequireUppercase = chkMaiuscula.IsChecked == true,
                    RequireLowercase = chkMinuscula.IsChecked == true,
                    RequireNumber = chkNumero.IsChecked == true,
                    ExpiracaoAtiva = expiracaoAtiva,
                    DiasExpiracao = diasExpiracao
                };

                PasswordPolicyFileHelper.Save(policy);

                policyExists = true;
                lblBtnSalvar.Text = "Atualizar";

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