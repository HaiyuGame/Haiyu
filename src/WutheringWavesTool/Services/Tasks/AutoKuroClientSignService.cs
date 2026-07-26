using Waves.Api.Models.KuroClient;
using Waves.Api.Models.KuroClient.Options;
using Waves.Core.Contracts.Tasks;
using Waves.Core.Services;
using Waves.Core.Services.Tasks;

namespace Haiyu.Services.Tasks;

public sealed class AutoKuroClientSignService : TimedTaskServiceBase, ITaskName
{
    private const int PageSize = 20;
    private const int BrowseTarget = 3;
    private const int LikeTarget = 5;
    private const int MaxAttempts = 3;

    public AutoKuroClientSignService(
        SystemEventPublisher publisher,
        [FromKeyedServices("AppLog")] LoggerService logger,
        IKuroAccountService kuroAccountService,
        IKuroClient kuroClient
    )
        : base(publisher, logger)
    {
        TargetTime = new TimeOnly(8, 0);
        KuroAccountService = kuroAccountService;
        KuroClient = kuroClient;
    }

    public IKuroAccountService KuroAccountService { get; }
    public IKuroClient KuroClient { get; }

    public string DisplayName => "库街区每日任务";

    public string Description => "为 Haiyu 中保存的库街区账号执行签到、浏览、点赞和分享任务";

    public string Guid => "6096E7CF-84CF-4CFD-9876-800104A7C566";

    public string Note => "AutoKuroTaskEnable";

    public override async Task InvokeAsync(CancellationToken token = default)
    {
        Publisher.Publish(new() { Message = "开始执行库街区每日任务", Delay = 3 });

        var accounts = await KuroAccountService.GetUsersAsync();
        if (accounts is null || accounts.Count == 0)
        {
            Publisher.Publish(new() { Message = "未找到可执行每日任务的库街区账号", Delay = 4 });
            return;
        }

        var completedAccounts = 0;
        var failedAccounts = 0;

        foreach (var localAccount in accounts)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var account = KuroAccount.From(localAccount);
                if (await ExecuteAccountTasksAsync(account, token))
                {
                    completedAccounts++;
                }
                else
                {
                    failedAccounts++;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedAccounts++;
                Logger.WriteError(
                    $"库街区账号 {localAccount.TokenId} 的每日任务执行失败：{exception}"
                );
            }
        }

        Publisher.Publish(
            new()
            {
                Message =
                    $"库街区每日任务完成：成功 {completedAccounts} 个，失败 {failedAccounts} 个",
                Delay = 5,
            }
        );
    }

    private async Task<bool> ExecuteAccountTasksAsync(
        KuroAccount account,
        CancellationToken token
    )
    {
        // 1. 每日签到
        var signResult = await ExecuteWithRetryAsync(
            "签到",
            () => KuroClient.SignInClientAsync(account, token),
            token
        );
        if (signResult is null)
        {
            return false;
        }

        // 2. 随机获取一页帖子，并浏览其中 3 篇。
        var pageIndex = Random.Shared.Next(1, 11);
        var feedResult = await ExecuteWithRetryAsync(
            "获取帖子",
            () =>
                KuroClient.FeedHomeListsAsync(
                    account,
                    HomeFeedOption.CreateHomeWaves(pageIndex, PageSize),
                    token
                ),
            token
        );
        var posts = feedResult?.Data?.PostList?
            .Where(static post => !string.IsNullOrWhiteSpace(post.PostId))
            .GroupBy(static post => post.PostId)
            .Select(static group => group.First())
            .ToList();
        if (posts is null || posts.Count < BrowseTarget)
        {
            Logger.WriteError(
                $"库街区每日任务获取的有效帖子不足：pageIndex={pageIndex}, count={posts?.Count ?? 0}"
            );
            return false;
        }

        var browsed = 0;
        foreach (var post in posts.Take(BrowseTarget))
        {
            var detailResult = await ExecuteWithRetryAsync(
                $"浏览帖子 {post.PostId}",
                () =>
                    KuroClient.GetFeedPageDetailAsync(
                        account,
                        HomeFeedPostDetailOption.Create(post.PostId),
                        token
                    ),
                token
            );
            if (detailResult is not null)
            {
                browsed++;
            }
        }
        if (browsed < BrowseTarget)
        {
            return false;
        }

        // 3. 点赞 5 篇帖子，优先选择当前尚未点赞的帖子。
        var likePosts = posts
            .OrderBy(static post => post.IsLike == 1)
            .Take(LikeTarget)
            .ToList();
        if (likePosts.Count < LikeTarget)
        {
            return false;
        }

        var liked = 0;
        foreach (var post in likePosts)
        {
            var likeOption = HomeFeedLikeOption.CreateLikeWaves(
                post.PostId,
                post.PostType.ToString(),
                "1",
                string.Empty,
                string.Empty,
                post.UserId
            );
            var likeResult = await ExecuteWithRetryAsync(
                $"点赞帖子 {post.PostId}",
                () => KuroClient.PostIdLikeAsync(account, likeOption, token),
                token
            );
            if (likeResult is not null)
            {
                liked++;
            }
        }
        if (liked < LikeTarget)
        {
            return false;
        }

        // 4. 分享 1 篇帖子。使用帖子自身的 gameId，避免跨游戏分区。
        var sharePost = posts[0];
        var shareResult = await ExecuteWithRetryAsync(
            $"分享帖子 {sharePost.PostId}",
            () =>
                KuroClient.SharedPostIdAsync(
                    account,
                    new HomeFeedSharedOption
                    {
                        GameId = sharePost.GameId.ToString(),
                        PostId = sharePost.PostId,
                    },
                    token
                ),
            token
        );

        return shareResult is not null;
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(
        string operation,
        Func<Task<T?>> action,
        CancellationToken token
    )
        where T : class
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var result = await action();
                if (result is not null)
                {
                    return result;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.WriteError(
                    $"库街区每日任务“{operation}”\r\n第 {attempt}/{MaxAttempts} 次执行异常：{exception.Message}"
                );
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), token);
            }
        }

        Logger.WriteError($"库街区每日任务“{operation}”重试 {MaxAttempts} 次后仍失败");
        return null;
    }
}
