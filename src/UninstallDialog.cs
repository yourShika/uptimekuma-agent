using System.Drawing;
using System.Windows.Forms;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent;

public sealed class UninstallDialog : Form
{
    private readonly CheckBox _deleteConfig;
    private readonly CheckBox _deleteLogs;

    public UninstallDialog()
    {
        Text = I18n.T("Deinstallieren");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 210);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = I18n.T("Die Deinstallation entfernt den Autostart. Optional können Konfiguration und Logs gelöscht werden."),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _deleteConfig = new CheckBox { Text = I18n.T("Konfiguration löschen"), Dock = DockStyle.Fill };
        _deleteLogs = new CheckBox { Text = I18n.T("Logs löschen"), Dock = DockStyle.Fill };
        root.Controls.Add(_deleteConfig, 0, 1);
        root.Controls.Add(_deleteLogs, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var uninstall = new Button { Text = I18n.T("Deinstallieren"), Width = 120, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = I18n.T("Abbrechen"), Width = 100, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(uninstall);
        root.Controls.Add(buttons, 0, 3);

        AcceptButton = uninstall;
        CancelButton = cancel;
    }

    public bool DeleteConfig => _deleteConfig.Checked;
    public bool DeleteLogs => _deleteLogs.Checked;
}
