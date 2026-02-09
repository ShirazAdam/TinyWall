using System;
using System.IO;
using System.Text.Json;
using Xunit;
using ImmenseWall.Models;
using ImmenseWall.Models.IPC;
using ImmenseWall.Services;
using System.Collections.Generic;

namespace ImmenseWall.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void ServerConfiguration_Serialization_RoundTrip()
        {
            var config = new ServerConfiguration
            {
                ActiveProfileName = "TestProfile",
                Profiles = new List<ServerProfileConfiguration>
                {
                    new ServerProfileConfiguration("TestProfile")
                    {
                        AppExceptions = new List<FirewallExceptionV3>
                        {
                            new FirewallExceptionV3(GlobalSubject.Instance, HardBlockPolicy.Instance),
                            new FirewallExceptionV3(new ExecutableSubject(@"C:\Windows\System32\notepad.exe"), new UnrestrictedPolicy { LocalNetworkOnly = true })
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(config, SourceGenerationContext.Default.ServerConfiguration);
            var deserialized = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ServerConfiguration);

            Assert.NotNull(deserialized);
            Assert.Equal(config.ActiveProfileName, deserialized.ActiveProfileName);
            Assert.Single(deserialized.Profiles);
            Assert.Equal("TestProfile", deserialized.Profiles[0].ProfileName);
            Assert.Equal(2, deserialized.Profiles[0].AppExceptions.Count);
            
            // Verify Polymorphism
            Assert.IsType<GlobalSubject>(deserialized.Profiles[0].AppExceptions[0].Subject);
            Assert.IsType<HardBlockPolicy>(deserialized.Profiles[0].AppExceptions[0].Policy);
            
            Assert.IsType<ExecutableSubject>(deserialized.Profiles[0].AppExceptions[1].Subject);
            Assert.IsType<UnrestrictedPolicy>(deserialized.Profiles[0].AppExceptions[1].Policy);
            Assert.True(((UnrestrictedPolicy)deserialized.Profiles[0].AppExceptions[1].Policy).LocalNetworkOnly);
        }

        [Fact]
        public void ExceptionPolicy_PolymorphicSerialization()
        {
            ExceptionPolicy policy = new TcpUdpPolicy { AllowedRemoteTcpConnectPorts = "80,443" };
            var json = JsonSerializer.Serialize(policy, SourceGenerationContext.Default.ExceptionPolicy);
            var deserialized = JsonSerializer.Deserialize<ExceptionPolicy>(json, SourceGenerationContext.Default.ExceptionPolicy);

            Assert.IsType<TcpUdpPolicy>(deserialized);
            Assert.Equal("80,443", ((TcpUdpPolicy)deserialized).AllowedRemoteTcpConnectPorts);
        }

        [Fact]
        public void ExceptionSubject_PolymorphicSerialization()
        {
            ExceptionSubject subject = new ServiceSubject(@"C:\Windows\System32\svchost.exe", "Dnscache");
            var json = JsonSerializer.Serialize(subject, SourceGenerationContext.Default.ExceptionSubject);
            var deserialized = JsonSerializer.Deserialize<ExceptionSubject>(json, SourceGenerationContext.Default.ExceptionSubject);

            Assert.IsType<ServiceSubject>(deserialized);
            Assert.Equal("Dnscache", ((ServiceSubject)deserialized).ServiceName);
            Assert.Equal(@"C:\Windows\System32\svchost.exe", ((ServiceSubject)deserialized).ExecutablePath);
        }

        [Fact]
        public void TwMessage_PolymorphicSerialization()
        {
            TwMessage msg = TwMessageGetSettings.CreateRequest(Guid.NewGuid());
            var json = JsonSerializer.Serialize(msg, SourceGenerationContext.Default.TwMessage);
            var deserialized = JsonSerializer.Deserialize<TwMessage>(json, SourceGenerationContext.Default.TwMessage);

            Assert.IsType<TwMessageGetSettings>(deserialized);
            Assert.Equal(((TwMessageGetSettings)msg).Changeset, ((TwMessageGetSettings)deserialized).Changeset);
        }

        [Fact]
        public void TwMessage_DisplayPowerEvent_Serialization()
        {
            TwMessage msg = TwMessageDisplayPowerEvent.CreateRequest(true);
            var json = JsonSerializer.Serialize(msg, SourceGenerationContext.Default.TwMessage);
            var deserialized = JsonSerializer.Deserialize<TwMessage>(json, SourceGenerationContext.Default.TwMessage);

            Assert.IsType<TwMessageDisplayPowerEvent>(deserialized);
            Assert.True(((TwMessageDisplayPowerEvent)deserialized).PowerOn);
        }

        [Fact]
        public void TwMessage_Simple_Serialization()
        {
            TwMessage msg = new TwMessageSimple(MessageType.REENUMERATE_ADDRESSES);
            var json = JsonSerializer.Serialize(msg, SourceGenerationContext.Default.TwMessage);
            var deserialized = JsonSerializer.Deserialize<TwMessage>(json, SourceGenerationContext.Default.TwMessage);

            Assert.IsType<TwMessageSimple>(deserialized);
            Assert.Equal(MessageType.REENUMERATE_ADDRESSES, deserialized.Type);
        }
    }
}
