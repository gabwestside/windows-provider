using CredentialProviderAPP.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CredentialProviderAPP.Views;

public partial class NovaSenhaWindow : Window
{
    private readonly string login;
    private bool mostrandoDialog = false;
    private bool senhaValidadaLocal = false;

    private List<string> palavrasProibidas = new();

    private int minLength;
    private int minSpecialChars;
    private string allowedSpecialChars = string.Empty;
    private bool requireUppercase;
    private bool requireLowercase;
    private bool requireNumber;
    private bool politicaCarregada = false;

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

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetLoading(true, "Carregando regras de senha...");

            InicializarRegras();
            await CarregarPoliticaAsync();
            await CarregarBlacklistAsync();

            SetLoading(false);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                Keyboard.Focus(txtSenha);
            }));
        }
        catch (Exception ex)
        {
            SetLoading(false);

            MostrarMensagem(
                "Erro ao carregar regras de senha: " + ex.Message,
                "Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DialogResult = false;
            Close();
        }
    }

    private void InicializarRegras()
    {
        SetRule(dotLength, ruleLength, null, "Tamanho mínimo");
        SetRule(dotUpper, ruleUpper, null, "Letra maiúscula");
        SetRule(dotLower, ruleLower, null, "Letra minúscula");
        SetRule(dotNumber, ruleNumber, null, "Número");
        SetRule(dotSpecial, ruleSpecial, null, "Caracteres especiais");

        panelBlacklist.Visibility = Visibility.Collapsed;
        panelMatch.Visibility = Visibility.Collapsed;
    }

    private async Task CarregarPoliticaAsync()
    {
        var result = await ServerApiService.ObterPoliticaSenhaAsync();

        if (!result.Sucesso)
            throw new InvalidOperationException(result.Erro ?? "Não foi possível carregar a política de senha.");

        minLength = result.MinLength;
        minSpecialChars = result.MinSpecialChars;
        allowedSpecialChars = result.AllowedSpecialChars ?? string.Empty;
        requireUppercase = result.RequireUppercase;
        requireLowercase = result.RequireLowercase;
        requireNumber = result.RequireNumber;
        politicaCarregada = true;

        ruleLength.Text = minLength > 0
            ? $"Mínimo de {minLength} caracteres"
            : "Sem tamanho mínimo obrigatório";

        ruleUpper.Text = requireUppercase
            ? "Pelo menos uma letra maiúscula"
            : "Letra maiúscula não obrigatória";

        ruleLower.Text = requireLowercase
            ? "Pelo menos uma letra minúscula"
            : "Letra minúscula não obrigatória";

        ruleNumber.Text = requireNumber
            ? "Pelo menos um número"
            : "Número não obrigatório";

        ruleSpecial.Text = minSpecialChars > 0
            ? $"Pelo menos {minSpecialChars} especial(is): {allowedSpecialChars}"
            : "Caracteres especiais não obrigatórios";
    }

    private async Task CarregarBlacklistAsync()
    {
        var result = await ServerApiService.ObterBlacklistSenhaAsync();

        if (!result.Sucesso)
            throw new InvalidOperationException(result.Erro ?? "Não foi possível carregar a lista de palavras proibidas.");

        palavrasProibidas = result.Palavras ?? new List<string>();
    }

    private void SenhaChanged(object sender, RoutedEventArgs e)
    {
        senhaValidadaLocal = false;
        btnSalvar.IsEnabled = false;

        if (!politicaCarregada)
            return;

        ValidarSenhaLocal();
    }

    private void ValidarSenhaLocal()
    {
        string senha = txtSenha.Password;
        string confirmar = txtConfirmar.Password;

        bool lengthOk = senha.Length >= minLength;
        bool upperOk = !requireUppercase || senha.Any(char.IsUpper);
        bool lowerOk = !requireLowercase || senha.Any(char.IsLower);
        bool numberOk = !requireNumber || senha.Any(char.IsDigit);

        int specialCount = senha.Count(c => allowedSpecialChars.Contains(c));
        bool specialCountOk = minSpecialChars == 0 || specialCount >= minSpecialChars;

        var invalidSpecialChars = senha
            .Where(c => !char.IsLetterOrDigit(c) && !allowedSpecialChars.Contains(c))
            .Distinct()
            .ToArray();

        bool invalidSpecialOk = invalidSpecialChars.Length == 0;
        bool specialOk = specialCountOk && invalidSpecialOk;

        string? palavraProibida = palavrasProibidas
            .FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p) &&
                senha.Contains(p, StringComparison.OrdinalIgnoreCase));

        bool blacklistOk = string.IsNullOrWhiteSpace(palavraProibida);

        SetRule(dotLength, ruleLength, lengthOk,
            minLength > 0 ? $"Mínimo de {minLength} caracteres" : "Sem tamanho mínimo obrigatório");

        SetRule(dotUpper, ruleUpper, upperOk,
            requireUppercase ? "Pelo menos uma letra maiúscula" : "Letra maiúscula não obrigatória");

        SetRule(dotLower, ruleLower, lowerOk,
            requireLowercase ? "Pelo menos uma letra minúscula" : "Letra minúscula não obrigatória");

        SetRule(dotNumber, ruleNumber, numberOk,
            requireNumber ? "Pelo menos um número" : "Número não obrigatório");

        if (!invalidSpecialOk)
        {
            SetRule(dotSpecial, ruleSpecial, false,
                $"Caracteres não podem ser usados: {string.Join(" ", invalidSpecialChars)}");
        }
        else
        {
            SetRule(
                dotSpecial,
                ruleSpecial,
                specialOk,
                minSpecialChars > 0
                    ? $"Pelo menos {minSpecialChars} especial(is): {allowedSpecialChars}"
                    : "Caracteres especiais não obrigatórios");
        }

        if (!blacklistOk)
        {
            panelBlacklist.Visibility = Visibility.Visible;
            SetRule(dotBlacklist, ruleBlacklist, false,
                $"A senha contém uma palavra proibida:\n{palavraProibida}");
        }
        else
        {
            panelBlacklist.Visibility = Visibility.Collapsed;
        }

        bool temConfirmacao = !string.IsNullOrWhiteSpace(confirmar);

        if (temConfirmacao)
        {
            bool match = senha == confirmar;
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, match,
                match ? "Senhas coincidem" : "Senhas não coincidem");
        }
        else
        {
            panelMatch.Visibility = Visibility.Collapsed;
        }

        bool matchOk = !string.IsNullOrWhiteSpace(confirmar) && senha == confirmar;

        senhaValidadaLocal = lengthOk
                             && upperOk
                             && lowerOk
                             && numberOk
                             && specialOk
                             && blacklistOk
                             && matchOk;

        btnSalvar.IsEnabled = senhaValidadaLocal;
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
            MostrarMensagem(
                "Digite a nova senha.",
                "Validação",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (senha != confirmar)
        {
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, false, "Senhas não coincidem");

            MostrarMensagem(
                "Senhas não conferem.",
                "Validação",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            btnSalvar.IsEnabled = false;
            return;
        }

        if (!senhaValidadaLocal)
        {
            MostrarMensagem(
                "Corrija os requisitos da senha antes de continuar.",
                "Validação",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetLoading(true, "Validando senha no servidor...");

            var validacaoServidor = await ServerApiService.ValidarSenhaAsync(login, senha);

            if (!validacaoServidor.Sucesso || !validacaoServidor.Valida)
            {
                panelBlacklist.Visibility = Visibility.Visible;
                SetRule(
                    dotBlacklist,
                    ruleBlacklist,
                    false,
                    validacaoServidor.Erro ?? "Senha rejeitada pelo servidor.");

                MostrarMensagem(
                    validacaoServidor.Erro ?? "Senha rejeitada pelo servidor.",
                    "Validação",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SetLoading(true, "Alterando senha...");

            var result = await ServerApiService.TrocarSenhaAsync(login, senha);

            if (!result.Sucesso)
            {
                panelBlacklist.Visibility = Visibility.Visible;
                SetRule(
                    dotBlacklist,
                    ruleBlacklist,
                    false,
                    result.Erro ?? "Não foi possível alterar a senha.");

                MostrarMensagem(
                    result.Erro ?? "Não foi possível alterar a senha.",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            MostrarMensagem(
                "Senha alterada com sucesso!",
                "Sucesso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MostrarMensagem(
                "Erro ao alterar senha: " + ex.Message,
                "Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
            ValidarSenhaLocal();
        }
    }

    private void ToggleSalvar(bool enabled)
    {
        txtSenha.IsEnabled = enabled;
        txtConfirmar.IsEnabled = enabled;
        btnSalvar.IsEnabled = enabled && senhaValidadaLocal;
    }

    private void Senha_LostFocus(object sender, RoutedEventArgs e)
    {
        if (politicaCarregada)
            ValidarSenhaLocal();
    }

    private void Senha_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && politicaCarregada)
            ValidarSenhaLocal();
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

    private void SetLoading(bool loading, string texto = "Carregando...")
    {
        overlayLoading.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        txtLoading.Text = texto;

        txtSenha.IsEnabled = !loading;
        txtConfirmar.IsEnabled = !loading;
        btnSalvar.IsEnabled = !loading && senhaValidadaLocal;
    }

    private void SetStatusValidacao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            txtStatusValidacao.Text = string.Empty;
            txtStatusValidacao.Visibility = Visibility.Collapsed;
            return;
        }

        txtStatusValidacao.Text = texto;
        txtStatusValidacao.Visibility = Visibility.Visible;
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