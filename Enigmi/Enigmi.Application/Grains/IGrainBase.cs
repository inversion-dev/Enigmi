namespace Enigmi.Application.Grains;

public interface IGrainBase : IGrain
{
    internal Task ProcessEventQueue();

    internal Task<TaskScheduler> GetTaskScheduler();
}