using Microsoft.Samples;
using pylorak.Utilities;
using pylorak.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal sealed class TinyWallController : ApplicationContext
    {
        #region Vom Windows Form-Designer generierter Code

        private System.ComponentModel.IContainer components = new System.ComponentModel.Container();

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        [MemberNotNull(nameof(Tray),
            nameof(TrayMenu),
            nameof(toolStripMenuItem1),
            nameof(toolStripMenuItem2),
            nameof(mnuQuit),
            nameof(mnuMode),
            nameof(mnuModeNormal),
            nameof(mnuModeBlockAll),
            nameof(mnuModeDisabled),
            nameof(mnuManage),
            nameof(toolStripMenuItem5),
            nameof(mnuWhitelistByExecutable),
            nameof(mnuWhitelistByProcess),
            nameof(mnuWhitelistByWindow),
            nameof(mnuLock),
            nameof(mnuElevate),
            nameof(mnuConnections),
            nameof(mnuModeAllowOutgoing),
            nameof(ofd),
            nameof(toolStripMenuItem3),
            nameof(mnuAllowLocalSubnet),
            nameof(mnuEnableHostsBlocklist),
            nameof(mnuTrafficRate),
            nameof(mnuModeLearn)
        )]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TinyWallController));
            this.Tray = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnuTrafficRate = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuMode = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuModeNormal = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuModeBlockAll = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuModeAllowOutgoing = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuModeDisabled = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuModeLearn = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuManage = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuConnections = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuLock = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuElevate = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuAllowLocalSubnet = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuEnableHostsBlocklist = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuWhitelistByExecutable = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWhitelistByProcess = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWhitelistByWindow = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuQuit = new System.Windows.Forms.ToolStripMenuItem();
            this.ofd = new System.Windows.Forms.OpenFileDialog();
            this.TrayMenu.SuspendLayout();
            //
            // Tray
            //
            resources.ApplyResources(this.Tray, "Tray");
            this.Tray.Icon = global::pylorak.TinyWall.Resources.Icons.firewall;
            this.Tray.Visible = false;
            this.Tray.BalloonTipClicked += new System.EventHandler(this.Tray_BalloonTipClicked);
            this.Tray.MouseClick += new System.Windows.Forms.MouseEventHandler(this.Tray_MouseClick);
            //
            // TrayMenu
            //
            this.TrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuTrafficRate,
            this.toolStripMenuItem1,
            this.mnuMode,
            this.mnuManage,
            this.mnuConnections,
            this.mnuLock,
            this.mnuElevate,
            this.toolStripMenuItem2,
            this.mnuAllowLocalSubnet,
            this.mnuEnableHostsBlocklist,
            this.toolStripMenuItem3,
            this.mnuWhitelistByExecutable,
            this.mnuWhitelistByProcess,
            this.mnuWhitelistByWindow,
            this.toolStripMenuItem5,
            this.mnuQuit});
            this.TrayMenu.Name = "TrayMenu";
            resources.ApplyResources(this.TrayMenu, "TrayMenu");
            this.TrayMenu.Opening += new System.ComponentModel.CancelEventHandler(this.TrayMenu_Opening);
            //
            // mnuTrafficRate
            //
            this.mnuTrafficRate.AccessibleRole = System.Windows.Forms.AccessibleRole.StaticText;
            this.mnuTrafficRate.Image = global::pylorak.TinyWall.Resources.Icons.info;
            this.mnuTrafficRate.Name = "mnuTrafficRate";
            resources.ApplyResources(this.mnuTrafficRate, "mnuTrafficRate");
            //
            // toolStripMenuItem1
            //
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(this.toolStripMenuItem1, "toolStripMenuItem1");
            //
            // mnuMode
            //
            this.mnuMode.AccessibleRole = System.Windows.Forms.AccessibleRole.ButtonMenu;
            this.mnuMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuModeNormal,
            this.mnuModeBlockAll,
            this.mnuModeAllowOutgoing,
            this.mnuModeDisabled,
            this.mnuModeLearn});
            this.mnuMode.Name = "mnuMode";
            resources.ApplyResources(this.mnuMode, "mnuMode");
            //
            // mnuModeNormal
            //
            this.mnuModeNormal.Name = "mnuModeNormal";
            resources.ApplyResources(this.mnuModeNormal, "mnuModeNormal");
            this.mnuModeNormal.Click += new System.EventHandler(this.mnuModeNormal_Click);
            //
            // mnuModeBlockAll
            //
            this.mnuModeBlockAll.Name = "mnuModeBlockAll";
            resources.ApplyResources(this.mnuModeBlockAll, "mnuModeBlockAll");
            this.mnuModeBlockAll.Click += new System.EventHandler(this.mnuModeBlockAll_Click);
            //
            // mnuModeAllowOutgoing
            //
            this.mnuModeAllowOutgoing.Name = "mnuModeAllowOutgoing";
            resources.ApplyResources(this.mnuModeAllowOutgoing, "mnuModeAllowOutgoing");
            this.mnuModeAllowOutgoing.Click += new System.EventHandler(this.mnuAllowOutgoing_Click);
            //
            // mnuModeDisabled
            //
            this.mnuModeDisabled.Name = "mnuModeDisabled";
            resources.ApplyResources(this.mnuModeDisabled, "mnuModeDisabled");
            this.mnuModeDisabled.Click += new System.EventHandler(this.mnuModeDisabled_Click);
            //
            // mnuModeLearn
            //
            this.mnuModeLearn.Name = "mnuModeLearn";
            resources.ApplyResources(this.mnuModeLearn, "mnuModeLearn");
            this.mnuModeLearn.Click += new System.EventHandler(this.mnuModeLearn_Click);
            //
            // mnuManage
            //
            this.mnuManage.Image = global::pylorak.TinyWall.Resources.Icons.manage;
            this.mnuManage.Name = "mnuManage";
            resources.ApplyResources(this.mnuManage, "mnuManage");
            this.mnuManage.Click += new System.EventHandler(this.mnuManage_Click);
            //
            // mnuConnections
            //
            this.mnuConnections.Image = global::pylorak.TinyWall.Resources.Icons.connections;
            this.mnuConnections.Name = "mnuConnections";
            resources.ApplyResources(this.mnuConnections, "mnuConnections");
            this.mnuConnections.Click += new System.EventHandler(this.mnuConnections_Click);
            //
            // mnuLock
            //
            this.mnuLock.Image = global::pylorak.TinyWall.Resources.Icons.lock_small;
            this.mnuLock.Name = "mnuLock";
            resources.ApplyResources(this.mnuLock, "mnuLock");
            this.mnuLock.Click += new System.EventHandler(this.mnuLock_Click);
            //
            // mnuElevate
            //
            this.mnuElevate.Image = global::pylorak.TinyWall.Resources.Icons.w7uacshield;
            this.mnuElevate.Name = "mnuElevate";
            resources.ApplyResources(this.mnuElevate, "mnuElevate");
            this.mnuElevate.Click += new System.EventHandler(this.mnuElevate_Click);
            //
            // toolStripMenuItem2
            //
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            resources.ApplyResources(this.toolStripMenuItem2, "toolStripMenuItem2");
            //
            // mnuAllowLocalSubnet
            //
            this.mnuAllowLocalSubnet.Name = "mnuAllowLocalSubnet";
            resources.ApplyResources(this.mnuAllowLocalSubnet, "mnuAllowLocalSubnet");
            this.mnuAllowLocalSubnet.Click += new System.EventHandler(this.mnuAllowLocalSubnet_Click);
            //
            // mnuEnableHostsBlocklist
            //
            this.mnuEnableHostsBlocklist.Name = "mnuEnableHostsBlocklist";
            resources.ApplyResources(this.mnuEnableHostsBlocklist, "mnuEnableHostsBlocklist");
            this.mnuEnableHostsBlocklist.Click += new System.EventHandler(this.mnuEnableHostsBlocklist_Click);
            //
            // toolStripMenuItem3
            //
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            resources.ApplyResources(this.toolStripMenuItem3, "toolStripMenuItem3");
            //
            // mnuWhitelistByExecutable
            //
            this.mnuWhitelistByExecutable.Image = global::pylorak.TinyWall.Resources.Icons.executable;
            this.mnuWhitelistByExecutable.Name = "mnuWhitelistByExecutable";
            resources.ApplyResources(this.mnuWhitelistByExecutable, "mnuWhitelistByExecutable");
            this.mnuWhitelistByExecutable.Click += new System.EventHandler(this.mnuWhitelistByExecutable_Click);
            //
            // mnuWhitelistByProcess
            //
            this.mnuWhitelistByProcess.Image = global::pylorak.TinyWall.Resources.Icons.process;
            this.mnuWhitelistByProcess.Name = "mnuWhitelistByProcess";
            resources.ApplyResources(this.mnuWhitelistByProcess, "mnuWhitelistByProcess");
            this.mnuWhitelistByProcess.Click += new System.EventHandler(this.mnuWhitelistByProcess_Click);
            //
            // mnuWhitelistByWindow
            //
            this.mnuWhitelistByWindow.Image = global::pylorak.TinyWall.Resources.Icons.window;
            this.mnuWhitelistByWindow.Name = "mnuWhitelistByWindow";
            resources.ApplyResources(this.mnuWhitelistByWindow, "mnuWhitelistByWindow");
            this.mnuWhitelistByWindow.Click += new System.EventHandler(this.mnuWhitelistByWindow_Click);
            //
            // toolStripMenuItem5
            //
            this.toolStripMenuItem5.Name = "toolStripMenuItem5";
            resources.ApplyResources(this.toolStripMenuItem5, "toolStripMenuItem5");
            //
            // mnuQuit
            //
            this.mnuQuit.Image = global::pylorak.TinyWall.Resources.Icons.exit;
            this.mnuQuit.Name = "mnuQuit";
            resources.ApplyResources(this.mnuQuit, "mnuQuit");
            this.mnuQuit.Click += new System.EventHandler(this.mnuQuit_Click);
            //
            // ofd
            //
            resources.ApplyResources(this.ofd, "ofd");
            this.TrayMenu.ResumeLayout(false);
        }

        private System.Windows.Forms.NotifyIcon Tray;
        private System.Windows.Forms.ContextMenuStrip TrayMenu;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem mnuQuit;
        private System.Windows.Forms.ToolStripMenuItem mnuMode;
        private System.Windows.Forms.ToolStripMenuItem mnuModeNormal;
        private System.Windows.Forms.ToolStripMenuItem mnuModeBlockAll;
        private System.Windows.Forms.ToolStripMenuItem mnuModeDisabled;
        private System.Windows.Forms.ToolStripMenuItem mnuManage;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem mnuWhitelistByExecutable;
        private System.Windows.Forms.ToolStripMenuItem mnuWhitelistByProcess;
        private System.Windows.Forms.ToolStripMenuItem mnuWhitelistByWindow;
        private System.Windows.Forms.ToolStripMenuItem mnuLock;
        private System.Windows.Forms.ToolStripMenuItem mnuElevate;
        private System.Windows.Forms.ToolStripMenuItem mnuConnections;
        private System.Windows.Forms.ToolStripMenuItem mnuModeAllowOutgoing;
        private System.Windows.Forms.OpenFileDialog ofd;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem mnuAllowLocalSubnet;
        private System.Windows.Forms.ToolStripMenuItem mnuEnableHostsBlocklist;
        private System.Windows.Forms.ToolStripMenuItem mnuTrafficRate;
        private System.Windows.Forms.ToolStripMenuItem mnuModeLearn;

        #endregion

        private readonly MouseInterceptor _mouseInterceptor = new();
        private readonly System.Threading.Timer _updateTimer;
        private readonly System.Windows.Forms.Timer _serviceTimer;
        private readonly DateTime _appStarted = DateTime.Now;
        private readonly List<Form> _activeForms = new();
        private ServerState _firewallState = new();

        // Traffic rate monitoring
        private readonly System.Threading.Timer _trafficTimer;
        private readonly TrafficRateMonitor _trafficMonitor = new();
        private bool _trafficRateVisible = true;
        private bool _trayMenuShowing;

        private EventHandler<AnyEventArgs>? _balloonClickedCallback;
        private object? _balloonClickedCallbackArgument;
        [AllowNull]
        private SynchronizationContext _syncCtx;

        private Hotkey? _hotKeyWhitelistExecutable;
        private Hotkey? _hotKeyWhitelistProcess;
        private Hotkey? _hotKeyWhitelistWindow;

        private readonly CmdLineArgs _startupOpts;

        private bool _mLocked;

        private bool Locked
        {
            get => _mLocked;
            set
            {
                _mLocked = value;
                _firewallState.Locked = value;
                if (_mLocked)
                {
                    mnuLock.Text = Resources.Messages.Unlock;
                    mnuLock.Visible = false;
                }
                else
                {
                    mnuLock.Text = Resources.Messages.Lock;
                    mnuLock.Visible = _firewallState.HasPassword;
                }
            }
        }

        public TinyWallController(CmdLineArgs opts)
        {
            this._startupOpts = opts;

            ActiveConfig.Controller = ControllerSettings.Load();
            try
            {
                if (!ActiveConfig.Controller.Language.Equals("auto", StringComparison.InvariantCultureIgnoreCase))
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(ActiveConfig.Controller.Language);
                    System.Windows.Forms.Application.CurrentCulture = Thread.CurrentThread.CurrentUICulture;
                }
                else
                {
                    Thread.CurrentThread.CurrentUICulture = Program.DefaultOsCulture;
                    System.Windows.Forms.Application.CurrentCulture = Program.DefaultOsCulture;
                }
            }
            catch
            {
                // ignored
            }

            InitializeComponent();
            Utils.SetRightToLeft(TrayMenu);
            _mouseInterceptor.MouseLButtonDown += new MouseInterceptor.MouseHookLButtonDown(MouseInterceptor_MouseLButtonDown);
            _trafficTimer = new System.Threading.Timer(TrafficTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            _updateTimer = new System.Threading.Timer(UpdateTimerTick, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(240));
            _serviceTimer = new System.Windows.Forms.Timer(components);

            System.Windows.Forms.Application.Idle += Application_Idle;
            using var p = Process.GetCurrentProcess();
            ProcessManager.WakeMessageQueues(p);
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (_syncCtx != null) return;

            _syncCtx = SynchronizationContext.Current;
            System.Windows.Forms.Application.Idle -= Application_Idle;
            InitController();
        }

        private void TrayMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            TrayMenuShowing = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                // Manually added
                _hotKeyWhitelistExecutable?.Dispose();
                _hotKeyWhitelistProcess?.Dispose();
                _hotKeyWhitelistWindow?.Dispose();
                _mouseInterceptor.Dispose();

                using (WaitHandle wh = new AutoResetEvent(false))
                {
                    _updateTimer.Dispose(wh);
                    wh.WaitOne();
                }

                using (WaitHandle wh = new AutoResetEvent(false))
                {
                    _trafficTimer.Dispose(wh);
                    wh.WaitOne();
                }
                _trafficMonitor?.Dispose();

                components.Dispose();
                PathMapper.Instance.Dispose();
            }

            base.Dispose(disposing);
        }

        private void VerifyUpdates()
        {
            try
            {
                UpdateDescriptor? descriptor = _firewallState.Update;
                if (descriptor is null) return;

                UpdateModule mainAppModule = UpdateChecker.GetMainAppModule(descriptor)!;

                if (mainAppModule.ComponentVersion != null && new Version(mainAppModule.ComponentVersion) > new Version(System.Windows.Forms.Application.ProductVersion))
                {
                    Utils.Invoke(_syncCtx, (SendOrPostCallback)delegate (object o)
                    {
                        string prompt = string.Format(CultureInfo.CurrentCulture, pylorak.TinyWall.Resources.Messages.UpdateAvailableBubble, mainAppModule.ComponentVersion);
                        ShowBalloonTip(prompt, ToolTipIcon.Info, 5000, StartUpdate, mainAppModule.UpdateURL);
                    });
                }
            }
            catch
            {
                // This is an automatic update check in the background.
                // If we fail (for whatever reason, no internet, server down etc.),
                // we fail silently.
            }
        }

        private void UpdateTimerTick(object state)
        {
            if (ActiveConfig.Service.AutoUpdateCheck)
            {
                ThreadPool.QueueUserWorkItem((WaitCallback)delegate (object dummy)
                {
                    VerifyUpdates();
                });
            }
        }

        private void TrafficTimerTick(object? _)
        {
            if (!Monitor.TryEnter(_trafficTimer))
                return;

            try
            {
                _trafficMonitor.Update();
                UpdateTrafficRateText(_trafficMonitor.BytesReceivedPerSec, _trafficMonitor.BytesSentPerSec);
                TrafficRateVisible = true;
            }
            catch
            {
                TrafficRateVisible = false;
            }
            finally
            {
                Monitor.Exit(_trafficTimer);
            }
        }

        void UpdateTrafficRateText(long rxRate, long txRate)
        {
            if (!TrayMenuShowing || !TrafficRateVisible) return;

            float kBytesRxPerSec = (float)rxRate / 1024;
            float kBytesTxPerSec = (float)txRate / 1024;
            float mBytesRxPerSec = kBytesRxPerSec / 1024;
            float mBytesTxPerSec = kBytesTxPerSec / 1024;

            string rxDisplay = (mBytesRxPerSec > 1)
                ? string.Format(CultureInfo.CurrentCulture, "{0:f} MiB/s", mBytesRxPerSec)
                : string.Format(CultureInfo.CurrentCulture, "{0:f} KiB/s", kBytesRxPerSec);

            string txDisplay = (mBytesTxPerSec > 1)
                ? string.Format(CultureInfo.CurrentCulture, "{0:f} MiB/s", mBytesTxPerSec)
                : string.Format(CultureInfo.CurrentCulture, "{0:f} KiB/s", kBytesTxPerSec);

            var trafficRateText = string.Format(CultureInfo.CurrentCulture, "{0}: {1}    {2}: {3}", Resources.Messages.TrafficIn, rxDisplay, Resources.Messages.TrafficOut, txDisplay);

            Utils.Invoke(TrayMenu, (MethodInvoker)delegate
            {
                mnuTrafficRate.Text = trafficRateText;
            });
        }

        private bool TrayMenuShowing
        {
            get => _trayMenuShowing;
            set
            {
                _trayMenuShowing = value;

                // Update more often while visible
                if ((_trafficMonitor != null) && _trayMenuShowing)
                {
                    TrafficTimerTick(null);
                    _trafficTimer.Change(2000, 2000);
                }
                else
                    _trafficTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private bool TrafficRateVisible
        {
            get => _trafficRateVisible;
            set
            {
                if (value == _trafficRateVisible) return;

                _trafficRateVisible = value;
                Utils.Invoke(TrayMenu, (MethodInvoker)delegate
                {
                    mnuTrafficRate.Visible = _trafficRateVisible;
                    toolStripMenuItem1.Visible = _trafficRateVisible;
                });
            }
        }

        private void StartUpdate(object sender, AnyEventArgs e)
        {
            Updater.StartUpdate();
        }

        private void HotKeyWhitelistProcess_Pressed(object sender, HandledEventArgs e)
        {
            mnuWhitelistByProcess_Click(this, EventArgs.Empty);
        }

        private void HotKeyWhitelistExecutable_Pressed(object sender, HandledEventArgs e)
        {
            mnuWhitelistByExecutable_Click(this, EventArgs.Empty);
        }

        private void HotKeyWhitelistWindow_Pressed(object sender, HandledEventArgs e)
        {
            mnuWhitelistByWindow_Click(this, EventArgs.Empty);
        }

        private void mnuQuit_Click(object sender, EventArgs e)
        {
            Tray.Visible = false;
            ExitThread();
        }

        private void UpdateDisplay()
        {
            // Update UI based on current firewall mode
            string firewallModeName = Resources.Messages.FirewallModeUnknown;

            switch (_firewallState.Mode)
            {
                case FirewallMode.Normal:
                    Tray.Icon = Resources.Icons.firewall;
                    mnuMode.Image = mnuModeNormal.Image;
                    firewallModeName = Resources.Messages.FirewallModeNormal;
                    break;

                case FirewallMode.AllowOutgoing:
                    Tray.Icon = Resources.Icons.shield_red_small;
                    mnuMode.Image = mnuModeAllowOutgoing.Image;
                    firewallModeName = Resources.Messages.FirewallModeAllowOut;
                    break;

                case FirewallMode.BlockAll:
                    Tray.Icon = Resources.Icons.shield_yellow_small;
                    mnuMode.Image = mnuModeBlockAll.Image;
                    firewallModeName = Resources.Messages.FirewallModeBlockAll;
                    break;

                case FirewallMode.Disabled:
                    Tray.Icon = Resources.Icons.shield_grey_small;
                    mnuMode.Image = mnuModeDisabled.Image;
                    firewallModeName = Resources.Messages.FirewallModeDisabled;
                    break;

                case FirewallMode.Learning:
                    Tray.Icon = Resources.Icons.shield_blue_small;
                    mnuMode.Image = mnuModeLearn.Image;
                    firewallModeName = Resources.Messages.FirewallModeLearn;
                    break;

                case FirewallMode.Unknown:
                    Tray.Icon = Resources.Icons.shield_grey_small;
                    mnuMode.Image = Resources.Icons.shield_grey_small.ToBitmap();
                    firewallModeName = Resources.Messages.FirewallModeUnknown;
                    break;
                default:
                    //throw new ArgumentOutOfRangeException();
                    break;
            }

            Tray.Text = string.Format(CultureInfo.CurrentCulture, @"TinyWall
{0}: {1}",
                Resources.Messages.Mode, firewallModeName);

            // Find out if we are locked and if we have a password
            this.Locked = _firewallState.Locked;

            mnuAllowLocalSubnet.Checked = ActiveConfig.Service.ActiveProfile.AllowLocalSubnet;
            mnuEnableHostsBlocklist.Checked = ActiveConfig.Service.Blocklists.EnableBlocklists;
        }

        private void SetMode(FirewallMode mode)
        {
            MessageType resp = GlobalInstances.Controller.SwitchFirewallMode(mode);
            var userMessage = mode switch
            {
                FirewallMode.Normal => Resources.Messages.TheFirewallIsNowOperatingAsRecommended,
                FirewallMode.AllowOutgoing => Resources.Messages.TheFirewallIsNowAllowsOutgoingConnections,
                FirewallMode.BlockAll => Resources.Messages.TheFirewallIsNowBlockingAllInAndOut,
                FirewallMode.Disabled => Resources.Messages.TheFirewallIsNowDisabled,
                FirewallMode.Learning => Resources.Messages.TheFirewallIsNowLearning,
                _ => string.Empty
            };

            switch (resp)
            {
                case MessageType.MODE_SWITCH:
                    _firewallState.Mode = mode;
                    ShowBalloonTip(userMessage, ToolTipIcon.Info);
                    break;
                case MessageType.INVALID_COMMAND:
                    break;
                case MessageType.RESPONSE_ERROR:
                    break;
                case MessageType.RESPONSE_LOCKED:
                    break;
                case MessageType.COM_ERROR:
                    break;
                case MessageType.GET_SETTINGS:
                    break;
                case MessageType.GET_PROCESS_PATH:
                    break;
                case MessageType.READ_FW_LOG:
                    break;
                case MessageType.IS_LOCKED:
                    break;
                case MessageType.UNLOCK:
                    break;
                case MessageType.REINIT:
                    break;
                case MessageType.PUT_SETTINGS:
                    break;
                case MessageType.LOCK:
                    break;
                case MessageType.SET_PASSPHRASE:
                    break;
                case MessageType.STOP_SERVICE:
                    break;
                case MessageType.MINUTE_TIMER:
                    break;
                case MessageType.REENUMERATE_ADDRESSES:
                    break;
                case MessageType.DATABASE_UPDATED:
                    break;
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    break;
                case MessageType.RELOAD_WFP_FILTERS:
                    break;
                case MessageType.DISPLAY_POWER_EVENT:
                    break;
                default:
                    DefaultPopups(resp);
                    break;
            }
        }

        private void mnuModeDisabled_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            SetMode(FirewallMode.Disabled);
            UpdateDisplay();
        }

        private void mnuModeNormal_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            SetMode(FirewallMode.Normal);
            UpdateDisplay();
        }

        private void mnuModeBlockAll_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            SetMode(FirewallMode.BlockAll);
            UpdateDisplay();
        }

        private void mnuAllowOutgoing_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            SetMode(FirewallMode.AllowOutgoing);
            UpdateDisplay();
        }

        // Returns true if the local copy of the settings have been updated.
        private bool LoadSettingsFromServer()
        {
            return LoadSettingsFromServer(out bool _, false);
        }

        // Returns true if the local copy of the settings have been updated.
        private bool LoadSettingsFromServer(out bool comError, bool force = false)
        {
            Guid inChangeset = force ? Guid.Empty : GlobalInstances.ClientChangeset;
            Guid outChangeset = inChangeset;
            MessageType ret = GlobalInstances.Controller.GetServerConfig(out ServerConfiguration? config, out ServerState? state, ref outChangeset);

            comError = (MessageType.COM_ERROR == ret);
            bool updated = (inChangeset != outChangeset);

            if (MessageType.GET_SETTINGS == ret)
            {
                // Update our config based on what we received
                GlobalInstances.ClientChangeset = outChangeset;
                if (config is not null)
                    ActiveConfig.Service = config;
                if (state is not null)
                    _firewallState = state;
            }
            else
            {
                ActiveConfig.Controller = new ControllerSettings();
                ActiveConfig.Service = new ServerConfiguration
                {
                    ActiveProfileName = Resources.Messages.Default
                };
            }

            // See if there is a new notification for the client
            foreach (var t in _firewallState.ClientNotifs)
            {
                switch (t)
                {
                    case MessageType.DATABASE_UPDATED:
                        LoadDatabase();
                        break;
                    case MessageType.INVALID_COMMAND:
                        break;
                    case MessageType.RESPONSE_ERROR:
                        break;
                    case MessageType.RESPONSE_LOCKED:
                        break;
                    case MessageType.COM_ERROR:
                        break;
                    case MessageType.GET_SETTINGS:
                        break;
                    case MessageType.GET_PROCESS_PATH:
                        break;
                    case MessageType.READ_FW_LOG:
                        break;
                    case MessageType.IS_LOCKED:
                        break;
                    case MessageType.UNLOCK:
                        break;
                    case MessageType.MODE_SWITCH:
                        break;
                    case MessageType.REINIT:
                        break;
                    case MessageType.PUT_SETTINGS:
                        break;
                    case MessageType.LOCK:
                        break;
                    case MessageType.SET_PASSPHRASE:
                        break;
                    case MessageType.STOP_SERVICE:
                        break;
                    case MessageType.MINUTE_TIMER:
                        break;
                    case MessageType.REENUMERATE_ADDRESSES:
                        break;
                    case MessageType.ADD_TEMPORARY_EXCEPTION:
                        break;
                    case MessageType.RELOAD_WFP_FILTERS:
                        break;
                    case MessageType.DISPLAY_POWER_EVENT:
                        break;
                    default:
                        //throw new ArgumentOutOfRangeException();
                        break;
                }
            }

            _firewallState.ClientNotifs.Clear();

            if (updated)
                UpdateDisplay();

            return updated;
        }

        private void TrayMenu_Opening(object sender, CancelEventArgs e)
        {
            e.Cancel = false;

            if (_firewallState.Mode == FirewallMode.Unknown)
            {
                if (!TinyWallDoctor.IsServiceRunning(Utils.LOG_ID_GUI, false))
                {
                    ShowBalloonTip(Resources.Messages.TheTinyWallServiceIsUnavailable, ToolTipIcon.Error, 10000);
                    e.Cancel = true;
                }
            }

            TrayMenuShowing = true;

            this.Locked = GlobalInstances.Controller.IsServerLocked;
            UpdateDisplay();
        }

        private void mnuWhitelistByExecutable_Click(object sender, EventArgs e)
        {
            if (FlashIfOpen(typeof(SettingsForm)))
                return;

            if (!EnsureUnlockedServer())
                return;

            using var dummy = new Form();
            try
            {
                _activeForms.Add(dummy);
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
            }
            finally
            {
                _activeForms.Remove(dummy);
            }

            var subj = new ExecutableSubject(PathMapper.Instance.ConvertPathIgnoreErrors(ofd.FileName, PathFormat.Win32));
            AddExceptions(GlobalInstances.AppDatabase.GetExceptionsForApp(subj, true, out _));
        }

        public void WhitelistProcesses(List<ProcessInfo> list)
        {
            var exceptions = new List<FirewallExceptionV3>();

            foreach (var sel in list)
            {
                if (string.IsNullOrEmpty(sel.Path))
                    continue;

                var subjects = new List<ExceptionSubject>();
                if (sel.Package.HasValue)
                    subjects.Add(new AppContainerSubject(sel.Package.Value));
                else if (sel.Services.Count > 0)
                {
                    subjects.AddRange(sel.Services.Select(srv => new ServiceSubject(sel.Path, srv)).Cast<ExceptionSubject>());
                }
                else
                    subjects.Add(new ExecutableSubject(sel.Path));

                foreach (var subj in from subj in subjects let found = exceptions.Any(ex => ex.Subject.Equals(subj)) where !found select subj)
                {
                    // Try to recognize app based on this file
                    exceptions.AddRange(GlobalInstances.AppDatabase.GetExceptionsForApp(subj, true, out _));
                }
            }

            AddExceptions(exceptions);
        }

        private void mnuWhitelistByProcess_Click(object sender, EventArgs e)
        {
            if (FlashIfOpen(typeof(SettingsForm)))
                return;

            if (!EnsureUnlockedServer())
                return;

            var selection = new List<ProcessInfo>();

            using (var pf = new ProcessesForm(true))
            {
                try
                {
                    _activeForms.Add(pf);

                    if (pf.ShowDialog(null) == DialogResult.Cancel)
                        return;

                    selection.AddRange(pf.Selection);
                }
                finally
                {
                    _activeForms.Remove(pf);
                }
            }
            WhitelistProcesses(selection);
        }

        internal TwMessage ApplyFirewallSettings(ServerConfiguration srvConfig, bool showUI = true)
        {
            if (!EnsureUnlockedServer(showUI))
                return TwMessageLocked.Instance;

            var resp = GlobalInstances.Controller.SetServerConfig(srvConfig, GlobalInstances.ClientChangeset);

            switch (resp.Type)
            {
                case MessageType.PUT_SETTINGS:
                    var respArgs = (TwMessagePutSettings)resp;
                    if (respArgs.State is not null)
                        _firewallState = respArgs.State;
                    ActiveConfig.Service = respArgs.Config;
                    GlobalInstances.ClientChangeset = respArgs.Changeset;
                    if (showUI)
                    {
                        if (respArgs.Warning)
                            ShowBalloonTip(Resources.Messages.SettingHaveChangedRetry, ToolTipIcon.Warning);
                        else
                            ShowBalloonTip(Resources.Messages.TheFirewallSettingsHaveBeenUpdated, ToolTipIcon.Info);
                    }
                    break;
                case MessageType.RESPONSE_ERROR:
                    if (showUI)
                        ShowBalloonTip(Resources.Messages.CouldNotApplySettingsInternalError, ToolTipIcon.Warning);
                    break;
                case MessageType.INVALID_COMMAND:
                    break;
                case MessageType.RESPONSE_LOCKED:
                    break;
                case MessageType.COM_ERROR:
                    break;
                case MessageType.GET_SETTINGS:
                    break;
                case MessageType.GET_PROCESS_PATH:
                    break;
                case MessageType.READ_FW_LOG:
                    break;
                case MessageType.IS_LOCKED:
                    break;
                case MessageType.UNLOCK:
                    break;
                case MessageType.MODE_SWITCH:
                    break;
                case MessageType.REINIT:
                    break;
                case MessageType.LOCK:
                    break;
                case MessageType.SET_PASSPHRASE:
                    break;
                case MessageType.STOP_SERVICE:
                    break;
                case MessageType.MINUTE_TIMER:
                    break;
                case MessageType.REENUMERATE_ADDRESSES:
                    break;
                case MessageType.DATABASE_UPDATED:
                    break;
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    break;
                case MessageType.RELOAD_WFP_FILTERS:
                    break;
                case MessageType.DISPLAY_POWER_EVENT:
                    break;
                default:
                    if (showUI)
                        DefaultPopups(resp.Type);
                    LoadSettingsFromServer();
                    break;
            }

            return resp;
        }

        private void DefaultPopups(MessageType op)
        {
            switch (op)
            {
                case MessageType.INVALID_COMMAND:
                    break;
                case MessageType.GET_SETTINGS:
                    break;
                case MessageType.GET_PROCESS_PATH:
                    break;
                case MessageType.READ_FW_LOG:
                    break;
                case MessageType.IS_LOCKED:
                    break;
                case MessageType.UNLOCK:
                    break;
                case MessageType.MODE_SWITCH:
                    break;
                case MessageType.REINIT:
                    break;
                case MessageType.PUT_SETTINGS:
                    break;
                case MessageType.LOCK:
                    break;
                case MessageType.SET_PASSPHRASE:
                    break;
                case MessageType.STOP_SERVICE:
                    break;
                case MessageType.MINUTE_TIMER:
                    break;
                case MessageType.REENUMERATE_ADDRESSES:
                    break;
                case MessageType.DATABASE_UPDATED:
                    break;
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    break;
                case MessageType.RELOAD_WFP_FILTERS:
                    break;
                case MessageType.DISPLAY_POWER_EVENT:
                    break;
                default:
                    ShowBalloonTip(Resources.Messages.Success, ToolTipIcon.Info);
                    break;
                case MessageType.RESPONSE_ERROR:
                    ShowBalloonTip(Resources.Messages.OperationFailed, ToolTipIcon.Error);
                    break;
                case MessageType.RESPONSE_LOCKED:
                    ShowBalloonTip(Resources.Messages.TinyWallIsCurrentlyLocked, ToolTipIcon.Warning);
                    break;
                case MessageType.COM_ERROR:
                    ShowBalloonTip(Resources.Messages.CommunicationWithTheServiceError, ToolTipIcon.Error);
                    break;
            }
        }

        public bool FlashIfOpen(Type formType)
        {
            foreach (var openForm in _activeForms.Where(openForm => openForm.GetType() == formType))
            {
                openForm.Activate();
                openForm.BringToFront();
                WindowFlasher.Flash(openForm.Handle, 2);
                return true;
            }

            return false;
        }
        public bool FlashIfOpen(Form frm)
        {
            return FlashIfOpen(frm.GetType());
        }

        private void mnuManage_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            // The settings form should not be used with other windows at the same time
            if (_activeForms.Count != 0)
            {
                FlashIfOpen(_activeForms[0]);
                return;
            }

            LoadSettingsFromServer();

            using var sf = new SettingsForm(Utils.DeepClone(ActiveConfig.Service), Utils.DeepClone(ActiveConfig.Controller));
            _activeForms.Add(sf);
            try
            {
                if (sf.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var oldLang = ActiveConfig.Controller.Language;

                // Save settings
                ActiveConfig.Controller = sf.TmpConfig.Controller;
                ActiveConfig.Controller.Save();
                ApplyFirewallSettings(sf.TmpConfig.Service);

                // Handle password change request
                string? newPassword = sf.NewPassword;
                if (newPassword is not null)
                {
                    // If the new password is empty, we do not hash it because an empty password
                    // is a special value signalizing the non-existence of a password.
                    MessageType resp = GlobalInstances.Controller.SetPassphrase(string.IsNullOrEmpty(newPassword) ? string.Empty : Hasher.HashString(newPassword));
                    if (resp != MessageType.SET_PASSPHRASE)
                    {
                        // Only display a popup for setting the password if it did not succeed
                        DefaultPopups(resp);
                        return;
                    }
                    else
                    {
                        // If the operation is successfull, do not report anything as we will be setting
                        // the other settings too and we want to avoid multiple popups.
                        _firewallState.HasPassword = !string.IsNullOrEmpty(newPassword);
                    }
                }

                if (oldLang == ActiveConfig.Controller.Language) return;

                Program.RestartOnQuit = true;
                ExitThread();
            }
            finally
            {
                _activeForms.Remove(sf);
                ApplyControllerSettings();
                UpdateDisplay();
            }
        }

        private void mnuWhitelistByWindow_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            if (!_mouseInterceptor.IsStarted)
            {
                _mouseInterceptor.Start();
                ShowBalloonTip(Resources.Messages.ClickOnAWindowWhitelisting, ToolTipIcon.Info);
            }
            else
            {
                _mouseInterceptor.Stop();
                ShowBalloonTip(Resources.Messages.WhitelistingCancelled, ToolTipIcon.Info);
            }
        }

        internal void MouseInterceptor_MouseLButtonDown(int x, int y)
        {
            // So, this looks crazy, doesn't it?
            // Call a method in a parallel thread just so that it can be called
            // on this same thread again?
            //
            // The point is, the body will execute on this same thread *after* this procedure
            // has terminated. We want this procedure to terminate before
            // calling MouseInterceptor.Dispose() or else it will lock up our UI thread for a
            // couple of seconds. It will lock up because we are currently running in a hook procedure,
            // and MouseInterceptor.Dispose() unhooks us while we are running.
            // This apparently brings Windows temporarily to its knees. Anyway, starting
            // another thread that will invoke the body on our own thread again makes sure that the hook
            // has terminated by the time we unhook it, resolving all our problems.

            ThreadPool.QueueUserWorkItem((WaitCallback)delegate (object state)
            {
                Utils.Invoke(_syncCtx, (SendOrPostCallback)delegate (object o)
                {
                    _mouseInterceptor.Stop();

                    uint pid = Utils.GetPidUnderCursor(x, y);
                    string exePath = Utils.GetPathOfProcessUseTwService(pid, GlobalInstances.Controller);
                    var packageList = new UwpPackageList();
                    var appContainer = packageList.FindPackageForProcess(pid);

                    ExceptionSubject subj;
                    if (appContainer.HasValue)
                    {
                        subj = new AppContainerSubject(appContainer.Value);
                    }
                    else if (string.IsNullOrEmpty(exePath))
                    {
                        ShowBalloonTip(Resources.Messages.CannotGetExecutablePathWhitelisting, ToolTipIcon.Error);
                        return;
                    }
                    else
                    {
                        subj = new ExecutableSubject(exePath);
                    }

                    AddExceptions(GlobalInstances.AppDatabase.GetExceptionsForApp(subj, true, out _));
                });
            });
        }

        // Called when a user double-clicks on a popup to edit the most recent exception
        private void EditRecentException(object sender, AnyEventArgs e)
        {
            using var f = new ApplicationExceptionForm((FirewallExceptionV3)e.Arg!);
            if (f.ShowDialog() == DialogResult.Cancel)
                return;

            // Add exceptions, along with other files that belong to this app
            AddExceptions(f.ExceptionSettings, false);
        }

        internal void AddExceptions(List<FirewallExceptionV3> list, bool showEditUi = true)
        {
            if (list.Count == 0)
                // Nothing to do
                return;

            LoadSettingsFromServer();

            bool single = (list.Count == 1);

            if (single && ActiveConfig.Controller.AskForExceptionDetails && showEditUi)
            {
                using var f = new ApplicationExceptionForm(list[0]);
                if (f.ShowDialog() == DialogResult.Cancel)
                    return;

                list.Clear();
                list.AddRange(f.ExceptionSettings);
                single = (list.Count == 1);
            }

            ServerConfiguration confCopy = Utils.DeepClone(ActiveConfig.Service);
            confCopy.ActiveProfile.AddExceptions(list);

            if (!single)
            {
                ApplyFirewallSettings(confCopy, true);
                return;
            }

            var resp = ApplyFirewallSettings(confCopy, false);
            switch (resp.Type)
            {
                case MessageType.PUT_SETTINGS:
                    var respArgs = (TwMessagePutSettings)resp;
                    if (respArgs.Warning)
                    {
                        // We tell the user to re-do his changes to the settings to prevent overwriting the wrong configuration.
                        ShowBalloonTip(Resources.Messages.SettingHaveChangedRetry, ToolTipIcon.Warning);
                    }
                    else
                    {
                        bool signedAndValid = false;
                        if (list[0].Subject is ExecutableSubject exesub)
                            signedAndValid = exesub.IsSigned && exesub.CertValid;

                        if (signedAndValid)
                            ShowBalloonTip(string.Format(CultureInfo.CurrentCulture, Resources.Messages.FirewallRulesForRecognizedChanged, list[0].Subject.ToString()), ToolTipIcon.Info, 5000, EditRecentException, Utils.DeepClone(list[0]));
                        else
                            ShowBalloonTip(string.Format(CultureInfo.CurrentCulture, Resources.Messages.FirewallRulesForUnrecognizedChanged, list[0].Subject.ToString()), ToolTipIcon.Info, 5000, EditRecentException, Utils.DeepClone(list[0]));
                    }
                    break;
                case MessageType.RESPONSE_ERROR:
                    ShowBalloonTip(string.Format(CultureInfo.CurrentCulture, Resources.Messages.CouldNotWhitelistProcess, list[0].Subject.ToString()), ToolTipIcon.Warning);
                    break;
                case MessageType.INVALID_COMMAND:
                    break;
                case MessageType.RESPONSE_LOCKED:
                    break;
                case MessageType.COM_ERROR:
                    break;
                case MessageType.GET_SETTINGS:
                    break;
                case MessageType.GET_PROCESS_PATH:
                    break;
                case MessageType.READ_FW_LOG:
                    break;
                case MessageType.IS_LOCKED:
                    break;
                case MessageType.UNLOCK:
                    break;
                case MessageType.MODE_SWITCH:
                    break;
                case MessageType.REINIT:
                    break;
                case MessageType.LOCK:
                    break;
                case MessageType.SET_PASSPHRASE:
                    break;
                case MessageType.STOP_SERVICE:
                    break;
                case MessageType.MINUTE_TIMER:
                    break;
                case MessageType.REENUMERATE_ADDRESSES:
                    break;
                case MessageType.DATABASE_UPDATED:
                    break;
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    break;
                case MessageType.RELOAD_WFP_FILTERS:
                    break;
                case MessageType.DISPLAY_POWER_EVENT:
                    break;
                default:
                    DefaultPopups(resp.Type);
                    LoadSettingsFromServer();
                    break;
            }
        }

        internal bool EnsureUnlockedServer(bool showUi = true)
        {
            Locked = GlobalInstances.Controller.IsServerLocked;
            if (!Locked)
                return true;

            using var pf = new PasswordForm();
            pf.BringToFront();
            pf.Activate();
            if (pf.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;

            MessageType resp = GlobalInstances.Controller.TryUnlockServer(pf.PassHash);
            switch (resp)
            {
                case MessageType.UNLOCK:
                    this.Locked = false;
                    return true;
                case MessageType.RESPONSE_ERROR:
                    if (showUi)
                        ShowBalloonTip(Resources.Messages.UnlockFailed, ToolTipIcon.Error);
                    break;
                case MessageType.INVALID_COMMAND:
                    break;
                case MessageType.RESPONSE_LOCKED:
                    break;
                case MessageType.COM_ERROR:
                    break;
                case MessageType.GET_SETTINGS:
                    break;
                case MessageType.GET_PROCESS_PATH:
                    break;
                case MessageType.READ_FW_LOG:
                    break;
                case MessageType.IS_LOCKED:
                    break;
                case MessageType.MODE_SWITCH:
                    break;
                case MessageType.REINIT:
                    break;
                case MessageType.PUT_SETTINGS:
                    break;
                case MessageType.LOCK:
                    break;
                case MessageType.SET_PASSPHRASE:
                    break;
                case MessageType.STOP_SERVICE:
                    break;
                case MessageType.MINUTE_TIMER:
                    break;
                case MessageType.REENUMERATE_ADDRESSES:
                    break;
                case MessageType.DATABASE_UPDATED:
                    break;
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    break;
                case MessageType.RELOAD_WFP_FILTERS:
                    break;
                case MessageType.DISPLAY_POWER_EVENT:
                    break;
                default:
                    if (showUi)
                        DefaultPopups(resp);
                    break;
            }

            return false;
        }

        private void mnuLock_Click(object sender, EventArgs e)
        {
            MessageType lockResp = GlobalInstances.Controller.LockServer();

            if ((lockResp == MessageType.LOCK) || (lockResp == MessageType.RESPONSE_LOCKED))
            {
                this.Locked = true;
            }

            UpdateDisplay();
        }

        private void mnuAllowLocalSubnet_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            LoadSettingsFromServer();

            // Copy, so that settings are not changed if they cannot be saved
            ServerConfiguration confCopy = Utils.DeepClone(ActiveConfig.Service);
            confCopy.ActiveProfile.AllowLocalSubnet = !mnuAllowLocalSubnet.Checked;
            ApplyFirewallSettings(confCopy);

            mnuAllowLocalSubnet.Checked = ActiveConfig.Service.ActiveProfile.AllowLocalSubnet;
        }

        private void mnuEnableHostsBlocklist_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            LoadSettingsFromServer();

            // Copy, so that settings are not changed if they cannot be saved
            ServerConfiguration confCopy = Utils.DeepClone(ActiveConfig.Service);
            confCopy.Blocklists.EnableBlocklists = !mnuEnableHostsBlocklist.Checked;
            ApplyFirewallSettings(confCopy);

            mnuEnableHostsBlocklist.Checked = ActiveConfig.Service.Blocklists.EnableBlocklists;
        }

        private void ShowBalloonTip(string msg, ToolTipIcon icon, int period_ms = 5000, EventHandler<AnyEventArgs>? balloonClicked = null, object? handlerArg = null)
        {
            _balloonClickedCallback = balloonClicked;
            _balloonClickedCallbackArgument = handlerArg;
            Tray.ShowBalloonTip(period_ms, "TinyWall", msg, icon);
            Thread.Sleep(500);
        }

        private static void SetHotkey(System.ComponentModel.ComponentResourceManager resman, ref Hotkey? hk, HandledEventHandler hkCallback, Keys keyCode, ToolStripMenuItem menu, string mnuName)
        {
            if (ActiveConfig.Controller.EnableGlobalHotkeys)
            {
                // enable hotkey
                if (hk != null) return;

                hk = new Hotkey(keyCode, true, true, false, false);
                hk.Pressed += hkCallback;
                hk.Register();
                resman.ApplyResources(menu, mnuName);
            }
            else
            {   // disable hotkey
                hk?.Dispose();
                hk = null;
                menu.ShortcutKeyDisplayString = string.Empty;
            }
        }

        private void ApplyControllerSettings()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(TinyWallController));
            SetHotkey(resources, ref _hotKeyWhitelistWindow, new HandledEventHandler(HotKeyWhitelistWindow_Pressed), Keys.W, mnuWhitelistByWindow, "mnuWhitelistByWindow");
            SetHotkey(resources, ref _hotKeyWhitelistExecutable, new HandledEventHandler(HotKeyWhitelistExecutable_Pressed), Keys.E, mnuWhitelistByExecutable, "mnuWhitelistByExecutable");
            SetHotkey(resources, ref _hotKeyWhitelistProcess, new HandledEventHandler(HotKeyWhitelistProcess_Pressed), Keys.P, mnuWhitelistByProcess, "mnuWhitelistByProcess");
        }

        private void mnuElevate_Click(object sender, EventArgs e)
        {
            try
            {
                Utils.StartProcess(Utils.ExecutablePath, string.Empty, true);
                System.Windows.Forms.Application.Exit();
            }
            catch
            {
                ShowBalloonTip(Resources.Messages.CouldNotElevatePrivileges, ToolTipIcon.Error);
            }
        }

        private void mnuConnections_Click(object sender, EventArgs e)
        {
            if (FlashIfOpen(typeof(SettingsForm)))
                return;

            if (FlashIfOpen(typeof(ConnectionsForm)))
                return;

            using var cf = new ConnectionsForm(this);
            try
            {
                _activeForms.Add(cf);
                cf.ShowDialog();
            }
            finally
            {
                _activeForms.Remove(cf);
            }
        }

        private void Tray_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Utils.SafeNativeMethods.DoMouseRightClick();
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Middle)
            {
                mnuConnections_Click(sender, e);
            }
        }

        private void Tray_BalloonTipClicked(object sender, EventArgs e)
        {
            _balloonClickedCallback?.Invoke(Tray, new AnyEventArgs(_balloonClickedCallbackArgument));
        }

        private void LoadDatabase()
        {
            try
            {
                GlobalInstances.AppDatabase = DatabaseClasses.AppDatabase.Load();
            }
            catch
            {
                GlobalInstances.AppDatabase = new DatabaseClasses.AppDatabase();
                ThreadPool.QueueUserWorkItem((WaitCallback)delegate (object state)
                {
                    Utils.Invoke(_syncCtx, (SendOrPostCallback)delegate (object o)
                    {
                        ShowBalloonTip(Resources.Messages.DatabaseIsMissingOrCorrupt, ToolTipIcon.Warning);
                    });
                });

                throw;
            }
        }

        private void AutoWhitelist()
        {
            // Copy, so that settings are not changed if they cannot be saved
            ServerConfiguration confCopy = Utils.DeepClone(ActiveConfig.Service);
            confCopy.ActiveProfile.AddExceptions(GlobalInstances.AppDatabase.FastSearchMachineForKnownApps());
            ApplyFirewallSettings(confCopy);
        }

        private void mnuModeLearn_Click(object sender, EventArgs e)
        {
            if (!EnsureUnlockedServer())
                return;

            Utils.SplitFirstLine(Resources.Messages.YouAreAboutToEnterLearningMode, out string firstLine, out string contentLines);

            var dialog = new TaskDialog
            {
                CustomMainIcon = Resources.Icons.firewall,
                WindowTitle = Resources.Messages.TinyWall,
                MainInstruction = firstLine,
                Content = contentLines,
                AllowDialogCancellation = false,
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
            };

            if (dialog.Show() != (int)DialogResult.Yes)
                return;

            SetMode(FirewallMode.Learning);
            UpdateDisplay();
        }

        private void InitController()
        {
            mnuTrafficRate.Text = string.Format(CultureInfo.CurrentCulture, @"{0}: {1}   {2}: {3}", Resources.Messages.TrafficIn, "...", Resources.Messages.TrafficOut, "...");

            // We will load our database parallel to other things to improve startup performance
            using (var barrier = new ThreadBarrier(2))
            {
                ThreadPool.QueueUserWorkItem((WaitCallback)delegate (object state)
                {
                    try
                    {
                        LoadDatabase();
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        barrier.Wait();
                    }
                });

                // --------------- CODE BETWEEN HERE MUST NOT USE DATABASE, SINCE IT IS BEING LOADED PARALLEL ---------------
                // BEGIN
                TrayMenu.Closed += TrayMenu_Closed;
                Tray.ContextMenuStrip = TrayMenu;
                mnuElevate.Visible = !Utils.RunningAsAdmin();
                mnuModeDisabled.Image = Resources.Icons.shield_grey_small.ToBitmap();
                mnuModeAllowOutgoing.Image = Resources.Icons.shield_red_small.ToBitmap();
                mnuModeBlockAll.Image = Resources.Icons.shield_yellow_small.ToBitmap();
                mnuModeNormal.Image = Resources.Icons.shield_green_small.ToBitmap();
                mnuModeLearn.Image = Resources.Icons.shield_blue_small.ToBitmap();
                TrayMenuShowing = false;

                ApplyControllerSettings();
                GlobalInstances.InitClient();

                barrier.Wait();
                // END
                // --------------- CODE BETWEEN HERE MUST NOT USE DATABASE, SINCE IT IS BEING LOADED PARALLEL ---------------
                // --- THREAD BARRIER ---
            }

            LoadSettingsFromServer(out bool comError, true);
#if !DEBUG
            if (comError)
            {
                if (TinyWallDoctor.EnsureServiceInstalledAndRunning(Utils.LOG_ID_GUI, false))
                    LoadSettingsFromServer(out comError, true);
                else
                    MessageBox.Show(Resources.Messages.TheTinyWallServiceIsUnavailable, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#endif

            if ((_firewallState.Mode != FirewallMode.Unknown) || (!_startupOpts.startup))
            {
                Tray.Visible = true;

                if (_startupOpts.autowhitelist)
                {
                    AutoWhitelist();
                }

                if (_startupOpts.updatenow)
                {
                    StartUpdate(this, AnyEventArgs.Empty);
                }
            }
            else
            {
                // Keep on trying to reach the service
                _serviceTimer.Tick += ServiceTimer_Tick;
                _serviceTimer.Interval = 2000;
                _serviceTimer.Enabled = true;
            }
        }

        private void ServiceTimer_Tick(object sender, EventArgs e)
        {
            LoadSettingsFromServer(out bool comError, true);

            bool maxTimeElapsed = (DateTime.Now - _appStarted) > TimeSpan.FromSeconds(90);

            if (comError && !maxTimeElapsed) return;

            _serviceTimer.Enabled = false;
            Tray.Visible = true;
        }
    }

    internal class AnyEventArgs : EventArgs
    {
        public new static AnyEventArgs Empty { get; } = new AnyEventArgs();

        public AnyEventArgs(object? arg = null)
        {
            Arg = arg;
        }

        public object? Arg { get; }
    }
}
