using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace pylorak.TinyWall
{
    [DataContract(Namespace = "TinyWall")]
    public class RuleDef
    {
        public static readonly string LOCALSUBNET_ID = "LocalSubnet";

        [DataMember]
        public string? Name;
        [DataMember(EmitDefaultValue = false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid ExceptionId;
        [DataMember]
        public RuleAction Action;

        [DataMember(EmitDefaultValue = false)]
        public string? AppContainerSid;
        [DataMember(EmitDefaultValue = false)]
        public string? Application;
        [DataMember(EmitDefaultValue = false)]
        public string? ServiceName;
        [DataMember(EmitDefaultValue = false)]
        public string? LocalPorts;
        [DataMember(EmitDefaultValue = false)]
        public string? RemotePorts;
        [DataMember(EmitDefaultValue = false)]
        public string? LocalAddresses;
        [DataMember(EmitDefaultValue = false)]
        public string? RemoteAddresses;
        [DataMember(EmitDefaultValue = false)]
        public string? IcmpTypesAndCodes;

        [DataMember]
        public Protocol Protocol;
        [DataMember]
        public RuleDirection Direction;
        [JsonIgnore]
        public ulong Weight;

        public RuleDef()
        { }

        public RuleDef ShallowCopy()
        {
            var copy = new RuleDef();
            copy.Name = this.Name;
            copy.ExceptionId = this.ExceptionId;
            copy.Action = this.Action;
            copy.Application = this.Application;
            copy.ServiceName = this.ServiceName;
            copy.AppContainerSid = this.AppContainerSid;
            copy.LocalPorts = this.LocalPorts;
            copy.RemotePorts = this.RemotePorts;
            copy.LocalAddresses = this.LocalAddresses;
            copy.RemoteAddresses = this.RemoteAddresses;
            copy.IcmpTypesAndCodes = this.IcmpTypesAndCodes;
            copy.Protocol = this.Protocol;
            copy.Direction = this.Direction;
            copy.Weight = this.Weight;
            return copy;
        }

        public void SetSubject(ExceptionSubject? subject)
        {
            if (subject == null) return;

            switch (subject)
            {
                case ServiceSubject service:
                    Application = service.ExecutablePath;
                    ServiceName = service.ServiceName;
                    AppContainerSid = null;
                    break;
                case ExecutableSubject exe:
                    Application = exe.ExecutablePath;
                    ServiceName = null;
                    AppContainerSid = null;
                    break;
                case AppContainerSubject uwp:
                    Application = null;
                    ServiceName = null;
                    AppContainerSid = uwp.Sid;
                    break;
                case GlobalSubject _:
                    Application = null;
                    ServiceName = null;
                    AppContainerSid = null;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public RuleDef(Guid exceptionId, string name, ExceptionSubject subject, RuleAction action, RuleDirection direction, Protocol protocol, ulong weight)
        {
            SetSubject(subject);
            Name = name;
            ExceptionId = exceptionId;
            Action = action;
            Direction = direction;
            Protocol = protocol;
            Weight = weight;
        }
    }
}
