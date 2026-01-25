using System;
using Xunit;

namespace TinyWall.Tests
{
    public class RuleDefTests
    {
        [Fact]
        public void RuleDef_DefaultConstructor_CreatesInstance()
        {
            // Act
            var rule = new RuleDef();

            // Assert
            Assert.NotNull(rule);
        }

        [Fact]
        public void RuleDef_ParameterizedConstructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var exceptionId = Guid.NewGuid();
            string name = "Test Rule";
            var subject = new ExecutableSubject("C:\\test.exe");
            var action = RuleAction.Allow;
            var direction = RuleDirection.Inbound;
            var protocol = Protocol.Tcp;
            ulong weight = 100;

            // Act
            var rule = new RuleDef(exceptionId, name, subject, action, direction, protocol, weight);

            // Assert
            Assert.Equal(name, rule.Name);
            Assert.Equal(exceptionId, rule.ExceptionId);
            Assert.Equal(action, rule.Action);
            Assert.Equal(direction, rule.Direction);
            Assert.Equal(protocol, rule.Protocol);
            Assert.Equal(weight, rule.Weight);
            Assert.Equal("C:\\test.exe", rule.Application);
        }

        [Fact]
        public void ShallowCopy_CopiesAllProperties()
        {
            // Arrange
            var original = new RuleDef
            {
                Name = "Test Rule",
                ExceptionId = Guid.NewGuid(),
                Action = RuleAction.Block,
                Application = "C:\\test.exe",
                ServiceName = "TestService",
                AppContainerSid = "S-1-2-3",
                LocalPorts = "80",
                RemotePorts = "443",
                LocalAddresses = "127.0.0.1",
                RemoteAddresses = "192.168.1.1",
                IcmpTypesAndCodes = "8:0",
                Protocol = Protocol.Udp,
                Direction = RuleDirection.Outbound,
                Weight = 50
            };

            // Act
            var copy = original.ShallowCopy();

            // Assert
            Assert.Equal(original.Name, copy.Name);
            Assert.Equal(original.ExceptionId, copy.ExceptionId);
            Assert.Equal(original.Action, copy.Action);
            Assert.Equal(original.Application, copy.Application);
            Assert.Equal(original.ServiceName, copy.ServiceName);
            Assert.Equal(original.AppContainerSid, copy.AppContainerSid);
            Assert.Equal(original.LocalPorts, copy.LocalPorts);
            Assert.Equal(original.RemotePorts, copy.RemotePorts);
            Assert.Equal(original.LocalAddresses, copy.LocalAddresses);
            Assert.Equal(original.RemoteAddresses, copy.RemoteAddresses);
            Assert.Equal(original.IcmpTypesAndCodes, copy.IcmpTypesAndCodes);
            Assert.Equal(original.Protocol, copy.Protocol);
            Assert.Equal(original.Direction, copy.Direction);
            Assert.Equal(original.Weight, copy.Weight);
        }

        [Fact]
        public void SetSubject_ExecutableSubject_SetsApplicationCorrectly()
        {
            // Arrange
            var rule = new RuleDef();
            var subject = new ExecutableSubject("C:\\test.exe");

            // Act
            rule.SetSubject(subject);

            // Assert
            Assert.Equal("C:\\test.exe", rule.Application);
            Assert.Null(rule.ServiceName);
            Assert.Null(rule.AppContainerSid);
        }

        [Fact]
        public void SetSubject_ServiceSubject_SetsServiceAndApplicationCorrectly()
        {
            // Arrange
            var rule = new RuleDef();
            var subject = new ServiceSubject("C:\\service.exe", "TestService");

            // Act
            rule.SetSubject(subject);

            // Assert
            Assert.Equal("C:\\service.exe", rule.Application);
            Assert.Equal("TestService", rule.ServiceName);
            Assert.Null(rule.AppContainerSid);
        }

        [Fact]
        public void SetSubject_AppContainerSubject_SetsAppContainerSidCorrectly()
        {
            // Arrange
            var rule = new RuleDef();
            var subject = new AppContainerSubject("S-1-2-3-4");

            // Act
            rule.SetSubject(subject);

            // Assert
            Assert.Null(rule.Application);
            Assert.Null(rule.ServiceName);
            Assert.Equal("S-1-2-3-4", rule.AppContainerSid);
        }

        [Fact]
        public void SetSubject_GlobalSubject_ClearsAllSubjectFields()
        {
            // Arrange
            var rule = new RuleDef
            {
                Application = "C:\\test.exe",
                ServiceName = "TestService",
                AppContainerSid = "S-1-2-3"
            };
            var subject = new GlobalSubject();

            // Act
            rule.SetSubject(subject);

            // Assert
            Assert.Null(rule.Application);
            Assert.Null(rule.ServiceName);
            Assert.Null(rule.AppContainerSid);
        }

        [Fact]
        public void SetSubject_NullSubject_DoesNotChangeProperties()
        {
            // Arrange
            var rule = new RuleDef
            {
                Application = "C:\\test.exe",
                ServiceName = "TestService",
                AppContainerSid = "S-1-2-3"
            };
            ExceptionSubject subject = null;

            // Act
            rule.SetSubject(subject);

            // Assert
            Assert.Equal("C:\\test.exe", rule.Application);
            Assert.Equal("TestService", rule.ServiceName);
            Assert.Equal("S-1-2-3", rule.AppContainerSid);
        }
    }
    
    // Supporting classes for testing since they're internal
    internal class ExecutableSubject : ExceptionSubject
    {
        public string ExecutablePath { get; }
        
        public ExecutableSubject(string executablePath)
        {
            ExecutablePath = executablePath;
        }
        
        public override string DisplayName => ExecutablePath;
    }
    
    internal class ServiceSubject : ExceptionSubject
    {
        public string ExecutablePath { get; }
        public string ServiceName { get; }
        
        public ServiceSubject(string executablePath, string serviceName)
        {
            ExecutablePath = executablePath;
            ServiceName = serviceName;
        }
        
        public override string DisplayName => ServiceName;
    }
    
    internal class AppContainerSubject : ExceptionSubject
    {
        public string Sid { get; }
        
        public AppContainerSubject(string sid)
        {
            Sid = sid;
        }
        
        public override string DisplayName => Sid;
    }
    
    internal class GlobalSubject : ExceptionSubject
    {
        public override string DisplayName => "All Applications";
    }
    
    internal abstract class ExceptionSubject
    {
        public abstract string DisplayName { get; }
    }
    
    // Enums needed for testing
    internal enum RuleAction { Allow, Block }
    internal enum RuleDirection { Inbound, Outbound }
    internal enum Protocol { Tcp, Udp, Any }
}