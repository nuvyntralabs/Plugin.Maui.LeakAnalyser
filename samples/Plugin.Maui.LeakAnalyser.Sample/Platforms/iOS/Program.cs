using ObjCRuntime;
using UIKit;

namespace Plugin.Maui.LeakAnalyser.Sample;

public class Program
{
    static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
