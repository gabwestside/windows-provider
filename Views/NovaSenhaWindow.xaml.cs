using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CredentialProviderAPP.Views;

public partial class NovaSenhaWindow : Window
{
    private string login;

    int minLength;
    int minSpecial;
    string allowedChars;
    bool needUpper;
    bool needLower;
    bool needNumber;

    string policyPath =
        Path.Combine(
            Path.GetDirectoryName(
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

    void LoadPolicy()
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

    void SenhaChanged(object sender, RoutedEventArgs e)
    {
        UpdateRules(txtSenha.Password);
    }

    void UpdateRules(string senha)
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

        SetRule(ruleLength, lengthOK, $"Mínimo {minLength} caracteres");
        SetRule(ruleUpper, upperOK, "Letra maiúscula");
        SetRule(ruleLower, lowerOK, "Letra minúscula");
        SetRule(ruleNumber, numberOK, "Número");
        SetRule(ruleSpecial, specialOK, $"Especial ({minSpecial})");

        if (match)
        {
            if (blacklistOK)
                SetRule(ruleBlacklist, true, "Não contém palavras proibidas");
            else
                SetRule(ruleBlacklist, false, $"Contém palavra proibida: {forbiddenWord}");
        }
        else
        {
            ruleBlacklist.Text = "";
        }

        if (txtConfirmar.Password.Length > 0)
            SetRule(ruleMatch, match, "Senhas coincidem");
        else
            ruleMatch.Text = "";

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

    void SetRule(TextBlock rule, bool ok, string text)
    {
        rule.Text = (ok ? "✔ " : "✖ ") + text;
        rule.Foreground = ok ? Brushes.Green : Brushes.Red;
    }

    bool ValidatePassword(string senha)
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
    static extern int NetUserSetInfo(
        string servername,
        string username,
        int level,
        ref USER_INFO_1003 buf,
        out int parm_err
    );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct USER_INFO_1003
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

        USER_INFO_1003 info = new();
        info.usri1003_password = senha;

        int err;

        int result =
            NetUserSetInfo(null, login, 1003, ref info, out err);

        if (result == 0)
        {
            MessageBox.Show("Senha alterada com sucesso!");
            Application.Current.Shutdown();
        }
        else
        {
            if (result == 2245)
            {
                MessageBox.Show("A senha não atende à política do Windows.");
            }
            else if (result == 5)
            {
                MessageBox.Show("Permissão negada. Execute como administrador.");
            }
            else
            {
                MessageBox.Show($"Erro ao alterar senha. Código: {result}");
            }
        }
    }
}