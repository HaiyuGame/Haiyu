using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.Launcher;

namespace Haiyu.Models.Wrapper
{
    public sealed partial class KuroGameTokenWrapper:ObservableObject
    {
        public KuroGameTokenWrapper(KRSDKGameTokenCache cache)
        {
            this.Cache = cache;
        }

        [ObservableProperty]
        public partial KRSDKGameTokenCache Cache { get; set; }


        [ObservableProperty]
        public partial bool IsSelect { get; set; }
    }

    

}
