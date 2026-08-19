using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel;

partial class WavesAnalysisRecordViewModel
{
    protected override void OnDisposing()
    {
        NavItems?.Clear();
        GuaranteItems?.Clear();
        StarItems?.Clear();
        GuaranteeChart?.Clear();
        StarRatioChart?.Clear();
        PoolChart?.Clear();
        TimeLineChart?.Clear();
        Header = null;
        SelectGuarante = null;
        SelectNavItem = null;
        FiveGroup = null;
        AllRole = null;
        AllWeapon = null;
        Cards = null;
        base.OnDisposing();
    }
}
