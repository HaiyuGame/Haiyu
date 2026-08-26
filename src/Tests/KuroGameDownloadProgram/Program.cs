using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Haiyu.Plugin.Services;
using Haiyu.RpcClient;
using KuroGameDownloadProgram;
using KuroGameDownloadProgram.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Settings;

//await KuroClientHomeFeedTest.StartTask();

await IoCircuitBreakerTest.StartTest(args);
