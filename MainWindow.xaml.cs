using System.Windows;

namespace CredentialProviderAPP;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Suporte_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Ferramenta de suporte ainda não implementada.");
    }

    private void QRLogin_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Login via QR Code ainda não implementado.");
    }
}