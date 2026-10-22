using Foundation;
using UIKit;

namespace Yap.Native.iOS;

public static class Program { public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate)); }

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new MainController() };
        Window.MakeKeyAndVisible(); return true;
    }
    public override void DidEnterBackground(UIApplication application)
    { if (Window?.RootViewController is MainController controller) controller.Stop(); }
}
