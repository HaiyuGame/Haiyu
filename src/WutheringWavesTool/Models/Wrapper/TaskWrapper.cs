using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Wrapper;


public partial class TaskWrapper
{
    public TaskWrapper(string guid, string header, string description)
    {
        this.Guid = guid;
        this.Header = header;
        this.Description = description;
    }

    public string Guid { get; }
    public string Header { get; }
    public string Description { get; }

    public IRelayCommand SendStartCommand => new RelayCommand(() =>
    {
        WeakReferenceMessenger.Default.Send<SendTaskMessager>(new(SendTaskType.Start, this));
    });

    public IRelayCommand SendStopCommand => new RelayCommand(() =>
    {
        WeakReferenceMessenger.Default.Send<SendTaskMessager>(new(SendTaskType.Stop, this));
    });

    public IRelayCommand SendInvokeCommand => new RelayCommand(() =>
    {
        WeakReferenceMessenger.Default.Send<SendTaskMessager>(new(SendTaskType.Invoke, this));
    });
}


public static class TaskExtensions
{
    extension(Tuple<string, string, string> taskName)
    {
        public TaskWrapper Create()
        {
            return new TaskWrapper(taskName.Item1, taskName.Item2, taskName.Item3);
        }
    }
}
