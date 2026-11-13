using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
#if !TINYWALL_CORE
using System.Windows.Forms;
#endif

namespace ModernTinyWall.TinyWall.DatabaseClasses
{
    [DataContract(Namespace = "TinyWall")]
    class AppDatabase : ISerializable<AppDatabase>
    {
        [DataMember(Name = "KnownApplications")]
        private readonly List<Application> _KnownApplications;

        public static string DBPath
        {
            get
            {
#if DEBUG
                return Path.Combine(AppContext.BaseDirectory, "profiles.db");
#else
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TinyWall", "profiles.db");
#endif
            }
        }

        internal static string LegacyPath => Path.ChangeExtension(DBPath, ".json");
        internal static string SeedPath => Path.Combine(AppContext.BaseDirectory, "catalogue", "profiles.db");
        [JsonIgnore]
        internal string? SourceHash { get; set; }

        public static AppDatabase Load()
        {
            return SqliteApplicationCatalogue.Load(File.Exists(DBPath) ? DBPath : SeedPath);
        }

        public void Save(string filePath)
        {
            SqliteApplicationCatalogue.Save(this, filePath);
        }

        [JsonConstructor]
        public AppDatabase(List<Application> knownApplications)
        {
            _KnownApplications = knownApplications;
        }

        public AppDatabase() :
            this(new List<Application>())
        { }

        public List<Application> KnownApplications
        {
            get { return _KnownApplications; }
        }

        public Application? GetApplicationByName(string name)
        {
            foreach (Application app in _KnownApplications)
            {
                if (app.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return app;
            }

            return null;
        }

        public List<FirewallExceptionV3> FastSearchMachineForKnownApps()
        {
            var ret = new List<FirewallExceptionV3>();

            foreach (DatabaseClasses.Application app in KnownApplications)
            {
                if (app.HasFlag("TWUI:Special"))
                    continue;

                foreach (SubjectIdentity id in app.Components)
                {
                    List<ExceptionSubject> subjects = id.SearchForFile();
                    foreach (var subject in subjects)
                    {
                        ret.Add(id.InstantiateException(subject));
                    }
                }
            }

            return ret;
        }

        internal Application? TryGetApp(ExecutableSubject fromSubject, out FirewallExceptionV3? fwex, bool matchSpecial)
        {
            foreach (var app in KnownApplications)
            {
                if (!matchSpecial && app.HasFlag("TWUI:Special"))
                    continue;

                foreach (var id in app.Components)
                {
                    if (id.DoesExecutableSatisfy(fromSubject))
                    {
                        fwex = id.InstantiateException(fromSubject);
                        return app;
                    }
                }
            }

            fwex = null;
            return null;
        }

        internal List<FirewallExceptionV3> GetExceptionsForApp(ExceptionSubject fromSubject, bool guiPrompt, out Application? app)
        {
            app = null;
            var exceptions = new List<FirewallExceptionV3>();

            if (fromSubject is AppContainerSubject)
            {
                exceptions.Add(new FirewallExceptionV3(fromSubject, new TcpUdpPolicy(true)));
                return exceptions;
            }
            else if (fromSubject is ExecutableSubject exeSubject)
            {
                // Try to find an application this subject might belong to
                app = TryGetApp(exeSubject, out FirewallExceptionV3? _, false);
                if (app == null)
                {
                    exceptions.Add(new FirewallExceptionV3(exeSubject, new TcpUdpPolicy(true)));
                    return exceptions;
                }

                // Now that we have the app, try to instantiate firewall exceptions
                // for all components.
                string? pathHint = System.IO.Path.GetDirectoryName(exeSubject.ExecutablePath);
                foreach (SubjectIdentity id in app.Components)
                {
                    List<ExceptionSubject> foundSubjects = id.SearchForFile(pathHint);
                    foreach (ExceptionSubject subject in foundSubjects)
                    {
                        var tmp = id.InstantiateException(subject);
                        if (fromSubject.Equals(subject))
                            // Make sure original subject is at index 0
                            exceptions.Insert(0, tmp);
                        else
                            exceptions.Add(tmp);
                    }
                }

                // If we have found dependencies, ask the user what to do
#if TINYWALL_CORE
                _ = guiPrompt;
#else
                if ((exceptions.Count > 1) && guiPrompt)
                {

                    // Try to get localized name
                    string localisedAppName = Resources.Exceptions.ResourceManager.GetString(app.Name);
                    localisedAppName = string.IsNullOrWhiteSpace(localisedAppName) ? app.Name : localisedAppName;

                    Utils.SplitFirstLine(string.Format(CultureInfo.InvariantCulture, Resources.Messages.UnblockApp, localisedAppName), out string firstLine, out string contentLines);
                    using var dialogIcon = new TaskDialogIcon(Resources.Icons.firewall);
                    var fileListStr = exceptions.Aggregate(string.Empty, (current, fwex) => current + (fwex.Subject.ToString() + Environment.NewLine));
                    var button1 = new TaskDialogCommandLinkButton(Resources.Messages.UnblockAppUnblockAllRecommended, string.Empty);
                    var button2 = new TaskDialogCommandLinkButton(Resources.Messages.UnblockAppUnblockOnlySelected, string.Empty);
                    var button3 = new TaskDialogCommandLinkButton(Resources.Messages.UnblockAppCancel, string.Empty);
                    var dialog = new TaskDialogPage
                    {
                        Caption = Resources.Messages.TinyWall,
                        Heading = firstLine,
                        Text = contentLines,
                        Icon = dialogIcon,
                        DefaultButton = button1,
                        AllowCancel = false,
                        Expander = new TaskDialogExpander
                        {
                            Text = fileListStr.Trim(),
                            CollapsedButtonText = Resources.Messages.UnblockAppShowRelated,
                            ExpandedButtonText = Resources.Messages.UnblockAppShowRelated
                        }
                    };
                    dialog.Buttons.Add(button1);
                    dialog.Buttons.Add(button2);
                    dialog.Buttons.Add(button3);

                    var result = TaskDialog.ShowDialog(dialog, TaskDialogStartupLocation.CenterScreen);
                    if (result == button2)
                    {
                        // Remove all exceptions with a different subject than the input argument
                        for (var i = exceptions.Count - 1; i >= 0; --i)
                        {
                            if (exceptions[i].Subject is ExecutableSubject exesub
                                && exesub.ExecutablePath.Equals(exeSubject.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                                continue;

                            exceptions.RemoveAt(i);
                        }
                        exceptions.RemoveRange(1, exceptions.Count - 1);
                    }
                    else if (result == button3)
                    {
                        exceptions.Clear();
                    }
                }
#endif
            }
            else
            {
                throw new NotImplementedException();
            }

            return exceptions;
        }

        public JsonTypeInfo<AppDatabase> GetJsonTypeInfo()
        {
            return SourceGenerationContext.Default.AppDatabase;
        }
    }
}
