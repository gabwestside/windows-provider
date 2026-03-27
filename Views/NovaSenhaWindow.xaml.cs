using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CredentialProviderAPP.Services;

namespace CredentialProviderAPP.Views;

public partial class NovaSenhaWindow : Window
{
    private readonly string login;
    private bool mostrandoDialog = false;
    private bool senhaValidadaServidor = false;

    private static readonly SolidColorBrush _neutral = new(Color.FromRgb(0xC4, 0xC9, 0xD4));
    private static readonly SolidColorBrush _ok = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush _fail = new(Color.FromRgb(0xEF, 0x44, 0x44));

    public NovaSenhaWindow(string user)
    {
        InitializeComponent();
        login = user;
        btnSalvar.IsEnabled = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            btnSalvar.IsEnabled = false;

            InicializarRegras();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                Keyboard.Focus(txtSenha);
            }));
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao inicializar tela de senha: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private void InicializarRegras()
    {
        SetRule(dotLength, ruleLength, null, "Validação da política no servidor");
        SetRule(dotUpper, ruleUpper, null, "Validação de maiúsculas/minúsculas/números");
        SetRule(dotLower, ruleLower, null, "Validação de caracteres especiais permitidos");
        SetRule(dotNumber, ruleNumber, null, "Validação de palavras proibidas");
        SetRule(dotSpecial, ruleSpecial, null, "A senha será validada ao sair do campo");

        panelBlacklist.Visibility = Visibility.Collapsed;
        panelMatch.Visibility = Visibility.Collapsed;
    }

    private void SenhaChanged(object sender, RoutedEventArgs e)
    {
        senhaValidadaServidor = false;
        btnSalvar.IsEnabled = false;

        bool temSenha = !string.IsNullOrWhiteSpace(txtSenha.Password);
        bool temConfirmacao = !string.IsNullOrWhiteSpace(txtConfirmar.Password);
        bool match = temSenha && temConfirmacao && txtSenha.Password == txtConfirmar.Password;

        panelBlacklist.Visibility = Visibility.Collapsed;

        if (temConfirmacao)
        {
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, match, match ? "Senhas coincidem" : "Senhas não coincidem");
        }
        else
        {
            panelMatch.Visibility = Visibility.Collapsed;
        }
    }

    private async Task ValidarSenhaServidorAsync()
    {
        string senha = txtSenha.Password;
        string confirmar = txtConfirmar.Password;

        senhaValidadaServidor = false;
        btnSalvar.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(senha))
        {
            panelBlacklist.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var result = await ServerApiService.ValidarSenhaAsync(login, senha);

            if (!result.Sucesso)
            {
                panelBlacklist.Visibility = Visibility.Visible;
                SetRule(dotBlacklist, ruleBlacklist, false,
                    AjustarMensagemServidor(result.Erro ?? "Erro ao validar senha no servidor"));
            }
            else if (result.Valida)
            {
                panelBlacklist.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelBlacklist.Visibility = Visibility.Visible;
                SetRule(dotBlacklist, ruleBlacklist, false,
                    AjustarMensagemServidor(result.Erro ?? "Senha inválida"));
            }

            bool temConfirmacao = !string.IsNullOrWhiteSpace(confirmar);

            if (temConfirmacao)
            {
                panelMatch.Visibility = Visibility.Visible;

                bool match = senha == confirmar;
                SetRule(dotMatch, ruleMatch, match, match ? "Senhas coincidem" : "Senhas não coincidem");

                if (result.Sucesso && result.Valida && match)
                {
                    senhaValidadaServidor = true;
                    btnSalvar.IsEnabled = true;
                }
            }
            else
            {
                panelMatch.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            panelBlacklist.Visibility = Visibility.Visible;
            SetRule(dotBlacklist, ruleBlacklist, false, "Erro ao validar senha: " + ex.Message);
        }
    }

    private static string AjustarMensagemServidor(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            return "Senha inválida.";

        const string prefixoAntigo = "A senha contém caractere(s) especial(is) não permitido(s):";

        if (mensagem.Contains(prefixoAntigo, StringComparison.OrdinalIgnoreCase))
        {
            mensagem = mensagem.Replace(prefixoAntigo, "Caracteres não podem ser usados:");
        }

        return mensagem;
    }

    private void SetRule(System.Windows.Shapes.Ellipse dot, System.Windows.Controls.TextBlock label, bool? ok, string text)
    {
        label.Text = text;

        if (ok == null)
        {
            dot.Fill = _neutral;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
        }
        else if (ok == true)
        {
            dot.Fill = _ok;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        }
        else
        {
            dot.Fill = _fail;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
    }

    private async void Salvar_Click(object sender, RoutedEventArgs e)
    {
        string senha = txtSenha.Password;
        string confirmar = txtConfirmar.Password;

        if (string.IsNullOrWhiteSpace(senha))
        {
            MostrarMensagem("Digite a nova senha.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (senha != confirmar)
        {
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, false, "Senhas não coincidem");

            MostrarMensagem("Senhas não conferem.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            btnSalvar.IsEnabled = false;
            return;
        }

        try
        {
            ToggleSalvar(false);

            await ValidarSenhaServidorAsync();

            if (!senhaValidadaServidor)
            {
                MostrarMensagem(
                    string.IsNullOrWhiteSpace(ruleBlacklist.Text) ? "Senha inválida." : ruleBlacklist.Text,
                    "Validação",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = await ServerApiService.TrocarSenhaAsync(login, senha);

            if (!result.Sucesso)
            {
                MostrarMensagem(result.Erro ?? "Não foi possível alterar a senha.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MostrarMensagem("Senha alterada com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao alterar senha: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleSalvar(true);
        }
    }

    private void ToggleSalvar(bool enabled)
    {
        txtSenha.IsEnabled = enabled;
        txtConfirmar.IsEnabled = enabled;

        if (!enabled)
        {
            btnSalvar.IsEnabled = false;
            return;
        }

        btnSalvar.IsEnabled = senhaValidadaServidor;
    }

    private async void Senha_LostFocus(object sender, RoutedEventArgs e)
    {
        await ValidarSenhaServidorAsync();
    }

    private async void Senha_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await ValidarSenhaServidorAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var result = MostrarMensagem(
            "Deseja cancelar a alteração de senha?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            DialogResult = false;
            Close();
        }
    }

    private MessageBoxResult MostrarMensagem(
        string msg,
        string titulo = "Aviso",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information)
    {
        mostrandoDialog = true;
        var result = MessageBox.Show(msg, titulo, buttons, icon);
        mostrandoDialog = false;
        return result;
    }
}