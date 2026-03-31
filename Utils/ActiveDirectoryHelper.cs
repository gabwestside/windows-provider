using CredentialProviderAPP.Config;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Security.Principal;

namespace CredentialProviderAPP.Helpers;

public static class ActiveDirectoryHelper
{
    public static DirectoryEntry CriarConexaoAD()
    {
        string ldap = ConfigHelper.Get("ActiveDirectory:LDAP");

        if (UsarCredencialAtual())
        {
            return new DirectoryEntry(ldap, null, null, AuthenticationTypes.Secure);
        }

        string usuario = ConfigHelper.Get("ActiveDirectory:Usuario");
        string senha = ConfigHelper.Get("ActiveDirectory:Senha");

        return new DirectoryEntry(ldap, usuario, senha, AuthenticationTypes.Secure);
    }

    public static PrincipalContext CriarContextoDominio()
    {
        string domain = ConfigHelper.Get("ActiveDirectory:Domain");

        if (UsarCredencialAtual())
        {
            return new PrincipalContext(ContextType.Domain, domain);
        }

        string usuario = ConfigHelper.Get("ActiveDirectory:Usuario");
        string senha = ConfigHelper.Get("ActiveDirectory:Senha");

        return new PrincipalContext(ContextType.Domain, domain, usuario, senha);
    }

    public static bool UsuarioEhAdministrador()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);

        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool EstaNoControladorDeDominio()
    {
        try
        {
            var domain = Domain.GetComputerDomain();
            string computer = Environment.MachineName;

            return domain.DomainControllers
                .Cast<DomainController>()
                .Any(dc =>
                    dc.Name.Equals(computer, StringComparison.OrdinalIgnoreCase) ||
                    dc.Name.StartsWith(computer + ".", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static bool UsarCredencialAtual()
    {
        return EstaNoControladorDeDominio() && UsuarioEhAdministrador();
    }
}