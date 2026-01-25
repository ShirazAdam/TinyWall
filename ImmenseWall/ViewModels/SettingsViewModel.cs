using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ImmenseWall.Properties;
using Microsoft.Win32;

namespace ImmenseWall.ViewModels;

public class ExceptionListItem : ViewModelBase
{
    private FirewallExceptionV3 _exception;
    private bool _isSelected;

    public ExceptionListItem(FirewallExceptionV3 exception, UwpPackageList packageList)
    {
        Exception = exception;

        var exeSubj = exception.Subject as ExecutableSubject;
        var srvSubj = exception.Subject as ServiceSubject;
        var uwpSubj = exception.Subject as AppContainerSubject;

        switch (exception.Subject.SubjectType)
        {
            case SubjectType.Executable:
                ApplicationName = exeSubj!.ExecutableName;
                Type = Resources.Messages.SubjectTypeExecutable;
                Details = exeSubj.ExecutablePath;
                IconKey = exeSubj.ExecutablePath;
                break;
            case SubjectType.Service:
                ApplicationName = srvSubj!.ServiceName;
                Type = Resources.Messages.SubjectTypeService;
                Details = srvSubj.ExecutablePath;
                IconKey = "service";
                break;
            case SubjectType.AppContainer:
                ApplicationName = uwpSubj!.DisplayName;
                Type = Resources.Messages.SubjectTypeUwpApp;
                Details = uwpSubj.PublisherId + ", " + uwpSubj.Publisher;
                IconKey = "store";
                break;
            case SubjectType.Global:
                ApplicationName = Resources.Messages.AllApplications;
                Type = Resources.Messages.SubjectTypeGlobal;
                Details = string.Empty;
                IconKey = "global";
                break;
        }

        LastModified = exception.CreationDate.ToString("yyyy/MM/dd HH:mm");
    }

    public FirewallExceptionV3 Exception
    {
        get => _exception;
        set => SetField(ref _exception, value);
    }

    public string ApplicationName { get; }
    public string Type { get; }
    public string Details { get; }
    public string LastModified { get; }
    public string IconKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}

public class LanguageItem
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class SpecialExceptionItem : ViewModelBase
{
    private bool _isChecked;

    public SpecialExceptionItem(string id, string name, bool isRecommended)
    {
        Id = id;
        Name = name;
        IsRecommended = isRecommended;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsRecommended { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetField(ref _isChecked, value)) OnCheckedChanged?.Invoke(this, value);
        }
    }

    public event Action<SpecialExceptionItem, bool> OnCheckedChanged;
}

public class SettingsViewModel : ViewModelBase
{
    private readonly List<ExceptionListItem> _allExceptions = new();
    private bool _changePassword;
    private string _confirmPassword;
    private string _filterText = string.Empty;
    private string _newPassword;
    private ExceptionListItem _selectedException;
    private LanguageItem _selectedLanguage;
    private int _selectedTabIndex;
    private ConfigContainer _tmpConfig;

    public SettingsViewModel(ServerConfiguration service, ControllerSettings controller)
    {
        _tmpConfig = new ConfigContainer(service, controller);
        _tmpConfig.Service.ActiveProfile.Normalize();

        InitializeLanguages();
        LoadSettings();
        LoadSpecialExceptions();
        LoadApplicationExceptions();

        // Commands
        AddExceptionCommand = new RelayCommand(ExecuteAddException);
        ModifyExceptionCommand = new RelayCommand(ExecuteModifyException, () => SelectedException != null);
        RemoveExceptionCommand = new RelayCommand(ExecuteRemoveException, () => SelectedException != null);
        RemoveAllExceptionsCommand = new RelayCommand(ExecuteRemoveAllExceptions);
        AutoDetectCommand = new RelayCommand(ExecuteAutoDetect);
        ImportCommand = new RelayCommand(ExecuteImport);
        ExportCommand = new RelayCommand(ExecuteExport);
        UpdateCommand = new RelayCommand(ExecuteUpdate);
        WebCommand = new RelayCommand(ExecuteWeb);
        DonateCommand = new RelayCommand(ExecuteDonate);
        GitHubCommand = new RelayCommand(ExecuteGitHub);
        OkCommand = new RelayCommand(ExecuteOk);
        CancelCommand = new RelayCommand(ExecuteCancel);
    }

    public ObservableCollection<LanguageItem> Languages { get; } = new();
    public ObservableCollection<SpecialExceptionItem> RecommendedExceptions { get; } = new();
    public ObservableCollection<SpecialExceptionItem> OptionalExceptions { get; } = new();
    public ObservableCollection<ExceptionListItem> FilteredExceptions { get; } = new();

    public LanguageItem SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetField(ref _selectedLanguage, value);
    }

    public ExceptionListItem SelectedException
    {
        get => _selectedException;
        set => SetField(ref _selectedException, value);
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value)) ApplyExceptionFilter();
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetField(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetField(ref _confirmPassword, value);
    }

    public bool ChangePassword
    {
        get => _changePassword;
        set => SetField(ref _changePassword, value);
    }

    // Settings properties
    public bool AutoUpdateCheck { get; set; }
    public bool AskForExceptionDetails { get; set; }
    public bool EnableHotkeys { get; set; }
    public bool DisplayOffBlock { get; set; }
    public bool LockHostsFile { get; set; }
    public bool EnableHostsBlocklist { get; set; }
    public bool EnablePortBlocklist { get; set; }
    public bool EnableBlocklists { get; set; }

    // Commands
    public ICommand AddExceptionCommand { get; }
    public ICommand ModifyExceptionCommand { get; }
    public ICommand RemoveExceptionCommand { get; }
    public ICommand RemoveAllExceptionsCommand { get; }
    public ICommand AutoDetectCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand WebCommand { get; }
    public ICommand DonateCommand { get; }
    public ICommand GitHubCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    private void InitializeLanguages()
    {
        Languages.Add(new LanguageItem { Id = "auto", Name = "Automatic" });
        Languages.Add(new LanguageItem { Id = "bg", Name = "български" });
        Languages.Add(new LanguageItem { Id = "cs", Name = "Čeština" });
        Languages.Add(new LanguageItem { Id = "de", Name = "Deutsch" });
        Languages.Add(new LanguageItem { Id = "en", Name = "English" });
        Languages.Add(new LanguageItem { Id = "es", Name = "Español" });
        Languages.Add(new LanguageItem { Id = "fr", Name = "Français" });
        Languages.Add(new LanguageItem { Id = "it", Name = "Italiano" });
        Languages.Add(new LanguageItem { Id = "hu", Name = "Magyar" });
        Languages.Add(new LanguageItem { Id = "nl", Name = "Nederlands" });
        Languages.Add(new LanguageItem { Id = "pl", Name = "Polski" });
        Languages.Add(new LanguageItem { Id = "pt-BR", Name = "Português Brasileiro" });
        Languages.Add(new LanguageItem { Id = "ru", Name = "Русский" });
        Languages.Add(new LanguageItem { Id = "tr", Name = "Türkçe" });
        Languages.Add(new LanguageItem { Id = "ja", Name = "日本語" });
        Languages.Add(new LanguageItem { Id = "ko", Name = "한국어" });
        Languages.Add(new LanguageItem { Id = "zh", Name = "汉语" });

        SelectedLanguage = Languages.FirstOrDefault(l =>
                               l.Id.Equals(_tmpConfig.Controller.Language, StringComparison.OrdinalIgnoreCase))
                           ?? Languages.First();
    }

    private void LoadSettings()
    {
        AutoUpdateCheck = _tmpConfig.Service.AutoUpdateCheck;
        AskForExceptionDetails = _tmpConfig.Controller.AskForExceptionDetails;
        EnableHotkeys = _tmpConfig.Controller.EnableGlobalHotkeys;
        DisplayOffBlock = _tmpConfig.Service.ActiveProfile.DisplayOffBlock;
        LockHostsFile = _tmpConfig.Service.LockHostsFile;
        EnableHostsBlocklist = _tmpConfig.Service.Blocklists.EnableHostsBlocklist;
        EnablePortBlocklist = _tmpConfig.Service.Blocklists.EnablePortBlocklist;
        EnableBlocklists = _tmpConfig.Service.Blocklists.EnableBlocklists;
        SelectedTabIndex = _tmpConfig.Controller.SettingsTabIndex;
    }

    private void LoadSpecialExceptions()
    {
        foreach (var app in GlobalInstances.AppDatabase.KnownApplications)
        {
            if (!app.HasFlag("TWUI:Special") || app.HasFlag("TWUI:Hidden")) continue;

            var item = new SpecialExceptionItem(
                app.Name,
                string.IsNullOrEmpty(app.LocalisedName) ? app.Name.Replace('_', ' ') : app.LocalisedName,
                app.HasFlag("TWUI:Recommended"));

            item.IsChecked = _tmpConfig.Service.ActiveProfile.SpecialExceptions.Contains(item.Id);
            item.OnCheckedChanged += OnSpecialExceptionCheckedChanged;

            if (item.IsRecommended)
                RecommendedExceptions.Add(item);
            else
                OptionalExceptions.Add(item);
        }
    }

    private void OnSpecialExceptionCheckedChanged(SpecialExceptionItem item, bool isChecked)
    {
        if (isChecked)
            _tmpConfig.Service.ActiveProfile.SpecialExceptions.Add(item.Id);
        else
            _tmpConfig.Service.ActiveProfile.SpecialExceptions.Remove(item.Id);
    }

    private void LoadApplicationExceptions()
    {
        var packageList = new UwpPackageList();
        _allExceptions.Clear();

        foreach (var ex in _tmpConfig.Service.ActiveProfile.AppExceptions)
            _allExceptions.Add(new ExceptionListItem(ex, packageList));

        ApplyExceptionFilter();
    }

    private void ApplyExceptionFilter()
    {
        FilteredExceptions.Clear();

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            foreach (var item in _allExceptions) FilteredExceptions.Add(item);
        }
        else
        {
            var filterUpper = FilterText.ToUpperInvariant();
            foreach (var item in _allExceptions)
                if (item.ApplicationName.ToUpperInvariant().Contains(filterUpper) ||
                    item.Details.ToUpperInvariant().Contains(filterUpper))
                    FilteredExceptions.Add(item);
        }
    }

    private void ExecuteAddException()
    {
        var viewModel = new ApplicationExceptionViewModel();
        var dialog = new ApplicationExceptionView { DataContext = viewModel };

        if (dialog.ShowDialog() == true && viewModel.ExceptionSettings.Any())
        {
            _tmpConfig.Service.ActiveProfile.AddExceptions(viewModel.ExceptionSettings);
            LoadApplicationExceptions();
        }
    }

    private void ExecuteModifyException()
    {
        if (SelectedException == null) return;

        var clonedException = Utils.DeepClone(SelectedException.Exception);
        clonedException.RegenerateId();

        var viewModel = new ApplicationExceptionViewModel(clonedException);
        var dialog = new ApplicationExceptionView { DataContext = viewModel };

        if (dialog.ShowDialog() == true)
        {
            _tmpConfig.Service.ActiveProfile.AppExceptions.Remove(SelectedException.Exception);
            _tmpConfig.Service.ActiveProfile.AddExceptions(viewModel.ExceptionSettings);
            LoadApplicationExceptions();
        }
    }

    private void ExecuteRemoveException()
    {
        if (SelectedException == null) return;

        _tmpConfig.Service.ActiveProfile.AppExceptions.Remove(SelectedException.Exception);
        LoadApplicationExceptions();
        SelectedException = null;
    }

    private void ExecuteRemoveAllExceptions()
    {
        // Show confirmation dialog
        if (MessageBox.Show(Resources.Messages.AreYouSureYouWantToRemoveAllExceptions,
                Resources.Messages.TinyWall,
                MessageBoxButton.YesNo,
                MessageBoxImage.Exclamation) == MessageBoxResult.No)
            return;

        _tmpConfig.Service.ActiveProfile.AppExceptions.Clear();
        LoadApplicationExceptions();
    }

    private void ExecuteAutoDetect()
    {
        var viewModel = new AppFinderViewModel();
        var dialog = new AppFinderView { DataContext = viewModel };

        if (dialog.ShowDialog() == true && viewModel.SelectedExceptions.Any())
        {
            _tmpConfig.Service.ActiveProfile.AddExceptions(viewModel.SelectedExceptions);
            LoadApplicationExceptions();
        }
    }

    private void ExecuteImport()
    {
        var dialog = new OpenFileDialog
        {
            Filter = string.Format(CultureInfo.CurrentCulture,
                @"{0} (*.tws)|*.tws|{1} (*)|*",
                Resources.Messages.TinyWallSettingsFileFilter,
                Resources.Messages.AllFilesFileFilter)
        };

        if (dialog.ShowDialog() == true)
            try
            {
                _tmpConfig = SerialisationHelper.DeserialiseFromFile(dialog.FileName, new ConfigContainer(), true);
                LoadSettings();
                LoadSpecialExceptions();
                LoadApplicationExceptions();

                MessageBox.Show(Resources.Messages.ConfigurationHasBeenImported,
                    Resources.Messages.TinyWall,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(Resources.Messages.ConfigurationImportError,
                    Resources.Messages.TinyWall,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
    }

    private void ExecuteExport()
    {
        var dialog = new SaveFileDialog
        {
            Filter = string.Format(CultureInfo.CurrentCulture,
                @"{0} (*.tws)|*.tws|{1} (*)|*",
                Resources.Messages.TinyWallSettingsFileFilter,
                Resources.Messages.AllFilesFileFilter),
            DefaultExt = "tws"
        };

        if (dialog.ShowDialog() == true)
        {
            SerialisationHelper.SerialiseToFile(_tmpConfig, dialog.FileName);
            MessageBox.Show(Resources.Messages.ConfigurationHasBeenExported,
                Resources.Messages.TinyWallSettingsFileFilter,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ExecuteUpdate()
    {
        Updater.StartUpdate();
    }

    private void ExecuteWeb()
    {
        Process.Start(new ProcessStartInfo(@"https://tinywall.pados.hu") { UseShellExecute = true });
    }

    private void ExecuteDonate()
    {
        Process.Start(new ProcessStartInfo(@"https://tinywall.pados.hu/donate.php") { UseShellExecute = true });
    }

    private void ExecuteGitHub()
    {
        Process.Start(new ProcessStartInfo(@"https://github.com/ShirazAdam/tinywall") { UseShellExecute = true });
    }

    private void ExecuteOk()
    {
        // Validate password
        if (ChangePassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show(Resources.Messages.PasswordFieldsDoNotMatch,
                    Resources.Messages.TinyWall,
                    MessageBoxButton.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            _tmpConfig.Controller.PasswordHash = Hasher.HashString(NewPassword);
        }

        // Save settings
        _tmpConfig.Controller.AskForExceptionDetails = AskForExceptionDetails;
        _tmpConfig.Controller.EnableGlobalHotkeys = EnableHotkeys;
        _tmpConfig.Service.AutoUpdateCheck = AutoUpdateCheck;
        _tmpConfig.Controller.SettingsTabIndex = SelectedTabIndex;
        _tmpConfig.Service.LockHostsFile = LockHostsFile;
        _tmpConfig.Service.Blocklists.EnablePortBlocklist = EnablePortBlocklist;
        _tmpConfig.Service.Blocklists.EnableHostsBlocklist = EnableHostsBlocklist;
        _tmpConfig.Service.Blocklists.EnableBlocklists = EnableBlocklists;
        _tmpConfig.Service.ActiveProfile.DisplayOffBlock = DisplayOffBlock;
        _tmpConfig.Controller.Language = SelectedLanguage.Id;

        // Close with OK result
    }

    private void ExecuteCancel()
    {
        // Cancel
    }
}