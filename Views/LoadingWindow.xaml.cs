using System.Windows;

namespace CredentialProviderAPP.Views
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow(string mensagem = "Conectando ao serviço...")
        {
            InitializeComponent();
            txtMensagem.Text = mensagem;
        }

        public void AtualizarMensagem(string mensagem)
        {
            txtMensagem.Text = mensagem;
        }
    }
}