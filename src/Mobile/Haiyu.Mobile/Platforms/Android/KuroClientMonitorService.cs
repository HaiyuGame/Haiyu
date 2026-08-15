using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Haiyu.Mobile.Platforms.Android;

/// <summary>
/// haiyu移动端后台服务
/// </summary>
[Service(
    Name = "com.haiyugame.mobile.KuroClientMonitorService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse
)]
public partial class KuroClientMonitorService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;


}
