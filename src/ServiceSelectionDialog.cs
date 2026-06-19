using System.Drawing;
using System.Windows.Forms;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent;

public sealed class ServiceSelectionDialog : Form
{
    private readonly CheckedListBox _serviceList = new();

    public ServiceSelectionDialog(IEnumerable<WindowsServiceInfo> services, IEnumerable<string> selectedServiceNames)
    {
        Text = I18n.T("Dienste auswählen");
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ClientSize = new Size(620, 560);

        var selected = new HashSet<string>(selectedServiceNames, StringComparer.OrdinalIgnoreCase);
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

        _serviceList.Dock = DockStyle.Fill;
        _serviceList.CheckOnClick = true;
        _serviceList.HorizontalScrollbar = true;
        foreach (var service in services.OrderBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new ServiceSelectionItem(service);
            _serviceList.Items.Add(item, selected.Contains(service.ServiceName));
        }

        root.Controls.Add(_serviceList, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var ok = new Button { Text = I18n.T("OK"), DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = I18n.T("Abbrechen"), DialogResult = DialogResult.Cancel, Width = 100 };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public IReadOnlyList<string> SelectedServiceNames => _serviceList.CheckedItems
        .Cast<ServiceSelectionItem>()
        .Select(item => item.Service.ServiceName)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToArray();

    private sealed class ServiceSelectionItem
    {
        public ServiceSelectionItem(WindowsServiceInfo service)
        {
            Service = service;
        }

        public WindowsServiceInfo Service { get; }

        public override string ToString()
        {
            return $"{Service.DisplayName} ({Service.ServiceName}) - {I18n.ServiceStatusName(Service.Status)}";
        }
    }
}
