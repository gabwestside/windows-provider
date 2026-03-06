using System.Windows;
using OtpNet;

namespace CredentialProviderAPP;

public partial class LoginMFAWindow : Window
{
    private byte[] key;

    public LoginMFAWindow(string secret)
    {
        InitializeComponent();
        key = Base32Encoding.ToBytes(secret);
    }

private void Validar_Click(object sender, RoutedEventArgs e)
{
    var totp = new Totp(key);

    bool valid = totp.VerifyTotp(
        txtCode.Text,
        out long step,
        new VerificationWindow(2,2)
    );

    if (valid)
    {
        MessageBox.Show("Código válido ✔");

        Environment.Exit(0); // sucesso
    }
    else
    {
        MessageBox.Show("Código inválido ❌");

        Environment.Exit(1); // falha
    }
}
}