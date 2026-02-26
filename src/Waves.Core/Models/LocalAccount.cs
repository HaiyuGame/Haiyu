using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MemoryPack;
using System.Text.Json.Serialization;

namespace Waves.Core.Models;

/// <summary>
/// 本地账号存储信息
/// </summary>
[MemoryPackable]
public partial class LocalAccount:ObservableObject
{
    public string Token { get; set; }

    public string TokenId { get; set; }

    public string TokenDid { get; set; }

    [MemoryPackIgnore]
    public bool IsSelect { get; set; }


    [MemoryPackIgnore]
    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [MemoryPackIgnore]
    [ObservableProperty]
    public partial string Phone { get; set; }
    
    [MemoryPackIgnore]
    [ObservableProperty]
    public partial string Cover { get; set; }
    
    [MemoryPackIgnore]
    public IRelayCommand DeleteLocalAccountCommand => new RelayCommand(() =>
    {
        WeakReferenceMessenger.Default.Send(new DeleteLocalAccount(TokenId));
    });

    [MemoryPackIgnore]
    public IRelayCommand SetCurrentAccountCommand => new RelayCommand(() =>
    {
        WeakReferenceMessenger.Default.Send(new SetCurrentAccount(TokenId));
    });

    [MemoryPackIgnore]
    public IAsyncRelayCommand CopyTokenCommand => new AsyncRelayCommand(async () =>
    {
        WeakReferenceMessenger.Default.Send(new CopyTokenAccount(this.Token));

    }); 
    
    [MemoryPackIgnore]
    public IAsyncRelayCommand CopyDeviceDidCommand => new AsyncRelayCommand(async () =>
    {
        WeakReferenceMessenger.Default.Send(new CopyDeviceDidAccount(this.TokenDid));

    });
}

/// <summary>
/// 删除账号消息
/// </summary>
/// <param name="userId"></param>
public record DeleteLocalAccount(string userId);

/// <summary>
/// 设置当前账号消息
/// </summary>
/// <param name="userId"></param>
public record SetCurrentAccount(string userId);

/// <summary>
/// 复制Token
/// </summary>
/// <param name="accountToken"></param>
public record CopyTokenAccount(string accountToken);

/// <summary>
/// 复制设备Did
/// </summary>
/// <param name="deviceDid"></param>
public record CopyDeviceDidAccount(string deviceDid);