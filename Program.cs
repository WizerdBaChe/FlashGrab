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

        // 具名事件(跨實例):第二次雙擊用它喚醒既有實例跳「已在執行中」氣泡,
        // 解決「重複按沒有任何反饋、不知道按到沒」的問題。
        var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "FlashGrab.ShowSignal");

        if (!createdNew)
        {
            // 已有實例在跑:通知它顯示提示,本實例直接結束(不開第二個托盤圖示)。
            showSignal.Set();
            showSignal.Dispose();
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext(showSignal);
        Application.Run(context);
    }
}
