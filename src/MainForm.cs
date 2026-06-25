using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using UptimeKumaTrayAgent.Utils;
using AgentCheckState = UptimeKumaTrayAgent.Models.CheckState;

namespace UptimeKumaTrayAgent;

public partial class MainForm : Form
{
    private readonly ConfigService _configService;
    private readonly Logger _logger;
    private readonly MonitoringService _monitoring;
    private readonly AutostartService _autostart;
    private readonly WindowsServiceManager _windowsServiceManager;
    private readonly GitHubUpdateService _updateService;
    private readonly WindowsUpdateInstaller _updateInstaller;
    private readonly bool _startMinimized;
    private readonly System.Windows.Forms.Timer _uiTimer = new();
    private readonly List<(TabPage Page, Button Button, Label? Count)> _navItems = new();
    private AgentConfig _config;
    private ThemePalette _theme = ThemePalette.Light;
    private Icon _appIcon = null!;
    private Icon? _trayStatusIcon;
    private AgentTrayState? _currentTrayState;
    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private ToolStripMenuItem _trayAutostart = null!;
    private bool _reallyExit;

    private TableLayoutPanel _shellLayout = null!;
    private Panel _sidebarPanel = null!;
    private FlowLayoutPanel _sidebarNav = null!;
    private TabPage _tabSettings = null!;
    private Label _sidebarVersion = null!;
    private Label _sectionTitle = null!;
    private Label _sectionSubtitle = null!;
    private Button _btnThemeToggle = null!;
    private WebView2 _dashboardWebView = null!;
    private bool _dashboardReady;
    private Label _lblAgentStatus = null!;
    private Label _lblMonitoring = null!;
    private Label _lblLastRun = null!;
    private Label _lblLastError = null!;
    private Label _lblVersion = null!;
    private Label _lblComputer = null!;
    private Label _lblKpiTotal = null!;
    private Label _lblKpiOk = null!;
    private Label _lblKpiDown = null!;
    private Label _lblKpiAvg = null!;
    private TextBox _txtMonitorSearch = null!;
    private ComboBox _cmbStatusFilter = null!;
    private NumericUpDown _numDefaultInterval = null!;
    private NumericUpDown _numHttpTimeout = null!;
    private NumericUpDown _numTcpTimeout = null!;
    private NumericUpDown _numPingTimeout = null!;
    private CheckBox _chkSendMachineInfo = null!;
    private CheckBox _chkMaskPushUrls = null!;
    private ComboBox _cmbLogLevel = null!;
    private ComboBox _cmbTheme = null!;
    private ComboBox _cmbLanguage = null!;
    private CheckBox _chkAutostart = null!;
    private CheckBox _chkMonitoringAutoStart = null!;
    private CheckBox _chkStartMinimized = null!;
    private CheckBox _chkMinimizeToTray = null!;
    private CheckBox _chkWatchdogEnabled = null!;
    private TextBox _txtWatchdogPushUrl = null!;
    private NumericUpDown _numWatchdogInterval = null!;
    private NumericUpDown _numWatchdogMax = null!;
    private CheckBox _chkWatchdogMachineInfo = null!;
    private DataGridView _statusGrid = null!;
    private DataGridView _pingGrid = null!;
    private DataGridView _tcpGrid = null!;
    private DataGridView _serviceGrid = null!;
    private DataGridView _driveGrid = null!;
    private DataGridView _availableServicesGrid = null!;
    private TextBox _txtLogs = null!;
    private Button _btnInstallUpdate = null!;
    private UpdateCheckResult? _availableUpdate;

    public MainForm(
        AgentConfig config,
        ConfigService configService,
        Logger logger,
        MonitoringService monitoring,
        AutostartService autostart,
        WindowsServiceManager windowsServiceManager,
        bool startMinimized)
    {
        _config = config;
        _configService = configService;
        _logger = logger;
        _monitoring = monitoring;
        _autostart = autostart;
        _windowsServiceManager = windowsServiceManager;
        _updateService = new GitHubUpdateService();
        _updateInstaller = new WindowsUpdateInstaller(_updateService, _logger);
        _startMinimized = startMinimized;
        _config.Normalize();
        I18n.Apply(_config.Global.Language);
        _theme = ThemeModes.PaletteFor(_config.Global.Theme);

        InitializeComponent();
        ConfigureBranding();
        BuildShellLayout();
        BuildTrayIcon();
        BuildGeneralTab();
        BuildPingTab();
        BuildTcpTab();
        BuildServicesTab();
        BuildDrivesTab();
        BuildLogsTab();
        BuildSettingsTab();
        BuildNavigation();

        LoadConfigIntoControls();
        RefreshAllCheckGrids();
        RefreshStatusGrid();
        RefreshAgentLabels();
        ApplyTheme();
        BuildHtmlDashboard();

        _monitoring.StateChanged += (_, _) => RunOnUi(() =>
        {
            RefreshAgentLabels();
            RefreshTrayMenu();
            _ = SyncDashboardAsync();
        });
        _monitoring.StatusesChanged += (_, _) => RunOnUi(() =>
        {
            RefreshStatusGrid();
            _ = SyncDashboardAsync();
        });

        _uiTimer.Interval = 1500;
        _uiTimer.Tick += (_, _) =>
        {
            RefreshAgentLabels();
            RefreshStatusGrid();
            _ = SyncDashboardAsync();
            if (tabMain.SelectedTab == tabLogs)
            {
                RefreshLogText();
            }
        };
        _uiTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_startMinimized)
        {
            HideToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit && _config.Global.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _uiTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayStatusIcon?.Dispose();
        _appIcon?.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayAutostart = new ToolStripMenuItem(I18n.T("Autostart aktivieren"), null, (_, _) => ToggleAutostartFromTray());

        _trayMenu.Items.Add(I18n.T("Fenster öffnen"), null, (_, _) => ShowMainWindow());
        _trayMenu.Items.Add(I18n.T("Monitoring starten"), null, (_, _) => StartMonitoring());
        _trayMenu.Items.Add(I18n.T("Monitoring stoppen"), null, (_, _) => StopMonitoring());
        _trayMenu.Items.Add(I18n.T("Status anzeigen"), null, (_, _) => ShowStatus());
        _trayMenu.Items.Add(I18n.T("Konfiguration öffnen"), null, (_, _) => OpenConfigFile());
        _trayMenu.Items.Add(I18n.T("Logs öffnen"), null, (_, _) => OpenLogsFolder());
        _trayMenu.Items.Add(_trayAutostart);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(I18n.T("Deinstallieren"), null, (_, _) => RunUninstallFlow());
        _trayMenu.Items.Add(I18n.T("Beenden"), null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon(components)
        {
            Icon = _appIcon,
            Text = "Uptime Kuma Tray Agent",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        RefreshTrayMenu();
    }

    private void ConfigureBranding()
    {
        _appIcon = AppIconFactory.CreateIcon();
        Icon = _appIcon;
        headerIcon.Image = AppIconFactory.CreateBitmap(64);
    }

    private void BuildHtmlDashboard()
    {
        FormBorderStyle = FormBorderStyle.None;
        _dashboardWebView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(11, 14, 17)
        };
        Controls.Add(_dashboardWebView);
        _dashboardWebView.BringToFront();
        _ = InitializeDashboardAsync();
    }

    private async Task InitializeDashboardAsync()
    {
        try
        {
            var userData = Path.Combine(_configService.DataDirectory, "WebView2");
            Directory.CreateDirectory(userData);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await _dashboardWebView.EnsureCoreWebView2Async(environment);
            _dashboardWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _dashboardWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _dashboardWebView.CoreWebView2.WebMessageReceived += async (_, e) => await HandleDashboardMessageAsync(e.WebMessageAsJson);
            _dashboardWebView.NavigationCompleted += async (_, _) =>
            {
                _dashboardReady = true;
                await InstallDashboardBridgeAsync();
                await SyncDashboardAsync();
            };

            var dashboardPath = Path.Combine(AppContext.BaseDirectory, "Assets", "UptimeKumaAgentDashboard.html");
            if (!File.Exists(dashboardPath))
            {
                dashboardPath = Path.Combine(AppContext.BaseDirectory, "UptimeKumaAgentDashboard.html");
            }

            _dashboardWebView.CoreWebView2.Navigate(new Uri(dashboardPath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            _logger.Error("HTML-Dashboard konnte nicht initialisiert werden", ex);
            MessageBox.Show(this, ex.Message, "HTML Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task InstallDashboardBridgeAsync()
    {
        if (_dashboardWebView.CoreWebView2 is null)
        {
            return;
        }

        var script = $$"""
        (() => {
          if (window.__uptimeAgentBridgeInstalled) return;
          window.__uptimeAgentBridgeInstalled = true;
          const send = (message) => {
            try { window.chrome?.webview?.postMessage(message); } catch {}
          };
          const textOf = (element) => (element?.innerText || element?.textContent || '').trim();
          const actionButtonStyle = 'display:flex;align-items:center;gap:7px;height:40px;padding:0 16px;border:1px solid var(--border);border-radius:10px;background:var(--surface);color:var(--text);cursor:pointer;font-size:13px;font-weight:600';
          const makeActionButton = (label, icon, name) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.dataset.agentBridge = '1';
            button.style.cssText = actionButtonStyle;
            button.innerHTML = `<span class="ms" style="font-size:18px">${icon}</span>${label}`;
            button.addEventListener('click', () => send({ type: 'action', name }));
            return button;
          };
          const ensureExtraActions = () => {
            const configButton = Array.from(document.querySelectorAll('button')).find((button) => textOf(button) === 'Konfiguration öffnen');
            const row = configButton?.parentElement;
            if (!row || row.querySelector('[data-agent-extra-actions]')) return;
            row.dataset.agentExtraActions = '1';
            row.style.flexWrap = 'wrap';
            row.append(
              makeActionButton('Logordner öffnen', 'folder_open', 'logs'),
              makeActionButton('Check for Updates', 'system_update_alt', 'checkUpdates'),
              makeActionButton('Update', 'download', 'update')
            );
          };
          const wireWindowControls = () => {
            const icons = Array.from(document.querySelectorAll('.ms,.material-symbols-rounded'));
            for (const icon of icons) {
              const label = textOf(icon).toLowerCase();
              const action = label === 'remove' ? 'minimize' : label === 'crop_square' ? 'maximize' : label === 'close' ? 'close' : '';
              if (!action) continue;
              const rect = icon.getBoundingClientRect();
              if (rect.top > 44 || rect.right < window.innerWidth - 170) continue;
              const target = icon.closest('button') || icon.parentElement;
              if (!target || target.dataset.agentWindowAction === action) continue;
              target.dataset.agentWindowAction = action;
              target.style.cursor = 'pointer';
              target.addEventListener('click', (event) => {
                event.preventDefault();
                event.stopPropagation();
                send({ type: 'window', action });
              });
            }
          };
          const wire = () => {
            ensureExtraActions();
            wireWindowControls();
            document.querySelectorAll('button').forEach((button) => {
              if (button.dataset.agentBridge) return;
              const label = textOf(button);
              const rect = button.getBoundingClientRect();
              button.dataset.agentBridge = '1';
              if (label.includes('Testlauf')) button.addEventListener('click', () => send({ type: 'action', name: 'test' }));
              if (label === 'Speichern') button.addEventListener('click', () => send({ type: 'action', name: 'save' }));
              if (label.includes('Konfiguration öffnen')) button.addEventListener('click', () => send({ type: 'action', name: 'config' }));
              if (label.includes('Logordner öffnen')) button.addEventListener('click', () => send({ type: 'action', name: 'logs' }));
              if (label.includes('Check for Updates')) button.addEventListener('click', () => send({ type: 'action', name: 'checkUpdates' }));
              if (label === 'Update') button.addEventListener('click', () => send({ type: 'action', name: 'update' }));
              if (label.includes('Monitoring aktiv') || label.includes('Monitoring pausiert')) {
                button.addEventListener('click', () => send({ type: 'action', name: 'toggleMonitoring' }));
              }
              if (rect.top < 44 && rect.right > window.innerWidth - 170) {
                const normalized = label.toLowerCase();
                if (normalized === '-' || normalized === '−' || normalized.includes('min')) {
                  button.addEventListener('click', () => send({ type: 'window', action: 'minimize' }));
                } else if (normalized === '□' || normalized === '▢' || normalized.includes('max')) {
                  button.addEventListener('click', () => send({ type: 'window', action: 'maximize' }));
                } else if (normalized === '×' || normalized === 'x' || normalized.includes('close')) {
                  button.addEventListener('click', () => send({ type: 'window', action: 'close' }));
                }
              }
            });
          };
          document.addEventListener('mousedown', (event) => {
            if (event.button !== 0 || event.clientY > 44 || event.target.closest('button,input,select,textarea')) return;
            send({ type: 'window', action: 'drag' });
          }, true);
          const replaceText = (from, to) => {
            const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
            const nodes = [];
            while (walker.nextNode()) nodes.push(walker.currentNode);
            for (const node of nodes) {
              if (node.nodeValue && node.nodeValue.includes(from)) node.nodeValue = node.nodeValue.split(from).join(to);
            }
          };
          const setTextByLabel = (label, value) => {
            const elements = Array.from(document.querySelectorAll('*')).filter((element) => textOf(element) === label);
            for (const element of elements) {
              const card = element.closest('div');
              if (!card) continue;
              const candidates = Array.from(card.querySelectorAll('div,span,strong')).filter((candidate) => /^\d+(\s?ms|%)?$|^-$/.test(textOf(candidate)));
              if (candidates.length) {
                candidates[0].textContent = value;
                return;
              }
            }
          };
          window.__uptimeAgentSync = (data) => {
            wire();
            replaceText('v1.0.3', 'v' + data.version);
            replaceText('PROXESS', data.machineName || 'PROXESS');
            setTextByLabel('Monitore', String(data.total));
            setTextByLabel('Erreichbar', String(data.up));
            setTextByLabel('Ausfälle', String(data.down));
            setTextByLabel('Ø Antwort', data.avgResponse);
          };
          setInterval(wire, 500);
          wire();
        })();
        """;
        await _dashboardWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async Task SyncDashboardAsync()
    {
        if (!_dashboardReady || _dashboardWebView.CoreWebView2 is null)
        {
            return;
        }

        var statuses = _monitoring.GetStatuses().Where(status => status.Enabled).ToList();
        var responses = statuses.Where(status => status.LastResponseMs.HasValue).Select(status => status.LastResponseMs!.Value).ToList();
        var payload = new
        {
            version = AppVersion.Current,
            machineName = Environment.MachineName,
            monitoring = IsMonitoringActive(),
            total = statuses.Count,
            up = statuses.Count(status => status.State == AgentCheckState.Up),
            down = statuses.Count(status => status.State == AgentCheckState.Down),
            warn = statuses.Count(status => status.State == AgentCheckState.Warning),
            avgResponse = responses.Count == 0 ? "-" : (long)Math.Round(responses.Average()) + " ms",
            lastRun = TimeFormatter.FormatDate(_monitoring.LastRun)
        };
        var json = JsonSerializer.Serialize(payload);
        await _dashboardWebView.CoreWebView2.ExecuteScriptAsync($"window.__uptimeAgentSync && window.__uptimeAgentSync({json});");
    }

    private async Task HandleDashboardMessageAsync(string messageJson)
    {
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : "";
            if (string.Equals(type, "window", StringComparison.OrdinalIgnoreCase))
            {
                var action = root.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "";
                HandleDashboardWindowAction(action);
                return;
            }

            if (string.Equals(type, "action", StringComparison.OrdinalIgnoreCase))
            {
                var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "";
                await HandleDashboardActionAsync(name);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("HTML-Dashboard-Aktion fehlgeschlagen", ex);
        }
    }

    private void HandleDashboardWindowAction(string? action)
    {
        switch (action?.Trim().ToLowerInvariant())
        {
            case "minimize":
                WindowState = FormWindowState.Minimized;
                break;
            case "maximize":
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                break;
            case "close":
                Close();
                break;
            case "drag":
                BeginNativeWindowDrag();
                break;
        }
    }

    private async Task HandleDashboardActionAsync(string? name)
    {
        switch (name?.Trim())
        {
            case "test":
                await TestAllChecksAsync();
                break;
            case "save":
                SaveConfig(showMessage: true);
                break;
            case "config":
                OpenConfigFile();
                break;
            case "logs":
                OpenLogsFolder();
                break;
            case "checkUpdates":
                await CheckForUpdatesAsync();
                break;
            case "update":
                await InstallUpdateAsync();
                break;
            case "toggleMonitoring":
                if (IsMonitoringActive())
                {
                    StopMonitoring();
                }
                else
                {
                    StartMonitoring();
                }
                break;
        }
    }

    private void BeginNativeWindowDrag()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }

        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private void BuildShellLayout()
    {
        Controls.Remove(mainLayout);

        _shellLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 20, 18, 18)
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _sidebarPanel.Controls.Add(sidebarLayout);

        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var brandIcon = new PictureBox
        {
            Image = AppIconFactory.CreateBitmap(48),
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Margin = new Padding(0, 0, 10, 0)
        };
        brand.Controls.Add(brandIcon, 0, 0);
        brand.SetRowSpan(brandIcon, 2);
        brand.Controls.Add(new Label
        {
            Text = "Uptime Kuma",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.BottomLeft
        }, 1, 0);
        brand.Controls.Add(new Label
        {
            Text = "Agent",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.TopLeft
        }, 1, 1);
        sidebarLayout.Controls.Add(brand, 0, 0);

        _sidebarNav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        sidebarLayout.Controls.Add(_sidebarNav, 0, 1);

        _sidebarVersion = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            Text = "v" + AppVersion.Current
        };
        sidebarLayout.Controls.Add(_sidebarVersion, 0, 2);

        _shellLayout.Controls.Add(_sidebarPanel, 0, 0);
        _shellLayout.Controls.Add(mainLayout, 1, 0);
        Controls.Add(_shellLayout);

        mainLayout.RowStyles[0].Height = 78;
        mainLayout.RowStyles[2].Height = 30;
        tabMain.Appearance = TabAppearance.FlatButtons;
        tabMain.ItemSize = new Size(0, 1);
        tabMain.SizeMode = TabSizeMode.Fixed;

        BuildHeaderChrome();
        tabMain.SelectedIndexChanged += (_, _) => RefreshNavigation();
    }

    private void BuildHeaderChrome()
    {
        headerPanel.Controls.Clear();
        headerPanel.Padding = new Padding(22, 12, 22, 10);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        var titleStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        _sectionTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = I18n.T("Allgemein"),
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.BottomLeft
        };
        _sectionSubtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = I18n.T("Lokale Hosts, TCP-Ports und Windows-Dienste im Blick behalten"),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.TopLeft
        };
        titleStack.Controls.Add(_sectionTitle, 0, 0);
        titleStack.Controls.Add(_sectionSubtitle, 0, 1);
        header.Controls.Add(titleStack, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        _btnThemeToggle = CreateButton("Theme", (_, _) => ToggleTheme());
        _btnThemeToggle.Width = 86;
        headerBadge.Width = 142;
        headerBadge.Height = 34;
        headerBadge.Margin = new Padding(8, 0, 0, 0);
        actions.Controls.Add(headerBadge);
        actions.Controls.Add(_btnThemeToggle);
        header.Controls.Add(actions, 1, 0);

        headerPanel.Controls.Add(header);
    }

    private void BuildNavigation()
    {
        _navItems.Clear();
        _sidebarNav.Controls.Clear();
        AddNavItem(tabGeneral, I18n.T("Allgemein"));
        AddNavItem(tabPing, I18n.T("Ping-Checks"));
        AddNavItem(tabTcp, I18n.T("TCP-Checks"));
        AddNavItem(tabServices, I18n.T("Windows-Dienste"));
        AddNavItem(tabDrives, I18n.T("Laufwerke"));
        AddNavItem(tabLogs, I18n.T("Logs"));
        AddNavItem(_tabSettings, I18n.T("Einstellungen"));
        RefreshNavigation();
    }

    private void AddNavItem(TabPage page, string label)
    {
        var button = new Button
        {
            Tag = page,
            Text = label,
            Width = 208,
            Height = 42,
            Margin = new Padding(0, 0, 0, 7),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(14, 0, 10, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            tabMain.SelectedTab = page;
            RefreshNavigation();
        };
        _sidebarNav.Controls.Add(button);
        _navItems.Add((page, button, null));
    }

    private void RefreshNavigation()
    {
        if (_navItems.Count == 0)
        {
            return;
        }

        foreach (var item in _navItems)
        {
            var selected = tabMain.SelectedTab == item.Page;
            var count = GetNavCount(item.Page);
            item.Button.Text = count is null ? item.Page.Text : $"{item.Page.Text}   {count}";
            item.Button.BackColor = selected ? _theme.AccentSoft : _theme.Surface;
            item.Button.ForeColor = selected ? _theme.AccentDark : _theme.MutedText;
        }

        var (title, subtitle) = GetSectionText(tabMain.SelectedTab);
        _sectionTitle.Text = title;
        _sectionSubtitle.Text = subtitle;
    }

    private int? GetNavCount(TabPage page)
    {
        if (page == tabGeneral)
        {
            return _config.PingChecks.Count + _config.TcpChecks.Count + _config.ServiceChecks.Count + _config.DriveChecks.Count;
        }

        if (page == tabPing)
        {
            return _config.PingChecks.Count;
        }

        if (page == tabTcp)
        {
            return _config.TcpChecks.Count;
        }

        if (page == tabServices)
        {
            return _config.ServiceChecks.Count;
        }

        if (page == tabDrives)
        {
            return _config.DriveChecks.Count;
        }

        return null;
    }

    private (string Title, string Subtitle) GetSectionText(TabPage? page)
    {
        if (page == tabPing)
        {
            return (I18n.T("Ping-Checks"), I18n.T("Erreichbarkeit lokaler Hosts"));
        }

        if (page == tabTcp)
        {
            return (I18n.T("TCP-Checks"), I18n.T("Offene Ports und Endpunkte"));
        }

        if (page == tabServices)
        {
            return (I18n.T("Windows-Dienste"), I18n.T("Status der überwachten Dienste"));
        }

        if (page == tabDrives)
        {
            return (I18n.T("Laufwerke"), I18n.T("Speicher und Mountpoints"));
        }

        if (page == tabLogs)
        {
            return (I18n.T("Logs"), I18n.T("Ereignisprotokoll des Agents"));
        }

        if (page == _tabSettings)
        {
            return (I18n.T("Einstellungen"), I18n.T("Globale Konfiguration und Watchdog"));
        }

        return (I18n.T("Übersicht"), I18n.T("Alle Monitore auf einen Blick"));
    }

    private void ToggleTheme()
    {
        _config.Global.Theme = string.Equals(ThemeModes.Normalize(_config.Global.Theme), "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";
        SelectComboValue(_cmbTheme, _config.Global.Theme);
        _theme = ThemeModes.PaletteFor(_config.Global.Theme);
        ApplyTheme();
    }

    private void BuildGeneralTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Padding = new Padding(10);
        tabGeneral.Controls.Add(root);

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.Controls.Add(top, 0, 0);

        var statusBox = CreateCard();
        var statusTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBox.Controls.Add(statusTable);
        _lblAgentStatus = AddStatusRow(statusTable, I18n.T("Agent"));
        _lblMonitoring = AddStatusRow(statusTable, I18n.T("Monitoring"));
        _lblLastRun = AddStatusRow(statusTable, I18n.T("Letzter Lauf"));
        _lblLastError = AddStatusRow(statusTable, I18n.T("Letzter Fehler"));
        _lblVersion = AddStatusRow(statusTable, I18n.T("Version"));
        _lblComputer = AddStatusRow(statusTable, I18n.T("Computername"));
        top.Controls.Add(statusBox, 0, 0);

        var actionsCard = CreateCard();
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(2)
        };
        buttonPanel.Controls.Add(CreateButton(I18n.T("Monitoring starten"), (_, _) => StartMonitoring()));
        buttonPanel.Controls.Add(CreateButton(I18n.T("Monitoring stoppen"), (_, _) => StopMonitoring()));
        buttonPanel.Controls.Add(CreateButton(I18n.T("Speichern"), (_, _) => SaveConfig(showMessage: true)));
        buttonPanel.Controls.Add(CreateButton(I18n.T("Logs öffnen"), (_, _) => OpenLogsFolder()));
        buttonPanel.Controls.Add(CreateButton(I18n.T("Testlauf ausführen"), async (_, _) => await TestAllChecksAsync()));
        buttonPanel.Controls.Add(CreateButton(I18n.T("Konfiguration öffnen"), (_, _) => OpenConfigFile()));
        buttonPanel.Controls.Add(CreateButton("Check for Updates", async (_, _) => await CheckForUpdatesAsync()));
        _btnInstallUpdate = CreateButton("Update", async (_, _) => await InstallUpdateAsync());
        _btnInstallUpdate.Enabled = false;
        buttonPanel.Controls.Add(_btnInstallUpdate);
        actionsCard.Controls.Add(buttonPanel);
        top.Controls.Add(actionsCard, 1, 0);

        var kpiGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 4, 0, 4) };
        kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _lblKpiTotal = AddMetricCard(kpiGrid, I18n.T("Monitore"), "0", 0);
        _lblKpiOk = AddMetricCard(kpiGrid, "OK", "0", 1);
        _lblKpiDown = AddMetricCard(kpiGrid, I18n.T("Fehler"), "0", 2);
        _lblKpiAvg = AddMetricCard(kpiGrid, I18n.T("Ø Antwort"), "0 ms", 3);
        root.Controls.Add(kpiGrid, 0, 1);

        _statusGrid = CreateGrid();
        _statusGrid.Columns.Add("Type", I18n.T("Typ"));
        _statusGrid.Columns.Add("Name", I18n.T("Name"));
        _statusGrid.Columns.Add("Target", I18n.T("Ziel"));
        _statusGrid.Columns.Add("Enabled", I18n.T("Aktiv"));
        _statusGrid.Columns.Add("State", I18n.T("Status"));
        _statusGrid.Columns.Add("Ping", I18n.T("Antwortzeit"));
        _statusGrid.Columns.Add("LastRun", I18n.T("Letzter Check"));
        _statusGrid.Columns.Add("LastOk", I18n.T("Letzter OK"));
        _statusGrid.Columns.Add("Error", I18n.T("Letzter Fehler"));
        _statusGrid.Columns.Add("Next", I18n.T("Nächste Ausführung"));
        _statusGrid.Columns.Add("Push", I18n.T("Letzter Push"));
        _statusGrid.Columns.Add("Http", I18n.T("HTTP"));

        var tableCard = CreateCard();
        var tableLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tableCard.Controls.Add(tableLayout);
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };
        _cmbStatusFilter = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbStatusFilter.Items.AddRange(new object[] { I18n.T("Alle"), "OK", I18n.T("Fehler"), I18n.T("Warnung"), I18n.T("Unbekannt") });
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += (_, _) => RefreshStatusGrid();
        _txtMonitorSearch = new TextBox { Width = 260, PlaceholderText = I18n.T("Suchen") };
        _txtMonitorSearch.TextChanged += (_, _) => RefreshStatusGrid();
        filterPanel.Controls.Add(_cmbStatusFilter);
        filterPanel.Controls.Add(_txtMonitorSearch);
        filterPanel.Controls.Add(new Label
        {
            Text = I18n.T("Aktuelle Monitore"),
            AutoSize = true,
            Margin = new Padding(0, 8, 16, 0),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point)
        });
        tableLayout.Controls.Add(filterPanel, 0, 0);
        tableLayout.Controls.Add(_statusGrid, 0, 1);
        root.Controls.Add(tableCard, 0, 2);
    }

    private void BuildSettingsTab()
    {
        _tabSettings = new TabPage
        {
            Text = I18n.T("Einstellungen"),
            Padding = new Padding(10),
            UseVisualStyleBackColor = true
        };
        tabMain.Controls.Add(_tabSettings);

        var settingsArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        settingsArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        settingsArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _tabSettings.Controls.Add(settingsArea);

        var globalBox = CreateCard();
        var globalTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(6) };
        globalTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        globalTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        globalTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        globalTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        globalBox.Controls.Add(globalTable);
        AddCardTitle(globalTable, I18n.T("Globale Einstellungen"), 0);
        _numDefaultInterval = AddNumber(globalTable, I18n.T("Standard-Intervall (s)"), 5, 86400, 30, 0, 1);
        _numHttpTimeout = AddNumber(globalTable, I18n.T("HTTP-Timeout (ms)"), 500, 120000, 5000, 2, 1);
        _numTcpTimeout = AddNumber(globalTable, I18n.T("TCP-Timeout (ms)"), 500, 120000, 3000, 0, 2);
        _numPingTimeout = AddNumber(globalTable, I18n.T("Ping-Timeout (ms)"), 500, 120000, 3000, 2, 2);
        _cmbLogLevel = AddOptionCombo(globalTable, I18n.T("Log-Level"), LogLevelKinds.All, I18n.LogLevelName, 0, 3);
        _cmbTheme = AddOptionCombo(globalTable, I18n.T("Darstellung"), ThemeModes.All, I18n.ThemeName, 2, 3);
        _cmbLanguage = AddLanguageCombo(globalTable, I18n.T("Sprache"), 0, 4);
        _chkMaskPushUrls = AddCheck(globalTable, I18n.T("Push-URLs maskieren"), 2, 4);
        _chkAutostart = AddCheck(globalTable, I18n.T("Autostart aktivieren"), 0, 5);
        _chkStartMinimized = AddCheck(globalTable, I18n.T("Start minimiert"), 2, 5);
        _chkMinimizeToTray = AddCheck(globalTable, I18n.T("Beim Schließen in Tray"), 0, 6);
        _chkSendMachineInfo = AddCheck(globalTable, I18n.T("Maschineninfos mitsenden"), 2, 6);
        _chkMonitoringAutoStart = AddCheck(globalTable, I18n.T("Monitoring automatisch starten"), 0, 7);
        _cmbTheme.SelectedIndexChanged += (_, _) =>
        {
            _config.Global.Theme = GetComboValue(_cmbTheme, "Light");
            _theme = ThemeModes.PaletteFor(_config.Global.Theme);
            ApplyTheme();
        };
        settingsArea.Controls.Add(globalBox, 0, 0);

        var watchdogBox = CreateCard();
        var watchdogTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(6) };
        watchdogTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        watchdogTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        watchdogBox.Controls.Add(watchdogTable);
        AddCardTitle(watchdogTable, I18n.T("Watchdog"), 0);
        _chkWatchdogEnabled = AddCheck(watchdogTable, I18n.T("Watchdog aktiv"), 0, 1);
        _txtWatchdogPushUrl = AddText(watchdogTable, I18n.T("Watchdog-Push-URL"), 0, 2);
        _numWatchdogInterval = AddNumber(watchdogTable, I18n.T("Intervall (s)"), 5, 86400, 60, 0, 3);
        _numWatchdogMax = AddNumber(watchdogTable, I18n.T("Max. ohne Erfolg (s)"), 30, 604800, 300, 0, 4);
        _chkWatchdogMachineInfo = AddCheck(watchdogTable, I18n.T("Maschineninfos mitsenden"), 0, 5);
        settingsArea.Controls.Add(watchdogBox, 1, 0);
    }

    private void BuildPingTab()
    {
        _pingGrid = CreateGrid();
        _pingGrid.Columns.Add("Enabled", I18n.T("Aktiv"));
        _pingGrid.Columns.Add("Name", I18n.T("Name"));
        _pingGrid.Columns.Add("Host", I18n.T("Host"));
        _pingGrid.Columns.Add("Interval", I18n.T("Intervall"));
        _pingGrid.Columns.Add("Timeout", I18n.T("Timeout"));
        _pingGrid.Columns.Add("Machine", I18n.T("Maschineninfos"));
        _pingGrid.Columns.Add("RestartServices", I18n.T("Fehleraktion"));
        _pingGrid.Columns.Add("Push", I18n.T("Push-URL"));
        _pingGrid.Columns.Add("Note", I18n.T("Notiz"));

        var buttons = CreateButtonPanel(
            (I18n.T("Hinzufügen"), (_, _) => AddPingCheck()),
            (I18n.T("Bearbeiten"), (_, _) => EditPingCheck()),
            (I18n.T("Löschen"), (_, _) => DeletePingCheck()),
            (I18n.T("Testen"), async (_, _) => await TestSelectedPingAsync()));

        AddGridWithButtons(tabPing, _pingGrid, buttons);
    }

    private void BuildTcpTab()
    {
        _tcpGrid = CreateGrid();
        _tcpGrid.Columns.Add("Enabled", I18n.T("Aktiv"));
        _tcpGrid.Columns.Add("Name", I18n.T("Name"));
        _tcpGrid.Columns.Add("Host", I18n.T("Host"));
        _tcpGrid.Columns.Add("Port", I18n.T("Port"));
        _tcpGrid.Columns.Add("Interval", I18n.T("Intervall"));
        _tcpGrid.Columns.Add("Timeout", I18n.T("Timeout"));
        _tcpGrid.Columns.Add("Machine", I18n.T("Maschineninfos"));
        _tcpGrid.Columns.Add("RestartServices", I18n.T("Fehleraktion"));
        _tcpGrid.Columns.Add("TcpLog", I18n.T("TCP-Log"));
        _tcpGrid.Columns.Add("Push", I18n.T("Push-URL"));
        _tcpGrid.Columns.Add("Note", I18n.T("Notiz"));

        var buttons = CreateButtonPanel(
            (I18n.T("Hinzufügen"), (_, _) => AddTcpCheck()),
            (I18n.T("Bearbeiten"), (_, _) => EditTcpCheck()),
            (I18n.T("Löschen"), (_, _) => DeleteTcpCheck()),
            (I18n.T("Testen"), async (_, _) => await TestSelectedTcpAsync()));

        AddGridWithButtons(tabTcp, _tcpGrid, buttons);
    }

    private void BuildServicesTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        tabServices.Controls.Add(root);

        _serviceGrid = CreateGrid();
        _serviceGrid.Columns.Add("Enabled", I18n.T("Aktiv"));
        _serviceGrid.Columns.Add("DisplayName", I18n.T("Anzeigename"));
        _serviceGrid.Columns.Add("ServiceName", I18n.T("Dienstname"));
        _serviceGrid.Columns.Add("Expected", I18n.T("Erwartet"));
        _serviceGrid.Columns.Add("Interval", I18n.T("Intervall"));
        _serviceGrid.Columns.Add("Restart", I18n.T("Auto-Neustart"));
        _serviceGrid.Columns.Add("Machine", I18n.T("Maschineninfos"));
        _serviceGrid.Columns.Add("RestartServices", I18n.T("Fehleraktion"));
        _serviceGrid.Columns.Add("Push", I18n.T("Push-URL"));
        _serviceGrid.Columns.Add("Note", I18n.T("Notiz"));
        var serviceCard = CreateCard();
        serviceCard.Controls.Add(_serviceGrid);
        root.Controls.Add(serviceCard, 0, 0);

        var buttons = CreateButtonPanel(
            (I18n.T("Hinzufügen"), (_, _) => AddServiceCheck(null)),
            (I18n.T("Aus Liste hinzufügen"), (_, _) => AddServiceCheckFromList()),
            (I18n.T("Bearbeiten"), (_, _) => EditServiceCheck()),
            (I18n.T("Löschen"), (_, _) => DeleteServiceCheck()),
            (I18n.T("Testen"), async (_, _) => await TestSelectedServiceAsync()),
            (I18n.T("Dienst starten"), async (_, _) => await RunServiceOperationAsync("start")),
            (I18n.T("Dienst stoppen"), async (_, _) => await RunServiceOperationAsync("stop")),
            (I18n.T("Dienst neu starten"), async (_, _) => await RunServiceOperationAsync("restart")),
            (I18n.T("Dienst hart neu starten"), async (_, _) => await RunServiceOperationAsync("restart-force")),
            (I18n.T("Lokale Dienste laden"), async (_, _) => await LoadLocalServicesAsync()));
        root.Controls.Add(buttons, 0, 1);

        _availableServicesGrid = CreateGrid();
        _availableServicesGrid.Columns.Add("DisplayName", I18n.T("Lokaler Dienst"));
        _availableServicesGrid.Columns.Add("ServiceName", I18n.T("Dienstname"));
        _availableServicesGrid.Columns.Add("Status", I18n.T("Status"));
        _availableServicesGrid.Columns.Add("CanStop", I18n.T("Stoppbar"));
        var localServiceCard = CreateCard();
        localServiceCard.Controls.Add(_availableServicesGrid);
        root.Controls.Add(localServiceCard, 0, 2);
    }

    private void BuildDrivesTab()
    {
        _driveGrid = CreateGrid();
        _driveGrid.Columns.Add("Enabled", I18n.T("Aktiv"));
        _driveGrid.Columns.Add("Name", I18n.T("Name"));
        _driveGrid.Columns.Add("Path", I18n.T("Pfad"));
        _driveGrid.Columns.Add("Interval", I18n.T("Intervall"));
        _driveGrid.Columns.Add("MinPercent", I18n.T("Min. frei %"));
        _driveGrid.Columns.Add("MinGb", I18n.T("Min. frei GB"));
        _driveGrid.Columns.Add("Reconnect", I18n.T("Reconnect"));
        _driveGrid.Columns.Add("Details", I18n.T("Logdetails"));
        _driveGrid.Columns.Add("Machine", I18n.T("Maschineninfos"));
        _driveGrid.Columns.Add("Push", I18n.T("Push-URL"));
        _driveGrid.Columns.Add("Note", I18n.T("Notiz"));

        var buttons = CreateButtonPanel(
            (I18n.T("Hinzufügen"), (_, _) => AddDriveCheck()),
            (I18n.T("Bearbeiten"), (_, _) => EditDriveCheck()),
            (I18n.T("Löschen"), (_, _) => DeleteDriveCheck()),
            (I18n.T("Testen"), async (_, _) => await TestSelectedDriveAsync()));

        AddGridWithButtons(tabDrives, _driveGrid, buttons);
    }

    private void BuildLogsTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabLogs.Controls.Add(root);

        var buttons = CreateButtonPanel(
            (I18n.T("Aktualisieren"), (_, _) => RefreshLogText()),
            (I18n.T("Logs öffnen"), (_, _) => OpenLogsFolder()));
        root.Controls.Add(buttons, 0, 0);

        _txtLogs = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font(FontFamily.GenericMonospace, 9)
        };
        var logCard = CreateCard();
        logCard.Controls.Add(_txtLogs);
        root.Controls.Add(logCard, 0, 1);
        RefreshLogText();
    }

    private void LoadConfigIntoControls()
    {
        _config.Normalize();
        _numDefaultInterval.Value = _config.Global.DefaultIntervalSeconds;
        _numHttpTimeout.Value = _config.Global.HttpTimeoutMs;
        _numTcpTimeout.Value = _config.Global.TcpTimeoutMs;
        _numPingTimeout.Value = _config.Global.PingTimeoutMs;
        _chkSendMachineInfo.Checked = _config.Global.SendMachineInfo;
        _chkMaskPushUrls.Checked = _config.Global.MaskPushUrls;
        SelectComboValue(_cmbLogLevel, LogLevelKinds.Normalize(_config.Global.LogLevel));
        SelectComboValue(_cmbTheme, ThemeModes.Normalize(_config.Global.Theme));
        SelectLanguageOption(_config.Global.Language);
        _chkAutostart.Checked = _config.Global.Autostart || _autostart.IsEnabled();
        _chkMonitoringAutoStart.Checked = _config.Global.MonitoringAutoStart;
        _chkStartMinimized.Checked = _config.Global.StartMinimized;
        _chkMinimizeToTray.Checked = _config.Global.MinimizeToTrayOnClose;

        _chkWatchdogEnabled.Checked = _config.Watchdog.Enabled;
        _txtWatchdogPushUrl.Text = _config.Watchdog.PushUrl;
        _numWatchdogInterval.Value = _config.Watchdog.IntervalSeconds;
        _numWatchdogMax.Value = _config.Watchdog.MaxSecondsWithoutSuccessfulCheck;
        _chkWatchdogMachineInfo.Checked = _config.Watchdog.SendMachineInfo;
        RefreshTrayMenu();
    }

    private void ApplyControlsToConfig()
    {
        _config.Global.DefaultIntervalSeconds = (int)_numDefaultInterval.Value;
        _config.Global.HttpTimeoutMs = (int)_numHttpTimeout.Value;
        _config.Global.TcpTimeoutMs = (int)_numTcpTimeout.Value;
        _config.Global.PingTimeoutMs = (int)_numPingTimeout.Value;
        _config.Global.SendMachineInfo = _chkSendMachineInfo.Checked;
        _config.Global.MaskPushUrls = _chkMaskPushUrls.Checked;
        _config.Global.LogLevel = GetComboValue(_cmbLogLevel, "Info");
        _config.Global.Theme = GetComboValue(_cmbTheme, "Light");
        _config.Global.Language = GetSelectedLanguage();
        _config.Global.Autostart = _chkAutostart.Checked;
        _config.Global.MonitoringAutoStart = _chkMonitoringAutoStart.Checked;
        _config.Global.StartMinimized = _chkStartMinimized.Checked;
        _config.Global.MinimizeToTrayOnClose = _chkMinimizeToTray.Checked;

        _config.Watchdog.Enabled = _chkWatchdogEnabled.Checked;
        _config.Watchdog.PushUrl = _txtWatchdogPushUrl.Text.Trim();
        _config.Watchdog.IntervalSeconds = (int)_numWatchdogInterval.Value;
        _config.Watchdog.MaxSecondsWithoutSuccessfulCheck = (int)_numWatchdogMax.Value;
        _config.Watchdog.SendMachineInfo = _chkWatchdogMachineInfo.Checked;
        _config.Normalize();
    }

    private bool SaveConfig(bool showMessage)
    {
        var previousLanguage = AppLanguages.Normalize(_config.Global.Language);
        ApplyControlsToConfig();
        var languageChanged = !string.Equals(previousLanguage, AppLanguages.Normalize(_config.Global.Language), StringComparison.OrdinalIgnoreCase);
        if (!ValidateConfig(out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, I18n.T("Konfiguration prüfen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            _configService.Save(_config);
            if (!languageChanged)
            {
                I18n.Apply(_config.Global.Language);
            }

            _logger.SetLevel(_config.Global.LogLevel);
            if (_config.Global.Autostart)
            {
                _autostart.Enable(Application.ExecutablePath);
            }
            else
            {
                _autostart.Disable();
            }

            _monitoring.ReloadConfig(_config);
            if (!_config.Global.MonitoringAutoStart)
            {
                _monitoring.Stop();
            }

            RefreshAllCheckGrids();
            RefreshAgentLabels();
            RefreshTrayMenu();
            _logger.Info("Konfiguration gespeichert: " + _configService.ConfigPath);
            statusLabel.Text = I18n.T("Konfiguration gespeichert");
            if (showMessage)
            {
                var message = languageChanged
                    ? I18n.T("Konfiguration gespeichert.") + "\r\n\r\n" + I18n.T("Die Sprache wurde gespeichert. Um die Sprache vollständig zu ändern, muss die App neu gestartet werden.")
                    : I18n.T("Konfiguration gespeichert.");
                var title = languageChanged ? I18n.T("Sprache geändert") : "Uptime Kuma Tray Agent";
                MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Konfiguration konnte nicht gespeichert werden", ex);
            MessageBox.Show(this, I18n.T("Konfiguration konnte nicht gespeichert werden: ") + ex.Message, I18n.T("Fehler"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ValidateConfig(out string message)
    {
        var errors = new List<string>();
        foreach (var check in _config.PingChecks.Where(check => check.Enabled))
        {
            if (!ValidationUtils.IsValidHost(check.Host))
            {
                errors.Add($"Ping '{check.Name}': {I18n.T("Hostname oder IP-Adresse ist ungültig.")}");
            }

            if (!ValidationUtils.IsValidHttpUrl(check.PushUrl, allowEmpty: true))
            {
                errors.Add($"Ping '{check.Name}': {I18n.T("Push-URL ist ungültig.")}");
            }
        }

        foreach (var check in _config.TcpChecks.Where(check => check.Enabled))
        {
            if (!ValidationUtils.IsValidHost(check.Host))
            {
                errors.Add($"TCP '{check.Name}': {I18n.T("Hostname oder IP-Adresse ist ungültig.")}");
            }

            if (!ValidationUtils.IsValidPort(check.Port))
            {
                errors.Add($"TCP '{check.Name}': {I18n.T("Port ist ungültig.")}");
            }

            if (!ValidationUtils.IsValidHttpUrl(check.PushUrl, allowEmpty: true))
            {
                errors.Add($"TCP '{check.Name}': {I18n.T("Push-URL ist ungültig.")}");
            }
        }

        foreach (var check in _config.ServiceChecks.Where(check => check.Enabled))
        {
            if (string.IsNullOrWhiteSpace(check.ServiceName))
            {
                errors.Add($"{I18n.T("Dienst")} '{check.DisplayName}': {I18n.T("Dienstname fehlt.")}");
            }

            if (!ValidationUtils.IsValidHttpUrl(check.PushUrl, allowEmpty: true))
            {
                errors.Add($"{I18n.T("Dienst")} '{check.DisplayName}': {I18n.T("Push-URL ist ungültig.")}");
            }
        }

        foreach (var check in _config.DriveChecks.Where(check => check.Enabled))
        {
            if (string.IsNullOrWhiteSpace(check.Path))
            {
                errors.Add($"{I18n.T("Laufwerk")} '{check.Name}': {I18n.T("Pfad oder Laufwerksbuchstabe fehlt.")}");
            }

            if (!ValidationUtils.IsValidHttpUrl(check.PushUrl, allowEmpty: true))
            {
                errors.Add($"{I18n.T("Laufwerk")} '{check.Name}': {I18n.T("Push-URL ist ungültig.")}");
            }

            if (check.ReconnectIfUnavailable
                && !check.Path.StartsWith(@"\\", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(check.ReconnectPath)
                && !(check.Path.Length >= 2 && check.Path[1] == ':'))
            {
                errors.Add($"{I18n.T("Laufwerk")} '{check.Name}': {I18n.T("Für Reconnect wird ein Laufwerksbuchstabe oder UNC-Pfad benötigt.")}");
            }
        }

        if (_config.Watchdog.Enabled && !ValidationUtils.IsValidHttpUrl(_config.Watchdog.PushUrl, allowEmpty: true))
        {
            errors.Add($"Watchdog: {I18n.T("Push-URL ist ungültig.")}");
        }

        message = string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    private void RefreshAgentLabels()
    {
        var monitoringActive = IsMonitoringActive();
        _lblAgentStatus.Text = I18n.T("Läuft");
        _lblMonitoring.Text = monitoringActive ? I18n.T("aktiv") : I18n.T("inaktiv");
        _lblLastRun.Text = TimeFormatter.FormatDate(_monitoring.LastRun);
        _lblLastError.Text = _monitoring.LastError;
        _lblVersion.Text = AppVersion.Current;
        _lblComputer.Text = Environment.MachineName;
        var statuses = _monitoring.GetStatuses();
        var ok = statuses.Count(status => status.Enabled && status.State == AgentCheckState.Up);
        var down = statuses.Count(status => status.Enabled && status.State == AgentCheckState.Down);
        statusLabel.Text = $"{(monitoringActive ? I18n.T("Monitoring aktiv") : I18n.T("Monitoring inaktiv"))} | {ok} OK / {down} Down";
        headerBadge.Text = monitoringActive ? I18n.T("Monitoring aktiv") : I18n.T("Monitoring inaktiv");
        headerBadge.BackColor = monitoringActive ? _theme.AccentSoft : _theme.SurfaceAlt;
        headerBadge.ForeColor = monitoringActive ? _theme.AccentDark : _theme.MutedText;
        UpdateTrayIcon();
    }

    private void RefreshStatusGrid()
    {
        if (_statusGrid is null)
        {
            return;
        }

        var statuses = _monitoring.GetStatuses();
        RefreshKpiCards(statuses);

        var query = _txtMonitorSearch?.Text.Trim() ?? "";
        var filter = _cmbStatusFilter?.SelectedItem?.ToString() ?? I18n.T("Alle");
        var displayStatuses = statuses.Where(status =>
        {
            var matchesQuery = string.IsNullOrWhiteSpace(query)
                || status.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || status.Target.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || status.LastMessage.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || status.LastError.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            if (!matchesQuery)
            {
                return false;
            }

            if (string.Equals(filter, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return status.State == AgentCheckState.Up;
            }

            if (string.Equals(filter, I18n.T("Fehler"), StringComparison.OrdinalIgnoreCase))
            {
                return status.State == AgentCheckState.Down;
            }

            if (string.Equals(filter, I18n.T("Warnung"), StringComparison.OrdinalIgnoreCase))
            {
                return status.State == AgentCheckState.Warning;
            }

            if (string.Equals(filter, I18n.T("Unbekannt"), StringComparison.OrdinalIgnoreCase))
            {
                return status.State == AgentCheckState.Unknown;
            }

            return true;
        }).ToList();

        var gridState = CaptureGridState(_statusGrid);
        _statusGrid.SuspendLayout();
        _statusGrid.Rows.Clear();
        foreach (var status in displayStatuses)
        {
            var index = _statusGrid.Rows.Add(
                I18n.CheckTypeName(status.Type),
                status.Name,
                status.Target,
                BoolText(status.Enabled),
                StateText(status.State),
                status.LastResponseMs.HasValue ? status.LastResponseMs + " ms" : "",
                TimeFormatter.FormatDate(status.LastRun),
                TimeFormatter.FormatDate(status.LastSuccessfulCheck),
                status.LastError,
                TimeFormatter.FormatDate(status.NextRun),
                TimeFormatter.FormatDate(status.LastPushAt),
                status.LastPushHttpStatus);

            _statusGrid.Rows[index].Tag = status;
            _statusGrid.Rows[index].DefaultCellStyle.BackColor = StateColor(status.State);
            _statusGrid.Rows[index].DefaultCellStyle.ForeColor = _theme.Text;
        }

        RestoreGridState(_statusGrid, gridState);
        _statusGrid.ResumeLayout();
        UpdateTrayIcon();
    }

    private void RefreshKpiCards(IReadOnlyCollection<CheckRuntimeStatus> statuses)
    {
        if (_lblKpiTotal is null)
        {
            return;
        }

        var enabled = statuses.Where(status => status.Enabled).ToList();
        var up = enabled.Count(status => status.State == AgentCheckState.Up);
        var down = enabled.Count(status => status.State == AgentCheckState.Down);
        var responseValues = enabled.Where(status => status.LastResponseMs.HasValue).Select(status => status.LastResponseMs!.Value).ToList();
        var avg = responseValues.Count == 0 ? 0 : (long)Math.Round(responseValues.Average());

        _lblKpiTotal.Text = enabled.Count.ToString();
        _lblKpiOk.Text = up.ToString();
        _lblKpiDown.Text = down.ToString();
        _lblKpiAvg.Text = responseValues.Count == 0 ? "-" : avg + " ms";
        _lblKpiOk.ForeColor = _theme.AccentDark;
        _lblKpiDown.ForeColor = down > 0 ? _theme.Danger : _theme.MutedText;
        _lblKpiAvg.ForeColor = _theme.Text;
        RefreshNavigation();
    }

    private bool IsMonitoringActive()
    {
        return _monitoring.IsRunning || (IsWindowsServiceRunning() && _config.Global.MonitoringAutoStart);
    }

    private AgentTrayState ResolveTrayState()
    {
        if (!IsMonitoringActive())
        {
            return AgentTrayState.Inactive;
        }

        var statuses = _monitoring.GetStatuses();
        return statuses.Any(status => status.Enabled && status.State is AgentCheckState.Down or AgentCheckState.Warning or AgentCheckState.Unknown)
            ? AgentTrayState.Warning
            : AgentTrayState.Ok;
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var state = ResolveTrayState();
        var text = state switch
        {
            AgentTrayState.Ok => "Uptime Kuma Tray Agent - " + I18n.T("Monitoring OK"),
            AgentTrayState.Warning => "Uptime Kuma Tray Agent - " + I18n.T("Warnung oder Fehler"),
            AgentTrayState.Inactive => "Uptime Kuma Tray Agent - " + I18n.T("Monitoring inaktiv"),
            _ => "Uptime Kuma Tray Agent"
        };

        if (_currentTrayState == state && _trayStatusIcon is not null)
        {
            _trayIcon.Text = text;
            return;
        }

        var newIcon = AppIconFactory.CreateIconForState(state);
        var oldIcon = _trayStatusIcon;
        _trayStatusIcon = newIcon;
        _currentTrayState = state;
        _trayIcon.Icon = newIcon;
        _trayIcon.Text = text;
        oldIcon?.Dispose();
    }

    private void ApplyTheme()
    {
        _theme = ThemeModes.PaletteFor(_config.Global.Theme);
        BackColor = _theme.Window;
        ForeColor = _theme.Text;

        headerPanel.BackColor = _theme.Surface;
        _shellLayout.BackColor = _theme.Window;
        _sidebarPanel.BackColor = _theme.Surface;
        _btnThemeToggle.Text = string.Equals(ThemeModes.Normalize(_config.Global.Theme), "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";
        mainLayout.BackColor = _theme.Window;

        StyleControl(_shellLayout);
        _sectionTitle.ForeColor = _theme.Text;
        _sectionSubtitle.ForeColor = _theme.MutedText;
        _sidebarVersion.ForeColor = _theme.MutedText;
        StyleStatusStrip();
        StyleTrayMenu();
        StyleGrid(_statusGrid);
        StyleGrid(_pingGrid);
        StyleGrid(_tcpGrid);
        StyleGrid(_serviceGrid);
        StyleGrid(_driveGrid);
        StyleGrid(_availableServicesGrid);
        RefreshAgentLabels();
        RefreshStatusGrid();
        RefreshNavigation();
    }

    private void StyleControl(Control control)
    {
        switch (control)
        {
            case ModernCardPanel card:
                card.FillColor = _theme.Surface;
                card.BorderColor = _theme.Border;
                card.BackColor = control.Parent is null ? _theme.Window : control.Parent.BackColor;
                card.ForeColor = _theme.Text;
                card.Invalidate();
                break;
            case DataGridView grid:
                StyleGrid(grid);
                return;
            case Button button:
                StyleButton(button);
                break;
            case TextBox textBox:
                textBox.BackColor = _theme.SurfaceAlt;
                textBox.ForeColor = _theme.Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = _theme.SurfaceAlt;
                comboBox.ForeColor = _theme.Text;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = _theme.SurfaceAlt;
                numeric.ForeColor = _theme.Text;
                break;
            case CheckBox checkBox:
                checkBox.BackColor = _theme.Surface;
                checkBox.ForeColor = _theme.Text;
                checkBox.UseVisualStyleBackColor = false;
                break;
            case GroupBox groupBox:
                groupBox.BackColor = _theme.Surface;
                groupBox.ForeColor = _theme.Text;
                break;
            case TabPage tabPage:
                tabPage.BackColor = _theme.Window;
                tabPage.ForeColor = _theme.Text;
                break;
            case Label label when label != headerBadge:
                label.BackColor = Color.Transparent;
                label.ForeColor = _theme.Text;
                break;
            case Panel or TableLayoutPanel or FlowLayoutPanel:
                if (control != headerPanel)
                {
                    control.BackColor = control.Parent is GroupBox or ModernCardPanel ? _theme.Surface : _theme.Window;
                    control.ForeColor = _theme.Text;
                }
                break;
        }

        foreach (Control child in control.Controls)
        {
            StyleControl(child);
        }
    }

    private void StyleButton(Button button)
    {
        var danger = ContainsAnyButtonText(button.Text, "Löschen", "Dienst stoppen", "Monitoring stoppen", "Deinstallieren");
        var secondary = ContainsAnyButtonText(button.Text, "Logs öffnen", "Konfiguration öffnen", "Bearbeiten", "Aktualisieren");

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = secondary ? 1 : 0;
        button.FlatAppearance.BorderColor = _theme.Border;
        button.BackColor = !button.Enabled ? _theme.Disabled : danger ? _theme.Danger : secondary ? _theme.SurfaceAlt : _theme.Accent;
        button.ForeColor = !button.Enabled ? _theme.MutedText : danger ? Color.White : !secondary ? _theme.ButtonText : _theme.Text;
        button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
        button.Padding = new Padding(10, 3, 10, 3);
        button.Height = Math.Max(button.Height, 34);
    }

    private static bool ContainsAnyButtonText(string text, params string[] keys)
    {
        return keys.Any(key => text.Contains(I18n.T(key), StringComparison.OrdinalIgnoreCase));
    }

    private void StyleGrid(DataGridView? grid)
    {
        if (grid is null)
        {
            return;
        }

        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = _theme.Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = _theme.GridLine;
        grid.ForeColor = _theme.Text;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle.BackColor = _theme.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = _theme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
        grid.DefaultCellStyle.BackColor = _theme.Surface;
        grid.DefaultCellStyle.ForeColor = _theme.Text;
        grid.DefaultCellStyle.SelectionBackColor = _theme.AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = _theme.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = _theme.SurfaceAlt;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = _theme.Text;
        grid.RowTemplate.Height = 28;
    }

    private void StyleStatusStrip()
    {
        statusStrip.BackColor = _theme.SurfaceAlt;
        statusStrip.ForeColor = _theme.MutedText;
        statusLabel.ForeColor = _theme.MutedText;
    }

    private void StyleTrayMenu()
    {
        if (_trayMenu is null)
        {
            return;
        }

        _trayMenu.BackColor = _theme.Surface;
        _trayMenu.ForeColor = _theme.Text;
        foreach (ToolStripItem item in _trayMenu.Items)
        {
            item.BackColor = _theme.Surface;
            item.ForeColor = _theme.Text;
        }
    }

    private void RefreshAllCheckGrids()
    {
        RefreshPingGrid();
        RefreshTcpGrid();
        RefreshServiceGrid();
        RefreshDriveGrid();
        RefreshNavigation();
    }

    private void RefreshPingGrid()
    {
        var gridState = CaptureGridState(_pingGrid);
        _pingGrid.SuspendLayout();
        _pingGrid.Rows.Clear();
        foreach (var check in _config.PingChecks)
        {
            var index = _pingGrid.Rows.Add(
                BoolText(check.Enabled),
                check.Name,
                check.Host,
                check.IntervalSeconds,
                check.TimeoutMs,
                BoolText(check.SendMachineInfo),
                FormatRestartServices(check.RestartServicesOnFailure, check.ForceKillRestartServicesOnTimeout),
                DisplayPushUrl(check.PushUrl),
                check.Note);
            _pingGrid.Rows[index].Tag = check;
        }

        RestoreGridState(_pingGrid, gridState);
        _pingGrid.ResumeLayout();
    }

    private void RefreshTcpGrid()
    {
        var gridState = CaptureGridState(_tcpGrid);
        _tcpGrid.SuspendLayout();
        _tcpGrid.Rows.Clear();
        foreach (var check in _config.TcpChecks)
        {
            var index = _tcpGrid.Rows.Add(
                BoolText(check.Enabled),
                check.Name,
                check.Host,
                check.Port,
                check.IntervalSeconds,
                check.TimeoutMs,
                BoolText(check.SendMachineInfo),
                FormatRestartServices(check.RestartServicesOnFailure, check.ForceKillRestartServicesOnTimeout),
                FormatTcpConnectionLogging(check),
                DisplayPushUrl(check.PushUrl),
                check.Note);
            _tcpGrid.Rows[index].Tag = check;
        }

        RestoreGridState(_tcpGrid, gridState);
        _tcpGrid.ResumeLayout();
    }

    private void RefreshServiceGrid()
    {
        var gridState = CaptureGridState(_serviceGrid);
        _serviceGrid.SuspendLayout();
        _serviceGrid.Rows.Clear();
        foreach (var check in _config.ServiceChecks)
        {
            var index = _serviceGrid.Rows.Add(
                BoolText(check.Enabled),
                check.DisplayName,
                check.ServiceName,
                I18n.ServiceStatusName(check.ExpectedStatus),
                check.IntervalSeconds,
                BoolText(check.RestartIfStopped),
                BoolText(check.SendMachineInfo),
                FormatRestartServices(check.RestartServicesOnFailure, check.ForceKillRestartServicesOnTimeout),
                DisplayPushUrl(check.PushUrl),
                check.Note);
            _serviceGrid.Rows[index].Tag = check;
        }

        RestoreGridState(_serviceGrid, gridState);
        _serviceGrid.ResumeLayout();
    }

    private void RefreshDriveGrid()
    {
        var gridState = CaptureGridState(_driveGrid);
        _driveGrid.SuspendLayout();
        _driveGrid.Rows.Clear();
        foreach (var check in _config.DriveChecks)
        {
            var index = _driveGrid.Rows.Add(
                BoolText(check.Enabled),
                check.Name,
                check.Path,
                check.IntervalSeconds,
                check.MinimumFreePercent,
                check.MinimumFreeGb,
                BoolText(check.ReconnectIfUnavailable),
                BoolText(check.LogDetails),
                BoolText(check.SendMachineInfo),
                DisplayPushUrl(check.PushUrl),
                check.Note);
            _driveGrid.Rows[index].Tag = check;
        }

        RestoreGridState(_driveGrid, gridState);
        _driveGrid.ResumeLayout();
    }

    private string DisplayPushUrl(string pushUrl)
    {
        return _config.Global.MaskPushUrls ? UrlMasker.MaskPushUrl(pushUrl) : pushUrl;
    }

    private static string BoolText(bool value)
    {
        return value ? I18n.T("Ja") : I18n.T("Nein");
    }

    private static string FormatRestartServices(IReadOnlyCollection<string> serviceNames, bool forceKillOnTimeout)
    {
        if (serviceNames.Count == 0)
        {
            return I18n.T("Keine");
        }

        var suffix = forceKillOnTimeout ? I18n.T(" (Force bei Timeout)") : "";
        return I18n.T("Neustart: ") + string.Join(", ", serviceNames) + suffix;
    }

    private static string FormatTcpConnectionLogging(TcpCheckConfig check)
    {
        return check.LogTcpConnections
            ? I18n.TcpDirectionName(check.TcpConnectionLogDirection)
            : I18n.T("Aus");
    }

    private void StartMonitoring()
    {
        _chkMonitoringAutoStart.Checked = true;
        _config.Global.MonitoringAutoStart = true;
        if (!SaveConfig(showMessage: false))
        {
            return;
        }

        if (IsWindowsServiceRunning())
        {
            statusLabel.Text = I18n.T("Monitoring im Windows-Dienst aktiviert");
        }
        else
        {
            _monitoring.Start();
        }

        RefreshAgentLabels();
    }

    private void StopMonitoring()
    {
        _chkMonitoringAutoStart.Checked = false;
        _config.Global.MonitoringAutoStart = false;
        SaveConfig(showMessage: false);
        _monitoring.Stop();
        statusLabel.Text = I18n.T("Monitoring deaktiviert");
        RefreshAgentLabels();
    }

    private bool IsWindowsServiceRunning()
    {
        var status = _windowsServiceManager.GetStatus(AgentWindowsService.WindowsServiceName);
        return status.Success && string.Equals(status.Status, "Running", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TestAllChecksAsync()
    {
        if (!SaveConfig(showMessage: false))
        {
            return;
        }

        var sendPush = AskSendPushForTest();
        if (sendPush is null)
        {
            return;
        }

        statusLabel.Text = I18n.T("Testlauf läuft...");
        try
        {
            await _monitoring.RunAllOnceAsync(sendPush.Value, CancellationToken.None);
            RefreshStatusGrid();
            MessageBox.Show(this, I18n.T("Testlauf abgeschlossen."), I18n.T("Testlauf"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Testlauf fehlgeschlagen", ex);
            MessageBox.Show(this, ex.Message, I18n.T("Testlauf fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            statusLabel.Text = I18n.T("Bereit");
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        _availableUpdate = null;
        _btnInstallUpdate.Enabled = false;
        statusLabel.Text = "Suche nach Updates...";

        try
        {
            _config.Normalize();
            var result = await _updateService
                .CheckForUpdatesAsync(_config.Updates, AppVersion.Current, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!result.UpdateAvailable)
            {
                MessageBox.Show(this, result.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _availableUpdate = result;
            _btnInstallUpdate.Enabled = result.Asset is not null;

            var assetText = result.Asset is null
                ? "Kein passendes Installationspaket gefunden."
                : "Paket: " + result.Asset.Name;
            MessageBox.Show(
                this,
                $"{result.Message}\r\n\r\n{assetText}\r\nRelease: {result.Release?.HtmlUrl}",
                "Update",
                MessageBoxButtons.OK,
                result.Asset is null ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Updateprüfung fehlgeschlagen", ex);
            MessageBox.Show(this, ex.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            statusLabel.Text = I18n.T("Bereit");
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_availableUpdate?.Asset is null)
        {
            await CheckForUpdatesAsync();
            if (_availableUpdate?.Asset is null)
            {
                return;
            }
        }

        var update = _availableUpdate;
        var confirm = MessageBox.Show(
            this,
            $"Version {update.Release?.Version} installieren?\r\n\r\nDer MSI-Installer wird heruntergeladen und mit Administratorrechten gestartet.",
            "Update",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _btnInstallUpdate.Enabled = false;
        statusLabel.Text = "Update wird heruntergeladen...";
        var progress = new Progress<DownloadProgress>(download =>
        {
            statusLabel.Text = download.Percent.HasValue
                ? $"Update wird heruntergeladen... {download.Percent:0.0}%"
                : $"Update wird heruntergeladen... {download.BytesReceived / 1024 / 1024} MB";
        });

        var result = await _updateInstaller
            .DownloadAndStartInstallerAsync(update, progress, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Success)
        {
            _btnInstallUpdate.Enabled = true;
            statusLabel.Text = I18n.T("Bereit");
            MessageBox.Show(this, result.Message, "Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(
            this,
            "Der MSI-Installer wurde gestartet. Die App wird jetzt beendet, damit die Dateien aktualisiert werden können.",
            "Update",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        ExitApplication();
    }

    private bool? AskSendPushForTest()
    {
        var result = MessageBox.Show(
            this,
            I18n.T("Soll der Test auch an Uptime Kuma gesendet werden?\r\n\r\nJa = Push senden\r\nNein = nur lokal testen"),
            I18n.T("Testlauf"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => true,
            DialogResult.No => false,
            _ => null
        };
    }

    private void AddPingCheck()
    {
        var check = new PingCheckConfig
        {
            IntervalSeconds = _config.Global.DefaultIntervalSeconds,
            TimeoutMs = _config.Global.PingTimeoutMs
        };

        if (CheckEditorForm.EditPing(this, check, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            _config.PingChecks.Add(check);
            PersistCheckChanges();
        }
    }

    private void EditPingCheck()
    {
        var selected = GetSelected<PingCheckConfig>(_pingGrid);
        if (selected is null)
        {
            return;
        }

        var copy = selected.Clone();
        if (CheckEditorForm.EditPing(this, copy, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            ReplaceById(_config.PingChecks, selected.Id, copy);
            PersistCheckChanges();
        }
    }

    private void DeletePingCheck()
    {
        var selected = GetSelected<PingCheckConfig>(_pingGrid);
        if (selected is null || !ConfirmDelete(selected.Name))
        {
            return;
        }

        _config.PingChecks.RemoveAll(check => check.Id == selected.Id);
        PersistCheckChanges();
    }

    private async Task TestSelectedPingAsync()
    {
        var selected = GetSelected<PingCheckConfig>(_pingGrid);
        if (selected is null)
        {
            return;
        }

        await TestSingleAsync(CheckType.Ping, selected.Id);
    }

    private void AddTcpCheck()
    {
        var check = new TcpCheckConfig
        {
            IntervalSeconds = _config.Global.DefaultIntervalSeconds,
            TimeoutMs = _config.Global.TcpTimeoutMs
        };

        if (CheckEditorForm.EditTcp(this, check, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            _config.TcpChecks.Add(check);
            PersistCheckChanges();
        }
    }

    private void EditTcpCheck()
    {
        var selected = GetSelected<TcpCheckConfig>(_tcpGrid);
        if (selected is null)
        {
            return;
        }

        var copy = selected.Clone();
        if (CheckEditorForm.EditTcp(this, copy, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            ReplaceById(_config.TcpChecks, selected.Id, copy);
            PersistCheckChanges();
        }
    }

    private void DeleteTcpCheck()
    {
        var selected = GetSelected<TcpCheckConfig>(_tcpGrid);
        if (selected is null || !ConfirmDelete(selected.Name))
        {
            return;
        }

        _config.TcpChecks.RemoveAll(check => check.Id == selected.Id);
        PersistCheckChanges();
    }

    private async Task TestSelectedTcpAsync()
    {
        var selected = GetSelected<TcpCheckConfig>(_tcpGrid);
        if (selected is null)
        {
            return;
        }

        await TestSingleAsync(CheckType.Tcp, selected.Id);
    }

    private void AddServiceCheck(ServiceInfo? serviceInfo)
    {
        var check = new ServiceCheckConfig
        {
            DisplayName = serviceInfo?.DisplayName ?? "",
            ServiceName = serviceInfo?.ServiceName ?? "",
            IntervalSeconds = _config.Global.DefaultIntervalSeconds
        };

        if (CheckEditorForm.EditService(this, check, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            _config.ServiceChecks.Add(check);
            PersistCheckChanges();
        }
    }

    private void AddServiceCheckFromList()
    {
        var selected = GetSelected<ServiceInfo>(_availableServicesGrid);
        if (selected is null)
        {
            MessageBox.Show(this, I18n.T("Bitte zuerst einen lokalen Dienst auswählen."), I18n.T("Dienst auswählen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AddServiceCheck(selected);
    }

    private void EditServiceCheck()
    {
        var selected = GetSelected<ServiceCheckConfig>(_serviceGrid);
        if (selected is null)
        {
            return;
        }

        var copy = selected.Clone();
        if (CheckEditorForm.EditService(this, copy, _config.Global.MaskPushUrls, _windowsServiceManager))
        {
            ReplaceById(_config.ServiceChecks, selected.Id, copy);
            PersistCheckChanges();
        }
    }

    private void DeleteServiceCheck()
    {
        var selected = GetSelected<ServiceCheckConfig>(_serviceGrid);
        if (selected is null || !ConfirmDelete(selected.DisplayName))
        {
            return;
        }

        _config.ServiceChecks.RemoveAll(check => check.Id == selected.Id);
        PersistCheckChanges();
    }

    private async Task TestSelectedServiceAsync()
    {
        var selected = GetSelected<ServiceCheckConfig>(_serviceGrid);
        if (selected is null)
        {
            return;
        }

        await TestSingleAsync(CheckType.Service, selected.Id);
    }

    private void AddDriveCheck()
    {
        var check = new DriveCheckConfig
        {
            IntervalSeconds = Math.Max(60, _config.Global.DefaultIntervalSeconds),
            Path = "C:\\"
        };

        if (CheckEditorForm.EditDrive(this, check, _config.Global.MaskPushUrls))
        {
            _config.DriveChecks.Add(check);
            PersistCheckChanges();
        }
    }

    private void EditDriveCheck()
    {
        var selected = GetSelected<DriveCheckConfig>(_driveGrid);
        if (selected is null)
        {
            return;
        }

        var copy = selected.Clone();
        if (CheckEditorForm.EditDrive(this, copy, _config.Global.MaskPushUrls))
        {
            ReplaceById(_config.DriveChecks, selected.Id, copy);
            PersistCheckChanges();
        }
    }

    private void DeleteDriveCheck()
    {
        var selected = GetSelected<DriveCheckConfig>(_driveGrid);
        if (selected is null || !ConfirmDelete(selected.Name))
        {
            return;
        }

        _config.DriveChecks.RemoveAll(check => check.Id == selected.Id);
        PersistCheckChanges();
    }

    private void PersistCheckChanges()
    {
        _config.Normalize();
        SaveConfig(showMessage: false);
    }

    private async Task TestSelectedDriveAsync()
    {
        var selected = GetSelected<DriveCheckConfig>(_driveGrid);
        if (selected is null)
        {
            return;
        }

        await TestSingleAsync(CheckType.Drive, selected.Id);
    }

    private async Task TestSingleAsync(CheckType type, string id)
    {
        if (!SaveConfig(showMessage: false))
        {
            return;
        }

        var sendPush = AskSendPushForTest();
        if (sendPush is null)
        {
            return;
        }

        try
        {
            var result = await _monitoring.RunSingleAsync(type, id, sendPush.Value, CancellationToken.None);
            RefreshStatusGrid();
            MessageBox.Show(
                this,
                result is null ? I18n.T("Check wurde nicht gefunden.") : $"{result.Name}: {StateText(result.State)}\r\n{result.LastMessage}\r\n{result.LastError}",
                I18n.T("Testergebnis"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Einzeltest fehlgeschlagen", ex);
            MessageBox.Show(this, ex.Message, I18n.T("Testergebnis"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadLocalServicesAsync()
    {
        statusLabel.Text = I18n.T("Lokale Dienste werden geladen...");
        try
        {
            var services = await Task.Run(() => _windowsServiceManager.ListServices());
            var gridState = CaptureGridState(_availableServicesGrid);
            _availableServicesGrid.SuspendLayout();
            _availableServicesGrid.Rows.Clear();
            foreach (var service in services)
            {
                var index = _availableServicesGrid.Rows.Add(service.DisplayName, service.ServiceName, I18n.ServiceStatusName(service.Status), BoolText(service.CanStop));
                _availableServicesGrid.Rows[index].Tag = service;
            }

            RestoreGridState(_availableServicesGrid, gridState);
            _availableServicesGrid.ResumeLayout();
            statusLabel.Text = $"{services.Count} {I18n.T("lokale Dienste geladen")}";
        }
        catch (Exception ex)
        {
            _logger.Error("Lokale Dienste konnten nicht geladen werden", ex);
            MessageBox.Show(this, ex.Message, I18n.T("Dienste laden"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = I18n.T("Dienste konnten nicht geladen werden");
        }
    }

    private async Task RunServiceOperationAsync(string operation)
    {
        var serviceName = GetSelectedServiceName();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            MessageBox.Show(this, I18n.T("Bitte zuerst einen Dienst-Check oder lokalen Dienst auswählen."), I18n.T("Dienst auswählen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (operation == "restart-force")
        {
            var confirm = MessageBox.Show(
                this,
                I18n.F("Dienst {0} hart neu starten?\r\n\r\nWenn der Dienst beim Stoppen hängen bleibt, wird der zugehörige Prozess erzwungen beendet.", serviceName),
                I18n.T("Dienst hart neu starten"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            ServiceOperationResult result = operation switch
            {
                "start" => await _windowsServiceManager.StartServiceAsync(serviceName, TimeSpan.FromSeconds(30), CancellationToken.None),
                "stop" => await _windowsServiceManager.StopServiceAsync(serviceName, TimeSpan.FromSeconds(30), CancellationToken.None),
                "restart" => await _windowsServiceManager.RestartServiceAsync(serviceName, TimeSpan.FromSeconds(30), CancellationToken.None),
                "restart-force" => await _windowsServiceManager.RestartServiceAsync(serviceName, TimeSpan.FromSeconds(30), CancellationToken.None, forceKillOnStopTimeout: true),
                _ => new ServiceOperationResult { Success = false, Message = I18n.T("Unbekannte Aktion") }
            };

            MessageBox.Show(
                this,
                $"{serviceName}: {result.Message}\r\n{I18n.T("Status")}: {I18n.ServiceStatusName(result.Status)}\r\n{result.ErrorCategory}",
                I18n.T("Dienstaktion"),
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            await LoadLocalServicesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Dienstaktion fehlgeschlagen", ex);
            MessageBox.Show(this, ex.Message, I18n.T("Dienstaktion"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GetSelectedServiceName()
    {
        var configured = GetSelected<ServiceCheckConfig>(_serviceGrid);
        if (configured is not null)
        {
            return configured.ServiceName;
        }

        var available = GetSelected<ServiceInfo>(_availableServicesGrid);
        return available?.ServiceName ?? "";
    }

    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(_logger.LogDirectory);
            Process.Start(new ProcessStartInfo { FileName = _logger.LogDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, I18n.T("Logs öffnen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenConfigFile()
    {
        if (!SaveConfig(showMessage: false))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = Quote(_configService.ConfigPath), UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, I18n.T("Konfiguration öffnen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshLogText()
    {
        if (_txtLogs is null)
        {
            return;
        }

        var latestText = _logger.ReadRecentLines();
        if (string.Equals(_txtLogs.Text, latestText, StringComparison.Ordinal))
        {
            return;
        }

        var selectionStart = Math.Min(_txtLogs.SelectionStart, latestText.Length);
        var selectionLength = Math.Min(_txtLogs.SelectionLength, latestText.Length - selectionStart);
        _txtLogs.Text = latestText;
        _txtLogs.SelectionStart = selectionStart;
        _txtLogs.SelectionLength = selectionLength;
    }

    private void ToggleAutostartFromTray()
    {
        _chkAutostart.Checked = !_chkAutostart.Checked;
        SaveConfig(showMessage: false);
        RefreshTrayMenu();
    }

    private void RefreshTrayMenu()
    {
        if (_trayAutostart is null)
        {
            return;
        }

        _trayAutostart.Checked = _chkAutostart?.Checked ?? _autostart.IsEnabled();
        _trayAutostart.Text = _trayAutostart.Checked ? I18n.T("Autostart deaktivieren") : I18n.T("Autostart aktivieren");
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ShowStatus()
    {
        ShowMainWindow();
        tabMain.SelectedTab = tabGeneral;
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.ShowBalloonTip(1500, "Uptime Kuma Tray Agent", I18n.T("Agent läuft im Infobereich weiter."), ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        Close();
    }

    private void RunUninstallFlow()
    {
        using var dialog = new UninstallDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _monitoring.Stop();
            _autostart.Disable();
            if (dialog.DeleteConfig)
            {
                _configService.DeleteConfig();
            }

            if (dialog.DeleteLogs)
            {
                _logger.DeleteLogs();
            }

            MessageBox.Show(
                this,
                I18n.T("Autostart und ausgewählte Daten wurden entfernt. Der Programmordner kann danach gelöscht werden."),
                I18n.T("Deinstallation"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            ExitApplication();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, I18n.T("Deinstallation"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void ReplaceById<T>(List<T> items, string id, T replacement) where T : class
    {
        for (var i = 0; i < items.Count; i++)
        {
            var currentId = items[i] switch
            {
                PingCheckConfig ping => ping.Id,
                TcpCheckConfig tcp => tcp.Id,
                ServiceCheckConfig service => service.Id,
                DriveCheckConfig drive => drive.Id,
                _ => ""
            };

            if (string.Equals(currentId, id, StringComparison.OrdinalIgnoreCase))
            {
                items[i] = replacement;
                return;
            }
        }
    }

    private static T? GetSelected<T>(DataGridView grid) where T : class
    {
        if (grid.SelectedRows.Count == 0)
        {
            return null;
        }

        return grid.SelectedRows[0].Tag as T;
    }

    private bool ConfirmDelete(string name)
    {
        return MessageBox.Show(
            this,
            $"'{name}' {I18n.T("wirklich löschen?")}",
            I18n.T("Löschen bestätigen"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private static Label AddStatusRow(TableLayoutPanel table, string label)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        var value = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        table.Controls.Add(value, 1, row);
        return value;
    }

    private static NumericUpDown AddNumber(TableLayoutPanel table, string label, int min, int max, int value, int column, int row)
    {
        EnsureRows(table, row + 1);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Dock = DockStyle.Fill,
            ThousandsSeparator = true
        };
        table.Controls.Add(number, column + 1, row);
        return number;
    }

    private static ComboBox AddCombo(TableLayoutPanel table, string label, IEnumerable<string> values, int column, int row)
    {
        EnsureRows(table, row + 1);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values.Cast<object>().ToArray());
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }

        table.Controls.Add(combo, column + 1, row);
        return combo;
    }

    private static ComboBox AddOptionCombo(
        TableLayoutPanel table,
        string label,
        IEnumerable<string> values,
        Func<string, string> displaySelector,
        int column,
        int row)
    {
        EnsureRows(table, row + 1);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values.Select(value => new DisplayOption(value, displaySelector)).Cast<object>().ToArray());
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }

        table.Controls.Add(combo, column + 1, row);
        return combo;
    }

    private static ComboBox AddLanguageCombo(TableLayoutPanel table, string label, int column, int row)
    {
        EnsureRows(table, row + 1);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(AppLanguages.All.Select(language => new LanguageOption(language)).Cast<object>().ToArray());
        combo.SelectedIndex = 0;
        table.Controls.Add(combo, column + 1, row);
        return combo;
    }

    private string GetSelectedLanguage()
    {
        return (_cmbLanguage.SelectedItem as LanguageOption)?.Value ?? AppLanguages.System;
    }

    private static string GetComboValue(ComboBox combo, string fallback)
    {
        return combo.SelectedItem switch
        {
            DisplayOption option => option.Value,
            LanguageOption option => option.Value,
            string value when !string.IsNullOrWhiteSpace(value) => value,
            _ => fallback
        };
    }

    private static void SelectComboValue(ComboBox combo, string value)
    {
        foreach (var item in combo.Items)
        {
            var itemValue = item switch
            {
                DisplayOption option => option.Value,
                LanguageOption option => option.Value,
                string text => text,
                _ => item?.ToString() ?? ""
            };

            if (string.Equals(itemValue, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void SelectLanguageOption(string language)
    {
        var normalized = AppLanguages.Normalize(language);
        foreach (var item in _cmbLanguage.Items.OfType<LanguageOption>())
        {
            if (string.Equals(item.Value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _cmbLanguage.SelectedItem = item;
                return;
            }
        }

        _cmbLanguage.SelectedIndex = 0;
    }

    private static TextBox AddText(TableLayoutPanel table, string label, int column, int row)
    {
        EnsureRows(table, row + 1);
        table.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
        var textBox = new TextBox { Dock = DockStyle.Fill };
        table.Controls.Add(textBox, column + 1, row);
        return textBox;
    }

    private ModernCardPanel CreateCard()
    {
        return new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            FillColor = _theme.Surface,
            BorderColor = _theme.Border,
            BackColor = Color.Transparent
        };
    }

    private Label AddMetricCard(TableLayoutPanel parent, string label, string value, int column)
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        card.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold, GraphicsUnit.Point)
        }, 0, 0);

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Consolas", 20F, FontStyle.Bold, GraphicsUnit.Point)
        };
        layout.Controls.Add(valueLabel, 0, 1);
        parent.Controls.Add(card, column, 0);
        return valueLabel;
    }

    private static void AddCardTitle(TableLayoutPanel table, string text, int row)
    {
        EnsureRows(table, row + 1);
        var title = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point)
        };
        table.Controls.Add(title, 0, row);
        table.SetColumnSpan(title, table.ColumnCount);
    }

    private static CheckBox AddCheck(TableLayoutPanel table, string text, int column, int row)
    {
        EnsureRows(table, row + 1);
        var check = new CheckBox { Text = text, Dock = DockStyle.Fill, AutoSize = true };
        table.Controls.Add(check, column, row);
        if (table.ColumnCount > column + 1)
        {
            table.SetColumnSpan(check, 2);
        }

        return check;
    }

    private static void EnsureRows(TableLayoutPanel table, int count)
    {
        while (table.RowCount < count)
        {
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Margin = new Padding(4)
        };
        button.Click += onClick;
        return button;
    }

    private static FlowLayoutPanel CreateButtonPanel(params (string Text, EventHandler Handler)[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(2)
        };

        foreach (var button in buttons)
        {
            panel.Controls.Add(CreateButton(button.Text, button.Handler));
        }

        return panel;
    }

    private void AddGridWithButtons(TabPage tabPage, DataGridView grid, Control buttons)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(buttons, 0, 0);
        var gridCard = CreateCard();
        gridCard.Controls.Add(grid);
        root.Controls.Add(gridCard, 0, 1);
        tabPage.Controls.Add(root);
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window
        };
    }

    private sealed class LanguageOption
    {
        public LanguageOption(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override string ToString()
        {
            return I18n.LanguageDisplayName(Value);
        }
    }

    private sealed class DisplayOption
    {
        private readonly Func<string, string> _displaySelector;

        public DisplayOption(string value, Func<string, string> displaySelector)
        {
            Value = value;
            _displaySelector = displaySelector;
        }

        public string Value { get; }

        public override string ToString()
        {
            return _displaySelector(Value);
        }
    }

    private static GridViewState CaptureGridState(DataGridView? grid)
    {
        if (grid is null)
        {
            return GridViewState.Empty;
        }

        var state = new GridViewState
        {
            SelectedKey = grid.SelectedRows.Count > 0 ? GetGridRowKey(grid.SelectedRows[0]) : "",
            SelectedRowIndex = grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : -1
        };

        try
        {
            state.FirstDisplayedRowIndex = grid.Rows.Count > 0 ? grid.FirstDisplayedScrollingRowIndex : -1;
        }
        catch
        {
            state.FirstDisplayedRowIndex = -1;
        }

        try
        {
            state.HorizontalOffset = grid.HorizontalScrollingOffset;
        }
        catch
        {
            state.HorizontalOffset = 0;
        }

        return state;
    }

    private static void RestoreGridState(DataGridView? grid, GridViewState state)
    {
        if (grid is null || grid.Rows.Count == 0)
        {
            return;
        }

        try
        {
            grid.ClearSelection();
            var selectedRow = FindGridRowByKey(grid, state.SelectedKey)
                              ?? (state.SelectedRowIndex >= 0 && state.SelectedRowIndex < grid.Rows.Count
                                  ? grid.Rows[state.SelectedRowIndex]
                                  : null);

            if (selectedRow is not null)
            {
                selectedRow.Selected = true;
                grid.CurrentCell = selectedRow.Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
            }
        }
        catch
        {
            // Selection restore is a convenience only; failed restores should never break refresh.
        }

        try
        {
            if (state.FirstDisplayedRowIndex >= 0 && state.FirstDisplayedRowIndex < grid.Rows.Count)
            {
                grid.FirstDisplayedScrollingRowIndex = state.FirstDisplayedRowIndex;
            }
        }
        catch
        {
            // Ignore transient layout states while the grid is rebuilding.
        }

        try
        {
            grid.HorizontalScrollingOffset = Math.Max(0, state.HorizontalOffset);
        }
        catch
        {
            // Horizontal scrolling is not available for every grid layout.
        }
    }

    private static DataGridViewRow? FindGridRowByKey(DataGridView grid, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return grid.Rows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => string.Equals(GetGridRowKey(row), key, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGridRowKey(DataGridViewRow row)
    {
        return row.Tag switch
        {
            CheckRuntimeStatus status => $"{status.Type}:{status.Id}",
            PingCheckConfig ping => "Ping:" + ping.Id,
            TcpCheckConfig tcp => "Tcp:" + tcp.Id,
            ServiceCheckConfig service => "Service:" + service.Id,
            DriveCheckConfig drive => "Drive:" + drive.Id,
            ServiceInfo serviceInfo => "LocalService:" + serviceInfo.ServiceName,
            _ => ""
        };
    }

    private sealed class GridViewState
    {
        public static readonly GridViewState Empty = new();
        public int FirstDisplayedRowIndex { get; set; } = -1;
        public int HorizontalOffset { get; set; }
        public string SelectedKey { get; set; } = "";
        public int SelectedRowIndex { get; set; } = -1;
    }

    private static string StateText(AgentCheckState state)
    {
        return I18n.CheckStateName(state);
    }

    private Color StateColor(AgentCheckState state)
    {
        if (string.Equals(_theme.Name, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return state switch
            {
                AgentCheckState.Up => Color.FromArgb(34, 68, 47),
                AgentCheckState.Down => Color.FromArgb(83, 38, 42),
                AgentCheckState.Warning => Color.FromArgb(80, 62, 30),
                AgentCheckState.Disabled => Color.FromArgb(49, 57, 61),
                _ => _theme.Surface
            };
        }

        return state switch
        {
            AgentCheckState.Up => Color.FromArgb(215, 242, 218),
            AgentCheckState.Down => Color.FromArgb(255, 222, 222),
            AgentCheckState.Warning => Color.FromArgb(255, 244, 198),
            AgentCheckState.Disabled => Color.FromArgb(232, 232, 232),
            _ => _theme.Surface
        };
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch
            {
                // The window may be closing while a background check reports status.
            }
        }
        else
        {
            action();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, int wParam, int lParam);
}
