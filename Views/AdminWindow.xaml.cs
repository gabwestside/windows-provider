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

                    ModernMessageBox.Show(
                        "Este computador não está conectado a um domínio.\nSerão exibidos apenas usuários locais.",
                        "Aviso",
                        ModernMessageBox.Kind.Info);

                    _usuariosLocais = ObterUsuariosLocais();
                    AtualizarGrid();
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao iniciar:\n" + ex.Message);
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
        //  BUSCA AD — MFA lido diretamente do extensionAttribute1
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
                search.PropertiesToLoad.Add("mail");
                search.PropertiesToLoad.Add("extensionAttribute1"); // 👈 MFA

                foreach (SearchResult r in search.FindAll())
                {
                    string? login = r.Properties["samAccountname"].Count > 0
                        ? r.Properties["samAccountname"][0].ToString()
                        : "";

                    string? nome = r.Properties["displayname"].Count > 0
                        ? r.Properties["displayname"][0].ToString()
                        : login;

                    string? email = r.Properties["mail"].Count > 0
                        ? r.Properties["mail"][0].ToString()
                        : "";

                    string data = "-";
                    if (r.Properties["lastlogontimestamp"].Count > 0)
                    {
                        long ticks = (long)r.Properties["lastlogontimestamp"][0];
                        data = DateTime.FromFileTimeUtc(ticks).ToString("dd/MM/yyyy HH:mm");
                    }

                    // 🔐 MFA vindo direto do AD
                    string? mfaRaw = r.Properties["extensionattribute1"].Count > 0
                        ? r.Properties["extensionattribute1"][0].ToString()
                        : "";

                    if (!string.IsNullOrWhiteSpace(filtro) &&
                        !login!.Contains(filtro, StringComparison.OrdinalIgnoreCase) &&
                        !nome!.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                        continue;

                    usuarios.Add(new UsuarioViewModel
                    {
                        Tipo = "Domínio",
                        Login = login!,
                        NomeCompleto = nome!,
                        DataCadastro = data,
                        Email = email!,
                        MFAStatus = ObterStatusMFA(mfaRaw!)
                    });
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao consultar Active Directory:\n" + ex.Message);
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
                            DataCadastro = u.LastLogon?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                            Email = "",
                            MFAStatus = "Não configurado"
                        });
                }
            }
            catch { ModernMessageBox.Show("Erro ao buscar usuários locais."); }

            return usuarios.OrderBy(x => x.NomeCompleto).ThenBy(x => x.Login).ToList();
        }

        // ══════════════════════════════════════════════════════════════
        //  ATUALIZAR GRID (paginação + ordenação)
        //  MFAStatus já vem preenchido pelo BuscarUsuariosAD / ObterUsuariosLocais
        // ══════════════════════════════════════════════════════════════
        private void AtualizarGrid()
        {
            if (dgUsuarios == null) return;

            var todos = new List<UsuarioViewModel>();
            string filtro = txtPesquisa.Text?.Trim()!;

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

            // MFAStatus já vem preenchido — não precisa recalcular aqui

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

            if (_computadorEmDominio) await BuscarUsuariosAsync(txtPesquisa.Text);
            else AtualizarGrid();

            ModernMessageBox.Show("Lista atualizada.");
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
            {
                if (w is RegraSenhaWindow)
                {
                    w.Activate();
                    return;
                }
            }

            var win = new RegraSenhaWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            win.ShowDialog();
        }

        private void MenuConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w is ConfiguracoesWindow)
                {
                    w.Activate();
                    return;
                }
            }

            var win = new ConfiguracoesWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            win.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════════
        //  MFA — STATUS (lido do extensionAttribute1 do AD)
        //  Vazio          → "Não configurado"
        //  "setup"        → "Pendente"
        //  qualquer outro → "Ativo"
        // ══════════════════════════════════════════════════════════════
        private string ObterStatusMFA(string valorAD)
        {
            if (string.IsNullOrWhiteSpace(valorAD))
                return "Não configurado";

            if (valorAD.Equals("setup", StringComparison.OrdinalIgnoreCase))
                return "Pendente";

            return "Ativo";
        }


        private void AtivarMFAEmMassa(List<UsuarioViewModel> usuarios)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                using var root = new DirectoryEntry(ldap);

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = EscapeLdap(NormalizarLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null) continue;

                        using var entry = result.GetDirectoryEntry();

                        entry.Properties["extensionAttribute1"].Value = "setup";
                        entry.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                ModernMessageBox.Show($"MFA ativado para {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao ativar MFA: " + ex.Message);
            }
        }
        private static string EscapeLdap(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            return input
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29");
        }
        private void AtivarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { ModernMessageBox.Show("Selecione pelo menos um usuário."); return; }
            AtivarMFAEmMassa(sel);
            AtualizarGrid();
        }

        private void AtivarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus == "Não configurado").ToList();

            if (!todos.Any()) { ModernMessageBox.Show("Nenhum usuário precisa de MFA."); return; }

            if (ModernMessageBox.ShowYesNo($"Ativar MFA para {todos.Count} usuários?", "Confirmação", ModernMessageBox.Kind.Warning) != MessageBoxResult.Yes) return;

            AtivarMFAEmMassa(todos);
            AtualizarGrid();
        }

        private void ResetarMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                using var root = new DirectoryEntry(ldap);

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = EscapeLdap(NormalizarLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null) continue;

                        using var entry = result.GetDirectoryEntry();

                        entry.Properties["extensionAttribute1"].Value = "setup";
                        entry.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                ModernMessageBox.Show($"MFA resetado para {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao resetar MFA: " + ex.Message);
            }
        }

        private void ResetarMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { ModernMessageBox.Show("Selecione pelo menos um usuário."); return; }

            if (ModernMessageBox.ShowYesNo($"Resetar MFA para {sel.Count} usuários?", "Confirmação", ModernMessageBox.Kind.Warning) != MessageBoxResult.Yes) return;

            ResetarMFA(sel);
            AtualizarGrid();
        }

        private void ResetarMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus != "Não configurado").ToList();

            if (!todos.Any()) { ModernMessageBox.Show("Nenhum usuário possui MFA."); return; }

            if (ModernMessageBox.ShowYesNo($"Resetar MFA para {todos.Count} usuários?", "Confirmação", ModernMessageBox.Kind.Warning) != MessageBoxResult.Yes) return;

            ResetarMFA(todos);
            AtualizarGrid();
        }

        private void RemoverMFA(List<UsuarioViewModel> usuarios)
        {
            try
            {
                string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                using var root = new DirectoryEntry(ldap);

                foreach (var user in usuarios)
                {
                    try
                    {
                        var login = EscapeLdap(NormalizarLogin(user.Login));

                        using var searcher = new DirectorySearcher(root)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null) continue;

                        using var entry = result.GetDirectoryEntry();

                        entry.Properties["extensionAttribute1"].Clear();
                        entry.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                ModernMessageBox.Show($"MFA removido de {usuarios.Count} usuários.");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Erro ao remover MFA: " + ex.Message);
            }
        }
        private void RemoverMFASelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();
            if (!sel.Any()) { ModernMessageBox.Show("Selecione pelo menos um usuário."); return; }
            RemoverMFA(sel);
            AtualizarGrid();
        }

        private void RemoverMFATodos_Click(object sender, RoutedEventArgs e)
        {
            var todos = CombinarUsuarios()
                .Where(u => u.MFAStatus != "Não configurado").ToList();

            if (!todos.Any()) { ModernMessageBox.Show("Nenhum usuário possui MFA."); return; }

            if (ModernMessageBox.ShowYesNo($"Remover MFA de {todos.Count} usuários?", "Confirmação", ModernMessageBox.Kind.Warning) != MessageBoxResult.Yes) return;

            RemoverMFA(todos);
            AtualizarGrid();
        }

        // ══════════════════════════════════════════════════════════════
        //  TROCAR SENHA NO PRÓXIMO LOGIN
        // ══════════════════════════════════════════════════════════════
        private void TrocarSenhaProximoLoginSelecionados_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgUsuarios.SelectedItems.Cast<UsuarioViewModel>().ToList();

            if (!sel.Any())
            {
                ModernMessageBox.Show("Selecione pelo menos um usuário.", "Atenção", ModernMessageBox.Kind.Warning);
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
                ModernMessageBox.Show("Nenhum usuário de domínio encontrado.", "Atenção", ModernMessageBox.Kind.Warning);
                return;
            }

            AbrirModalTrocaSenha(adUsers);
        }

        private void AbrirModalTrocaSenha(List<UsuarioViewModel> usuarios)
        {
            var modal = new TrocarSenhaWindow(usuarios) { Owner = this };
            modal.ShowDialog();

            if (!modal.Confirmado) return;

            MostrarLoading();
            try
            {
                ForcarTrocaSenha(usuarios);
            }
            finally
            {
                OcultarLoading();
            }
        }

        private void ForcarTrocaSenha(List<UsuarioViewModel> usuarios)
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

                        if (u == null) { fail++; continue; }

                        u.ExpirePasswordNow();
                        u.Save();
                        ok++;
                    }
                    else
                    {
                        string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

                        var login = EscapeLdap(NormalizarLogin(user.Login));

                        using var searcher = new DirectorySearcher(new DirectoryEntry(ldap))
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={login}))"
                        };
                        searcher.PropertiesToLoad.Add("distinguishedName");

                        var result = searcher.FindOne();
                        if (result == null) { fail++; continue; }

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

            string msg = $"✅ {ok} usuário(s) configurados para trocar senha no próximo login.";
            if (fail > 0)
                msg += $"\n⚠️ {fail} usuário(s) não puderam ser processados.";

            ModernMessageBox.Show(msg, "Trocar Senha no Próximo Login", ModernMessageBox.Kind.Info);
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

        private static string NormalizarLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return "";

            if (login.Contains("\\"))
                login = login.Split('\\')[1];

            if (login.Contains("@"))
                login = login.Split('@')[0];

            return login.Trim();
        }
    }
}