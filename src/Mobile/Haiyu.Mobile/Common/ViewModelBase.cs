using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Haiyu.Mobile.Common
{
    public partial class ViewModelBase:ObservableRecipient,IDisposable
    {
        public CancellationTokenSource CTS { get; }
        public ViewModelBase()
        {
            this.CTS = new CancellationTokenSource();
        }

        public virtual void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}
