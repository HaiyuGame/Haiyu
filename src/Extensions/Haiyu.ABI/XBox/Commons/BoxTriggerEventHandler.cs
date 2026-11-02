using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ABI.XBox.Commons;

public class BoxTriggerEventArgs : EventArgs.ValueChangeArgs<bool>
{
    public BoxTriggerEventArgs(bool isEnable)
        : base(isEnable) { }
}

public delegate void BoxTriggerEventHandler(object sender, BoxTriggerEventArgs e);
