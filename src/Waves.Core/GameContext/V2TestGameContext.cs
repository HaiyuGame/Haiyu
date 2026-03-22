using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.CoreApi;

namespace Waves.Core.GameContext;

public class V2TestGameContext : KuroGameContextBaseV2
{
    public V2TestGameContext(KuroGameApiConfig config, string contextName)
        : base(config, contextName) { }

}
