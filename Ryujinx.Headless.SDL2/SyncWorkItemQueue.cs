using Ryujinx.Common;

namespace Ryujinx.Headless.SDL2;
/// <summary>
/// Naive implementation of IWorkItemQueue to process
/// work item synchronously on submit.
/// </summary>
public class SyncWorkItemQueue : IWorkItemQueue
{
    public void Submit(IWorkItemQueue.WorkItem workItem)
    {
        workItem.Invoke();
    }
}
