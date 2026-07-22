using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Messanger
{
    public record SendTaskMessager(SendTaskType type, TaskWrapper wrapper);

    public enum SendTaskType:uint
    {
        Start = 0,
        Stop = 1,
        Invoke = 2
    }
}
