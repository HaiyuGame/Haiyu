using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Contracts.Tasks;
using Waves.Core.Services;
using Waves.Core.Services.Tasks;

namespace Haiyu.Services.Tasks
{
    public class AutoKuroClientSignService : TimedTaskServiceBase,ITaskName
    {
        public AutoKuroClientSignService(
            SystemEventPublisher publisher,
            [FromKeyedServices("AppLog")] LoggerService logger,
            IKuroAccountService kuroAccountService,
            IKuroClient kuroClient
        )
            : base(publisher, logger)
        {
            KuroAccountService = kuroAccountService;
            this.KuroClient = kuroClient;
        }

        public IKuroAccountService KuroAccountService { get; }
        public IKuroClient KuroClient { get; }

        public string DisplayName => "库街区本体账号签到";

        public string Description => "启动后将对Haiyu中存储的所有库街区账号进行签到";

        public string Guid => "6096E7CF-84CF-4CFD-9876-800104A7C566";

        public string Note => "";

        public async override Task InvokeAsync(CancellationToken token = default)
        {

        }
    }
}
