using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;
using DigitalSecretary.App;
using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;
using DigitalSecretary.App.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var repoRoot = LocateRepoRoot();
        var pluginsRoot = Path.Combine(repoRoot, "src", "DigitalSecretary.App", "bin", "Debug", "net9.0-windows", "plugins");
        var outDir = Path.Combine(repoRoot, "docs", "user-guide", "images");
        Directory.CreateDirectory(outDir);

        var dataRoot = Path.Combine(Path.GetTempPath(), "ds_docshots_data");
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
        SeedSampleData(dataRoot);

        var catalog = new PluginCatalog(pluginsRoot);
        var loader = new PluginLoader();
        IFeatureContext Ctx(string id) => new FeatureContext(id, dataRoot, _ => { });
        var features = catalog.Discover().Select(m => new LoadedFeature(m, loader, Ctx)).ToList();
        var settings = new AppSettings();

        using (var main = new MainForm(features, settings))
            CaptureForm(main, 1000, 640, Path.Combine(outDir, "main-shell.png"));

        using (var dlg = new ConfigureFeaturesForm(features, settings))
            CaptureForm(dlg, 470, 380, Path.Combine(outDir, "configure-features.png"));

        foreach (var f in features)
        {
            var view = f.GetView();
            var path = Path.Combine(outDir, f.Manifest.Id + ".png");

            if (f.Manifest.Id == "calculator")
            {
                var tb = FindFirstTextBox(view);
                if (tb is not null) tb.Text = "(12 + 5) * 3 / 2";
            }

            if (f.Manifest.Id == "clipboard-history")
                CaptureClipboard(view, path);
            else
                CaptureControl(view, 980, 560, path);
        }

        Console.WriteLine("Screenshots written to " + outDir);
    }

    private static void CaptureForm(Form form, int w, int h, string path)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-4000, -4000);
        form.ClientSize = new Size(w, h);
        form.Show();
        Pump(450);
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        bmp.Save(path, ImageFormat.Png);
        form.Hide();
    }

    private static void CaptureControl(Control view, int w, int h, string path)
    {
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ClientSize = new Size(w, h),
            BackColor = Color.White
        };
        view.Dock = DockStyle.Fill;
        host.Controls.Add(view);
        host.Show();
        Pump(450);
        using var bmp = new Bitmap(w, h);
        host.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
        bmp.Save(path, ImageFormat.Png);
        host.Hide();
        host.Controls.Remove(view);
    }

    private static void CaptureClipboard(Control view, string path)
    {
        string? original = null;
        try { if (Clipboard.ContainsText()) original = Clipboard.GetText(); } catch { }

        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ClientSize = new Size(980, 560),
            BackColor = Color.White
        };
        view.Dock = DockStyle.Fill;
        host.Controls.Add(view);
        host.Show();
        Pump(300);

        TrySetClipboard("Meeting notes: ship v2 on Friday and email the team.");
        Pump(1000);
        TrySetClipboard("https://example.com/orders/12345");
        Pump(1000);
        TrySetClipboard("OTP 449120 (expires in 5 minutes)");
        Pump(1000);

        using var bmp = new Bitmap(980, 560);
        host.DrawToBitmap(bmp, new Rectangle(0, 0, 980, 560));
        bmp.Save(path, ImageFormat.Png);
        host.Hide();
        host.Controls.Remove(view);

        if (original is not null) TrySetClipboard(original);
    }

    private static TextBox? FindFirstTextBox(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is TextBox tb) return tb;
            var nested = FindFirstTextBox(c);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static void TrySetClipboard(string text)
    {
        try { Clipboard.SetText(text); } catch { }
    }

    private static void Pump(int ms)
    {
        var end = Environment.TickCount + ms;
        while (Environment.TickCount < end)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
    }

    private static void SeedSampleData(string dataRoot)
    {
        var json = new JsonSerializerOptions { WriteIndented = true };

        var launcher = Path.Combine(dataRoot, "launcher");
        Directory.CreateDirectory(launcher);
        var shortcuts = new[]
        {
            new { Name = "Notepad",      Target = @"C:\Windows\System32\notepad.exe", Arguments = (string?)null },
            new { Name = "Documents",    Target = @"C:\Users\Public\Documents",        Arguments = (string?)null },
            new { Name = "Anthropic",    Target = "https://www.anthropic.com",         Arguments = (string?)null },
            new { Name = "Calculator",   Target = @"C:\Windows\System32\calc.exe",      Arguments = (string?)null }
        };
        File.WriteAllText(Path.Combine(launcher, "shortcuts.json"), JsonSerializer.Serialize(shortcuts, json));

        var email = Path.Combine(dataRoot, "email-downloader");
        Directory.CreateDirectory(email);
        var settings = new { Email = "yourname@yahoo.com", DownloadDir = @"C:\Users\You\Documents\DigitalSecretary_Email" };
        File.WriteAllText(Path.Combine(email, "email_settings.json"), JsonSerializer.Serialize(settings, json));
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DigitalSecretary.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("DigitalSecretary.sln not found above " + AppContext.BaseDirectory);
    }
}
