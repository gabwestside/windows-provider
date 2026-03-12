using CredentialProviderAPP.Data;
using CredentialProviderAPP.Models;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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

        public AdminWindow()
        {
            InitializeComponent();
            this.Loaded += AdminWindow_Loaded;
        }

        private void MostrarLoading()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void OcultarLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // ── Arrastar janela ──
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        // ── Fechar janela ──
        private void BtnFechar_Click(object sender, RoutedEventArgs e) => this.Close();

        // ══════════════════════════════════════════════════════════════
        //  BOTÃO "FUNÇÕES" — abre o ContextMenu posicionado abaixo do botão
        // ══════════════════════════════════════════════════════════════
        private void BtnFuncoes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = btn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CHECKBOX "SELECIONAR TODOS" no cabeçalho da coluna
        // ══════════════════════════════════════════════════════════════
        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox chk) return;

            if (chk.IsChecked == true)
                dgUsuarios.SelectAll();
            else
                dgUsuarios.UnselectAll();
        }

        // ══════════════════════════════════════════════════════════════
        //  INICIALIZAÇÃO
        // ══════════════════════════════════════════════════════════════
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
                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);

                    _usuariosLocais = ObterUsuariosLocais();
                    AtualizarGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao iniciar:\n" + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ORDENAÇÃO
        // ══════════════════════════════════════════════════════════════
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

            e.Handled = true; // impede que o DataGrid ordene sozinho
            _paginaAtual = 1;
            AtualizarGrid();
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════
        private bool VerificarDominio()
        {
            try { Domain.GetComputerDomain(); return true; }
            catch { return false; }
        }

        private int ExtrairNumero(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return 0;
            var nums = new string(texto.Where(char.IsDigit).ToArray());
            return int.TryParse(nums, out int r) ? r : 0;
        }

        // ══════════════════════════════════════════════════════════════
        //  BUSCA AD
        // ══════════════════════════════════════════════════════════════
        private async Task BuscarUsuariosAsync(string filtro)
        {
            dgUsuarios.ItemsSource = new List<UsuarioViewModel>
            {
                new() { NomeCompleto = "Buscando usuários...", Login = "", Tipo = "", DataCadastro = "" }
            };

            await Task.Run(() => { _usuariosAD = BuscarUsuariosAD(filtro); });
            AtualizarGrid();
        }

        private List<UsuarioViewModel> BuscarUsuariosAD(string filtro)
        {
            var usuarios = new List<UsuarioViewModel>();

            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                using var root = new DirectoryEntry(ldap);
                using var search = new DirectorySearcher(root)
                {
                    Filter = "(&(objectCategory=person)(objectClass=user))",
                    PageSize = 1000
                };

                search.PropertiesToLoad.Add("samAccountName");
                search.PropertiesToLoad.Add("displayName");
                search.PropertiesToLoad.Add("lastLogonTimestamp");

                foreach (SearchResult r in search.FindAll())
                {
                    string login = r.Properties["samAccountname"].Count > 0
                        ? r.Properties["samAccountname"][0].ToString()
                        : "";

                    string nome = r.Properties["displayname"].Count > 0
                        ? r.Properties["displayname"][0].ToString()
                        : login;

                    string data = "-";

                    if (r.Properties["lastlogontimestamp"].Count > 0)
                    {
                        long ticks = (long)r.Properties["lastlogontimestamp"][0];
                        data = DateTime.FromFileTimeUtc(ticks)
                            .ToString("dd/MM/yyyy HH:mm");
                    }

                    if (!string.IsNullOrWhiteSpace(filtro) &&
                        !login.Contains(filtro, StringComparison.OrdinalIgnoreCase) &&
                        !nome.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                        continue;

                    usuarios.Add(new UsuarioViewModel
                    {
                        Tipo = "Domínio",
                        Login = login,
                        NomeCompleto = nome,
                        DataCadastro = data
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao consultar Active Directory:\n" + ex.Message);
            }

            return usuarios;
        }

        private List<UsuarioViewModel> ObterUsuariosLocais()
        {
            var usuarios = new List<UsuarioViewModel>();
            try
            {
                using var ctx = new PrincipalContext(ContextType.Machine);
                var searcher = new PrincipalSearcher(new UserPrincipal(ctx));
                foreach (var result in searcher.FindAll())
                {
                    if (result is UserPrincipal u)
                        usuarios.Add(new UsuarioViewModel
                        {
                            Tipo = "Local",
                            NomeCompleto = u.DisplayName ?? u.Name,
                            Login = u.SamAccountName,
                            DataCadastro = u.LastLogon?.ToString("dd/MM/yyyy HH:mm") ?? "-"
                        });
                }
            }
            catch { MessageBox.Show("Erro ao buscar usuários locais."); }

            return usuarios.OrderBy(x => x.NomeCompleto).ThenBy(x => x.Login).ToList();
        }

        // ══════════════════════════════════════════════════════════════
        //  ATUALIZAR GRID (paginação + ordenação + MFA)
        // ══════════════════════════════════════════════════════════════
        private void AtualizarGrid()
        {
            if (dgUsuarios == null) return;

            var todos = new List<UsuarioViewModel>();
            string filtro = txtPesquisa.Text?.Trim();

            if (chkUsuariosAD.IsChecked == true)
            {
                var lista = string.IsNullOrWhiteSpace(filtro) ? _usuariosAD :
                    _usuariosAD.Where(u =>
                        (u.Login?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.NomeCompleto?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false));
                todos.AddRange(lista);
            }

            if (chkUsuariosLocais.IsChecked == true)
            {
                if (_usuariosLocais.Count == 0)
                    _usuariosLocais = ObterUsuariosLocais();

                var lista = string.IsNullOrWhiteSpace(filtro) ? _usuariosLocais :
                    _usuariosLocais.Where(u =>
                        (u.Login?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.NomeCompleto?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false));
                todos.AddRange(lista);
            }

            foreach (var u in todos)
                u.MFAStatus = ObterStatusMFA(u.Login);

            IEnumerable<UsuarioViewModel> ordenado = _colunaOrdenacao switch
            {
                "Login" => _ordemAscendente
                    ? todos.OrderBy(x => ExtrairNumero(x.Login)).ThenBy(x => x.Login)
                    : todos.OrderByDescending(x => ExtrairNumero(x.Login)).ThenByDescending(x => x.Login),
                "Tipo" => _ordemAscendente
                    ? todos.OrderBy(x => x.Tipo)
                    : todos.OrderByDescending(x => x.Tipo),
                "DataCadastro" => _ordemAscendente
                    ? todos.OrderBy(x => x.DataCadastro)
                    : todos.OrderByDescending(x => x.DataCadastro),
                _ => _ordemAscendente
                    ? todos.OrderBy(x => ExtrairNumero(x.NomeCompleto)).ThenBy(x => x.NomeCompleto)
                    : todos.OrderByDescending(x => ExtrairNumero(x.NomeCompleto)).ThenByDescending(x => x.NomeCompleto)
            };

            var lista2 = ordenado.ToList();
            int total = lista2.Count;
            int skip = (_paginaAtual - 1) * _tamanhoPagina;
            var pagina = lista2.Skip(skip).Take(_tamanhoPagina).ToList();

            dgUsuarios.ItemsSource = pagina;
            _temProximaPagina = total > skip + _tamanhoPagina;

            int inicio = total == 0 ? 0 : skip + 1;
            int fim = skip + pagina.Count;

            lblPagina.Text = $"Página {_paginaAtual}";
            lblTotal.Text = $"Exibindo {inicio}–{fim} de {total} usuários";

            btnAnterior.IsEnabled = _paginaAtual > 1;
            btnProxima.IsEnabled = _temProximaPagina;
        }

        // ══════════════════════════════════════════════════════════════
        //  EVENTOS DE FILTRO / ATUALIZAR / PAGINAÇÃO / MENU
        // ══════════════════════════════════════════════════════════════
        private async void FiltroAlterado(object sender, RoutedEventArgs e)
        {
            if (!_computadorEmDominio) { AtualizarGrid(); return; }
            _paginaAtual = 1;
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }

        private async void Atualizar_Click(object sender, RoutedEventArgs e)
        {
            _usuariosLocais.Clear();
            _usuariosAD.Clear();
            _usuariosMFA = CarregarMFA();

            if (_computadorEmDominio) await BuscarUsuariosAsync(txtPesquisa.Text);
            else AtualizarGrid();

            MessageBox.Show("Lista atualizada.");
        }

        private async void ProximaPagina_Click(object sender, RoutedEventArgs e)
        {
            _paginaAtual++;
            lblPagina.Text = $"Página {_paginaAtual}";
            if (_computadorEmDominio && chkUsuariosAD.IsChecked == true)
                await BuscarUsuariosAsync(txtPesquisa.Text);
            else AtualizarGrid();
        }

        private async void PaginaAnterior_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaAtual > 1) _paginaAtual--;
            lblPagina.Text = $"Página {_paginaAtual}";
            if (_computadorEmDominio && chkUsuariosAD.IsChecked == true)
                await BuscarUsuariosAsync(txtPesquisa.Text);
            else AtualizarGrid();
        }

        private void MenuRegraSenha_Click(object sender, RoutedEventArgs e)
        {
            new RegraSenhaWindow().ShowDialog();
        }

        // ══════════════════════════════════════════════════════════════
        //  MFA — STATUS
        // ══════════════════════════════════════════════════════════════
        private string ObterStatusMFA(string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return "Não configurado";
            login = Database.Normalize(login);
            if (_usuariosMFA.TryGetValue(login, out var d))
            {
                if (d.mfaenabled == 1 && d.configured == 1) return "Ativo";
                if (d.mfaenabled == 1 && d.configured == 0) return "Pendente";
            }
            return "Não configurado";
        }

        private Dictionary<string, (int mfaenabled, int configured)> CarregarMFA()
        {
            var resultado = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT username, mfaenabled, configured FROM users";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    resultado[reader.GetString(0).Trim()] = (reader.GetInt32(1), reader.GetInt32(2));
            }
            catch (Exception ex) { MessageBox.Show("Erro carregando MFA:\n" + ex.Message); }
            return resultado;
        }

        // ══════════════════════════════════════════════════════════════
        //  MFA — ATIVAR
        // ══════════════════════════════════════════════════════════════
        private void AtivarMFAEmMassa(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var user in usuarios)
                {
                    var login = Database.Normalize(user.Login);
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO users (username, mfaenabled, configured, createdat)
                        SELECT username, 1, 0, datetime('now')
                        FROM (SELECT @username AS username)
                        WHERE NOT EXISTS (
                            SELECT 1 FROM users WHERE LOWER(username) = LOWER(@username)
                        );
                        UPDATE users SET mfaenabled = 1
                        WHERE LOWER(username) = LOWER(@username);";
                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                MessageBox.Show($"MFA ativado para {usuarios.Count} usuários.");
            }
            catch (Exception ex) { MessageBox.Show("Erro ao ativar MFA: " + ex.Message); }
        }

        private void AtivarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { MessageBox.Show("Selecione pelo menos um usuário."); return; }
            AtivarMFAEmMassa(sel);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void AtivarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => ObterStatusMFA(u.Login) == "Não configurado").ToList();

            if (!todos.Any()) { MessageBox.Show("Nenhum usuário precisa de MFA."); return; }

            if (MessageBox.Show($"Ativar MFA para {todos.Count} usuários?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            AtivarMFAEmMassa(todos);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        // ══════════════════════════════════════════════════════════════
        //  MFA — RESETAR
        // ══════════════════════════════════════════════════════════════
        private void ResetarMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var user in usuarios)
                {
                    var login = NormalizarLogin(user.Login);
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE users
                        SET configured = 0, totpsecret = NULL, mfaenabled = 1
                        WHERE LOWER(username) = LOWER(@username)";
                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                MessageBox.Show($"MFA resetado para {usuarios.Count} usuários.");
            }
            catch (Exception ex) { MessageBox.Show("Erro ao resetar MFA: " + ex.Message); }
        }

        private void ResetarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { MessageBox.Show("Selecione pelo menos um usuário."); return; }

            if (MessageBox.Show($"Resetar MFA para {sel.Count} usuários?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            ResetarMFA(sel);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void ResetarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => ObterStatusMFA(u.Login) != "Não configurado").ToList();

            if (!todos.Any()) { MessageBox.Show("Nenhum usuário possui MFA."); return; }

            if (MessageBox.Show($"Resetar MFA para {todos.Count} usuários?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            ResetarMFA(todos);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        // ══════════════════════════════════════════════════════════════
        //  MFA — REMOVER
        // ══════════════════════════════════════════════════════════════
        private void RemoverMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var user in usuarios)
                {
                    var login = NormalizarLogin(user.Login);
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE users
                        SET mfaenabled = 0, configured = 0, totpsecret = NULL
                        WHERE LOWER(username) = LOWER(@username)";
                    cmd.Parameters.AddWithValue("@username", login);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                MessageBox.Show($"MFA removido de {usuarios.Count} usuários.");
            }
            catch (Exception ex) { MessageBox.Show("Erro ao remover MFA: " + ex.Message); }
        }

        private void RemoverMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { MessageBox.Show("Selecione pelo menos um usuário."); return; }
            RemoverMFA(sel);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        private void RemoverMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => ObterStatusMFA(u.Login) != "Não configurado").ToList();

            if (!todos.Any()) { MessageBox.Show("Nenhum usuário possui MFA."); return; }

            if (MessageBox.Show($"Remover MFA de {todos.Count} usuários?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            RemoverMFA(todos);
            _usuariosMFA = CarregarMFA();
            AtualizarGrid();
        }

        // ══════════════════════════════════════════════════════════════
        //  TROCAR SENHA NO PRÓXIMO LOGIN (AD — pwdLastSet = 0)

        private void ForcarTrocaSenha(List<UsuarioViewModel> usuarios)
        {
            int ok = 0, fail = 0;

            foreach (var user in usuarios)
            {
                try
                {
                    // ───────────── LOCAL ─────────────
                    if (user.Tipo?.Equals("Local", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        using var ctx = new PrincipalContext(ContextType.Machine);
                        var u = UserPrincipal.FindByIdentity(ctx, user.Login);

                        if (u == null)
                        {
                            fail++;
                            continue;
                        }

                        u.ExpirePasswordNow();
                        u.Save();

                        ok++;
                    }
                    else
                    {
                        // ───────────── AD ─────────────
                        string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                        using var searcher = new DirectorySearcher(new DirectoryEntry(ldap))
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={user.Login}))"
                        };

                        searcher.PropertiesToLoad.Add("distinguishedName");

                        var result = searcher.FindOne();

                        if (result == null)
                        {
                            fail++;
                            continue;
                        }

                        using var entry = result.GetDirectoryEntry();
                        entry.Properties["pwdLastSet"].Value = 0;
                        entry.CommitChanges();

                        ok++;
                    }
                }
                catch
                {
                    fail++;
                }
            }

            string msg = $"✅ {ok} usuário(s) marcados para trocar senha no próximo login.";

            if (fail > 0)
                msg += $"\n⚠️ {fail} usuário(s) não puderam ser processados.";

            MessageBox.Show(msg,
                "Trocar Senha no Próximo Login",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void TrocarSenhaProximoLoginTodos_Click(object sender, RoutedEventArgs e)
        {
            var adUsers = _usuariosAD.Where(u =>
                !u.Tipo?.Equals("Local", StringComparison.OrdinalIgnoreCase) ?? false).ToList();

            if (!adUsers.Any())
            {
                MessageBox.Show("Nenhum usuário de domínio encontrado.", "Atenção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                $"Forçar troca de senha no próximo login para TODOS os {adUsers.Count} usuários de domínio?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            MostrarLoading();

            await Task.Run(() =>
            {
                ForcarTrocaSenha(adUsers);
            });

            OcultarLoading();
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS INTERNOS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Combina as listas AD + Local conforme toggles ativos.</summary>
        private List<UsuarioViewModel> CombinarUsuarios()
        {
            var lista = new List<UsuarioViewModel>();
            if (chkUsuariosAD.IsChecked == true) lista.AddRange(_usuariosAD);
            if (chkUsuariosLocais.IsChecked == true) lista.AddRange(_usuariosLocais);
            return lista;
        }

        /// <summary>Remove prefixo DOMAIN\ do login se presente.</summary>
        private static string NormalizarLogin(string login)
        {
            login = Database.Normalize(login);
            return login.Contains('\\') ? login.Split('\\')[1] : login;
        }
        private async void TrocarSenhaProximoLoginSelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();

            if (!sel.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.", "Atenção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                $"Forçar troca de senha no próximo login para {sel.Count} usuário(s)?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            MostrarLoading();

            try
            {
                await Task.Run(() =>
                {
                    ForcarTrocaSenha(sel);
                });
            }
            finally
            {
                OcultarLoading();
            }
        }
    }
}