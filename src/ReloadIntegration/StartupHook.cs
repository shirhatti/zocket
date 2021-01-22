using Microsoft.Win32;
using System.Diagnostics;

internal class StartupHook
{
    public static void Initialize()
    {
        while (!Debugger.IsAttached)
        {
        }

        Registry.CurrentUser.SetValue("zocketprocessid2", Process.GetCurrentProcess().Id);
        // Set event 
    }
}
