using System;
using System.Windows;
using System.Windows.Input;

namespace CredentialProviderAPP.Utils;

public static class WindowFocusHelper
{
    public static void ForcarFoco(Window window, IInputElement? elementoFoco = null)
    {
        if (window == null || !window.IsVisible)
            return;

        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                window.Topmost = true;
                window.Activate();
                window.Focus();

                if (elementoFoco != null)
                    Keyboard.Focus(elementoFoco);
            }
            catch
            {
                // evita crash silencioso
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}