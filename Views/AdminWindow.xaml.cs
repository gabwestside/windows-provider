using CredentialProviderAPP.Models;
using CredentialProviderAPP.Utils;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Security.Principal;
using CredentialProviderAPP.Helpers;

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

        public AdminWindow()
        {
            InitializeComponent();
            this.Loaded += AdminWindow_Loaded;
        }

        private void MostrarLoading() => LoadingOverlay.Visibility = Visibility.Visible;
        private void OcultarLoading() => LoadingOverlay.Visibility = Visibility.Collapsed;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => this.Close();

        private void BtnFuncoes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = btn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox chk) return;

            if (chk.IsChecked == true) dgUsuarios.SelectAll();
            else dgUsuarios.UnselectAll();
        }

        // ══════════════════════════════════════════════════════════════
        //  INICIALIZAÇÃO
        // ══════════════════════════════════════════════════════════════
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

            e.Handled = true;
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
            MostrarLoading();

            dgUsuarios.ItemsSource = new List<UsuarioViewModel>
    {
        new() { NomeCompleto = "Buscando usuários...", Login = "", Tipo = "", DataCadastro = "" }
    };

            try
            {
                await Task.Run(() => { _usuariosAD = BuscarUsuariosAD(filtro); });
                AtualizarGrid();
            }
            finally
            {
                OcultarLoading();
            }
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
                            DataCadastro = u.LastLogon?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                            Email = "",
                            MFAStatus = "Não configurado"
                        });
                }
            }
            catch { MessageBox.Show("Erro ao buscar usuários locais."); }

            return usuarios.OrderBy(x => x.NomeCompleto).ThenBy(x => x.Login).ToList();
        }

        // ══════════════════════════════════════════════════════════════
        //  ATUALIZAR GRID
        // ══════════════════════════════════════════════════════════════
        private void AtualizarGrid()
        {
            if (dgUsuarios == null) return;

            var todos = new List<UsuarioViewModel>();
            string? filtro = txtPesquisa.Text?.Trim();

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
        //  FILTRO / ATUALIZAR / PAGINAÇÃO / MENUS
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
            foreach (Window w in Application.Current.Windows)
                if (w is RegraSenhaWindow) { w.Activate(); return; }

            new RegraSenhaWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private void MenuConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window w in Application.Current.Windows)
                if (w is ConfiguracoesWindow) { w.Activate(); return; }

            new ConfiguracoesWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }



        private async void AtivarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { MessageBox.Show("Selecione pelo menos um usuário."); return; }
            AtivarMFAEmMassa(sel);
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }

        private async void AtivarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus == "Não configurado").ToList();

            if (!todos.Any()) { MessageBox.Show("Nenhum usuário precisa de MFA."); return; }

            if (MessageBox.Show($"Ativar MFA para {todos.Count} usuário(s)?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            AtivarMFAEmMassa(todos);
            await BuscarUsuariosAsync(txtPesquisa.Text); // ← era AtualizarGrid()
        }




private static string ObterStatusMFA(string? valor)
{
    if (string.IsNullOrWhiteSpace(valor))
        return "Não configurado";

    if (valor.Equals("setup", StringComparison.OrdinalIgnoreCase))
        return "Pendente";

    if (valor.StartsWith("pending-app:", StringComparison.OrdinalIgnoreCase) ||
        valor.StartsWith("pending-sms:", StringComparison.OrdinalIgnoreCase) ||
        valor.StartsWith("pending:", StringComparison.OrdinalIgnoreCase))
        return "Pendente";

    if (valor.StartsWith("active-app:", StringComparison.OrdinalIgnoreCase) ||
        valor.StartsWith("active-sms:", StringComparison.OrdinalIgnoreCase) ||
        valor.StartsWith("active:", StringComparison.OrdinalIgnoreCase))
        return "Ativo";

    return "Não configurado";
}

        private List<UsuarioViewModel> BuscarUsuariosAD(string filtro)
        {
            var usuarios = new List<UsuarioViewModel>();

            try
            {
                using var root = ActiveDirectoryHelper.CriarConexaoAD();
                using var search = new DirectorySearcher(root)
                {
                    Filter = "(&(objectCategory=person)(objectClass=user))",
                    PageSize = 1000
                };

                search.PropertiesToLoad.Add("samAccountName");
                search.PropertiesToLoad.Add("displayName");
                search.PropertiesToLoad.Add("lastLogonTimestamp");
                search.PropertiesToLoad.Add("mail");
                search.PropertiesToLoad.Add("description");
                search.PropertiesToLoad.Add("info");

                foreach (SearchResult r in search.FindAll())
                {
                    string login = r.Properties["samAccountname"].Count > 0
                        ? r.Properties["samAccountname"][0].ToString()! : "";

                    string nome = r.Properties["displayname"].Count > 0
                        ? r.Properties["displayname"][0].ToString()! : login;

                    // ✅ tenta mail primeiro, depois description como fallback
                    string email = "";
                    if (r.Properties["mail"].Count > 0)
                        email = r.Properties["mail"][0].ToString()!;
                    else if (r.Properties["description"].Count > 0)
                        email = r.Properties["description"][0].ToString()!;

                    string data = "-";
                    if (r.Properties["lastlogontimestamp"].Count > 0)
                    {
                        long ticks = (long)r.Properties["lastlogontimestamp"][0];
                        data = DateTime.FromFileTimeUtc(ticks).ToString("dd/MM/yyyy HH:mm");
                    }

                    string mfaRaw = r.Properties["info"].Count > 0
                        ? r.Properties["info"][0].ToString()! : "";

                    if (!string.IsNullOrWhiteSpace(filtro) &&
                        !login.Contains(filtro, StringComparison.OrdinalIgnoreCase) &&
                        !nome.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                        continue;

                    usuarios.Add(new UsuarioViewModel
                    {
                        Tipo = "Domínio",
                        Login = login,
                        NomeCompleto = nome,
                        DataCadastro = data,
                        Email = email,
                        MFAStatus = ObterStatusMFA(mfaRaw)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao consultar Active Directory:\n" + ex.Message);
            }

            return usuarios;
        }

        private void AtivarMFAEmMassa(List<UsuarioViewModel> usuarios)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
                using var root = ActiveDirectoryHelper.CriarConexaoAD();

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = LdapHelper.Escape(LdapHelper.NormalizeLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null) continue;

                        using var entry = result.GetDirectoryEntry();
                        entry.Properties["info"].Value = "setup";
                        entry.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                MessageBox.Show($"MFA ativado para {usuarios.Count} usuário(s).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao ativar MFA: " + ex.Message);
            }
        }

        private void ResetarMFA(List<UsuarioViewModel> usuarios)
        {
            int ok = 0;
            int fail = 0;

            try
            {
                using var root = ActiveDirectoryHelper.CriarConexaoAD();

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = LdapHelper.Escape(LdapHelper.NormalizeLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null)
                        {
                            fail++;
                            continue;
                        }

                        using var entry = result.GetDirectoryEntry();
                        entry.Properties["info"].Value = "setup";
                        entry.CommitChanges();
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        fail++;
                    }
                }

                MessageBox.Show(
                    $"MFA resetado para {ok} usuário(s)." +
                    (fail > 0 ? $"\nFalha em {fail} usuário(s)." : ""));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao resetar MFA: " + ex.Message);
            }
        }

        private void RemoverMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
                using var root = ActiveDirectoryHelper.CriarConexaoAD();

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = LdapHelper.Escape(LdapHelper.NormalizeLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null) continue;

                        using var entry = result.GetDirectoryEntry();

                        if (entry.Properties.Contains("info")) // ← era extensionAttribute1
                            entry.Properties["info"].Clear();

                        entry.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                MessageBox.Show($"MFA removido de {usuarios.Count} usuário(s).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao remover MFA: " + ex.Message);
            }
        }

        private async void RemoverMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.");
                return;
            }

            RemoverMFA(sel);
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }

        private async void RemoverMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus != "Não configurado")
                .ToList();

            if (!todos.Any())
            {
                MessageBox.Show("Nenhum usuário possui MFA.");
                return;
            }

            if (MessageBox.Show($"Remover MFA de {todos.Count} usuário(s)?", "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            RemoverMFA(todos);
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }

        // ══════════════════════════════════════════════════════════════
        //  TROCAR SENHA NO PRÓXIMO LOGIN
        // ══════════════════════════════════════════════════════════════


        private bool EstaNoControladorDeDominio()
        {
            try
            {
                var domain = Domain.GetComputerDomain();
                string computerName = Environment.MachineName;

                return domain.DomainControllers
                    .Cast<DomainController>()
                    .Any(dc => dc.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                               dc.Name.StartsWith(computerName + ".", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private bool UsarCredencialAtualDoWindows()
        {
            return EstaNoControladorDeDominio() && UsuarioEhAdministradorWindows();
        }

        private bool UsuarioEhAdministradorWindows()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private PrincipalContext CriarContextoDominio()
        {
            string domFQDN = ConfigHelper.Get("ActiveDirectory:Domain");

            if (UsarCredencialAtualDoWindows())
            {
                return new PrincipalContext(ContextType.Domain, domFQDN);
            }

            string adUser = ConfigHelper.Get("ActiveDirectory:Usuario");
            string adSenha = ConfigHelper.Get("ActiveDirectory:Senha");

            return new PrincipalContext(ContextType.Domain, domFQDN, adUser, adSenha);
        }

        // ══════════════════════════════════════════════════════════════
        //  TROCAR SENHA NO PRÓXIMO LOGIN
        // ══════════════════════════════════════════════════════════════
        private void TrocarSenhaProximoLoginSelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();

            if (!sel.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.", "Atenção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AbrirModalTrocaSenha(sel);
        }

        private void TrocarSenhaProximoLoginTodos_Click(object sender, RoutedEventArgs e)
        {
            var adUsers = _usuariosAD
                .Where(u => !u.Tipo?.Equals("Local", StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            if (!adUsers.Any())
            {
                MessageBox.Show("Nenhum usuário de domínio encontrado.", "Atenção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AbrirModalTrocaSenha(adUsers);
        }

        private void AbrirModalTrocaSenha(List<UsuarioViewModel> usuarios)
        {
            var modal = new TrocarSenhaWindow(usuarios) { Owner = this };
            modal.ShowDialog();

            if (!modal.Confirmado)
                return;

            MostrarLoading();

            try
            {
                if (modal.SomenteForcarTrocaNoProximoLogin)
                    MarcarTrocaSenhaProximoLoginSemAlterarSenha(usuarios);
                else
                    ForcarTrocaSenha(usuarios, modal.SenhaGerada);
            }
            finally
            {
                OcultarLoading();
            }
        }

        private void MarcarTrocaSenhaProximoLoginSemAlterarSenha(List<UsuarioViewModel> usuarios)
        {
            int ok = 0, fail = 0;

            foreach (var user in usuarios)
            {
                try
                {
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
                        var login = LdapHelper.Escape(LdapHelper.NormalizeLogin(user.Login));

                        using var searcher = new DirectorySearcher(ActiveDirectoryHelper.CriarConexaoAD())
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    fail++;
                }
            }

            string msg = $"✅ {ok} usuário(s) marcados para trocar senha no próximo login.";
            if (fail > 0)
                msg += $"\nFalha em {fail} usuário(s).";

            MessageBox.Show(msg, "Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ForcarTrocaSenha(List<UsuarioViewModel> usuarios, string senhaNova)
        {
            int ok = 0, fail = 0;

            foreach (var user in usuarios)
            {
                try
                {
                    if (user.Tipo?.Equals("Local", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        using var ctx = new PrincipalContext(ContextType.Machine);
                        var u = UserPrincipal.FindByIdentity(ctx, user.Login);

                        if (u == null)
                        {
                            fail++;
                            continue;
                        }

                        u.SetPassword(senhaNova);
                        u.ExpirePasswordNow();
                        u.Save();
                        ok++;
                    }
                    else
                    {
                        var login = LdapHelper.Escape(LdapHelper.NormalizeLogin(user.Login));

                        using var ctx = CriarContextoDominio();
                        var u = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, login);

                        if (u == null)
                        {
                            fail++;
                            continue;
                        }

                        u.SetPassword(senhaNova);
                        u.Save();

                        using var searcher = new DirectorySearcher(ActiveDirectoryHelper.CriarConexaoAD())
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null)
                        {
                            fail++;
                            continue;
                        }

                        using var entry = result.GetDirectoryEntry();

                        entry.Properties["pwdLastSet"].Value = 0;

                        if (entry.Properties.Contains("info"))
                            entry.Properties["info"].Clear();

                        entry.CommitChanges();
                        ok++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    fail++;
                }
            }

            string msg = $"✅ {ok} usuário(s) configurados para trocar senha no próximo login.";
            if (fail > 0)
                msg += $"\nFalha em {fail} usuário(s).";

            MessageBox.Show(msg, "Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS INTERNOS
        // ══════════════════════════════════════════════════════════════
        private List<UsuarioViewModel> CombinarUsuarios()
        {
            var lista = new List<UsuarioViewModel>();
            if (chkUsuariosAD.IsChecked == true) lista.AddRange(_usuariosAD);
            if (chkUsuariosLocais.IsChecked == true) lista.AddRange(_usuariosLocais);
            return lista;
        }
        private async void ResetarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();

            if (!sel.Any())
            {
                MessageBox.Show("Selecione pelo menos um usuário.");
                return;
            }

            ResetarMFA(sel);
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }

        private async void ResetarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus != "Não configurado")
                .ToList();

            if (!todos.Any())
            {
                MessageBox.Show("Nenhum usuário possui MFA para resetar.");
                return;
            }

            if (MessageBox.Show($"Resetar MFA de {todos.Count} usuário(s)?",
                "Confirmação",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            ResetarMFA(todos);
            await BuscarUsuariosAsync(txtPesquisa.Text);
        }
    }
}