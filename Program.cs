using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TaskTile.Services;

namespace TaskTile;

public static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var launch = JumpListService.ParseArgs(args);

        // 25H2: process AUMID must match the pin BEFORE any window or destination-list write.
        if (launch.GroupId != Guid.Empty && launch.Action != "parent-settings")
            JumpListService.SetCurrentProcessAppId(JumpListService.GetGroupAppId(launch.GroupId));
        else
            JumpListService.SetCurrentProcessAppId("TaskTile");

        if (launch.UpdateJumpList && launch.GroupId != Guid.Empty)
        {
            JumpListService.WriteGroupJumpListInProcess(launch.GroupId);
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(launch.RunPath))
        {
            JumpListService.LaunchHostedApp(launch.GroupId, launch.RunPath, launch.RunArgs, launch.RunAdmin);
            return 0;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
        return 0;
    }
}
