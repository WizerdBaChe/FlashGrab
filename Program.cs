using System.Threading;
using FlashGrab.App;

namespace FlashGrab;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 單一實例:避免重複常駐與快捷鍵衝突
        using var mutex = new Mutex(initiallyOwned: true, "FlashGrab.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
