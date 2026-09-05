using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common;

public class DialogSession
{
    private ContentDialog? _dialog;

    public object? Result { get; internal set; }

    public bool IsClosed { get; private set; }

    internal void Attach(ContentDialog dialog)
    {
        if (_dialog is not null)
        {
            throw new InvalidOperationException("当前 DialogSession 已绑定 Dialog。");
        }

        _dialog = dialog;
    }

    public void Close(object? result)
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;

        Result = result;

        _dialog?.Hide();
    }

    internal void Complete(object result)
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        Result = result;
    }

    internal void Detach()
    {
        _dialog = null;
    }
}
