namespace Ryujinx.Common;

/// <summary>
/// This interface declares queue to process chunk of work
/// potentially outside of current thread context.
/// </summary>
public interface IWorkItemQueue
{
    delegate void WorkItem();
    /// <summary>
    /// This method submits workItem to queue.
    /// </summary>
    /// <param name="workItem"></param>
    void Submit(WorkItem workItem);
}
