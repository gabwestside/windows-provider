using Microsoft.Data.Sqlite;
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
    private string allowedChars;
    private bool needUpper;
    private bool needLower;
    private bool needNumber;

    // Cores dos indicadores
    private static readonly SolidColorBrush _neutral = new(Color.FromRgb(0xC4, 0xC9, 0xD4)); // cinza

    private static readonly SolidColorBrush _ok = new(Color.FromRgb(0x22, 0xC5, 0x5E)); // verde
    private static readonly SolidColorBrush _fail = new(Color.FromRgb(0xEF, 0x44, 0x44)); // vermelho

    private string policyPath =
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName
            ),
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

    // ── Permite arrastar a janela (WindowStyle=None) ──
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void LoadPolicy()
    {
        if (!File.Exists(policyPath))
        {
            MessageBox.Show("Política de senha não encontrada.");
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

        // ── Regras principais — sempre visíveis ──
        SetRule(dotLength, ruleLength, lengthOK, $"Mínimo {minLength} caracteres", senha.Length > 0);
        SetRule(dotUpper, ruleUpper, upperOK, "Letra maiúscula", senha.Length > 0);
        SetRule(dotLower, ruleLower, lowerOK, "Letra minúscula", senha.Length > 0);
        SetRule(dotNumber, ruleNumber, numberOK, "Número", senha.Length > 0);
        SetRule(dotSpecial, ruleSpecial, specialOK, $"Especial ({minSpecial})", senha.Length > 0);

        // ── Blacklist — aparece apenas quando senhas coincidem ──
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

        // ── Confirmação — aparece apenas quando o campo tem texto ──
        if (txtConfirmar.Password.Length > 0)
        {
            panelMatch.Visibility = Visibility.Visible;
            SetRule(dotMatch, ruleMatch, match, "Senhas coincidem", true);
        }
        else
        {
            panelMatch.Visibility = Visibility.Collapsed;
        }

        bool allValid =
            lengthOK &&
            upperOK &&
            lowerOK &&
            numberOK &&
            specialOK &&
            blacklistOK &&
            match;

        btnSalvar.IsEnabled = allValid;
    }

    private void EnableMFA()
    {
        try
        {
            string dbPath =
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(
                        System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName
                    ),
                    "mfa.db"
                );

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE users
                SET mfaenabled  = 1,
                    configured   = 0,
                    totpsecret   = NULL
                WHERE lower(username) = lower($user)
                  AND configured = 0
            ";

            cmd.Parameters.AddWithValue("$user", login);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao atualizar MFA: " + ex.Message);
        }
    }

    /// <summary>
    /// Define o estado visual de um indicador (dot + label).
    /// Se <paramref name="active"/> for false, mantém o dot neutro (cinza).
    /// </summary>
    private void SetRule(Ellipse dot, TextBlock label, bool ok, string text, bool active)
    {
        label.Text = text;

        if (!active)
        {
            dot.Fill = _neutral;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)); // TextMuted
        }
        else if (ok)
        {
            dot.Fill = _ok;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)); // verde
        }
        else
        {
            dot.Fill = _fail;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // vermelho
        }
    }

    private bool ValidatePassword(string senha)
    {
        if (senha.Length < minLength)
            return false;

        if (needUpper && !senha.Any(char.IsUpper))
            return false;

        if (needLower && !senha.Any(char.IsLower))
            return false;

        if (needNumber && !senha.Any(char.IsDigit))
            return false;

        if (senha.Count(c => allowedChars.Contains(c)) < minSpecial)
            return false;

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

        if (!ValidatePassword(senha))
        {
            MessageBox.Show("Senha não atende à política.");
            return;
        }

        string? forbidden = PasswordBlacklist.GetForbiddenWord(senha);

        if (forbidden != null)
        {
            MessageBox.Show($"A senha contém a palavra proibida: {forbidden}");
            return;
        }

        if (senha != confirmar)
        {
            MessageBox.Show("Senhas não conferem.");
            return;
        }

        string normalizedUser = login;

        // remove DOMAIN\
        if (normalizedUser.Contains("\\"))
            normalizedUser = normalizedUser.Split('\\')[1];

        // remove @domain
        if (normalizedUser.Contains("@"))
            normalizedUser = normalizedUser.Split('@')[0];

        bool senhaAlterada = false;

        // ─────────────────────────────────────────
        // 1️⃣ TENTAR ALTERAR SENHA NO ACTIVE DIRECTORY
        // ─────────────────────────────────────────
        try
        {
            using var ctx = new PrincipalContext(ContextType.Domain);
            var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, normalizedUser);

            if (user != null)
            {
                user.SetPassword(senha);
                user.Save();

                senhaAlterada = true;
            }
        }
        catch (Exception)
        {
            // se falhar no AD tenta local
        }

        // ─────────────────────────────────────────
        // 2️⃣ SE NÃO FOR AD → TENTA USUÁRIO LOCAL
        // ─────────────────────────────────────────
        if (!senhaAlterada)
        {
            try
            {
                USER_INFO_1003 info = new();
                info.usri1003_password = senha;

                int err;
                int result = NetUserSetInfo(".", normalizedUser, 1003, ref info, out err);

                if (result == 0)
                    senhaAlterada = true;
                else
                {
                    if (result == 2245)
                    {
                        MessageBox.Show("A senha não atende à política do Windows.");
                        return;
                    }

                    if (result == 5)
                    {
                        MessageBox.Show("Permissão negada. Execute como administrador.");
                        return;
                    }

                    MessageBox.Show($"Erro ao alterar senha. Código: {result}");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao alterar senha: " + ex.Message);
                return;
            }
        }

        // ─────────────────────────────────────────
        // 3️⃣ SUCESSO
        // ─────────────────────────────────────────
        if (senhaAlterada)
        {
            EnableMFA();

            MessageBox.Show("Senha alterada com sucesso!");

            Application.Current.Shutdown();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Deseja cancelar a alteração de senha?",
            "Cancelar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            MessageBox.Show("Alteração de senha cancelada.");

            Application.Current.Shutdown();
        }
    }
}