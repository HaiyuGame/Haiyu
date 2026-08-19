using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common;

public partial class WindowViewModelBase:ViewModelBase,IDisposable
{
    internal Window? Window { get; private set; }

    public void Initialization(Window win)
    {
        this.Window = win;
    }

    protected override void OnDisposing()
    {
        Window = null;
    }
}
