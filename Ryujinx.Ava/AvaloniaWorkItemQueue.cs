using Avalonia.Threading;
using Ryujinx.Common;
using System;

namespace Ryujinx.Ava;

/// <summary>
/// Implements queue to process work items on main Avalonia UI thread.
/// </summary>
public class AvaloniaWorkItemQueue : IWorkItemQueue
{
    public void Submit(IWorkItemQueue.WorkItem workItem)
    {
        Dispatcher.UIThread.Post(workItem.Invoke);
    }
}
