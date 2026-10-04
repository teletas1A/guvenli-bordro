// Güvenli Bordro - Yönetim paneli masaüstü uygulaması
// 1) Parametresiz açılınca: paneli Microsoft Edge (yoksa Chrome) "uygulama penceresi" modunda açar.
// 2) Panelden "guvenlibordro://gonder?u=..." linkiyle açılınca: WhatsApp masaüstü uygulamasıyla toplu otomatik gönderim yapar.
// Adresi değiştirmek için exe'nin yanına "adres.txt" dosyası koyup içine yeni adresi yazın.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Güvenli Bordro")]
[assembly: AssemblyProduct("Güvenli Bordro")]
[assembly: AssemblyDescription("Güvenli Bordro Yönetim Paneli")]
[assembly: AssemblyCompany("Güvenli Hipermarketler")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class GuvenliBordro
{
    const string DefaultUrl = "http://guvenlihipermarketbordo.com/yonetim-k7x4q9m2/";
    public const string Title = "Güvenli Bordro";

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        string panelUrl = PanelUrl();
        if (args.Length > 0 && args[0].StartsWith("guvenlibordro:", StringComparison.OrdinalIgnoreCase))
        {
            return Sender.Run(args[0], panelUrl);
        }
        return OpenPanel(panelUrl);
    }

    static string PanelUrl()
    {
        // Masaüstü (Electron) uygulaması, ayarlardaki panel adresini bu değişkenle iletir
        string env = Environment.GetEnvironmentVariable("GB_PANEL_URL");
        if (!string.IsNullOrEmpty(env) && (env.StartsWith("http://") || env.StartsWith("https://"))) return env;
        string custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adres.txt");
        if (File.Exists(custom))
        {
            string t = File.ReadAllText(custom).Trim();
            if (t.StartsWith("http://") || t.StartsWith("https://")) return t;
        }
        return DefaultUrl;
    }

    static int OpenPanel(string url)
    {
        // Ayrı profil: oturum, normal tarayıcıdan bağımsız kalır ve görev çubuğunda ayrı pencere olur
        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuvenliBordro");
        Directory.CreateDirectory(profile);

        string browser = FindBrowser();
        if (browser == null)
        {
            MessageBox.Show("Microsoft Edge veya Google Chrome bulunamadı. Panel varsayılan tarayıcıda açılacak.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.Start(url);
            return 0;
        }
        var psi = new ProcessStartInfo(browser,
            "--app=\"" + url + "\" --user-data-dir=\"" + profile + "\" --window-size=1366,850 --no-first-run --no-default-browser-check");
        psi.UseShellExecute = false;
        Process.Start(psi);
        return 0;
    }

    static string FindBrowser()
    {
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        {
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
        };
        foreach (string c in candidates)
            if (File.Exists(c)) return c;
        return null;
    }
}

/// <summary>WhatsApp masaüstü uygulamasıyla toplu otomatik gönderim</summary>
static class Sender
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    const byte VK_RETURN = 0x0D, VK_MENU = 0x12;
    const uint KEYUP = 0x0002;

    static volatile bool stopRequested;
    // Test için: GB_DRYRUN=1 ortam değişkeniyle Enter'a basmadan tüm adımları çalıştırır
    static readonly bool DryRun = Environment.GetEnvironmentVariable("GB_DRYRUN") == "1";

    public static int Run(string protocolArg, string panelUrl)
    {
        bool created;
        using (var mutex = new Mutex(true, "GuvenliBordro.Sender", out created))
        {
            if (!created)
            {
                MessageBox.Show("Zaten devam eden bir gönderim var.", GuvenliBordro.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            try { return RunJob(protocolArg, panelUrl); }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, GuvenliBordro.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    static int RunJob(string protocolArg, string panelUrl)
    {
        // 1) Link doğrulama: yalnızca kendi panelimizin ürettiği işleri kabul et
        int u = protocolArg.IndexOf("u=", StringComparison.Ordinal);
        if (u < 0) throw new Exception("Geçersiz bağlantı.");
        string jobUrl = Uri.UnescapeDataString(protocolArg.Substring(u + 2));
        Uri job, panel;
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out job) || !Uri.TryCreate(panelUrl, UriKind.Absolute, out panel))
            throw new Exception("Geçersiz bağlantı.");
        string panelPath = panel.AbsolutePath.EndsWith("/") ? panel.AbsolutePath : panel.AbsolutePath + "/";
        if ((job.Scheme != "http" && job.Scheme != "https")
            || !string.Equals(BareHost(job.Host), BareHost(panel.Host), StringComparison.OrdinalIgnoreCase)
            || !job.AbsolutePath.Equals(panelPath + "is.php", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Bu gönderim isteği tanınmayan bir adresten geldi ve engellendi:\n" + job.Host,
                GuvenliBordro.Title, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return 1;
        }

        // 2) İşi sunucudan al
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var data = json.Deserialize<Dictionary<string, object>>(Http(jobUrl, null));
        if (!(data.ContainsKey("ok") && (bool)data["ok"]))
            throw new Exception(data.ContainsKey("error") ? Convert.ToString(data["error"]) : "İş alınamadı.");
        var items = (ArrayList)data["items"];
        int wait = Convert.ToInt32(data["wait"]);
        if (items.Count == 0)
        {
            Finish(jobUrl, "bitti");
            MessageBox.Show("Gönderilecek kişi kalmadı.", GuvenliBordro.Title);
            return 0;
        }

        // 3) WhatsApp kurulu mu?
        if (Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("whatsapp") == null)
        {
            MessageBox.Show("WhatsApp masaüstü uygulaması bulunamadı. Microsoft Store'dan WhatsApp'ı kurup şirket hesabıyla giriş yapın.",
                GuvenliBordro.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Finish(jobUrl, "durduruldu");
            return 1;
        }

        var first = (Dictionary<string, object>)items[0];
        var answer = DryRun ? DialogResult.Yes : MessageBox.Show(
            items.Count + " kişiye bordro mesajı WhatsApp ile otomatik gönderilecek.\n\n" +
            "İlk kişi: " + first["name"] + "\n" +
            "Mesajlar arası bekleme: ~" + wait + " saniye\n" +
            "Tahmini süre: ~" + Math.Ceiling(items.Count * (wait + 8) / 60.0) + " dakika\n\n" +
            "• WhatsApp uygulaması açık ve giriş yapılmış olmalı.\n" +
            "• Gönderim bitene kadar klavye ve fareye dokunmayın.\n" +
            "• Durdurmak için küçük penceredeki DURDUR butonuna basın.\n\nBaşlatılsın mı?",
            GuvenliBordro.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            Finish(jobUrl, "durduruldu");
            return 0;
        }

        // 4) İlerleme penceresi + arka planda gönderim
        var form = new ProgressForm(items.Count);
        form.StopClicked += delegate { stopRequested = true; form.SetStatus("Durduruluyor… (mevcut mesaj bitince)"); };
        int sent = 0, reportFail = 0;
        string endStatus = "bitti";
        string error = null;

        var worker = new Thread(delegate ()
        {
            var rnd = new Random();
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (stopRequested) { endStatus = "durduruldu"; break; }
                    var it = (Dictionary<string, object>)items[i];
                    string name = Convert.ToString(it["name"]);
                    string phone = Convert.ToString(it["phone"]);
                    string msg = Convert.ToString(it["message"]);
                    form.SetStatus((i + 1) + " / " + items.Count + " → " + name);

                    Process.Start(new ProcessStartInfo("whatsapp://send?phone=" + phone + "&text=" + Uri.EscapeDataString(msg)) { UseShellExecute = true });
                    SleepCheck(i == 0 ? 7000 : 4500);

                    // Enter'a basmadan önce WhatsApp'ın gerçekten önde olduğundan emin ol
                    int tries = 0;
                    while (!FocusWhatsApp())
                    {
                        if (stopRequested) break;
                        if (++tries >= 3)
                        {
                            var r = form.Ask("WhatsApp penceresi öne alınamadı.\n\nWhatsApp'ın açık olduğundan emin olun ve 'Yeniden dene'ye basın.\n(" + name + " henüz gönderilmedi.)");
                            if (r != DialogResult.Retry) { stopRequested = true; break; }
                            tries = 0;
                        }
                        Thread.Sleep(800);
                    }
                    if (stopRequested) { endStatus = "durduruldu"; break; }

                    if (!DryRun)
                    {
                        keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_RETURN, 0, KEYUP, UIntPtr.Zero);
                    }
                    Thread.Sleep(1500);
                    sent++;

                    if (!Report(jobUrl, it["id"])) reportFail++;
                    form.SetProgress(i + 1, sent);

                    if (i < items.Count - 1) SleepCheck((wait + rnd.Next(0, 5)) * 1000);
                }
            }
            catch (Exception ex) { error = ex.Message; endStatus = "durduruldu"; }
            form.CloseSafe();
        });
        worker.IsBackground = true;
        form.Shown += delegate { worker.Start(); };
        Application.Run(form);
        worker.Join(3000);

        Finish(jobUrl, endStatus);
        string summary = (endStatus == "bitti" ? "Gönderim tamamlandı." : "Gönderim durduruldu.") + "\n\n" + sent + " mesaj gönderildi.";
        if (reportFail > 0) summary += "\n\n" + reportFail + " gönderim panele işlenemedi (internet bağlantısını kontrol edin).";
        if (error != null) summary += "\n\nHata: " + error;
        if (DryRun) { Console.WriteLine(summary); return 0; }
        MessageBox.Show(summary, GuvenliBordro.Title, MessageBoxButtons.OK, endStatus == "bitti" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        return 0;
    }

    static string BareHost(string h)
    {
        return h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h.Substring(4) : h;
    }

    static void SleepCheck(int ms)
    {
        for (int t = 0; t < ms && !stopRequested; t += 200) Thread.Sleep(200);
    }

    static bool IsWhatsApp(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        try { return Process.GetProcessById((int)pid).ProcessName.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    static bool FocusWhatsApp()
    {
        if (IsWhatsApp(GetForegroundWindow())) return true;
        foreach (var p in Process.GetProcesses())
        {
            if (!p.ProcessName.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase) || p.MainWindowHandle == IntPtr.Zero) continue;
            IntPtr h = p.MainWindowHandle;
            if (IsIconic(h)) ShowWindow(h, 9);
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);       // Windows'un odak kısıtlamasını aşmak için
            keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
            SetForegroundWindow(h);
            Thread.Sleep(400);
            if (IsWhatsApp(GetForegroundWindow())) return true;
        }
        return false;
    }

    static bool Report(string jobUrl, object id)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Http(jobUrl, new NameValueCollection { { "action", "sent" }, { "id", Convert.ToString(id) } });
                return true;
            }
            catch { Thread.Sleep(2000); }
        }
        return false;
    }

    static void Finish(string jobUrl, string status)
    {
        try { Http(jobUrl, new NameValueCollection { { "action", "finish" }, { "status", status } }); } catch { }
    }

    static string Http(string url, NameValueCollection post)
    {
        using (var wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.UserAgent] = "GuvenliBordro-PC/1.1";
            try
            {
                return post == null ? wc.DownloadString(url) : Encoding.UTF8.GetString(wc.UploadValues(url, post));
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                    using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
                throw new Exception("Sunucuya bağlanılamadı: " + ex.Message);
            }
        }
    }
}

/// <summary>Her zaman üstte duran küçük ilerleme penceresi</summary>
class ProgressForm : Form
{
    readonly Label status = new Label();
    readonly Label counter = new Label();
    readonly ProgressBar bar = new ProgressBar();
    readonly Button stop = new Button();
    readonly int total;
    public event EventHandler StopClicked;

    public ProgressForm(int total)
    {
        this.total = total;
        Text = GuvenliBordro.Title + " – WhatsApp gönderimi";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(380, 140);
        var wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        Font = new Font("Segoe UI", 9.5f);

        status.SetBounds(14, 10, 352, 22);
        status.Text = "Başlatılıyor…";
        counter.SetBounds(14, 34, 352, 20);
        counter.ForeColor = Color.DimGray;
        counter.Text = "0 / " + total + " gönderildi";
        bar.SetBounds(14, 58, 352, 18);
        bar.Maximum = total;
        stop.SetBounds(14, 90, 352, 36);
        stop.Text = "DURDUR";
        stop.BackColor = Color.FromArgb(185, 28, 28);
        stop.ForeColor = Color.White;
        stop.FlatStyle = FlatStyle.Flat;
        stop.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        stop.Click += delegate { stop.Enabled = false; if (StopClicked != null) StopClicked(this, EventArgs.Empty); };
        Controls.AddRange(new Control[] { status, counter, bar, stop });
        FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing && stop.Enabled) { e.Cancel = true; stop.PerformClick(); } };
    }

    void Ui(Action a) { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
    public void SetStatus(string s) { Ui(() => status.Text = s); }
    public void SetProgress(int done, int sent) { Ui(() => { bar.Value = Math.Min(done, total); counter.Text = sent + " / " + total + " gönderildi"; }); }
    public void CloseSafe() { Ui(() => { stop.Enabled = false; Close(); }); }
    public DialogResult Ask(string msg)
    {
        DialogResult r = DialogResult.Cancel;
        if (IsHandleCreated) Invoke((Action)(() => r = MessageBox.Show(this, msg, GuvenliBordro.Title, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning)));
        return r;
    }
}
