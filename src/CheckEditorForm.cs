using System.Drawing;
using System.Windows.Forms;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent;

public sealed class CheckEditorForm : Form
{
    private readonly TableLayoutPanel _fields = new();
    private Func<string>? _validator;

    private CheckEditorForm(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(680, 520);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        _fields.Dock = DockStyle.Fill;
        _fields.ColumnCount = 2;
        _fields.AutoScroll = true;
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(_fields, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var ok = new Button { Text = I18n.T("OK"), DialogResult = DialogResult.None, Width = 100 };
        var cancel = new Button { Text = I18n.T("Abbrechen"), DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) =>
        {
            var error = _validator?.Invoke() ?? "";
            if (!string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(this, error, I18n.T("Eingaben prüfen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public static bool EditPing(IWin32Window owner, PingCheckConfig check, bool maskPushUrl, WindowsServiceManager windowsServiceManager)
    {
        using var form = new CheckEditorForm(I18n.T("Ping-Check"));
        var enabled = form.AddCheck(I18n.T("Aktiv"), check.Enabled);
        var name = form.AddText(I18n.T("Name"), check.Name);
        var host = form.AddText(I18n.T("Hostname oder IP-Adresse"), check.Host);
        var pushUrl = form.AddPushUrl(I18n.T("Uptime-Kuma-Push-URL"), check.PushUrl, maskPushUrl);
        var interval = form.AddNumber(I18n.T("Intervall in Sekunden"), check.IntervalSeconds, 5, 86400);
        var timeout = form.AddNumber(I18n.T("Timeout in Millisekunden"), check.TimeoutMs, 500, 120000);
        var machineInfo = form.AddCheck(I18n.T("Maschineninfos mitsenden"), check.SendMachineInfo);
        var restartServices = form.AddServiceRestartSelector(I18n.T("Dienste bei Fehler neu starten"), check.RestartServicesOnFailure, windowsServiceManager);
        var restartCooldown = form.AddNumber(I18n.T("Neustart-Cooldown (s)"), check.RestartServicesCooldownSeconds, 30, 86400);
        var restartBootDelay = form.AddNumber(I18n.T("Neustart nach Systemstart warten (min)"), check.RestartServicesDelayAfterBootMinutes, 0, 1440);
        var restartFailureDelay = form.AddNumber(I18n.T("Neustart nach Fehler warten (min)"), check.RestartServicesDelayAfterFailureMinutes, 0, 1440);
        var restartForceKill = form.AddCheck(I18n.T("Bei Stop-Timeout Prozess beenden"), check.ForceKillRestartServicesOnTimeout);
        var note = form.AddText(I18n.T("Beschreibung / Notiz"), check.Note, multiline: true);

        form._validator = () =>
        {
            if (enabled.Checked && !ValidationUtils.IsValidHost(host.Text))
            {
                return I18n.T("Hostname oder IP-Adresse ist ungültig.");
            }

            if (!ValidationUtils.IsValidHttpUrl(pushUrl.Text, allowEmpty: true))
            {
                return I18n.T("Push-URL ist ungültig.");
            }

            return "";
        };

        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        check.Enabled = enabled.Checked;
        check.Name = name.Text.Trim();
        check.Host = host.Text.Trim();
        check.PushUrl = pushUrl.Text.Trim();
        check.IntervalSeconds = (int)interval.Value;
        check.TimeoutMs = (int)timeout.Value;
        check.SendMachineInfo = machineInfo.Checked;
        check.RestartServicesOnFailure = restartServices.SelectedServiceNames.ToList();
        check.RestartServicesCooldownSeconds = (int)restartCooldown.Value;
        check.RestartServicesDelayAfterBootMinutes = (int)restartBootDelay.Value;
        check.RestartServicesDelayAfterFailureMinutes = (int)restartFailureDelay.Value;
        check.ForceKillRestartServicesOnTimeout = restartForceKill.Checked;
        check.Note = note.Text.Trim();
        check.Normalize(30, 3000);
        return true;
    }

    public static bool EditTcp(IWin32Window owner, TcpCheckConfig check, bool maskPushUrl, WindowsServiceManager windowsServiceManager)
    {
        using var form = new CheckEditorForm(I18n.T("TCP-Port-Check"));
        var enabled = form.AddCheck(I18n.T("Aktiv"), check.Enabled);
        var name = form.AddText(I18n.T("Name"), check.Name);
        var host = form.AddText(I18n.T("Hostname oder IP-Adresse"), check.Host);
        var port = form.AddNumber(I18n.T("Port"), check.Port, 1, 65535);
        var pushUrl = form.AddPushUrl(I18n.T("Uptime-Kuma-Push-URL"), check.PushUrl, maskPushUrl);
        var interval = form.AddNumber(I18n.T("Intervall in Sekunden"), check.IntervalSeconds, 5, 86400);
        var timeout = form.AddNumber(I18n.T("Timeout in Millisekunden"), check.TimeoutMs, 500, 120000);
        var machineInfo = form.AddCheck(I18n.T("Maschineninfos mitsenden"), check.SendMachineInfo);
        var restartServices = form.AddServiceRestartSelector(I18n.T("Dienste bei Fehler neu starten"), check.RestartServicesOnFailure, windowsServiceManager);
        var restartCooldown = form.AddNumber(I18n.T("Neustart-Cooldown (s)"), check.RestartServicesCooldownSeconds, 30, 86400);
        var restartBootDelay = form.AddNumber(I18n.T("Neustart nach Systemstart warten (min)"), check.RestartServicesDelayAfterBootMinutes, 0, 1440);
        var restartFailureDelay = form.AddNumber(I18n.T("Neustart nach Fehler warten (min)"), check.RestartServicesDelayAfterFailureMinutes, 0, 1440);
        var restartForceKill = form.AddCheck(I18n.T("Bei Stop-Timeout Prozess beenden"), check.ForceKillRestartServicesOnTimeout);
        var logConnections = form.AddCheck(I18n.T("TCP-Verbindungen ins Log schreiben"), check.LogTcpConnections);
        var connectionDirection = form.AddCombo(I18n.T("TCP-Log Richtung"), TcpConnectionLogDirections.All, TcpConnectionLogDirections.Normalize(check.TcpConnectionLogDirection), I18n.TcpDirectionName);
        var note = form.AddText(I18n.T("Beschreibung / Notiz"), check.Note, multiline: true);

        form._validator = () =>
        {
            if (enabled.Checked && !ValidationUtils.IsValidHost(host.Text))
            {
                return I18n.T("Hostname oder IP-Adresse ist ungültig.");
            }

            if (!ValidationUtils.IsValidPort((int)port.Value))
            {
                return I18n.T("Port ist ungültig.");
            }

            if (!ValidationUtils.IsValidHttpUrl(pushUrl.Text, allowEmpty: true))
            {
                return I18n.T("Push-URL ist ungültig.");
            }

            return "";
        };

        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        check.Enabled = enabled.Checked;
        check.Name = name.Text.Trim();
        check.Host = host.Text.Trim();
        check.Port = (int)port.Value;
        check.PushUrl = pushUrl.Text.Trim();
        check.IntervalSeconds = (int)interval.Value;
        check.TimeoutMs = (int)timeout.Value;
        check.SendMachineInfo = machineInfo.Checked;
        check.RestartServicesOnFailure = restartServices.SelectedServiceNames.ToList();
        check.RestartServicesCooldownSeconds = (int)restartCooldown.Value;
        check.RestartServicesDelayAfterBootMinutes = (int)restartBootDelay.Value;
        check.RestartServicesDelayAfterFailureMinutes = (int)restartFailureDelay.Value;
        check.ForceKillRestartServicesOnTimeout = restartForceKill.Checked;
        check.LogTcpConnections = logConnections.Checked;
        check.TcpConnectionLogDirection = GetComboValue(connectionDirection, TcpConnectionLogDirections.Both);
        check.Note = note.Text.Trim();
        check.Normalize(30, 3000);
        return true;
    }

    public static bool EditService(IWin32Window owner, ServiceCheckConfig check, bool maskPushUrl, WindowsServiceManager windowsServiceManager)
    {
        using var form = new CheckEditorForm(I18n.T("Windows-Dienst-Check"));
        var enabled = form.AddCheck(I18n.T("Aktiv"), check.Enabled);
        var displayName = form.AddText(I18n.T("Anzeigename"), check.DisplayName);
        var serviceName = form.AddText(I18n.T("Dienstname"), check.ServiceName);
        var expected = form.AddCombo(I18n.T("Erwarteter Status"), ServiceStates.All, ServiceStates.Normalize(check.ExpectedStatus), I18n.ServiceStatusName);
        var pushUrl = form.AddPushUrl(I18n.T("Uptime-Kuma-Push-URL"), check.PushUrl, maskPushUrl);
        var interval = form.AddNumber(I18n.T("Intervall in Sekunden"), check.IntervalSeconds, 5, 86400);
        var restart = form.AddCheck(I18n.T("Dienst automatisch neu starten"), check.RestartIfStopped);
        var restartIfStoppedBootDelay = form.AddNumber(I18n.T("Auto-Neustart nach Systemstart warten (min)"), check.RestartIfStoppedDelayAfterBootMinutes, 0, 1440);
        var restartIfStoppedFailureDelay = form.AddNumber(I18n.T("Auto-Neustart nach Dienstfehler warten (min)"), check.RestartIfStoppedDelayAfterFailureMinutes, 0, 1440);
        var machineInfo = form.AddCheck(I18n.T("Maschineninfos mitsenden"), check.SendMachineInfo);
        var restartServices = form.AddServiceRestartSelector(I18n.T("Weitere Dienste bei Fehler neu starten"), check.RestartServicesOnFailure, windowsServiceManager);
        var restartCooldown = form.AddNumber(I18n.T("Neustart-Cooldown (s)"), check.RestartServicesCooldownSeconds, 30, 86400);
        var restartBootDelay = form.AddNumber(I18n.T("Fehleraktion nach Systemstart warten (min)"), check.RestartServicesDelayAfterBootMinutes, 0, 1440);
        var restartFailureDelay = form.AddNumber(I18n.T("Fehleraktion nach Fehler warten (min)"), check.RestartServicesDelayAfterFailureMinutes, 0, 1440);
        var restartForceKill = form.AddCheck(I18n.T("Bei Stop-Timeout Prozess beenden"), check.ForceKillRestartServicesOnTimeout);
        var note = form.AddText(I18n.T("Beschreibung / Notiz"), check.Note, multiline: true);

        form._validator = () =>
        {
            if (enabled.Checked && string.IsNullOrWhiteSpace(serviceName.Text))
            {
                return I18n.T("Dienstname fehlt.");
            }

            if (!ValidationUtils.IsValidHttpUrl(pushUrl.Text, allowEmpty: true))
            {
                return I18n.T("Push-URL ist ungültig.");
            }

            return "";
        };

        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        check.Enabled = enabled.Checked;
        check.DisplayName = displayName.Text.Trim();
        check.ServiceName = serviceName.Text.Trim();
        check.ExpectedStatus = GetComboValue(expected, "Running");
        check.PushUrl = pushUrl.Text.Trim();
        check.IntervalSeconds = (int)interval.Value;
        check.RestartIfStopped = restart.Checked;
        check.RestartIfStoppedDelayAfterBootMinutes = (int)restartIfStoppedBootDelay.Value;
        check.RestartIfStoppedDelayAfterFailureMinutes = (int)restartIfStoppedFailureDelay.Value;
        check.SendMachineInfo = machineInfo.Checked;
        check.RestartServicesOnFailure = restartServices.SelectedServiceNames.ToList();
        check.RestartServicesCooldownSeconds = (int)restartCooldown.Value;
        check.RestartServicesDelayAfterBootMinutes = (int)restartBootDelay.Value;
        check.RestartServicesDelayAfterFailureMinutes = (int)restartFailureDelay.Value;
        check.ForceKillRestartServicesOnTimeout = restartForceKill.Checked;
        check.Note = note.Text.Trim();
        check.Normalize(30);
        return true;
    }

    public static bool EditDrive(IWin32Window owner, DriveCheckConfig check, bool maskPushUrl)
    {
        using var form = new CheckEditorForm(I18n.T("Laufwerks-Check"));
        var enabled = form.AddCheck(I18n.T("Aktiv"), check.Enabled);
        var name = form.AddText(I18n.T("Name"), check.Name);
        var path = form.AddText(I18n.T("Pfad / Laufwerk"), check.Path);
        var pushUrl = form.AddPushUrl(I18n.T("Uptime-Kuma-Push-URL"), check.PushUrl, maskPushUrl);
        var interval = form.AddNumber(I18n.T("Intervall in Sekunden"), check.IntervalSeconds, 5, 86400);
        var minPercent = form.AddNumber(I18n.T("Minimum frei (%)"), check.MinimumFreePercent, 0, 100);
        var minGb = form.AddDecimalNumber(I18n.T("Minimum frei (GB)"), check.MinimumFreeGb, 0, 1024 * 1024, 1);
        var reconnect = form.AddCheck(I18n.T("Netzlaufwerk automatisch verbinden"), check.ReconnectIfUnavailable);
        var reconnectPath = form.AddText(I18n.T("Reconnect UNC-Pfad"), check.ReconnectPath);
        var logDetails = form.AddCheck(I18n.T("Details ins Log schreiben"), check.LogDetails);
        var machineInfo = form.AddCheck(I18n.T("Maschineninfos mitsenden"), check.SendMachineInfo);
        var note = form.AddText(I18n.T("Beschreibung / Notiz"), check.Note, multiline: true);

        form._validator = () =>
        {
            if (enabled.Checked && string.IsNullOrWhiteSpace(path.Text))
            {
                return I18n.T("Pfad oder Laufwerksbuchstabe fehlt.");
            }

            if (!ValidationUtils.IsValidHttpUrl(pushUrl.Text, allowEmpty: true))
            {
                return I18n.T("Push-URL ist ungültig.");
            }

            return "";
        };

        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        check.Enabled = enabled.Checked;
        check.Name = name.Text.Trim();
        check.Path = path.Text.Trim();
        check.PushUrl = pushUrl.Text.Trim();
        check.IntervalSeconds = (int)interval.Value;
        check.MinimumFreePercent = (int)minPercent.Value;
        check.MinimumFreeGb = minGb.Value;
        check.ReconnectIfUnavailable = reconnect.Checked;
        check.ReconnectPath = reconnectPath.Text.Trim();
        check.LogDetails = logDetails.Checked;
        check.SendMachineInfo = machineInfo.Checked;
        check.Note = note.Text.Trim();
        check.Normalize(60);
        return true;
    }

    private TextBox AddText(string label, string value, bool multiline = false)
    {
        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };
        AddRow(label, textBox, multiline ? 82 : 32);
        return textBox;
    }

    private TextBox AddPushUrl(string label, string value, bool masked)
    {
        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = masked
        };
        var toggle = new Button { Text = masked ? I18n.T("Anzeigen") : I18n.T("Maskieren"), Width = 95, Dock = DockStyle.Right };
        toggle.Click += (_, _) =>
        {
            textBox.UseSystemPasswordChar = !textBox.UseSystemPasswordChar;
            toggle.Text = textBox.UseSystemPasswordChar ? I18n.T("Anzeigen") : I18n.T("Maskieren");
        };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(toggle, 1, 0);
        AddRow(label, panel, 34);
        return textBox;
    }

    private NumericUpDown AddNumber(string label, int value, int min, int max)
    {
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Dock = DockStyle.Left,
            Width = 160,
            ThousandsSeparator = true
        };
        AddRow(label, number, 32);
        return number;
    }

    private NumericUpDown AddDecimalNumber(string label, decimal value, decimal min, decimal max, int decimalPlaces)
    {
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            DecimalPlaces = decimalPlaces,
            Increment = decimalPlaces > 0 ? 0.1M : 1,
            Dock = DockStyle.Left,
            Width = 160,
            ThousandsSeparator = true
        };
        AddRow(label, number, 32);
        return number;
    }

    private CheckBox AddCheck(string label, bool value)
    {
        var check = new CheckBox { Checked = value, Dock = DockStyle.Fill };
        AddRow(label, check, 32);
        return check;
    }

    private ComboBox AddCombo(string label, IEnumerable<string> values, string selected, Func<string, string>? displaySelector = null)
    {
        var combo = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values
            .Select(value => displaySelector is null ? (object)value : new DisplayOption(value, displaySelector))
            .ToArray());
        SelectComboValue(combo, selected);
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }

        AddRow(label, combo, 32);
        return combo;
    }

    private static string GetComboValue(ComboBox combo, string fallback)
    {
        return combo.SelectedItem switch
        {
            DisplayOption option => option.Value,
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
                string text => text,
                _ => item?.ToString() ?? ""
            };

            if (string.Equals(itemValue, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private ServiceRestartSelection AddServiceRestartSelector(
        string label,
        IEnumerable<string> selectedServiceNames,
        WindowsServiceManager windowsServiceManager)
    {
        var selection = new ServiceRestartSelection(selectedServiceNames);
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = selection.DisplayText
        };
        var button = new Button { Text = I18n.T("Auswählen"), Width = 110, Dock = DockStyle.Right };
        button.Click += (_, _) =>
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                var services = windowsServiceManager.ListServices();
                Cursor.Current = Cursors.Default;
                using var dialog = new ServiceSelectionDialog(services, selection.SelectedServiceNames);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selection.SelectedServiceNames = dialog.SelectedServiceNames.ToList();
                    textBox.Text = selection.DisplayText;
                }
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show(this, I18n.T("Lokale Dienste konnten nicht geladen werden: ") + ex.Message, I18n.T("Dienste auswählen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(button, 1, 0);
        AddRow(label, panel, 34);
        return selection;
    }

    private void AddRow(string label, Control control, int height)
    {
        var row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _fields.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        _fields.Controls.Add(control, 1, row);
    }

    private sealed class ServiceRestartSelection
    {
        public ServiceRestartSelection(IEnumerable<string> serviceNames)
        {
            SelectedServiceNames = CheckServiceActions.NormalizeServiceNames(serviceNames);
        }

        public List<string> SelectedServiceNames { get; set; }

        public string DisplayText => SelectedServiceNames.Count == 0
            ? I18n.T("Keine")
            : string.Join(", ", SelectedServiceNames);
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
}
