using System.Windows;
using Clipboard = System.Windows.Clipboard;

namespace MeaningOverlay;

/// <summary>
/// Thread-safe clipboard reader. WPF clipboard requires STA thread.
/// </summary>
public static class ClipboardHelper
{
    public static string GetText()
    {
        string text = string.Empty;

        // Clipboard must be accessed on STA thread — WPF dispatcher is already STA
        if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
        {
            try { text = Clipboard.GetText(); }
            catch { /* clipboard may be locked by another process */ }
        }
        else
        {
            var thread = new System.Threading.Thread(() =>
            {
                try { text = Clipboard.GetText(); }
                catch { }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        return text ?? string.Empty;
    }
}
