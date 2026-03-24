using CredentialProviderAPP.Utils;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CredentialProviderAPP.Views;

public partial class NovaSenhaWindow : Window
{
    private string login;

    private int minLength;
    private int minSpecial;
    private string allowedChars = "";
    private bool needUpper;
    private bool needLower;
    private bool needNumber;
    private bool mostrandoDialog = false;

    // ✅ _forcandoFoco removido — NovaSenhaWindow não sequestra foco
    private static readonly SolidColorBrush _neutral = new(Color.FromRgb(0xC4, 0xC9, 0xD4));

    private static readonly SolidColorBrush _ok = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush _fail = new(Color.FromRgb(0xEF, 0x44, 0x44));

    private string policyPath =
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName
            )!,
            "password_policy.txt"
        );

    public NovaSenhaWindow(string user)
    {
        InitializeComponent();
        login = user;

        LoadPolicy();
        UpdateRules("");

        btnSalvar.IsEnabled = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void LoadPolicy()
    {
        if (!File.Exists(policyPath))
        {
            MostrarMensagem("Política de senha não encontrada.");
            Close();
            return;
        }

        var lines = File.ReadAllLines(policyPath);

        minLength = int.Parse(lines[0]);
        minSpecial = int.Parse(lines[1]);
        allowedChars = lines[2];
        needUpper = bool.Parse(lines[3]);
        needLower = bool.Parse(lines[4]);
        needNumber = bool.Parse(lines[5]);
    }

    private void SenhaChanged(object sender, RoutedEventArgs e)
    {
        UpdateRules(txtSenha.Password);
    }

    private void UpdateRules(string senha)
    {
        bool lengthOK = senha.Length >= minLength;
        bool upperOK = !needUpper || senha.Any(char.IsUpper);
        bool lowerOK = !needLower || senha.Any(char.IsLower);
        bool numberOK = !needNumber || senha.Any(char.IsDigit);

        int specialCount = senha.Count(c => allowedChars.Contains(c));
        bool specialOK = specialCount >= minSpecial;

        string? forbiddenWord = null;
        bool blacklistOK = true;

        bool match = txtConfirmar.Password == senha && senha.Length > 0;

        if (match)
        {
            forbiddenWord = PasswordBlacklist.GetForbiddenWord(senha);
            blacklistOK = forbiddenWord == null;
        }

        SetRule(dotLength, ruleLength, lengthOK, $"Mínimo {minLength} caracteres", senha.Length > 0);
        SetRule(dotUpper, ruleUpper, upperOK, "Letra maiúscula", senha.Length > 0);
        SetRule(dotLower, ruleLower, lowerOK, "Letra minúscula", senha.Length > 0);
        SetRule(dotNumber, ruleNumber, numberOK, "Número", senha.Length > 0);
        SetRule(dotSpecial, ruleSpecial, specialOK, $"Especial ({minSpecial})", senha.Length > 0);

        if (match)
        {
            panelBlacklist.Visibility = Visibility.Visible;
            SetRule(dotBlacklist, ruleBlacklist, blacklistOK,
                blacklistOK
                    ? "Não contém palavras proibidas"
                    : $"Palavra não pode ser usada: {forbiddenWord}",
                true);
        }
        else
        {
            panelBlacklist.Visibility = Visibility.Collapsed;
        }

        if (txtConfirmar.Password.Length > 0)
        {
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, match, "Senhas coincidem", true);
        }
        else
        {
            panelMatch.Visibility = Visibility.Collapsed;
        }

        btnSalvar.IsEnabled =
            lengthOK && upperOK && lowerOK && numberOK &&
            specialOK && blacklistOK && match;
    }

    private void EnableMFA()
    {
        try
        {
            string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
            string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
            string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");

            using var root = new DirectoryEntry(ldap, adUser, adSenha, AuthenticationTypes.Secure);

            var normalizedUser = LdapHelper.Escape(LdapHelper.NormalizeLogin(login));

            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(samAccountName={normalizedUser}))"
            };
            searcher.PropertiesToLoad.Add("info");

            var result = searcher.FindOne();
            if (result == null) { MostrarMensagem("Usuário não encontrado no Active Directory."); return; }

            using var entry = new DirectoryEntry(result.Path, adUser, adSenha, AuthenticationTypes.Secure);

            string? valorAtual = entry.Properties["info"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(valorAtual)) return;

            entry.Properties["info"].Value = "setup";
            entry.CommitChanges();
        }
        catch (Exception ex)
        {
            MostrarMensagem("Erro ao atualizar MFA no AD: " + ex.Message);
        }
    }

    private void SetRule(Ellipse dot, TextBlock label, bool ok, string text, bool active)
    {
        label.Text = text;

        if (!active)
        {
            dot.Fill = _neutral;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
        }
        else if (ok)
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

    private bool ValidatePassword(string senha)
    {
        if (senha.Length < minLength) return false;
        if (needUpper && !senha.Any(char.IsUpper)) return false;
        if (needLower && !senha.Any(char.IsLower)) return false;
        if (needNumber && !senha.Any(char.IsDigit)) return false;
        if (senha.Count(c => allowedChars.Contains(c)) < minSpecial) return false;
        return true;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserSetInfo(
        string servername,
        string username,
        int level,
        ref USER_INFO_1003 buf,
        out int parm_err
    );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct USER_INFO_1003
    {
        public string usri1003_password;
    }

    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        string senha = txtSenha.Password;
        string confirmar = txtConfirmar.Password;

        if (!ValidatePassword(senha)) { MostrarMensagem("Senha não atende à política."); return; }

        string? forbidden = PasswordBlacklist.GetForbiddenWord(senha);
        if (forbidden != null) { MostrarMensagem($"A senha contém a palavra proibida: {forbidden}"); return; }

        if (senha != confirmar) { MostrarMensagem("Senhas não conferem."); return; }

        string normalizedUser = LdapHelper.NormalizeLogin(login);
        bool senhaAlterada = false;

        string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
        string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");
        string domFQDN = ConfigHelper.Get("ActiveDirectory:Domain");
        string domNB = ConfigHelper.Get("ActiveDirectory:DomainNetBIOS");

        // 1️⃣ AD — FQDN
        try
        {
            using var ctx = new PrincipalContext(ContextType.Domain, domFQDN, adUser, adSenha);
            var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, normalizedUser);
            if (user != null) { user.SetPassword(senha); user.Save(); senhaAlterada = true; }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AD FQDN] Falhou: {ex.Message}"); }

        // 2️⃣ AD — NetBIOS
        if (!senhaAlterada)
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain, domNB, adUser, adSenha);
                var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, normalizedUser);
                if (user != null) { user.SetPassword(senha); user.Save(); senhaAlterada = true; }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AD NetBIOS] Falhou: {ex.Message}"); }
        }

        // 3️⃣ fallback local
        if (!senhaAlterada)
        {
            try
            {
                USER_INFO_1003 info = new() { usri1003_password = senha };
                int result = NetUserSetInfo(".", normalizedUser, 1003, ref info, out _);

                if (result == 0) { senhaAlterada = true; }
                else
                {
                    MostrarMensagem(result switch
                    {
                        2245 => "A senha não atende à política do Windows.",
                        5 => "Permissão negada. Execute como administrador.",
                        _ => $"Erro ao alterar senha. Código: {result}"
                    });
                    return;
                }
            }
            catch (Exception ex) { MostrarMensagem("Erro ao alterar senha: " + ex.Message); return; }
        }

        if (senhaAlterada)
        {
            EnableMFA();
            MostrarMensagem("Senha alterada com sucesso!");
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var result = MostrarMensagem(
            "Deseja cancelar a alteração de senha?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes) { DialogResult = false; Close(); }
    }

    // ✅ só foca uma vez ao carregar — sem Window_Deactivated
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Activate();
            Keyboard.Focus(txtSenha);
        }));
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