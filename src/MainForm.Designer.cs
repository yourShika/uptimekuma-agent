using System.Drawing;
using System.Windows.Forms;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel mainLayout = null!;
    private Panel headerPanel = null!;
    private PictureBox headerIcon = null!;
    private Label headerTitle = null!;
    private Label headerSubtitle = null!;
    private Label headerBadge = null!;
    private TabControl tabMain = null!;
    private TabPage tabGeneral = null!;
    private TabPage tabPing = null!;
    private TabPage tabTcp = null!;
    private TabPage tabServices = null!;
    private TabPage tabDrives = null!;
    private TabPage tabLogs = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainLayout = new TableLayoutPanel();
        headerPanel = new Panel();
        headerIcon = new PictureBox();
        headerTitle = new Label();
        headerSubtitle = new Label();
        headerBadge = new Label();
        tabMain = new TabControl();
        tabGeneral = new TabPage();
        tabPing = new TabPage();
        tabTcp = new TabPage();
        tabServices = new TabPage();
        tabDrives = new TabPage();
        tabLogs = new TabPage();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();

        mainLayout.SuspendLayout();
        headerPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)headerIcon).BeginInit();
        tabMain.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();

        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(headerPanel, 0, 0);
        mainLayout.Controls.Add(tabMain, 0, 1);
        mainLayout.Controls.Add(statusStrip, 0, 2);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.RowCount = 3;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        mainLayout.Size = new Size(1184, 761);

        headerPanel.Dock = DockStyle.Fill;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Margin = new Padding(0);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(22, 16, 22, 14);
        headerPanel.Size = new Size(1184, 86);
        headerPanel.Controls.Add(headerBadge);
        headerPanel.Controls.Add(headerSubtitle);
        headerPanel.Controls.Add(headerTitle);
        headerPanel.Controls.Add(headerIcon);

        headerIcon.Location = new Point(22, 18);
        headerIcon.Name = "headerIcon";
        headerIcon.Size = new Size(48, 48);
        headerIcon.SizeMode = PictureBoxSizeMode.StretchImage;
        headerIcon.TabStop = false;

        headerTitle.AutoSize = true;
        headerTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point);
        headerTitle.Location = new Point(82, 16);
        headerTitle.Name = "headerTitle";
        headerTitle.Size = new Size(284, 32);
        headerTitle.Text = "Uptime Kuma Tray Agent";

        headerSubtitle.AutoSize = true;
        headerSubtitle.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
        headerSubtitle.Location = new Point(84, 50);
        headerSubtitle.Name = "headerSubtitle";
        headerSubtitle.Size = new Size(383, 17);
        headerSubtitle.Text = I18n.T("Lokale Hosts, TCP-Ports und Windows-Dienste im Blick behalten");

        headerBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        headerBadge.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
        headerBadge.Location = new Point(1016, 27);
        headerBadge.Name = "headerBadge";
        headerBadge.Size = new Size(142, 30);
        headerBadge.Text = I18n.T("Monitoring inaktiv");
        headerBadge.TextAlign = ContentAlignment.MiddleCenter;

        tabMain.Controls.Add(tabGeneral);
        tabMain.Controls.Add(tabPing);
        tabMain.Controls.Add(tabTcp);
        tabMain.Controls.Add(tabServices);
        tabMain.Controls.Add(tabDrives);
        tabMain.Controls.Add(tabLogs);
        tabMain.Dock = DockStyle.Fill;
        tabMain.Location = new Point(0, 86);
        tabMain.Margin = new Padding(0);
        tabMain.Name = "tabMain";
        tabMain.SelectedIndex = 0;
        tabMain.Size = new Size(1184, 651);

        tabGeneral.Location = new Point(4, 24);
        tabGeneral.Name = "tabGeneral";
        tabGeneral.Padding = new Padding(10);
        tabGeneral.Size = new Size(1176, 623);
        tabGeneral.Text = I18n.T("Allgemein");
        tabGeneral.UseVisualStyleBackColor = true;

        tabPing.Location = new Point(4, 24);
        tabPing.Name = "tabPing";
        tabPing.Padding = new Padding(10);
        tabPing.Size = new Size(1176, 623);
        tabPing.Text = I18n.T("Ping-Checks");
        tabPing.UseVisualStyleBackColor = true;

        tabTcp.Location = new Point(4, 24);
        tabTcp.Name = "tabTcp";
        tabTcp.Padding = new Padding(10);
        tabTcp.Size = new Size(1176, 623);
        tabTcp.Text = I18n.T("TCP-Checks");
        tabTcp.UseVisualStyleBackColor = true;

        tabServices.Location = new Point(4, 24);
        tabServices.Name = "tabServices";
        tabServices.Padding = new Padding(10);
        tabServices.Size = new Size(1176, 623);
        tabServices.Text = I18n.T("Windows-Dienste");
        tabServices.UseVisualStyleBackColor = true;

        tabDrives.Location = new Point(4, 24);
        tabDrives.Name = "tabDrives";
        tabDrives.Padding = new Padding(10);
        tabDrives.Size = new Size(1176, 623);
        tabDrives.Text = I18n.T("Laufwerke");
        tabDrives.UseVisualStyleBackColor = true;

        tabLogs.Location = new Point(4, 24);
        tabLogs.Name = "tabLogs";
        tabLogs.Padding = new Padding(10);
        tabLogs.Size = new Size(1176, 623);
        tabLogs.Text = I18n.T("Logs");
        tabLogs.UseVisualStyleBackColor = true;

        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusStrip.Dock = DockStyle.Fill;
        statusStrip.Location = new Point(0, 737);
        statusStrip.Margin = new Padding(0);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1184, 24);

        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(39, 17);
        statusLabel.Text = I18n.T("Bereit");

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1320, 820);
        Controls.Add(mainLayout);
        MinimumSize = new Size(1120, 720);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Uptime Kuma Tray Agent";

        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        headerPanel.ResumeLayout(false);
        headerPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)headerIcon).EndInit();
        tabMain.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }
}
