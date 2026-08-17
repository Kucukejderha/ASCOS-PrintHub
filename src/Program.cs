using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AscosPrintHub;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    static readonly Color Navy = Color.FromArgb(25, 48, 78);
    static readonly Color Blue = Color.FromArgb(43, 111, 184);
    static readonly Color Canvas = Color.FromArgb(244, 247, 251);
    static readonly Color Border = Color.FromArgb(210, 220, 232);
    static readonly Color Ink = Color.FromArgb(30, 43, 61);
    static readonly Color Muted = Color.FromArgb(97, 113, 135);

    readonly TextBox folder = new() { Dock = DockStyle.Fill, ReadOnly = true };
    readonly ComboBox printer = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox recursive = new() { Text = "Alt klasörleri dahil et", AutoSize = true };
    readonly NumericUpDown copies = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 58 };
    readonly NumericUpDown delay = new() { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = .5M, Value = 2, Width = 64 };
    readonly CheckedListBox fileList = new() { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true, BorderStyle = BorderStyle.None };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Height = 8 };
    readonly Label status = new() { Text = "Hazır", AutoSize = true, ForeColor = Muted };
    readonly Label selected = new() { Text = "0 belge seçili", AutoSize = true, ForeColor = Muted };
    readonly Button start = ActionButton("Yazdırmayı başlat", true);
    readonly Button stop = ActionButton("Durdur", false);
    CancellationTokenSource? cancellation;

    public MainForm()
    {
        Text = "ASCOS PrintHub";
        Size = new Size(980, 680);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Canvas;
        Font = new Font("Segoe UI", 9F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildRail(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 1, 0);
        Controls.Add(root);
        LoadPrinters();
    }

    Control BuildRail()
    {
        var rail = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(22, 28, 18, 22) };
        var logo = new Label { Text = "A", ForeColor = Color.White, BackColor = Blue, Font = new Font("Segoe UI Semibold", 18F), TextAlign = ContentAlignment.MiddleCenter, Size = new Size(48, 48), Location = new Point(22, 28) };
        var brand = new Label { Text = "ASCOS\nPrintHub", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15F), AutoSize = true, Location = new Point(82, 29) };
        var section = new Label { Text = "YAZDIRMA GÖREVİ", ForeColor = Color.FromArgb(145, 170, 202), Font = new Font("Segoe UI Semibold", 8F), AutoSize = true, Location = new Point(22, 124) };
        var title = new Label { Text = "Toplu PDF\nyazdırma", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 17F), AutoSize = true, Location = new Point(22, 150) };
        var desc = new Label { Text = "Belgeleri seçin ve ağ\nyazıcısına güvenle\ngönderin.", ForeColor = Color.FromArgb(190, 207, 228), AutoSize = true, Location = new Point(22, 214) };
        var websiteCaption = new Label { Text = "ASCOS HAKKINDA", ForeColor = Color.FromArgb(145, 170, 202), Font = new Font("Segoe UI Semibold", 8F), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
        var website = new LinkLabel { Text = "rotaniz.com  ↗", LinkColor = Color.White, ActiveLinkColor = Color.FromArgb(133, 190, 244), VisitedLinkColor = Color.White, Font = new Font("Segoe UI Semibold", 10F), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, LinkBehavior = LinkBehavior.HoverUnderline };
        website.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://rotaniz.com/") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Web sitesi açılamadı.\n" + ex.Message, "ASCOS PrintHub", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var connected = new Label { Text = "●  Yazdırma motoru hazır", ForeColor = Color.FromArgb(105, 213, 164), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, Location = new Point(22, 570) };
        rail.Controls.AddRange(new Control[] { logo, brand, section, title, desc, websiteCaption, website, connected });
        rail.Resize += (_, _) =>
        {
            connected.Top = rail.ClientSize.Height - 48;
            website.Top = connected.Top - 45;
            websiteCaption.Top = website.Top - 22;
            website.Left = websiteCaption.Left = connected.Left = 22;
        };
        return rail;
    }

    Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 26, 20), ColumnCount = 1, RowCount = 6, BackColor = Canvas };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var heading = new Panel { Dock = DockStyle.Top, Height = 60 };
        heading.Controls.Add(new Label { Text = "Yazdırma görevi oluştur", ForeColor = Ink, Font = new Font("Segoe UI Semibold", 18F), AutoSize = true, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "PDF belgelerini seçili yazıcıya gönderin.", ForeColor = Muted, AutoSize = true, Location = new Point(2, 35) });
        workspace.Controls.Add(heading, 0, 0);

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 0, 0, 12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var browse = SecondaryButton("Gözat…"); browse.Click += (_, _) => ChooseFolder();
        var refresh = SecondaryButton("Yenile"); refresh.Click += (_, _) => LoadPrinters();
        AddField(fields, 0, "PDF KLASÖRÜ", folder, browse);
        AddField(fields, 1, "HEDEF YAZICI", printer, refresh);
        workspace.Controls.Add(fields, 0, 1);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 4, 0, 8), WrapContents = true };
        var scan = SecondaryButton("PDF'leri listele"); var all = SecondaryButton("Tümünü seç"); var none = SecondaryButton("Tümünü iptal");
        scan.Click += (_, _) => Scan(); all.Click += (_, _) => SetAllChecks(true); none.Click += (_, _) => SetAllChecks(false);
        recursive.Padding = new Padding(0, 7, 10, 0);
        toolbar.Controls.AddRange(new Control[] { scan, all, none, recursive, selected }); selected.Padding = new Padding(8, 8, 0, 0);
        workspace.Controls.Add(toolbar, 0, 2);

        var listFrame = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
        fileList.BackColor = Color.White; fileList.ForeColor = Ink; fileList.Font = new Font("Segoe UI", 9.5F); fileList.IntegralHeight = false;
        fileList.ItemCheck += (_, _) => BeginInvoke(new Action(UpdateSelectedCount));
        listFrame.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, listFrame.ClientRectangle, Border, ButtonBorderStyle.Solid);
        listFrame.Controls.Add(fileList);
        workspace.Controls.Add(listFrame, 0, 3);

        var progressArea = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 12, 0, 8) };
        progressArea.Controls.Add(progress, 0, 0); progressArea.Controls.Add(status, 0, 1);
        workspace.Controls.Add(progressArea, 0, 4);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, Height = 58, ColumnCount = 5, Padding = new Padding(0, 8, 0, 4) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var copyGroup = OptionGroup("Kopya", copies); var delayGroup = OptionGroup("Bekleme (sn)", delay);
        stop.Enabled = false; stop.Margin = new Padding(8, 2, 0, 2); start.Margin = new Padding(8, 2, 0, 2); stop.Click += (_, _) => cancellation?.Cancel(); start.Click += async (_, _) => await PrintAll();
        footer.Controls.Add(copyGroup, 0, 0); footer.Controls.Add(delayGroup, 1, 0); footer.Controls.Add(stop, 3, 0); footer.Controls.Add(start, 4, 0);
        workspace.Controls.Add(footer, 0, 5);
        return workspace;
    }

    static void AddField(TableLayoutPanel panel, int row, string label, Control input, Control action)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8F), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 11, 10, 10) }, 0, row);
        input.Margin = new Padding(0, 5, 8, 5); input.Height = 30; panel.Controls.Add(input, 1, row);
        action.Margin = new Padding(0, 4, 0, 4); panel.Controls.Add(action, 2, row);
    }

    static Control OptionGroup(string text, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = false, Size = new Size(text.StartsWith("Bekleme") ? 170 : 120, 42), Margin = new Padding(0, 0, 18, 0), WrapContents = false };
        input.Margin = new Padding(0, 5, 0, 5);
        panel.Controls.Add(new Label { Text = text, ForeColor = Muted, AutoSize = true, Margin = new Padding(0, 9, 7, 0) }); panel.Controls.Add(input); return panel;
    }

    static Button SecondaryButton(string text) => new() { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Ink, Padding = new Padding(8, 3, 8, 3), FlatAppearance = { BorderColor = Border } };
    static Button ActionButton(string text, bool primary) => new() { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Ink, Padding = new Padding(12, 5, 12, 5), Margin = new Padding(8, 0, 0, 0), FlatAppearance = { BorderColor = primary ? Blue : Border } };

    void LoadPrinters()
    {
        var old = printer.Text; printer.Items.Clear(); foreach (string p in PrinterSettings.InstalledPrinters) printer.Items.Add(p);
        var def = new PrinterSettings().PrinterName; printer.SelectedItem = printer.Items.Contains(old) ? old : printer.Items.Contains(def) ? def : printer.Items.Count > 0 ? printer.Items[0] : null;
    }

    void ChooseFolder() { using var dialog = new FolderBrowserDialog(); if (dialog.ShowDialog() == DialogResult.OK) { folder.Text = dialog.SelectedPath; Scan(); } }
    string[] Files()
    {
        if (!Directory.Exists(folder.Text)) throw new InvalidOperationException("Geçerli bir klasör seçin.");
        return Directory.GetFiles(folder.Text, "*.pdf", recursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    void Scan()
    {
        try { var files = Files(); fileList.Items.Clear(); foreach (var file in files) fileList.Items.Add(file, true); UpdateSelectedCount(); status.Text = $"{files.Length} PDF bulundu."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    void SetAllChecks(bool value) { for (int i = 0; i < fileList.Items.Count; i++) fileList.SetItemChecked(i, value); UpdateSelectedCount(); status.Text = value ? "Tüm belgeler seçildi." : "Tüm belge seçimleri kaldırıldı."; }
    void UpdateSelectedCount() { selected.Text = $"{fileList.CheckedItems.Count} belge seçili"; }
    string[] SelectedFiles() => fileList.CheckedItems.Cast<object>().Select(x => x.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToArray();

    static string GetPrintEngine()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ASCOS-PrintHub");
        var engine = Path.Combine(directory, "SumatraPDF.exe");
        Directory.CreateDirectory(directory);
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ASCOS.PrintHub.SumatraPDF.exe");
        if (resource == null) throw new InvalidOperationException("Gömülü yazdırma motoru bulunamadı.");
        if (!File.Exists(engine) || new FileInfo(engine).Length != resource.Length)
        {
            using var output = new FileStream(engine, FileMode.Create, FileAccess.Write, FileShare.None);
            resource.CopyTo(output);
        }
        return engine;
    }

    async Task PrintAll()
    {
        try
        {
            var files = SelectedFiles(); if (files.Length == 0) throw new InvalidOperationException("Yazdırmak için en az bir PDF seçin."); if (printer.SelectedItem is null) throw new InvalidOperationException("Bir yazıcı seçin.");
            var name = printer.Text; var count = (int)copies.Value;
            if (MessageBox.Show($"{files.Length} PDF, {count} kopya olarak\n'{name}' yazıcısına gönderilsin mi?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            cancellation = new(); start.Enabled = false; stop.Enabled = true; progress.Maximum = files.Length * count; progress.Value = 0;
            var engine = GetPrintEngine();
            foreach (var file in files) for (int n = 1; n <= count; n++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var info = new ProcessStartInfo(engine, $"-silent -print-to \"{name}\" \"{file}\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                using var process = Process.Start(info); if (process == null) throw new InvalidOperationException("Yazdırma motoru başlatılamadı.");
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0) throw new InvalidOperationException($"Yazdırma hatası: {process.ExitCode} ({Path.GetFileName(file)})");
                progress.Value++; status.Text = $"{progress.Value}/{progress.Maximum} — Gönderildi: {Path.GetFileName(file)}";
                await Task.Delay(TimeSpan.FromSeconds((double)delay.Value), cancellation.Token);
            }
            status.Text = "Tüm belgeler yazdırma kuyruğuna gönderildi."; MessageBox.Show(status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { status.Text = "Yazdırma durduruldu."; }
        catch (Exception ex) { status.Text = "Hata oluştu."; MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { start.Enabled = true; stop.Enabled = false; cancellation?.Dispose(); cancellation = null; }
    }
}
