using NAPS2.EtoForms;
using NAPS2.EtoForms.WinForms;
using NAPS2.Modules;

namespace NAPS2.EntryPoints;

/// <summary>
/// The entry point logic for NAPS2.exe, the NAPS2 GUI.
/// </summary>
public static class WinFormsEntryPoint
{
    public static int Run(string[] args)
    {
        EtoPlatform.Current = new WinFormsEtoPlatform();

        var subArgs = args.Skip(1).ToArray();
        return args switch
        {
            ["worker", ..] => WindowsNativeWorkerEntryPoint.Run(subArgs),
            // Scanner sharing was removed from this fork. A startup shortcut created by an older
            // build still passes "server"; exit quietly instead of opening the main window.
            ["server", ..] => 0,
            _ => GuiEntryPoint.Run(args, new GdiModule(), new WinFormsModule())
        };
    }
}