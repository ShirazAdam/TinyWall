using System.Text.Json.Serialization;
using ImmenseWall.Models.Database;
using ImmenseWall.Models.IPC;
using ImmenseWall.Models;
using pylorak.Windows.WFP;

namespace ImmenseWall.Services
{
    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Default,
        IgnoreReadOnlyFields = false,
        IgnoreReadOnlyProperties = false,
        IncludeFields = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
        WriteIndented = true
        )]
    [JsonSerializable(typeof(TwMessage))]
    [JsonSerializable(typeof(TwMessageGetSettings))]
    [JsonSerializable(typeof(TwMessagePutSettings))]
    [JsonSerializable(typeof(TwMessageComError))]
    [JsonSerializable(typeof(TwMessageError))]
    [JsonSerializable(typeof(TwMessageLocked))]
    [JsonSerializable(typeof(TwMessageGetProcessPath))]
    [JsonSerializable(typeof(TwMessageReadFwLog))]
    [JsonSerializable(typeof(TwMessageIsLocked))]
    [JsonSerializable(typeof(TwMessageUnlock))]
    [JsonSerializable(typeof(TwMessageModeSwitch))]
    [JsonSerializable(typeof(TwMessageSetPassword))]
    [JsonSerializable(typeof(TwMessageSimple))]
    [JsonSerializable(typeof(TwMessageAddTempException))]
    [JsonSerializable(typeof(TwMessageDisplayPowerEvent))]
    [JsonSerializable(typeof(GlobalSubject))]
    [JsonSerializable(typeof(AppContainerSubject))]
    [JsonSerializable(typeof(ExecutableSubject))]
    [JsonSerializable(typeof(ServiceSubject))]
    [JsonSerializable(typeof(HardBlockPolicy))]
    [JsonSerializable(typeof(UnrestrictedPolicy))]
    [JsonSerializable(typeof(TcpUdpPolicy))]
    [JsonSerializable(typeof(RuleListPolicy))]
    [JsonSerializable(typeof(FirewallExceptionV3))]
    [JsonSerializable(typeof(ServerConfiguration))]
    [JsonSerializable(typeof(ControllerSettings))]
    [JsonSerializable(typeof(UpdateDescriptor))]
    [JsonSerializable(typeof(ConfigContainer))]
    [JsonSerializable(typeof(SubjectIdentity))]
    [JsonSerializable(typeof(ImmenseWall.Models.Database.Application))]
    [JsonSerializable(typeof(AppDatabase))]
    [JsonSerializable(typeof(ImmenseWall.Models.ExceptionSubject))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
