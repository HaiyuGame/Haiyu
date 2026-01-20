using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ServiceHost.XBox.Commons;

public class BoxTriggerEventArgs : EventArgs
{
    public BoxTriggerEventArgs(bool isEnable)
    {
        IsEnable = isEnable;
    }

    public bool IsEnable { get; }
}


public delegate void BoxTriggerEventHandler(object sender, BoxTriggerEventArgs e);
