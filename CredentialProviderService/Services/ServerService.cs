using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CredentialProviderAPP.Models.Api;
using CredentialProviderAPP.Utils;
using CredentialProviderAPP.Helpers;
using System.Linq;
using CredentialProviderAPP.Models;
using CredentialProviderAPP.Services.Sms;
using CredentialProviderAPP.Models.Api;

namespace CredentialProviderAPP.Services
{
    public static class ServerService
    {
        public static void Start()
        {
            HttpListener listener = new HttpListener();

            string prefix = ConfigHelper.Get("ServerListener:Prefix");

            if (string.IsNullOrWhiteSpace(prefix))
                throw new InvalidOperationException("A configuração ServerListener:Prefix não foi definida.");

            listener.Prefixes.Add(prefix);
            listener.Start();

            while (true)
            {
                var ctx = listener.GetContext();
                _ = Task.Run(() => ProcessarRequisicao(ctx));
            }
        }

        private static async Task ProcessarRequisicao(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            try
            {
                res.ContentType = "application/json; charset=utf-8";

                string path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "";

                if (path == "/health")
                {
                    await ResponderJson(res, 200, new
                    {
                        Status = "ok",
                        Server = Environment.MachineName,
                        Time = DateTime.Now,
                        Version = typeof(ServerService).Assembly.GetName().Version?.ToString()
                    });
                    return;
                }

                if (path == "/mfa/status" && req.HttpMethod == "GET")
                {
                    string login = req.QueryString["login"] ?? "";
                    string clientMachine = req.QueryString["clientMachine"] ?? "";

                    if (string.IsNullOrWhiteSpace(login))
                    {
                        await ResponderJson(res, 400, new MfaStatusResponse
                        {
                            Sucesso = false,
                            Erro = "Login não informado."
                        });
                        return;
                    }

                    using var root = CriarConexaoAD();
                    var result = BuscarUsuarioNoAD(root, login);

                    if (result == null)
                    {
                        await ResponderJson(res, 404, new MfaStatusResponse
                        {
                            Sucesso = false,
                            Erro = "Usuário não encontrado no Active Directory."
                        });
                        return;
                    }

                    // 🔥 NOVO: verifica se pode pular MFA
                    // 🔥 verifica se pode pular MFA
                    if (PodePularMfa(result, clientMachine))
                    {
                        string infoTrusted = ObterPropriedade(result, "info");
                        string metodoTrusted = ExtrairMetodo(infoTrusted);

                        await ResponderJson(res, 200, new MfaStatusResponse
                        {
                            Sucesso = true,
                            Status = "Trusted",
                            Metodo = metodoTrusted
                        });
                        return;
                    }

                    string info = ObterPropriedade(result, "info");
                    string status = ObterStatusMfa(info);
                    string metodo = ExtrairMetodo(info);

                    await ResponderJson(res, 200, new MfaStatusResponse
                    {
                        Sucesso = true,
                        Status = status,
                        Metodo = metodo // novo
                    });
                    return;
                }

                if (path == "/mfa/setup" && req.HttpMethod == "GET")
                {
                    string login = req.QueryString["login"] ?? "";

                    try
                    {
                        File.AppendAllText(
                            @"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/setup iniciado. login={login}{Environment.NewLine}");

                        if (string.IsNullOrWhiteSpace(login))
                        {
                            await ResponderJson(res, 400, new MfaSetupResponse
                            {
                                Sucesso = false,
                                Erro = "Login não informado."
                            });
                            return;
                        }

                        using var root = CriarConexaoAD();
                        var result = BuscarUsuarioNoAD(root, login);

                        if (result == null)
                        {
                            File.AppendAllText(
                                @"C:\CredentialProvider\server_error.txt",
                                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/setup usuário não encontrado. login={login}{Environment.NewLine}");

                            await ResponderJson(res, 404, new MfaSetupResponse
                            {
                                Sucesso = false,
                                Erro = "Usuário não encontrado no Active Directory."
                            });
                            return;
                        }

                        string sam = ObterPropriedade(result, "samAccountName");
                        string nome = ObterPropriedade(result, "displayName");
                        string mail = ObterPropriedade(result, "mail");
                        string description = ObterPropriedade(result, "description");
                        string info = ObterPropriedade(result, "info");

                        if (string.IsNullOrWhiteSpace(nome))
                            nome = sam;

                        string email = !string.IsNullOrWhiteSpace(mail) ? mail : description;
                        string status = ObterStatusMfa(info);

                        File.AppendAllText(
                            @"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/setup usuário encontrado. sam={sam} nome={nome} email={email} status={status} info={info}{Environment.NewLine}");

                        if (status == "NotConfigured")
                        {
                            await ResponderJson(res, 400, new MfaSetupResponse
                            {
                                Sucesso = false,
                                Erro = "MFA não está pendente para este usuário."
                            });
                            return;
                        }

                        if (status == "Configured")
                        {
                            await ResponderJson(res, 200, new MfaSetupResponse
                            {
                                Sucesso = false,
                                Login = sam,
                                Nome = nome,
                                Email = email,
                                Erro = "MFA já configurado para este usuário."
                            });
                            return;
                        }

                        string secret;

                        using (var entry = CriarEntryComCredenciais(result.Path))
                        {
                            if (string.Equals(info, "setup", StringComparison.OrdinalIgnoreCase))
                            {
                                secret = GerarSecretBase32();

                                File.AppendAllText(
                                    @"C:\CredentialProvider\server_error.txt",
                                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] Gravando info com credencial configurada. sam={sam}{Environment.NewLine}");

                                entry.Properties["info"].Value = $"pending:{secret}";
                                entry.CommitChanges();
                            }
                            else if (info.StartsWith("pending:", StringComparison.OrdinalIgnoreCase))
                            {
                                secret = info.Substring("pending:".Length).Trim();
                            }
                            else
                            {
                                secret = GerarSecretBase32();

                                entry.Properties["info"].Value = $"pending:{secret}";
                                entry.CommitChanges();
                            }
                        }

                        string issuer = ConfigHelper.Get("Mfa:Issuer");
                        if (string.IsNullOrWhiteSpace(issuer))
                            issuer = "ASPEC";

                        string accountName = !string.IsNullOrWhiteSpace(email) ? email : sam;
                        string otpAuthUrl = MontarOtpAuthUrl(issuer, accountName, secret);

                        File.AppendAllText(
                            @"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/setup finalizado com sucesso. sam={sam} accountName={accountName} issuer={issuer}{Environment.NewLine}");

                        await ResponderJson(res, 200, new MfaSetupResponse
                        {
                            Sucesso = true,
                            Login = sam,
                            Nome = nome,
                            Email = email,
                            OtpAuthUrl = otpAuthUrl
                        });

                        return;
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(
                            @"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] ERRO /mfa/setup login={login}: {ex}{Environment.NewLine}");

                        await ResponderJson(res, 500, new MfaSetupResponse
                        {
                            Sucesso = false,
                            Erro = ex.Message
                        });

                        return;
                    }
                }

                // GET — busca o telefone mascarado
                if (path == "/mfa/telefone" && req.HttpMethod == "GET")
                {
                    string login = req.QueryString["login"] ?? "";

                    if (string.IsNullOrWhiteSpace(login))
                    {
                        await ResponderJson(res, 400, new { Sucesso = false, Erro = "Login não informado." });
                        return;
                    }

                    using var root = CriarConexaoAD();
                    var result = BuscarUsuarioNoAD(root, login);

                    if (result == null)
                    {
                        await ResponderJson(res, 404, new { Sucesso = false, Erro = "Usuário não encontrado." });
                        return;
                    }

                    string telefone = ObterPropriedade(result, "mobile");

                    if (string.IsNullOrWhiteSpace(telefone))
                    {
                        await ResponderJson(res, 200, new { Sucesso = true, TemTelefone = false, TelefoneMascarado = "" });
                        return;
                    }

                    // Remove +55 e mascara: 997****43
                    string numero = telefone.Replace("+55", "").Replace(" ", "").Trim();
                    string mascarado = numero.Length >= 6
                        ? numero[..3] + new string('*', numero.Length - 6) + numero[^3..]
                        : numero;

                    await ResponderJson(res, 200, new { Sucesso = true, TemTelefone = true, TelefoneMascarado = mascarado });
                    return;
                }

                // POST — salva o telefone no AD
                if (path == "/mfa/telefone" && req.HttpMethod == "POST")
                {
                    try
                    {
                        TelefoneRequest? data;

                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            File.AppendAllText(@"C:\CredentialProvider\server_error.txt",
                                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/telefone POST body={body}{Environment.NewLine}");
                            data = JsonSerializer.Deserialize<TelefoneRequest>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }

                        if (data == null || string.IsNullOrWhiteSpace(data.Login) || string.IsNullOrWhiteSpace(data.Telefone))
                        {
                            await ResponderJson(res, 400, new DefaultApiResponse { Sucesso = false, Erro = "Dados inválidos." });
                            return;
                        }

                        string telefoneFormatado = data.Telefone.Trim();
                        if (!telefoneFormatado.StartsWith("+"))
                            telefoneFormatado = "+55" + telefoneFormatado.TrimStart('0');

                        File.AppendAllText(@"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/telefone salvando. login={data.Login} telefone={telefoneFormatado}{Environment.NewLine}");

                        using var root = CriarConexaoAD();
                        var result = BuscarUsuarioNoAD(root, data.Login);

                        if (result == null)
                        {
                            await ResponderJson(res, 404, new DefaultApiResponse { Sucesso = false, Erro = "Usuário não encontrado." });
                            return;
                        }

                        using var entry = CriarEntryComCredenciais(result.Path);
                        entry.Properties["mobile"].Value = telefoneFormatado;
                        entry.CommitChanges();

                        File.AppendAllText(@"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] /mfa/telefone salvo com sucesso.{Environment.NewLine}");

                        await ResponderJson(res, 200, new DefaultApiResponse { Sucesso = true });
                        return;
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(@"C:\CredentialProvider\server_error.txt",
                            $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] ERRO /mfa/telefone POST: {ex}{Environment.NewLine}");

                        await ResponderJson(res, 500, new DefaultApiResponse { Sucesso = false, Erro = ex.Message });
                        return;
                    }
                }

                if (path == "/mfa/validate" && req.HttpMethod == "POST")
                {
                    ValidateMfaRequest? data;

                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        string body = await reader.ReadToEndAsync();
                        data = JsonSerializer.Deserialize<ValidateMfaRequest>(body,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }

                    if (data == null || string.IsNullOrWhiteSpace(data.Login) || string.IsNullOrWhiteSpace(data.Codigo))
                    {
                        await ResponderJson(res, 400, new ValidateMfaResponse
                        {
                            Sucesso = false,
                            Erro = "Dados inválidos."
                        });
                        return;
                    }

                    try
                    {
                        using var root = CriarConexaoAD();
                        var result = BuscarUsuarioNoAD(root, data.Login);

                        if (result == null)
                        {
                            await ResponderJson(res, 404, new ValidateMfaResponse
                            {
                                Sucesso = false,
                                Erro = "Usuário não encontrado no Active Directory."
                            });
                            return;
                        }

                        string info = ObterPropriedade(result, "info");

                        if (string.IsNullOrWhiteSpace(info))
                        {
                            await ResponderJson(res, 200, new ValidateMfaResponse
                            {
                                Sucesso = true,
                                Valido = false,
                                Erro = "MFA não configurado."
                            });
                            return;
                        }

                        string? secret = ExtrairSecret(info);

                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            await ResponderJson(res, 200, new ValidateMfaResponse
                            {
                                Sucesso = true,
                                Valido = false,
                                Erro = "MFA ainda não possui secret válido."
                            });
                            return;
                        }

                        bool isSms = data.Metodo?.Equals("sms", StringComparison.OrdinalIgnoreCase) == true;

                        if (isSms)
                        {
                            bool validoSms = SmsMfaService.ValidarCodigo(data.Login, data.Codigo);

                            if (!validoSms)
                            {
                                await ResponderJson(res, 200, new ValidateMfaResponse
                                {
                                    Sucesso = true,
                                    Valido = false
                                });
                                return;
                            }

                            if (info.StartsWith("pending:", StringComparison.OrdinalIgnoreCase) ||
                                info.StartsWith("pending-sms:", StringComparison.OrdinalIgnoreCase))
                            {
                                using var entry = CriarEntryComCredenciais(result.Path);
                                entry.Properties["info"].Value = $"active-sms:{secret}";
                                entry.CommitChanges();
                            }

                            SalvarMaquinaConfiavel(result, data.Login, data.ClientMachine, "sms");

                            await ResponderJson(res, 200, new ValidateMfaResponse
                            {
                                Sucesso = true,
                                Valido = true
                            });
                            return;
                        }

                        bool valido = ValidarTotp(secret, data.Codigo);

                        if (!valido)
                        {
                            await ResponderJson(res, 200, new ValidateMfaResponse
                            {
                                Sucesso = true,
                                Valido = false
                            });
                            return;
                        }

                        if (info.StartsWith("pending:", StringComparison.OrdinalIgnoreCase) ||
                            info.StartsWith("pending-app:", StringComparison.OrdinalIgnoreCase))
                        {
                            using var entry = CriarEntryComCredenciais(result.Path);
                            entry.Properties["info"].Value = $"active-app:{secret}";
                            entry.CommitChanges();
                        }

                        SalvarMaquinaConfiavel(result, data.Login, data.ClientMachine, "app");

                        await ResponderJson(res, 200, new ValidateMfaResponse
                        {
                            Sucesso = true,
                            Valido = true
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        File.WriteAllText(@"C:\CredentialProvider\erro.txt", ex.ToString());

                        await ResponderJson(res, 500, new ValidateMfaResponse
                        {
                            Sucesso = false,
                            Erro = ex.Message
                        });
                        return;
                    }
                }

                if (path == "/password/policy" && req.HttpMethod == "GET")
                {
                    var policy = PasswordPolicyFileHelper.Load();

                    if (policy == null)
                    {
                        await ResponderJson(res, 404, new
                        {
                            Sucesso = false,
                            Erro = "Política de senha não cadastrada."
                        });
                        return;
                    }

                    await ResponderJson(res, 200, new
                    {
                        Sucesso = true,
                        MinLength = policy.MinLength,
                        MinSpecialChars = policy.MinSpecialChars,
                        AllowedSpecialChars = policy.AllowedSpecialChars,
                        RequireUppercase = policy.RequireUppercase,
                        RequireLowercase = policy.RequireLowercase,
                        RequireNumber = policy.RequireNumber
                    });
                    return;
                }

                if (path == "/mfa/sms/send" && req.HttpMethod == "POST")
                {
                    try
                    {
                        SmsSendRequest? data;

                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            data = JsonSerializer.Deserialize<SmsSendRequest>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }

                        if (data == null || string.IsNullOrWhiteSpace(data.Login))
                        {
                            await ResponderJson(res, 400, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Login não informado."
                            });
                            return;
                        }

                        // 🔹 VERIFICA SE JÁ EXISTE CÓDIGO VÁLIDO
                        var (podeEnviar, _) = SmsMfaService.VerificarReenvio(data.Login);

                        if (!podeEnviar)
                        {
                            await ResponderJson(res, 200, new
                            {
                                Sucesso = true,
                                CodigoDisponivel = true,
                                EnviadoAgora = false
                            });
                            return;
                        }

                        using var root = CriarConexaoAD();
                        var result = BuscarUsuarioNoAD(root, data.Login);

                        if (result == null)
                        {
                            await ResponderJson(res, 404, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Usuário não encontrado."
                            });
                            return;
                        }

                        string telefone = ObterPropriedade(result, "mobile");

                        if (string.IsNullOrWhiteSpace(telefone))
                            telefone = "SEM_TELEFONE_CADASTRADO";

                        // 🔹 ENVIA NOVO SMS
                        await SmsMfaService.EnviarCodigoAsync(data.Login, telefone);

                        await ResponderJson(res, 200, new
                        {
                            Sucesso = true,
                            CodigoDisponivel = true,
                            EnviadoAgora = true
                        });

                        return;
                    }
                    catch (Exception ex)
                    {
                        await ResponderJson(res, 500, new DefaultApiResponse
                        {
                            Sucesso = false,
                            Erro = "Erro ao enviar SMS: " + ex.Message
                        });
                        return;
                    }
                }

                if (path == "/password/change" && req.HttpMethod == "POST")
                {
                    try
                    {
                        ChangePasswordRequest? data;

                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            data = JsonSerializer.Deserialize<ChangePasswordRequest>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }

                        if (data == null || string.IsNullOrWhiteSpace(data.Login) || string.IsNullOrWhiteSpace(data.NovaSenha))
                        {
                            await ResponderJson(res, 400, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Dados inválidos."
                            });
                            return;
                        }

                        var policy = PasswordPolicyFileHelper.Load();

                        if (policy == null)
                        {
                            await ResponderJson(res, 404, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Política de senha não cadastrada."
                            });
                            return;
                        }

                        string? erroValidacao = ValidarSenhaPolitica(data.NovaSenha, policy);

                        if (!string.IsNullOrWhiteSpace(erroValidacao))
                        {
                            await ResponderJson(res, 200, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = erroValidacao
                            });
                            return;
                        }

                        using var root = CriarConexaoAD();
                        var result = BuscarUsuarioNoAD(root, data.Login);

                        if (result == null)
                        {
                            await ResponderJson(res, 404, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Usuário não encontrado no Active Directory."
                            });
                            return;
                        }

                        string sam = ObterPropriedade(result, "samAccountName");

                        using var domainContext = ActiveDirectoryHelper.CriarContextoDominio();
                        var user = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(
                            domainContext,
                            System.DirectoryServices.AccountManagement.IdentityType.SamAccountName,
                            sam
                        );

                        if (user == null)
                        {
                            await ResponderJson(res, 404, new DefaultApiResponse
                            {
                                Sucesso = false,
                                Erro = "Usuário não encontrado no contexto do domínio."
                            });
                            return;
                        }

                        user.SetPassword(data.NovaSenha);
                        user.Save();

                        await ResponderJson(res, 200, new DefaultApiResponse
                        {
                            Sucesso = true
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        await ResponderJson(res, 500, new DefaultApiResponse
                        {
                            Sucesso = false,
                            Erro = "Erro ao alterar senha: " + ex.Message
                        });
                        return;
                    }
                }

                // GET — verifica se pode reenviar
                if (path == "/mfa/sms/status" && req.HttpMethod == "GET")
                {
                    string login = req.QueryString["login"] ?? "";
                    if (string.IsNullOrWhiteSpace(login))
                    {
                        await ResponderJson(res, 400, new { Sucesso = false, Erro = "Login não informado." });
                        return;
                    }

                    var (podeEnviar, _) = SmsMfaService.VerificarReenvio(login);

                    await ResponderJson(res, 200, new
                    {
                        Sucesso = true,
                        CodigoDisponivel = !podeEnviar,
                        PodeEnviar = podeEnviar
                    });
                    return;
                }

                if (path == "/password/blacklist" && req.HttpMethod == "GET")
                {
                    try
                    {
                        string blacklistPath = ConfigHelper.Get("PasswordPolicy:BlacklistPath");

                        if (string.IsNullOrWhiteSpace(blacklistPath))
                        {
                            await ResponderJson(res, 404, new PasswordBlacklistResponse
                            {
                                Sucesso = false,
                                Erro = "Caminho da blacklist não configurado."
                            });
                            return;
                        }

                        if (!File.Exists(blacklistPath))
                        {
                            await ResponderJson(res, 200, new PasswordBlacklistResponse
                            {
                                Sucesso = true,
                                Palavras = new List<string>()
                            });
                            return;
                        }

                        var palavras = File.ReadAllLines(blacklistPath)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        await ResponderJson(res, 200, new PasswordBlacklistResponse
                        {
                            Sucesso = true,
                            Palavras = palavras
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        await ResponderJson(res, 500, new PasswordBlacklistResponse
                        {
                            Sucesso = false,
                            Erro = "Erro ao carregar blacklist: " + ex.Message
                        });
                        return;
                    }
                }

                if (path == "/password/validate-candidate" && req.HttpMethod == "POST")
                {
                    try
                    {
                        ChangePasswordRequest? data;

                        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            data = JsonSerializer.Deserialize<ChangePasswordRequest>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }

                        if (data == null || string.IsNullOrWhiteSpace(data.Login) || string.IsNullOrWhiteSpace(data.NovaSenha))
                        {
                            await ResponderJson(res, 400, new ValidatePasswordResponse
                            {
                                Sucesso = false,
                                Valida = false,
                                Erro = "Dados inválidos."
                            });
                            return;
                        }

                        var policy = PasswordPolicyFileHelper.Load();

                        if (policy == null)
                        {
                            await ResponderJson(res, 404, new ValidatePasswordResponse
                            {
                                Sucesso = false,
                                Valida = false,
                                Erro = "Política de senha não cadastrada."
                            });
                            return;
                        }

                        string? erroValidacao = ValidarSenhaPolitica(data.NovaSenha, policy);

                        await ResponderJson(res, 200, new ValidatePasswordResponse
                        {
                            Sucesso = true,
                            Valida = string.IsNullOrWhiteSpace(erroValidacao),
                            Erro = erroValidacao
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        await ResponderJson(res, 500, new ValidatePasswordResponse
                        {
                            Sucesso = false,
                            Valida = false,
                            Erro = "Erro ao validar senha: " + ex.Message
                        });
                        return;
                    }
                }

                await ResponderJson(res, 404, new
                {
                    Sucesso = false,
                    Erro = "Rota não encontrada."
                });
            }
            catch (Exception ex)
            {
                await ResponderJson(res, 500, new
                {
                    Sucesso = false,
                    Erro = ex.Message
                });
            }
            finally
            {
                res.OutputStream.Close();
            }
        }

        private static string? ObterPalavraProibida(string senha)
        {
            string path = ConfigHelper.Get("PasswordPolicy:BlacklistPath");

            if (!File.Exists(path))
                return null;

            foreach (var linha in File.ReadLines(path))
            {
                string palavra = linha.Trim();

                if (string.IsNullOrWhiteSpace(palavra))
                    continue;

                if (senha.Contains(palavra, StringComparison.OrdinalIgnoreCase))
                    return palavra;
            }

            return null;
        }

        private static DirectoryEntry CriarConexaoAD()
        {
            string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");
            string usuario = ConfigHelper.Get("ActiveDirectory:Usuario");
            string senha = ConfigHelper.Get("ActiveDirectory:Senha");

            if (string.IsNullOrWhiteSpace(ldap))
                throw new InvalidOperationException("A configuração ActiveDirectory:LDAP não foi definida.");

            return new DirectoryEntry(ldap, usuario, senha, AuthenticationTypes.Secure);
        }

        private static SearchResult? BuscarUsuarioNoAD(DirectoryEntry root, string entrada)
        {
            if (root == null || string.IsNullOrWhiteSpace(entrada))
                return null;

            string valorOriginal = entrada.Trim();
            string valorNormalizado = LdapHelper.NormalizeLogin(valorOriginal);

            string valorOriginalEscapado = LdapHelper.Escape(valorOriginal);
            string valorNormalizadoEscapado = LdapHelper.Escape(valorNormalizado);

            static DirectorySearcher CriarSearcher(DirectoryEntry rootEntry, string filtro)
            {
                var s = new DirectorySearcher(rootEntry)
                {
                    Filter = filtro,
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1
                };

                s.PropertiesToLoad.Add("samAccountName");
                s.PropertiesToLoad.Add("displayName");
                s.PropertiesToLoad.Add("mail");
                s.PropertiesToLoad.Add("description");
                s.PropertiesToLoad.Add("info");
                s.PropertiesToLoad.Add("mobile"); // novo

                return s;
            }

            using (var s1 = CriarSearcher(
                root,
                $"(&(objectCategory=person)(objectClass=user)(samAccountName={valorNormalizadoEscapado}))"))
            {
                var r1 = s1.FindOne();
                if (r1 != null)
                    return r1;
            }

            using (var s2 = CriarSearcher(
                root,
                $"(&(objectCategory=person)(objectClass=user)(samAccountName={valorOriginalEscapado}))"))
            {
                var r2 = s2.FindOne();
                if (r2 != null)
                    return r2;
            }

            using (var s3 = CriarSearcher(
                root,
                $"(&(objectCategory=person)(objectClass=user)(mail={valorOriginalEscapado}))"))
            {
                var r3 = s3.FindOne();
                if (r3 != null)
                    return r3;
            }

            using (var s4 = CriarSearcher(
                root,
                $"(&(objectCategory=person)(objectClass=user)(description={valorOriginalEscapado}))"))
            {
                var r4 = s4.FindOne();
                if (r4 != null)
                    return r4;
            }

            if (valorOriginal.Contains('@'))
            {
                string antesDoArroba = valorOriginal.Split('@')[0].Trim();
                string antesDoArrobaEscapado = LdapHelper.Escape(antesDoArroba);

                using var s5 = CriarSearcher(
                    root,
                    $"(&(objectCategory=person)(objectClass=user)(samAccountName={antesDoArrobaEscapado}))");

                var r5 = s5.FindOne();
                if (r5 != null)
                    return r5;
            }

            return null;
        }

        private static string ObterPropriedade(SearchResult result, string nome)
        {
            if (result.Properties.Contains(nome) && result.Properties[nome].Count > 0)
                return result.Properties[nome][0]?.ToString() ?? "";

            return "";
        }

        private static string ObterStatusMfa(string? info)
        {
            if (string.IsNullOrWhiteSpace(info))
                return "NotConfigured";

            if (string.Equals(info, "setup", StringComparison.OrdinalIgnoreCase))
                return "Pending";

            if (info.StartsWith("pending-app:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("pending-sms:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("pending:", StringComparison.OrdinalIgnoreCase)) // retrocompat
                return "Pending";

            if (info.StartsWith("active-app:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("active-sms:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("active:", StringComparison.OrdinalIgnoreCase)) // retrocompat
                return "Configured";

            return "Configured";
        }

        private static string? ExtrairSecret(string info)
        {
            if (string.IsNullOrWhiteSpace(info))
                return null;

            string valor;

            if (info.StartsWith("pending-app:", StringComparison.OrdinalIgnoreCase))
                valor = info["pending-app:".Length..].Trim();
            else if (info.StartsWith("pending-sms:", StringComparison.OrdinalIgnoreCase))
                valor = info["pending-sms:".Length..].Trim();
            else if (info.StartsWith("active-app:", StringComparison.OrdinalIgnoreCase))
                valor = info["active-app:".Length..].Trim();
            else if (info.StartsWith("active-sms:", StringComparison.OrdinalIgnoreCase))
                valor = info["active-sms:".Length..].Trim();
            else if (info.StartsWith("pending:", StringComparison.OrdinalIgnoreCase))
                valor = info["pending:".Length..].Trim();
            else if (info.StartsWith("active:", StringComparison.OrdinalIgnoreCase))
                valor = info["active:".Length..].Trim();
            else if (string.Equals(info, "setup", StringComparison.OrdinalIgnoreCase))
                return null;
            else
                valor = info.Trim();

            int pipe = valor.IndexOf('|');
            if (pipe >= 0)
                valor = valor[..pipe].Trim();

            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }

        private static string ExtrairMetodo(string info)
        {
            if (string.IsNullOrWhiteSpace(info))
                return "app";

            if (info.StartsWith("pending-sms:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("active-sms:", StringComparison.OrdinalIgnoreCase))
                return "sms";

            if (info.StartsWith("pending-app:", StringComparison.OrdinalIgnoreCase) ||
                info.StartsWith("active-app:", StringComparison.OrdinalIgnoreCase))
                return "app";

            // retrocompatibilidade com formato antigo
            if (info.Contains("-sms:", StringComparison.OrdinalIgnoreCase))
                return "sms";

            return "app";
        }

        private static string GerarSecretBase32(int numBytes = 20)
        {
            byte[] bytes = new byte[numBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base32Encode(bytes);
        }

        private static string MontarOtpAuthUrl(string issuer, string accountName, string secret)
        {
            return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
                   $"?secret={Uri.EscapeDataString(secret)}&issuer={Uri.EscapeDataString(issuer)}";
        }

        private static bool ValidarTotp(string secretBase32, string codigo, int janela = 1)
        {
            if (string.IsNullOrWhiteSpace(secretBase32) ||
                string.IsNullOrWhiteSpace(codigo) ||
                codigo.Length != 6)
                return false;

            byte[] key = Base32Decode(secretBase32);
            long timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

            for (long offset = -janela; offset <= janela; offset++)
            {
                string esperado = GerarTotp(key, timestep + offset);
                if (string.Equals(esperado, codigo, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string GerarTotp(byte[] key, long timestepNumber)
        {
            byte[] timestepBytes = BitConverter.GetBytes(timestepNumber);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(timestepBytes);

            using var hmac = new HMACSHA1(key);
            byte[] hash = hmac.ComputeHash(timestepBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode =
                ((hash[offset] & 0x7F) << 24) |
                ((hash[offset + 1] & 0xFF) << 16) |
                ((hash[offset + 2] & 0xFF) << 8) |
                (hash[offset + 3] & 0xFF);

            int otp = binaryCode % 1_000_000;
            return otp.ToString("D6");
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

            if (data == null || data.Length == 0)
                return string.Empty;

            StringBuilder result = new StringBuilder((data.Length + 4) / 5 * 8);

            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;

            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= data[next++] & 0xFF;
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                result.Append(alphabet[index]);
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<byte>();

            string working = input.Trim().TrimEnd('=').ToUpperInvariant();

            List<byte> output = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (char c in working)
            {
                int index = alphabet.IndexOf(c);
                if (index < 0)
                    throw new FormatException("Secret Base32 inválido.");

                buffer <<= 5;
                buffer |= index & 0x1F;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                    bitsLeft -= 8;
                }
            }

            return output.ToArray();
        }

        private static DirectoryEntry CriarEntryComCredenciais(string ldapPath)
        {
            string usuario = ConfigHelper.Get("ActiveDirectory:Usuario");
            string senha = ConfigHelper.Get("ActiveDirectory:Senha");

            return new DirectoryEntry(ldapPath, usuario, senha, AuthenticationTypes.Secure);
        }

        private static string? ValidarSenhaPolitica(string senha, PasswordPolicyConfig policy)
        {
            var erros = new List<string>();

            if (string.IsNullOrWhiteSpace(senha))
                erros.Add("Senha não informada.");

            if (senha.Length < policy.MinLength)
                erros.Add($"A senha deve ter no mínimo {policy.MinLength} caracteres.");

            if (policy.RequireUppercase && !senha.Any(char.IsUpper))
                erros.Add("A senha deve conter ao menos uma letra maiúscula.");

            if (policy.RequireLowercase && !senha.Any(char.IsLower))
                erros.Add("A senha deve conter ao menos uma letra minúscula.");

            if (policy.RequireNumber && !senha.Any(char.IsDigit))
                erros.Add("A senha deve conter ao menos um número.");

            string allowedSpecialChars = policy.AllowedSpecialChars ?? string.Empty;

            var invalidSpecialChars = senha
                .Where(c => !char.IsLetterOrDigit(c) && !allowedSpecialChars.Contains(c))
                .Distinct()
                .ToArray();

            if (invalidSpecialChars.Length > 0)
                erros.Add($"Caracteres não podem ser usados: {string.Join(" ", invalidSpecialChars)}");

            int specialCount = senha.Count(c => allowedSpecialChars.Contains(c));

            if (specialCount < policy.MinSpecialChars)
                erros.Add($"A senha deve conter ao menos {policy.MinSpecialChars} caractere(s) especial(is) permitido(s).");

            string? palavraProibida = ObterPalavraProibida(senha);
            if (palavraProibida != null)
                erros.Add($"A senha contém uma palavra proibida: {palavraProibida}");

            if (erros.Count == 0)
                return null;

            return string.Join(Environment.NewLine, erros);
        }

        private static async Task ResponderJson(HttpListenerResponse res, int statusCode, object payload)
        {
            res.StatusCode = statusCode;
            string json = JsonSerializer.Serialize(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static void SalvarMaquinaConfiavel(SearchResult result, string login, string? clientMachine, string? metodoForcado = null)
        {
            string maquina = string.IsNullOrWhiteSpace(clientMachine)
                ? "UNKNOWN"
                : clientMachine.Trim();

            string servidor = Environment.MachineName;

            int dias = 1;
            if (int.TryParse(ConfigHelper.Get("Mfa:TrustedMachineDays"), out int d))
                dias = d;

            string expira = DateTime.UtcNow.AddDays(dias).ToString("o");

            using var entry = CriarEntryComCredenciais(result.Path);

            string infoAtual = entry.Properties["info"].Value?.ToString() ?? "";
            string? secret = ExtrairSecret(infoAtual);

            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Não foi possível salvar máquina confiável: secret MFA não encontrado.");

            string metodo = !string.IsNullOrWhiteSpace(metodoForcado)
                ? metodoForcado.Trim().ToLowerInvariant()
                : ExtrairMetodo(infoAtual);

            string prefixo = metodo == "sms"
                ? "active-sms:"
                : "active-app:";

            entry.Properties["info"].Value = $"{prefixo}{secret}|{maquina}|{servidor}|{expira}";
            entry.CommitChanges();
        }

        private static bool PodePularMfa(SearchResult result, string? clientMachine)
        {
            string info = ObterPropriedade(result, "info");

            if (string.IsNullOrWhiteSpace(info))
                return false;

            var partes = info.Split('|');

            if (partes.Length < 4)
                return false;

            string maquina = partes[^3];
            string servidor = partes[^2];
            string expiraStr = partes[^1];

            if (!DateTime.TryParse(expiraStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expira))
                return false;

            if (expira < DateTime.UtcNow)
                return false;

            string maquinaAtual = string.IsNullOrWhiteSpace(clientMachine)
                ? "UNKNOWN"
                : clientMachine.Trim();

            string servidorAtual = Environment.MachineName;

            return maquina == maquinaAtual && servidor == servidorAtual;
        }
    }
}