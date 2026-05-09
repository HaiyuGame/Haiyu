using Haiyu.Plugin.Models;
using Haiyu.Services.DialogServices;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class UpdateAppViewModel : DialogViewModelBase
{
    private DisplayVersionInfo _info;
    public UpdateAppViewModel([FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager) : base(dialogManager)
    {
    }

    internal void SetInfo(DisplayVersionInfo info)
    {
        this._info = info;
    }
}
