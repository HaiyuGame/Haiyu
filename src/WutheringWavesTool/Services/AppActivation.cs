using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Windows.AppLifecycle;
using Waves.Core.Services;
using Windows.ApplicationModel.Activation;
using Windows.UI.StartScreen;
using WinRT;

namespace Haiyu.Services
{
    public sealed partial class AppActivation : IAppActivation
    {
        public AppActivation(SystemEventPublisher systemEventPublisher)
        {
            SystemEventPublisher = systemEventPublisher;
        }

        public SystemEventPublisher SystemEventPublisher { get; }

        public async Task<JumpListItem?> CreateJumpListsAndInitCoreAsync(IGameContextV2 context)
        {
            var status = await context.GetGameContextStatusAsync();
            var displayName = context.DisplayName;
            if (status.IsGameExists && status.IsLauncher)
            {
                var jumpItem = JumpListItem.CreateWithArguments($"{context.ContextName}/startGame", "startGame");
                jumpItem.GroupName = LanguageService.GetStringByText("快捷启动");
                jumpItem.Description = LanguageService.FormatByText(LanguageService.GetStringByText("启动{0}"), displayName);
                jumpItem.DisplayName = LanguageService.FormatByText(LanguageService.GetStringByText("启动{0}"), displayName);
                return jumpItem;
            }
            return null;
        }
        public async Task ExecLaunchActivatedEventArgs(AppActivationArguments e)
        {
            if (e.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File) { }
            if (e.Kind == ExtendedActivationKind.Launch)
            {
                string[] argumentsArray = Environment.GetCommandLineArgs();
                if (e.Data is ILaunchActivatedEventArgs launchArgs)
                {
                    var argument = launchArgs.Arguments.Split(" ");
                    var launcheArgument = argument.Last().Split("/");
                    if (launcheArgument.Length == 2 && launcheArgument[1] == "startGame")
                    {
                        var context = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(launcheArgument[0]);
                        var status = await context.GetGameContextStatusAsync();
                        if (status.IsUpdate)
                        {
                            SystemEventPublisher.Publish(new SystemMessagerModel()
                            {
                                Message = LanguageService.GetStringByText("游戏有更新，无法启动"),
                                Delay = TimeSpan.FromSeconds(30).TotalSeconds
                            });
                            return;
                        }
                        await context.StartGameAsync();
                    }
                }
            }
        }
    }
}
