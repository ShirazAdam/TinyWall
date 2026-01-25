using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace pylorak.TinyWall
{
    public enum FirewallMode
    {
        Normal,
        BlockAll,
        AllowOutgoing,
        Disabled,
        Learning,
        Unknown = 100
    }

    [DataContract(Namespace = "TinyWall")]
    public sealed class BlockListSettings
    {
        [DataMember(EmitDefaultValue = false)]
        public bool EnableBlocklists;

        [DataMember(EmitDefaultValue = false)]
        public bool EnablePortBlocklist;

        [DataMember(EmitDefaultValue = false)]
        public bool EnableHostsBlocklist;
    }

    [DataContract(Namespace = "TinyWall")]
    public sealed class ServerProfileConfiguration
    {
        [DataMember(EmitDefaultValue = false)]
        public string ProfileName { get; set; } = string.Empty;

        [DataMember(EmitDefaultValue = false)]
        public List<string> SpecialExceptions { get; set; } = new();

        [DataMember(EmitDefaultValue = false)]
        public bool AllowLocalSubnet { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public List<FirewallExceptionV3> AppExceptions { get; set; } = new();

        [DataMember(EmitDefaultValue = false)]
        public bool DisplayOffBlock { get; set; }

        public ServerProfileConfiguration()
        { }

        public ServerProfileConfiguration(string name)
        {
            ProfileName = name;
        }

        public bool HasSpecialException(string name)
        {
            foreach (string appName in SpecialExceptions)
            {
                if (string.Equals(name, appName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void AddExceptions(List<FirewallExceptionV3> newList)
        {
            var oldList = new List<FirewallExceptionV3>(AppExceptions);

            foreach (var oldEx in oldList)
            {
                foreach (var newEx in newList)
                {
                    if (oldEx.Id.Equals(newEx.Id))
                    {
                        // With equal exception IDs, keep only the newer one.
                        // Two exceptions can have the same IDs if the user just edited one.
                        AppExceptions.Remove(oldEx);
                    }
                    else if (oldEx.Subject.Equals(newEx.Subject)
                        && (oldEx.Timer == AppExceptionTimer.Permanent)
                        && (newEx.Timer == AppExceptionTimer.Permanent)
                    )
                    {
                        // Merge rules
                        var newPolicy = newEx.Policy;
                        if (oldEx.Policy.MergeRulesTo(ref newPolicy))
                        {
                            AppExceptions.Remove(oldEx);
                            newEx.Policy = newPolicy;
                            newEx.RegenerateId();
                        }
                    }
                }
            } // for all exceptions

            AppExceptions.AddRange(newList);
        } // method

        public void Normalise()
        {
            for (var i = 0; i < AppExceptions.Count; ++i)
            {
                var app1 = AppExceptions[i];

                for (var j = AppExceptions.Count - 1; j > i; --j)
                {
                    var app2 = AppExceptions[j];

                    if (app1.Id.Equals(app2.Id))
                    {
                        // With equal exception IDs, keep only the newer one
                        // Two exceptions can have the same IDs if the user just edited
                        // an exception, in which case the newer (edited) version
                        // is added using the same ID as the unedited one.

                        var older = app1.CreationDate > app2.CreationDate ? app2 : app1;
                        AppExceptions.Remove(older);
                    }
                    else if (app1.Subject.Equals(app2.Subject)
                        && (app1.Timer == AppExceptionTimer.Permanent)
                        && (app2.Timer == AppExceptionTimer.Permanent)
                    )
                    {
                        // Merge rules
                        var targetPolicy = app1.Policy;

                        if (!app2.Policy.MergeRulesTo(ref targetPolicy)) continue;

                        AppExceptions.Remove(app2);
                        app1.Policy = targetPolicy;
                        app1.RegenerateId();
                    }
                }
            } // for all exceptions
        } // method
    }

    [DataContract(Namespace = "TinyWall")]
    public sealed class ServerConfiguration : ISerializable<ServerConfiguration>
    {
        public int ConfigVersion { get; set; } = 1;

        // Machine settings
        [DataMember(EmitDefaultValue = false)]
        public BlockListSettings Blocklists { get; set; } = new();

        [DataMember(EmitDefaultValue = false)]
        public bool LockHostsFile { get; set; } = true;

        [DataMember(EmitDefaultValue = false)]
        public bool AutoUpdateCheck { get; set; } = true;

        [DataMember(EmitDefaultValue = false)]
        public FirewallMode StartupMode { get; set; } = FirewallMode.Normal;

        [DataMember(EmitDefaultValue = false)]
        public List<ServerProfileConfiguration> Profiles { get; set; } = new();

        private string _activeProfileName = string.Empty;

        [DataMember(EmitDefaultValue = false)]
        public string ActiveProfileName
        {
            get => _activeProfileName;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException($"Argument {nameof(value)} may not be null or empty.");

                _activeProfileName = value;
                _activeProfile = null;
            }
        }


        private ServerProfileConfiguration? _activeProfile;
        public ServerProfileConfiguration ActiveProfile
        {
            get
            {
                if (string.IsNullOrEmpty(ActiveProfileName))
                    throw new InvalidOperationException();

                if (_activeProfile is not null) return _activeProfile;

                foreach (var profile in Profiles.Where(profile => profile.ProfileName.Equals(ActiveProfileName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    _activeProfile = profile;
                    break;
                }

                if (_activeProfile is not null) return _activeProfile;

                _activeProfile = new ServerProfileConfiguration(ActiveProfileName);
                Profiles.Add(_activeProfile);

                return _activeProfile;
            }
        }

        private const string ENC_SALT = @";n~3+i=wV;eg6Q@f";
        private const string ENC_IV = @"0!.&3x=GGu%>$G&5";   // must be 16/24/32 bytes

        public void Save(string filePath)
        {
            var key = Hasher.HashString(ENC_SALT).Substring(0, 16);
            SerialisationHelper.SerialiseToEncryptedFile(this, filePath, key, ENC_IV);
        }

        public static ServerConfiguration Load(string filePath)
        {
            var key = Hasher.HashString(ENC_SALT).Substring(0, 16);
            return SerialisationHelper.DeserialiseFromEncryptedFile(filePath, key, ENC_IV, new ServerConfiguration());
        }

        public void Normalize()
        {
            foreach (var profile in Profiles)
            {
                profile.Normalise();
            }
        }

        public JsonTypeInfo<ServerConfiguration> GetJsonTypeInfo()
        {
            return SourceGenerationContext.Default.ServerConfiguration;
        }

        internal static string AppDataPath
        {
            get
            {
#if DEBUG
                return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!;
#else
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TinyWall");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
#endif
            }
        }
    } // class
}
