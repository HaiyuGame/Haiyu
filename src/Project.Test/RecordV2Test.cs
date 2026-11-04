using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Record;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Services;
using Waves.Settings;

namespace Project.Test;

[TestClass]
public class RecordV2Test
{
    public IServiceProvider Provider { get; private set; }

    [TestMethod]
    public async Task RecordTestSave()
    {
        
    }
}
