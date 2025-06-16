#if !DEBUG
using System;
#endif
using pylorak.Utilities;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace pylorak.TinyWall
{
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/PKSoft")]
    public sealed class ControllerSettings : ISerializable<ControllerSettings>
    {
        // UI Localization
        [DataMember(EmitDefaultValue = false)]
        public string Language = "auto";

        // Connections window
        [DataMember(EmitDefaultValue = false)]
        public System.Windows.Forms.FormWindowState ConnFormWindowState = System.Windows.Forms.FormWindowState.Normal;
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Point ConnFormWindowLoc = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Size ConnFormWindowSize = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public Dictionary<string, int> ConnFormColumnWidths = new();
        [DataMember(EmitDefaultValue = false)]
        public bool ConnFormShowConnections = true;
        [DataMember(EmitDefaultValue = false)]
        public bool ConnFormShowOpenPorts;
        [DataMember(EmitDefaultValue = false)]
        public bool ConnFormShowBlocked;

        // Processes window
        [DataMember(EmitDefaultValue = false)]
        public System.Windows.Forms.FormWindowState ProcessesFormWindowState = System.Windows.Forms.FormWindowState.Normal;
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Point ProcessesFormWindowLoc = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Size ProcessesFormWindowSize = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public Dictionary<string, int> ProcessesFormColumnWidths = new();

        // Services window
        [DataMember(EmitDefaultValue = false)]
        public System.Windows.Forms.FormWindowState ServicesFormWindowState = System.Windows.Forms.FormWindowState.Normal;
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Point ServicesFormWindowLoc = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Size ServicesFormWindowSize = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public Dictionary<string, int> ServicesFormColumnWidths = new();

        // UwpPackages window
        [DataMember(EmitDefaultValue = false)]
        public System.Windows.Forms.FormWindowState UwpPackagesFormWindowState = System.Windows.Forms.FormWindowState.Normal;
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Point UwpPackagesFormWindowLoc = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Size UwpPackagesFormWindowSize = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public Dictionary<string, int> UwpPackagesFormColumnWidths = new();

        // Manage window
        [DataMember(EmitDefaultValue = false)]
        public bool AskForExceptionDetails;
        [DataMember(EmitDefaultValue = false)]
        public int SettingsTabIndex;
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Point SettingsFormWindowLoc = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public System.Drawing.Size SettingsFormWindowSize = new(0, 0);
        [DataMember(EmitDefaultValue = false)]
        public Dictionary<string, int> SettingsFormAppListColumnWidths = new();

        // Hotkeys
        [DataMember(EmitDefaultValue = false)]
        public bool EnableGlobalHotkeys = true;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc)
        {
            ConnFormColumnWidths ??= new Dictionary<string, int>();
            ProcessesFormColumnWidths ??= new Dictionary<string, int>();
            ServicesFormColumnWidths ??= new Dictionary<string, int>();
            UwpPackagesFormColumnWidths ??= new Dictionary<string, int>();
            SettingsFormAppListColumnWidths ??= new Dictionary<string, int>();
        }

        internal static string UserDataPath
        {
            get
            {
#if DEBUG
                return Path.GetDirectoryName(Utils.ExecutablePath) ?? string.Empty;
#else
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                dir = System.IO.Path.Combine(dir, "TinyWall");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
#endif
            }
        }

        internal static string FilePath => Path.Combine(UserDataPath, "ControllerConfig");

        internal void Save()
        {
            try
            {
                SerialisationHelper.SerialiseToFile(this, FilePath);
            }
            catch
            {
                // ignored
            }
        }

        internal static ControllerSettings Load()
        {
            try
            {
                return SerialisationHelper.DeserialiseFromFile(FilePath, new ControllerSettings());
            }
            catch
            {
                // ignored
            }

            return new ControllerSettings();
        }

        public JsonTypeInfo<ControllerSettings> GetJsonTypeInfo()
        {
            return SourceGenerationContext.Default.ControllerSettings;
        }
    }

    public static class PasswordLock
    {
        internal static string PasswordFilePath { get; } = Path.Combine(Utils.AppDataPath, "pwd");

        private static bool _locked;

        internal static bool Locked
        {
            get => _locked && HasPassword;
            set
            {
                if (value && HasPassword)
                    _locked = true;
            }
        }

        internal static void SetPass(string password)
        {
            // Construct file path
            var settingsFile = PasswordFilePath;

            if (password == string.Empty)
                // If we have no password, delete password explicitly
                File.Delete(settingsFile);
            else
            {
                using var fileUpdater = new AtomicFileUpdater(PasswordFilePath);
                var salt = Utils.RandomString(8);
                var hash = Pbkdf2.GetHashForStorage(password, salt, 150000, 16);
                File.WriteAllText(fileUpdater.TemporaryFilePath, hash, Encoding.UTF8);
                fileUpdater.Commit();
            }
        }

        internal static bool Unlock(string password)
        {
            if (!HasPassword)
                return true;

            try
            {
                var storedHash = System.IO.File.ReadAllText(PasswordFilePath, System.Text.Encoding.UTF8);
                _locked = !Pbkdf2.CompareHash(storedHash, password);
            }
            catch
            {
                // ignored
            }

            return !_locked;
        }

        internal static bool HasPassword
        {
            get
            {
                if (!File.Exists(PasswordFilePath))
                    return false;

                var fi = new FileInfo(PasswordFilePath);
                return (fi.Length != 0);
            }
        }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/PKSoft")]
    public sealed class ConfigContainer : ISerializable<ConfigContainer>
    {
        [DataMember(EmitDefaultValue = false)]
        public ServerConfiguration Service;
        [DataMember(EmitDefaultValue = false)]
        public ControllerSettings Controller;

        public ConfigContainer()
        {
            Service = new ServerConfiguration();
            Controller = new ControllerSettings();
        }

        public ConfigContainer(ServerConfiguration server, ControllerSettings client)
        {
            Service = server;
            Controller = client;
        }

        public JsonTypeInfo<ConfigContainer> GetJsonTypeInfo()
        {
            return SourceGenerationContext.Default.ConfigContainer;
        }
    }

    internal static class ActiveConfig
    {
        [AllowNull]
        internal static ServerConfiguration Service = null;

        [AllowNull]
        internal static ControllerSettings Controller = null;
    }
}
