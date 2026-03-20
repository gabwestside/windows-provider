using CredentialProviderAPP.Views;
using System.Windows;

namespace CredentialProviderAPP.Utils
{
    /// <summary>
    /// Helper reutilizável para exibir modais modernos em qualquer janela.
    ///
    /// Uso: injete a flag <c>mostrandoDialog</c> via ref para que o <c>Window_Deactivated</c> não
    ///      force o foco enquanto o modal está aberto.
    ///
    /// Exemplo: MessageHelper.Aviso("Senha inválida.", ref mostrandoDialog, this);
    /// MessageHelper.Erro("Falha na conexão.", ref mostrandoDialog, this);
    /// MessageHelper.Sucesso("Salvo com sucesso!", ref mostrandoDialog, this);
    ///
    /// var r = MessageHelper.Confirmacao("Deseja continuar?", ref mostrandoDialog, this); if (r ==
    /// MessageBoxResult.Yes) { ... }
    ///
    /// Sem flag (janelas sem Window_Deactivated): MessageHelper.Aviso("Texto");
    /// </summary>
    public static class MessageHelper
    {
        // ══════════════════════════════════════════════════════════════
        //  COM CONTROLE DE FLAG (janelas com Window_Deactivated)
        // ══════════════════════════════════════════════════════════════

        public static void Aviso(string msg, ref bool flag, Window? owner = null)
            => Executar(ref flag, () =>
                ModernMessageBox.Show(msg, "Atenção", ModernMessageBox.Kind.Warning, owner));

        public static void Erro(string msg, ref bool flag, Window? owner = null)
            => Executar(ref flag, () =>
                ModernMessageBox.Show(msg, "Erro", ModernMessageBox.Kind.Error, owner));

        public static void Sucesso(string msg, ref bool flag, Window? owner = null)
            => Executar(ref flag, () =>
                ModernMessageBox.Show(msg, "Sucesso", ModernMessageBox.Kind.Success, owner));

        public static void Info(string msg, ref bool flag, Window? owner = null)
            => Executar(ref flag, () =>
                ModernMessageBox.Show(msg, "Informação", ModernMessageBox.Kind.Info, owner));

        public static MessageBoxResult Confirmacao(
            string msg, ref bool flag,
            Window? owner = null,
            string titulo = "Confirmar",
            ModernMessageBox.Kind kind = ModernMessageBox.Kind.Warning)
        {
            MessageBoxResult result = MessageBoxResult.Cancel;
            Executar(ref flag, () =>
            {
                result = ModernMessageBox.ShowYesNo(msg, titulo, kind, owner);
            });
            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  SEM CONTROLE DE FLAG (uso simples, sem Window_Deactivated)
        // ══════════════════════════════════════════════════════════════

        public static void Aviso(string msg, Window? owner = null)
            => ModernMessageBox.Show(msg, "Atenção", ModernMessageBox.Kind.Warning, owner);

        public static void Erro(string msg, Window? owner = null)
            => ModernMessageBox.Show(msg, "Erro", ModernMessageBox.Kind.Error, owner);

        public static void Sucesso(string msg, Window? owner = null)
            => ModernMessageBox.Show(msg, "Sucesso", ModernMessageBox.Kind.Success, owner);

        public static void Info(string msg, Window? owner = null)
            => ModernMessageBox.Show(msg, "Informação", ModernMessageBox.Kind.Info, owner);

        public static MessageBoxResult Confirmacao(
            string msg,
            Window? owner = null,
            string titulo = "Confirmar",
            ModernMessageBox.Kind kind = ModernMessageBox.Kind.Warning)
            => ModernMessageBox.ShowYesNo(msg, titulo, kind, owner);

        // ══════════════════════════════════════════════════════════════
        //  HELPER INTERNO — seta a flag, executa, reseta a flag
        // ══════════════════════════════════════════════════════════════
        private static void Executar(ref bool flag, Action acao)
        {
            flag = true;
            try { acao(); }
            finally { flag = false; }
        }
    }
}