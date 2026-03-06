using System.Windows;
using OtpNet;

namespace CredentialProviderAPP;

public partial class VerificarCodigoWindow : Window
{
    private byte[] key;

    public VerificarCodigoWindow(string secret)
    {
        InitializeComponent();
        key = Base32Encoding.ToBytes(secret);
    }

    private void Verificar_Click(object sender, RoutedEventArgs e)
    {
        var totp = new Totp(key);

        bool valid = totp.VerifyTotp(
            txtCode.Text,
            out long step,
            new VerificationWindow(2,2)
        );

        if(valid)
        {
            MessageBox.Show("Código válido ✔");
        }
        else
        {
            MessageBox.Show("Código inválido ❌");
        }
    }
}