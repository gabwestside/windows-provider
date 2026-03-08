using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CredentialProviderAPP.Models;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.IO;
using System.DirectoryServices;
using CredentialProviderAPP.Data;
using CredentialProviderAPP.Config;

namespace CredentialProviderAPP.Views
{
    public partial class AdminWindow : Window
    {

        private string _colunaOrdenacao = "NomeCompleto";
        private bool _ordemAscendente = true;
        private List<UsuarioViewModel> _usuariosLocais = new();
        private List<UsuarioViewModel> _usuariosAD = new();

        private Dictionary<string, (int mfaenabled, int configured)> _usuariosMFA =
    new(StringComparer.OrdinalIgnoreCase);

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
                _usuariosMFA = CarregarMFA();

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
                using var entry = new DirectoryEntry("LDAP://RootDSE");
                string domain = entry.Properties["defaultNamingContext"].Value.ToString();

                using var searchRoot = new DirectoryEntry($"LDAP://{domain}");
                using var searcher = new DirectorySearcher(searchRoot);

                searcher.Filter = "(&(objectCategory=person)(objectClass=user))";

                searcher.PropertiesToLoad.Add("samAccountName");
                searcher.PropertiesToLoad.Add("displayName");
                searcher.PropertiesToLoad.Add("lastLogonTimestamp");

                searcher.PageSize = 1000;

                var results = searcher.FindAll();

                foreach (SearchResult result in results)
                {
                    string login = result.Properties["samAccountname"].Count > 0
                        ? result.Properties["samAccountname"][0].ToString()
                        : "";

                    string nome = result.Properties["displayname"].Count > 0
                        ? result.Properties["displayname"][0].ToString()
                        : login;

                    string data = "-";

                    if (result.Properties["lastLogontimestamp"].Count > 0)
                    {
                        long ticks = (long)result.Properties["lastlogontimestamp"][0];
                        DateTime dt = DateTime.FromFileTimeUtc(ticks);
                        data = dt.ToString("dd/MM/yyyy HH:mm");
                    }

                    if (!string.IsNullOrWhiteSpace(filtro))
                    {
                        if (!login.Contains(filtro, StringComparison.OrdinalIgnoreCase) &&
                            !nome.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    usuarios.Add(new UsuarioViewModel
                    {
                        Tipo = "Domínio",
                        Login = login,
                        NomeCompleto = nome,
                        DataCadastro = data
                    });
                }
            }
            catch
            {
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

            // MFA STATUS
            foreach (var user in todosUsuarios)
            {
                user.MFAStatus = ObterStatusMFA(user.Login);
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

            _usuariosMFA = CarregarMFA();

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
        private string ObterStatusMFA(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                return "Não configurado";

            login = Database.Normalize(login);

            if (_usuariosMFA.TryGetValue(login, out var dados))
            {
                if (dados.mfaenabled == 1 && dados.configured == 1)
                    return "Ativo";

                if (dados.mfaenabled == 1 && dados.configured == 0)
                    return "Pendente";
            }

            return "Não configurado";
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

        private void MenuRegraSenha_Click(object sender, RoutedEventArgs e)
        {
            var tela = new RegraSenhaWindow();
            tela.ShowDialog();
        }

        private void AtivarMFAEmMassa(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();

                using var transaction = conn.BeginTransaction();

                foreach (var user in usuarios)
                {
                    var login = Database.Normalize(user.Login);

                    System.Diagnostics.Debug.WriteLine($"ATIVANDO MFA -> {login}");

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
INSERT INTO users (username, mfaenabled, configured, createdat)
SELECT username, 1, 0, datetime('now')
FROM (
    SELECT @username AS username
)
WHERE NOT EXISTS (
    SELECT 1 FROM users WHERE LOWER(username) = LOWER(@username)
);

UPDATE users
SET mfaenabled = 1
WHERE LOWER(username) = LOWER(@username);
";

                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();

                MessageBox.Show($"MFA ativado para {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao ativar MFA: " + ex.Message);
            }
        }

        private void ProvisionarMFAEmMassa(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();

                using var transaction = conn.BeginTransaction();

                foreach (var user in usuarios)
                {
                    if (user.MFAStatus != "Não configurado")
                        continue;

                    var login = Database.Normalize(user.Login);

                    if (login.Contains("\\"))
                        login = login.Split('\\')[1];

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
INSERT INTO users (username, mfaenabled, configured, createdat)
VALUES (@username, 1, 0, datetime('now'))
ON CONFLICT(username)
DO UPDATE SET mfaenabled = 1";

                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao provisionar MFA: " + ex.Message);
            }
        }
        private void AtivarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var usuarios = dgUsuarios.SelectedItems
                .Cast<UsuarioViewModel>()
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.");
                return;
            }

            AtivarMFAEmMassa(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private Dictionary<string, (int mfaenabled, int configured)> CarregarMFA()
        {
            var resultado = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var caminho = AppConfig.DatabasePath;

                MessageBox.Show("Carregando MFA do banco:\n" + caminho);

                using var conn = Database.GetConnection();
                conn.Open();

                MessageBox.Show("SQLite conectado em:\n" + conn.DataSource);

                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT username, mfaenabled, configured FROM users";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string user = reader.GetString(0).Trim(); // NÃO usa ToLower
                    int mfa = reader.GetInt32(1);
                    int configured = reader.GetInt32(2);

                    resultado[user] = (mfa, configured);

                    System.Diagnostics.Debug.WriteLine(
                        $"MFA LOAD -> {user} | enabled={mfa} configured={configured}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro carregando MFA:\n" + ex.Message);
            }

            return resultado;
        }

        private void AtivarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = new List<UsuarioViewModel>();

            if (chkUsuariosAD.IsChecked == true)
                todos.AddRange(_usuariosAD);

            if (chkUsuariosLocais.IsChecked == true)
                todos.AddRange(_usuariosLocais);

            var usuarios = todos
                .Where(u => ObterStatusMFA(u.Login) == "Não configurado")
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Nenhum usuário precisa de MFA.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Ativar MFA para {usuarios.Count} usuários?",
                "Confirmação",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            AtivarMFAEmMassa(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void ResetarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var usuarios = dgUsuarios.SelectedItems
                .Cast<UsuarioViewModel>()
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Resetar MFA para {usuarios.Count} usuários?",
                "Confirmação",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            ResetarMFA(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void ResetarMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                using var transaction = conn.BeginTransaction();

                foreach (var user in usuarios)
                {
                    var login = Database.Normalize(user.Login);

                    if (login.Contains("\\"))
                        login = login.Split('\\')[1];

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
UPDATE users
SET configured = 0,
    totpsecret = NULL,
    mfaenabled = 1
WHERE LOWER(username) = LOWER(@username)";

                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();

                MessageBox.Show($"MFA resetado para {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao resetar MFA: " + ex.Message);
            }
        }

        private void ResetarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = new List<UsuarioViewModel>();

            if (chkUsuariosAD.IsChecked == true)
                todos.AddRange(_usuariosAD);

            if (chkUsuariosLocais.IsChecked == true)
                todos.AddRange(_usuariosLocais);

            var usuarios = todos
                .Where(u => ObterStatusMFA(u.Login) != "Não configurado")
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Nenhum usuário possui MFA.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Resetar MFA para {usuarios.Count} usuários?",
                "Confirmação",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            ResetarMFA(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void RemoverMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var usuarios = dgUsuarios.SelectedItems
                .Cast<UsuarioViewModel>()
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.");
                return;
            }

            RemoverMFA(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void RemoverMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = new List<UsuarioViewModel>();

            if (chkUsuariosAD.IsChecked == true)
                todos.AddRange(_usuariosAD);

            if (chkUsuariosLocais.IsChecked == true)
                todos.AddRange(_usuariosLocais);

            var usuarios = todos
                .Where(u => ObterStatusMFA(u.Login) != "Não configurado")
                .ToList();

            if (!usuarios.Any())
            {
                MessageBox.Show("Nenhum usuário possui MFA.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Remover MFA de {usuarios.Count} usuários?",
                "Confirmação",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            RemoverMFA(usuarios);

            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }
        private void RemoverMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();

                using var transaction = conn.BeginTransaction();

                foreach (var user in usuarios)
                {
                    var login = Database.Normalize(user.Login);

                    if (login.Contains("\\"))
                        login = login.Split('\\')[1];

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
UPDATE users
SET 
    mfaenabled = 0,
    configured = 0,
    totpsecret = NULL
WHERE LOWER(username) = LOWER(@username)";

                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();

                MessageBox.Show($"MFA removido de {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao remover MFA: " + ex.Message);
            }
        }
    }
}