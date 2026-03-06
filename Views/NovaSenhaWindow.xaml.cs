using System.Runtime.InteropServices;
using System.Windows;

namespace CredentialProviderAPP.Views;

public partial class NovaSenhaWindow : Window
{
    private string login;

    public NovaSenhaWindow(string user)
    {
        InitializeComponent();
        login = user;
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

        if (string.IsNullOrEmpty(senha))
        {
            MessageBox.Show("Digite a nova senha.");
            return;
        }

        if (senha != confirmar)
        {
            MessageBox.Show("Senhas não conferem.");
            return;
        }

        USER_INFO_1003 info = new USER_INFO_1003();
        info.usri1003_password = senha;

        int err;

        int result = NetUserSetInfo(
            null,
            login,
            1003,
            ref info,
            out err
        );

        if (result == 0)
        {
            MessageBox.Show("Senha alterada com sucesso!");
            Close();
        }
        else
        {
            MessageBox.Show("Erro ao alterar senha. Código: " + result);
        }
    }
}