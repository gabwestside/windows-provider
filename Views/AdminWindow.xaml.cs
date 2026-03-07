using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CredentialProviderAPP.Models;
using System.Linq;

namespace CredentialProviderAPP.Views
{
    public partial class AdminWindow : Window
    {

        private string _colunaOrdenacao = "NomeCompleto";
        private bool _ordemAscendente = true;
        private List<UsuarioViewModel> _usuariosLocais = new();
        private List<UsuarioViewModel> _usuariosAD = new();

        private bool _computadorEmDominio = false;
        private bool _temProximaPagina = false;

        private int _paginaAtual = 1;
        private int _tamanhoPagina = 200;

        private string _grupoAD = "Domain Users"; // grupo padrão

        public AdminWindow()
        {
            InitializeComponent();
            this.Loaded += AdminWindow_Loaded;
        }

        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _computadorEmDominio = VerificarDominio();

                if (_computadorEmDominio)
                {
                    chkUsuariosAD.IsChecked = true;
                    chkUsuariosLocais.IsChecked = false;

                    await BuscarUsuariosAsync("");
                }
                else
                {
                    chkUsuariosAD.IsChecked = false;
                    chkUsuariosLocais.IsChecked = true;

                    MessageBox.Show(
                        "Este computador não está conectado a um domínio.\nSerão exibidos apenas usuários locais.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    _usuariosLocais = ObterUsuariosLocais();
                    AtualizarGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao iniciar:\n" + ex.Message);
            }
        }

private void dgUsuarios_Sorting(object sender, DataGridSortingEventArgs e)
{
    string coluna = e.Column.SortMemberPath;

    if (_colunaOrdenacao == coluna)
        _ordemAscendente = !_ordemAscendente;
    else
    {
        _colunaOrdenacao = coluna;
        _ordemAscendente = true;
    }

    foreach (var col in dgUsuarios.Columns)
        col.SortDirection = null;

    e.Column.SortDirection = _ordemAscendente
        ? System.ComponentModel.ListSortDirection.Ascending
        : System.ComponentModel.ListSortDirection.Descending;

    _paginaAtual = 1;

    AtualizarGrid();
}

        private bool VerificarDominio()
        {
            try
            {
                Domain.GetComputerDomain();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int ExtrairNumero(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return 0;

            var numeros = new string(texto.Where(char.IsDigit).ToArray());

            if (int.TryParse(numeros, out int resultado))
                return resultado;

            return 0;
        }

        private async Task BuscarUsuariosAsync(string filtro)
        {
            dgUsuarios.ItemsSource = new List<UsuarioViewModel>
            {
                new UsuarioViewModel
                {
                    NomeCompleto = "Buscando usuários...",
                    Login = "",
                    Tipo = "",
                    DataCadastro = ""
                }
            };

            await Task.Run(() =>
            {
                _usuariosAD = BuscarUsuariosAD(filtro);
            });

            AtualizarGrid();
        }

        private List<UsuarioViewModel> BuscarUsuariosAD(string filtro)
        {
            var usuarios = new List<UsuarioViewModel>();

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                {
                    GroupPrincipal grupo = GroupPrincipal.FindByIdentity(context, _grupoAD);

                    if (grupo == null)
                        return usuarios;

                    int skip = (_paginaAtual - 1) * _tamanhoPagina;

                    // pegamos 1 a mais para saber se existe próxima página
                    int take = _tamanhoPagina + 1;

                    int index = 0;

                    foreach (var member in grupo.GetMembers())
                    {
                        if (member is not UserPrincipal user)
                            continue;

                        if (!string.IsNullOrWhiteSpace(filtro))
                        {
                            if (!(user.SamAccountName?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false) &&
                                !(user.DisplayName?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false))
                                continue;
                        }

                        if (index++ < skip)
                            continue;

                        usuarios.Add(new UsuarioViewModel
                        {
                            Tipo = "Domínio",
                            NomeCompleto = user.DisplayName ?? user.Name,
                            Login = user.SamAccountName,
                            DataCadastro = user.LastLogon?.ToString("dd/MM/yyyy HH:mm") ?? "-"
                        });

                        if (usuarios.Count >= take)
                            break;
                    }

                    // verifica se existe próxima página
                    if (usuarios.Count > _tamanhoPagina)
                    {
                        _temProximaPagina = true;

                        // remove o último (era só para detectar próxima página)
                        usuarios.RemoveAt(usuarios.Count - 1);
                    }
                    else
                    {
                        _temProximaPagina = false;
                    }
                }
            }
            catch
            {
                _temProximaPagina = false;
            }

            return usuarios;
        }
        private List<UsuarioViewModel> ObterUsuariosLocais()
        {
            var usuarios = new List<UsuarioViewModel>();

            try
            {
                using (var context = new PrincipalContext(ContextType.Machine))
                {
                    var searcher = new PrincipalSearcher(new UserPrincipal(context));

                    foreach (var result in searcher.FindAll())
                    {
                        if (result is UserPrincipal user)
                        {
                            usuarios.Add(new UsuarioViewModel
                            {
                                Tipo = "Local",
                                NomeCompleto = user.DisplayName ?? user.Name,
                                Login = user.SamAccountName,
                                DataCadastro = user.LastLogon?.ToString("dd/MM/yyyy HH:mm") ?? "-"
                            });
                        }
                    }
                }
            }
            catch
            {
                MessageBox.Show("Erro ao buscar usuários locais.");
            }
            return usuarios
                .OrderBy(x => x.NomeCompleto)
                .ThenBy(x => x.Login)
                .ToList();
        }
        private void AtualizarGrid()
        {
            if (dgUsuarios == null)
                return;

            var todosUsuarios = new List<UsuarioViewModel>();

            string filtro = txtPesquisa.Text?.Trim();

            // AD
            if (chkUsuariosAD.IsChecked == true)
            {
                if (!string.IsNullOrWhiteSpace(filtro))
                {
                    var filtradosAD = _usuariosAD.Where(u =>
                        (u.Login?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.NomeCompleto?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false)
                    );

                    todosUsuarios.AddRange(filtradosAD);
                }
                else
                {
                    todosUsuarios.AddRange(_usuariosAD);
                }
            }

            // LOCAIS
            if (chkUsuariosLocais.IsChecked == true)
            {
                if (_usuariosLocais.Count == 0)
                    _usuariosLocais = ObterUsuariosLocais();

                if (!string.IsNullOrWhiteSpace(filtro))
                {
                    var filtradosLocais = _usuariosLocais.Where(u =>
                        (u.Login?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.NomeCompleto?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false)
                    );

                    todosUsuarios.AddRange(filtradosLocais);
                }
                else
                {
                    todosUsuarios.AddRange(_usuariosLocais);
                }
            }

            // ORDENAÇÃO
            IEnumerable<UsuarioViewModel> ordenado;

            switch (_colunaOrdenacao)
            {
                case "Login":
                    ordenado = _ordemAscendente
                        ? todosUsuarios.OrderBy(x => ExtrairNumero(x.Login)).ThenBy(x => x.Login)
                        : todosUsuarios.OrderByDescending(x => ExtrairNumero(x.Login)).ThenByDescending(x => x.Login);
                    break;

                case "Tipo":
                    ordenado = _ordemAscendente
                        ? todosUsuarios.OrderBy(x => x.Tipo)
                        : todosUsuarios.OrderByDescending(x => x.Tipo);
                    break;

                case "DataCadastro":
                    ordenado = _ordemAscendente
                        ? todosUsuarios.OrderBy(x => x.DataCadastro)
                        : todosUsuarios.OrderByDescending(x => x.DataCadastro);
                    break;

                default:
                    ordenado = _ordemAscendente
                        ? todosUsuarios.OrderBy(x => ExtrairNumero(x.NomeCompleto)).ThenBy(x => x.NomeCompleto)
                        : todosUsuarios.OrderByDescending(x => ExtrairNumero(x.NomeCompleto)).ThenByDescending(x => x.NomeCompleto);
                    break;
            }

            var listaOrdenada = ordenado.ToList();

            int total = listaOrdenada.Count;

            int skip = (_paginaAtual - 1) * _tamanhoPagina;

            var pagina = listaOrdenada
                .Skip(skip)
                .Take(_tamanhoPagina)
                .ToList();

            dgUsuarios.ItemsSource = pagina;

            _temProximaPagina = total > skip + _tamanhoPagina;

            int inicio = total == 0 ? 0 : skip + 1;
            int fim = skip + pagina.Count;

            lblPagina.Text = $"Página {_paginaAtual}";
            lblTotal.Text = $"Exibindo {inicio}–{fim} de {total} usuários";

            btnAnterior.IsEnabled = _paginaAtual > 1;
            btnProxima.IsEnabled = _temProximaPagina;
        }
        private async void FiltroAlterado(object sender, RoutedEventArgs e)
        {
            if (!_computadorEmDominio)
            {
                AtualizarGrid();
                return;
            }

            _paginaAtual = 1;

            string filtro = txtPesquisa.Text;

            await BuscarUsuariosAsync(filtro);
        }

        private async void Atualizar_Click(object sender, RoutedEventArgs e)
        {
            _usuariosLocais.Clear();
            _usuariosAD.Clear();

            if (_computadorEmDominio)
                await BuscarUsuariosAsync(txtPesquisa.Text);
            else
                AtualizarGrid();

            MessageBox.Show("Lista atualizada.");
        }

        private void NovoUsuario_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Criar novo usuário.");
        }

        private void ImportarUsuarios_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Importar usuários.");
        }

        private void EditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is UsuarioViewModel usuario)
                MessageBox.Show($"Editar usuário: {usuario.NomeCompleto}");
        }

        private void ExcluirUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is UsuarioViewModel usuario)
            {
                var confirm = MessageBox.Show(
                    $"Excluir usuário {usuario.NomeCompleto}?",
                    "Confirmar",
                    MessageBoxButton.YesNo);

                if (confirm == MessageBoxResult.Yes)
                    MessageBox.Show("Exclusão simulada.");
            }
        }
        private async void ProximaPagina_Click(object sender, RoutedEventArgs e)
        {
            _paginaAtual++;

            lblPagina.Text = $"Página {_paginaAtual}";

            if (_computadorEmDominio && chkUsuariosAD.IsChecked == true)
                await BuscarUsuariosAsync(txtPesquisa.Text);
            else
                AtualizarGrid();
        }

        private async void PaginaAnterior_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaAtual > 1)
                _paginaAtual--;

            lblPagina.Text = $"Página {_paginaAtual}";

            if (_computadorEmDominio && chkUsuariosAD.IsChecked == true)
                await BuscarUsuariosAsync(txtPesquisa.Text);
            else
                AtualizarGrid();
        }
    }

}