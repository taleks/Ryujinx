using Gtk;
using Ryujinx.Common;
using System;


namespace Ryujinx.Ui;

/// <summary>
/// This class delegates work item processing to GTK event queue.
/// </summary>
public class GtkWorkItemQueue : IWorkItemQueue
{
    public void Submit(IWorkItemQueue.WorkItem workItem)
    {
        Application.Invoke(delegate
            {
                workItem.Invoke();
            }
        );
    }
}
