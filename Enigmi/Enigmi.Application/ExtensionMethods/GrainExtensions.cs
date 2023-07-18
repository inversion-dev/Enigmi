using Enigmi.Common;

namespace Enigmi.Application.ExtensionMethods;

public static class GrainExtensions
{
    /// <summary>
    /// SelfInvokeAfter
    /// invoke self method in next 'turn'
    /// this is how it's done to avoid interleaving and re-entrant issues
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="grain"></param>
    /// <param name="action"></param>
    /// <param name="taskScheduler"></param>
    public static async Task SelfInvokeAfter<T>(this T grain, Func<T, Task> proxyCall)
        where T : IGrain
    {
        grain.ThrowIfNull();
        proxyCall.ThrowIfNull();

        var taskScheduler = await ((Enigmi.Application.Grains.IGrainBase)grain).GetTaskScheduler();

        await Task.Factory.StartNew(() =>
        {            
            proxyCall(grain.AsReference<T>());
        }, CancellationToken.None, TaskCreationOptions.PreferFairness, taskScheduler);
    }
}