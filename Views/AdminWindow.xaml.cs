using System.Windows;
using System.Windows.Controls;
using CredentialProviderAPP.Services;

namespace CredentialProviderAPP.Views
{
    public partial class AdminWindow : Window
    {
        private readonly UsuarioService _usuarioService;

        public AdminWindow()
        {
            InitializeComponent();
            _usuarioService = new UsuarioService();
            CarregarUsuarios();
        }

        private void CarregarUsuarios()
        {
            var usuarios = _usuarioService.ObterTodosUsuarios();
            dgUsuarios.ItemsSource = usuarios;
        }

        private void NovoUsuario_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Abrir janela/dialog para criar novo usuário
            // Exemplo de uso:
            // var novoUsuario = new UsuarioViewModel
            // {
            //     DataCadastro = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            //     Tipo = "Usuário",
            //     NomeCompleto = "Nome do Usuário",
            //     Login = "login"
            // };
            // _usuarioService.AdicionarUsuario(novoUsuario);
            // CarregarUsuarios();
            
            MessageBox.Show("Novo usuário");
        }

        private void ImportarUsuarios_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implementar lógica de importação
            // Exemplo de uso:
            // var usuariosImportados = new List<UsuarioViewModel> { ... };
            // _usuarioService.ImportarUsuarios(usuariosImportados);
            // CarregarUsuarios();
            
            MessageBox.Show("Importar usuários");
        }

        private void Atualizar_Click(object sender, RoutedEventArgs e)
        {
            CarregarUsuarios();
            MessageBox.Show("Lista atualizada");
        }

        private void EditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is UsuarioViewModel usuario)
            {
                // TODO: Abrir janela/dialog para editar usuário
                // Após edição, chamar:
                // _usuarioService.AtualizarUsuario(usuarioEditado);
                // CarregarUsuarios();
                
                MessageBox.Show($"Editar usuário: {usuario.NomeCompleto}");
            }
        }

        private void ExcluirUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is UsuarioViewModel usuario)
            {
                var resultado = MessageBox.Show(
                    $"Deseja realmente excluir o usuário '{usuario.NomeCompleto}'?",
                    "Confirmar Exclusão",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    var sucesso = _usuarioService.ExcluirUsuario(usuario.Login!);
                    if (sucesso)
                    {
                        CarregarUsuarios();
                        MessageBox.Show("Usuário excluído com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Erro ao excluir usuário.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}