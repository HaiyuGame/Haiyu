using System.Text.Json.Serialization;
using Waves.Api.Models.CloudGame;

namespace Waves.Api.Models;

[JsonSerializable(typeof(CloudGameLoginData))]
[JsonSerializable(typeof(CloudApiResponse<CloudGameLoginData>))]
[JsonSerializable(typeof(CloudSendSMS))]
[JsonSerializable(typeof(AccessData))]
[JsonSerializable(typeof(CloudApiResponse<AccessData>))]
[JsonSerializable(typeof(PhoneTokenData))]
[JsonSerializable(typeof(CloudApiResponse<PhoneTokenData>))]
[JsonSerializable(typeof(CloudApiResponse<EndLoginReponseData>))]
[JsonSerializable(typeof(CloudApiResponse<bool>))]
[JsonSerializable(typeof(EndLoginData))]
[JsonSerializable(typeof(EndLoginRequest))]
[JsonSerializable(typeof(ExperienceCardInfo))]
[JsonSerializable(typeof(FreeTimeInfo))]
[JsonSerializable(typeof(HsstsToken))]
[JsonSerializable(typeof(PayTimeInfo))]
[JsonSerializable(typeof(EndLoginModel))]
[JsonSerializable(typeof(TimeCardInfo))]
[JsonSerializable(typeof(WalletData))]
[JsonSerializable(typeof(EndLoginReponseData))]
[JsonSerializable(typeof(RecordModel))]
[JsonSerializable(typeof(RecordData))]
[JsonSerializable(typeof(RecardQuery))]
public partial class CloundContext : JsonSerializerContext { }
