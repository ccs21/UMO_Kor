using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class PcServerAssistantForm : Form
{
    private const string ArchiveName = "UtaMacrossDataArchive.zip";
    private const string PatchName = "UtaMacrossDataArchivePCPatch.zip";
    private readonly string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly string archiveDirectory;
    private readonly string dataDirectory;
    private readonly Label networkState = NewStateLabel();
    private readonly Label archiveState = NewStateLabel();
    private readonly Label dataState = NewStateLabel();
    private readonly Label serverState = NewStateLabel();
    private readonly Label actionGuide = new Label();
    private readonly Button installButton = new Button();
    private readonly Button startStopButton = new Button();
    private readonly Button copyIpButton = new Button();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label progressText = new Label();
    private readonly TextBox logBox = new TextBox();
    private UmoDataServer server;
    private bool busy;
    private string recommendedIp;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PcServerAssistantForm());
    }

    public PcServerAssistantForm()
    {
        archiveDirectory = Path.Combine(baseDirectory, "Archives");
        dataDirectory = Path.Combine(baseDirectory, "ServerData");
        Directory.CreateDirectory(archiveDirectory);

        Text = "우타마크로스 PC 서버";
        Font = new Font("Malgun Gothic", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(980, 720);
        ClientSize = new Size(1120, 840);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(29, 86, 145) };
        header.Controls.Add(new Label { Text = "우타마크로스 PC 서버", ForeColor = Color.White, Font = new Font(Font.FontFamily, 19F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 8) });
        header.Controls.Add(new Label { Text = "ZIP 두 개만 준비하면 데이터 배치부터 서버 실행까지 자동으로 처리합니다.", ForeColor = Color.WhiteSmoke, AutoSize = true, Location = new Point(22, 56) });
        root.Controls.Add(header, 0, 0);

        var checks = MakeGroup("1. 준비 상태");
        var checkGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, Padding = new Padding(12, 6, 12, 8) };
        checkGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        checkGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        checkGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        string[] labels = { "네트워크", "필수 ZIP 파일", "서버 데이터", "서버" };
        Label[] states = { networkState, archiveState, dataState, serverState };
        for (int i = 0; i < labels.Length; i++)
        {
            checkGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            checkGrid.Controls.Add(new Label { Text = labels[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold) }, 0, i);
            checkGrid.Controls.Add(states[i], 1, i);
        }
        copyIpButton.Text = "IP 복사"; StyleButton(copyIpButton); copyIpButton.Click += delegate { if (!string.IsNullOrEmpty(recommendedIp)) Clipboard.SetText(recommendedIp); };
        checkGrid.Controls.Add(copyIpButton, 2, 0);
        checks.Controls.Add(checkGrid);
        root.Controls.Add(checks, 0, 1);

        var controls = MakeGroup("2. ZIP 준비와 자동 설치");
        var controlGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 8, 12, 8) };
        controlGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        controlGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var buttonRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        var progressRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var downloadButton = MakeButton("다운로드 안내 열기", delegate { OpenUrl("https://umo.xele.org/getting-started/installation/install-android/"); });
        var folderButton = MakeButton("ZIP 파일 넣을 폴더 열기", delegate { Directory.CreateDirectory(archiveDirectory); Process.Start("explorer.exe", archiveDirectory); });
        var refreshButton = MakeButton("새로고침", async delegate { await RefreshStatus(false); });
        installButton.Text = "자동 설치 및 서버 시작"; StyleButton(installButton); installButton.Click += async delegate { await InstallAndStart(); };
        startStopButton.Text = "서버 시작"; StyleButton(startStopButton); startStopButton.Click += async delegate { await ToggleServer(); };
        buttonRow.Controls.Add(downloadButton, 0, 0);
        buttonRow.Controls.Add(folderButton, 1, 0);
        buttonRow.Controls.Add(refreshButton, 2, 0);
        buttonRow.Controls.Add(startStopButton, 3, 0);
        progress.Dock = DockStyle.Fill; progress.Style = ProgressBarStyle.Continuous; progress.Margin = new Padding(4, 12, 4, 12);
        progressText.Dock = DockStyle.Fill; progressText.TextAlign = ContentAlignment.MiddleLeft; progressText.Text = "대기 중";
        progressRow.Controls.Add(progress, 0, 0);
        progressRow.Controls.Add(progressText, 1, 0);
        progressRow.Controls.Add(installButton, 2, 0);
        controlGrid.Controls.Add(buttonRow, 0, 0);
        controlGrid.Controls.Add(progressRow, 0, 1);
        controls.Controls.Add(controlGrid);
        root.Controls.Add(controls, 0, 2);

        var bottom = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 78, IsSplitterFixed = true };
        actionGuide.Dock = DockStyle.Fill; actionGuide.BorderStyle = BorderStyle.FixedSingle; actionGuide.Padding = new Padding(12); actionGuide.TextAlign = ContentAlignment.MiddleLeft;
        actionGuide.Text = "상태를 확인하고 있습니다.";
        bottom.Panel1.Controls.Add(actionGuide);
        logBox.Dock = DockStyle.Fill; logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical; logBox.Font = new Font("Consolas", 9F); logBox.BackColor = Color.FromArgb(247, 249, 252);
        bottom.Panel2.Controls.Add(logBox);
        root.Controls.Add(bottom, 0, 3);

        FormClosing += delegate { if (server != null) server.Dispose(); };
        Shown += async delegate { await RefreshStatus(true); };
    }

    private static Label NewStateLabel()
    {
        return new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = "확인 중…" };
    }

    private GroupBox MakeGroup(string title)
    {
        return new GroupBox { Text = title, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(8) };
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var button = new Button { Text = text };
        StyleButton(button); button.Click += click; return button;
    }

    private void StyleButton(Button button)
    {
        button.Dock = DockStyle.Fill; button.Margin = new Padding(5); button.Font = new Font(Font, FontStyle.Bold); button.MinimumSize = new Size(0, 48);
    }

    private string FindArchive(string name)
    {
        string inArchive = Path.Combine(archiveDirectory, name);
        if (File.Exists(inArchive)) return inArchive;
        string besideExe = Path.Combine(baseDirectory, name);
        return File.Exists(besideExe) ? besideExe : null;
    }

    private bool DataReady(out string detail)
    {
        string manifest = Path.Combine(dataDirectory, "RequestGetFiles.json");
        string android = Path.Combine(dataDirectory, "android");
        string db = Path.Combine(dataDirectory, "db");
        string marker = Path.Combine(dataDirectory, ".umo-server-install-complete");
        var missing = new List<string>();
        if (!File.Exists(manifest) || new FileInfo(manifest).Length < 1000000) missing.Add("RequestGetFiles.json");
        if (!Directory.Exists(android)) missing.Add("android");
        if (!Directory.Exists(db)) missing.Add("db");
        if (missing.Count == 0 && !File.Exists(marker))
        {
            int androidFiles = CountFilesUpTo(android, 38000);
            int dbFiles = CountFilesUpTo(db, 4);
            if (androidFiles < 38000 || dbFiles < 4) missing.Add("설치 완료 검증");
        }
        detail = missing.Count == 0 ? "준비 완료 (ServerData)" : "미설치: " + string.Join(", ", missing.ToArray());
        return missing.Count == 0;
    }

    private static int CountFilesUpTo(string directory, int limit)
    {
        int count = 0;
        try { foreach (string ignored in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) if (++count >= limit) break; }
        catch { return 0; }
        return count;
    }

    private async Task RefreshStatus(bool autoStart)
    {
        if (busy) return;
        recommendedIp = NetworkHelper.GetRecommendedIpv4();
        if (recommendedIp == null)
        {
            SetState(networkState, false, "사용 가능한 로컬 IPv4를 찾지 못했습니다. Wi-Fi/유선 연결과 VPN을 확인하세요.");
        }
        else
        {
            SetState(networkState, true, "Android 수동 입력: " + recommendedIp + " (이 숫자만 입력) — 감지: " + string.Join(", ", NetworkHelper.GetLanIpv4Addresses().ToArray()));
        }
        copyIpButton.Enabled = recommendedIp != null;

        string archive = FindArchive(ArchiveName), patch = FindArchive(PatchName);
        SetState(archiveState, archive != null && patch != null,
            (archive != null ? "✓ " : "✗ ") + ArchiveName + "    " + (patch != null ? "✓ " : "✗ ") + PatchName);
        string dataDetail;
        bool ready = DataReady(out dataDetail);
        SetState(dataState, ready, dataDetail);
        string portProblem = null;
        bool portsReady = server != null && server.IsRunning || NetworkHelper.AreServerPortsAvailable(out portProblem);
        SetState(serverState, server != null && server.IsRunning, server != null && server.IsRunning ? "실행 중 — TCP 8000 / UDP 8001" : (portsReady ? "중지됨 — 포트 사용 가능" : "시작 불가 — " + portProblem));
        installButton.Enabled = !busy && archive != null && patch != null && !(server != null && server.IsRunning);
        startStopButton.Enabled = !busy && ready && portsReady;
        startStopButton.Text = server != null && server.IsRunning ? "서버 중지" : "서버 시작";

        if (!ready && (archive == null || patch == null))
            actionGuide.Text = "① 다운로드 안내를 열어 ZIP 두 개를 받으세요. ② ‘ZIP 파일 넣을 폴더 열기’를 누르고 ZIP을 그대로 넣으세요. ③ 새로고침 후 자동 설치를 누르세요.";
        else if (!ready)
            actionGuide.Text = "ZIP 두 개가 준비되었습니다. ‘자동 설치 및 서버 시작’을 누르세요. 압축 해제에는 저장장치 속도에 따라 시간이 걸립니다.";
        else if (server != null && server.IsRunning)
            actionGuide.Text = "서버 실행 중입니다. 휴대폰을 같은 Wi-Fi에 연결하고 게임의 데이터 설치를 진행하세요. 자동 탐색 실패 시 위 IP 숫자만 입력하세요. http:// 또는 :8000은 붙이지 않습니다.";
        else
            actionGuide.Text = "데이터 준비가 끝났습니다. ‘서버 시작’을 누르세요.";

        if (autoStart && ready && portsReady && recommendedIp != null && (server == null || !server.IsRunning))
            await StartServer();
    }

    private static void SetState(Label label, bool ok, string text)
    {
        label.Text = (ok ? "● " : "▲ ") + text;
        label.ForeColor = ok ? Color.FromArgb(0, 125, 65) : Color.FromArgb(190, 93, 0);
    }

    private async Task InstallAndStart()
    {
        if (busy) return;
        string archive = FindArchive(ArchiveName), patch = FindArchive(PatchName);
        if (archive == null || patch == null) { await RefreshStatus(false); return; }
        busy = true; installButton.Enabled = false; startStopButton.Enabled = false;
        progress.Value = 0; progressText.Text = "ZIP 검사 중…";
        try
        {
            var reporter = new Progress<InstallProgress>(delegate(InstallProgress p)
            {
                int value = Math.Max(0, Math.Min(100, p.Percent));
                progress.Value = value; progressText.Text = p.Message + "  " + value + "%";
            });
            await Task.Run(delegate { DataInstaller.Install(archive, patch, dataDirectory, reporter); });
            progress.Value = 100; progressText.Text = "설치 및 검증 완료";
            AppendLog("데이터 자동 배치가 완료되었습니다.");
        }
        catch (Exception ex)
        {
            progressText.Text = "오류 — 아래 로그를 확인하세요.";
            AppendLog("설치 오류: " + ex.Message);
            MessageBox.Show(this, "자동 설치 중 오류가 발생했습니다.\r\n\r\n" + ex.Message + "\r\n\r\nZIP 다운로드 완료 여부와 디스크 여유 공간을 확인하세요.", "설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { busy = false; }
        await RefreshStatus(false);
        string ignored;
        if (DataReady(out ignored)) await StartServer();
    }

    private async Task ToggleServer()
    {
        if (server != null && server.IsRunning)
        {
            server.Dispose(); server = null; AppendLog("서버를 중지했습니다."); await RefreshStatus(false);
        }
        else await StartServer();
    }

    private async Task StartServer()
    {
        string detail;
        if (!DataReady(out detail)) { await RefreshStatus(false); return; }
        try
        {
            if (server != null) server.Dispose();
            server = new UmoDataServer(dataDirectory, AppendLog);
            server.Start();
            await Task.Delay(180);
            AppendLog("서버가 시작되었습니다. Android 자동 탐색 신호를 보내고 있습니다.");
        }
        catch (Exception ex)
        {
            if (server != null) server.Dispose(); server = null;
            AppendLog("서버 시작 오류: " + ex.Message);
            MessageBox.Show(this, "서버를 시작하지 못했습니다.\r\n\r\n" + ex.Message + "\r\n\r\n다른 UMO 서버를 종료하고 TCP 8000/UDP 8001 포트를 확인하세요.", "서버 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        await RefreshStatus(false);
    }

    private void AppendLog(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), text); return; }
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine;
        logBox.AppendText(line);
        if (logBox.TextLength > 50000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 35000);
        logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret();
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { Process.Start("explorer.exe", url); }
    }
}

internal sealed class InstallProgress
{
    public int Percent;
    public string Message;
}

internal static class DataInstaller
{
    public static void Install(string archive, string patch, string destination, IProgress<InstallProgress> progress)
    {
        Directory.CreateDirectory(destination);
        string completionMarker = Path.Combine(destination, ".umo-server-install-complete");
        if (File.Exists(completionMarker)) File.Delete(completionMarker);
        long total = GetSize(archive, "data/") + GetSize(patch, null);
        if (total <= 0) throw new InvalidDataException("ZIP 내부에서 게임 데이터를 찾지 못했습니다.");
        string driveRoot = Path.GetPathRoot(Path.GetFullPath(destination));
        long free = new DriveInfo(driveRoot).AvailableFreeSpace;
        if (free < Math.Min(total, 20L * 1024 * 1024 * 1024))
            throw new IOException("디스크 여유 공간이 부족합니다. 최소 약 " + FormatBytes(Math.Min(total, 20L * 1024 * 1024 * 1024)) + "가 필요합니다.");

        long done = 0;
        Extract(archive, destination, "data/", "게임 데이터 압축 해제", total, ref done, progress);
        Extract(patch, destination, null, "PC 패치 적용", total, ref done, progress);
        string manifest = Path.Combine(destination, "RequestGetFiles.json");
        if (!File.Exists(manifest) || new FileInfo(manifest).Length < 1000000 || !Directory.Exists(Path.Combine(destination, "android")) || !Directory.Exists(Path.Combine(destination, "db")))
            throw new InvalidDataException("배치 후 필수 파일 검증에 실패했습니다. ZIP 파일이 올바른지 확인하세요.");
        File.WriteAllText(completionMarker, "completed=" + DateTime.UtcNow.ToString("o") + Environment.NewLine + "archiveBytes=" + new FileInfo(archive).Length + Environment.NewLine + "patchBytes=" + new FileInfo(patch).Length, new UTF8Encoding(false));
        progress.Report(new InstallProgress { Percent = 100, Message = "최종 검증 완료" });
    }

    private static long GetSize(string zipPath, string prefix)
    {
        using (var zip = ZipFile.OpenRead(zipPath))
            return zip.Entries.Where(delegate(ZipArchiveEntry e) { return Included(e, prefix); }).Sum(delegate(ZipArchiveEntry e) { return e.Length; });
    }

    private static bool Included(ZipArchiveEntry entry, string prefix)
    {
        string name = entry.FullName.Replace('\\', '/');
        return prefix == null || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void Extract(string zipPath, string destination, string stripPrefix, string stage, long total, ref long done, IProgress<InstallProgress> progress)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string relative = entry.FullName.Replace('\\', '/');
                if (stripPrefix != null)
                {
                    if (!relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    relative = relative.Substring(stripPrefix.Length);
                }
                if (relative.Length == 0) continue;
                string target = Path.GetFullPath(Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP에 안전하지 않은 경로가 있습니다: " + entry.FullName);
                if (relative.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target) && new FileInfo(target).Length == entry.Length)
                {
                    done += entry.Length; Report(progress, done, total, stage + " (기존 파일 확인)"); continue;
                }
                string partial = target + ".umo-part";
                if (File.Exists(partial)) File.Delete(partial);
                using (Stream input = entry.Open())
                using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[1024 * 1024]; int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read); done += read;
                        if ((done & 0x7fffff) < read) Report(progress, done, total, stage);
                    }
                }
                if (File.Exists(target)) File.Delete(target);
                File.Move(partial, target);
            }
        }
    }

    private static void Report(IProgress<InstallProgress> progress, long done, long total, string stage)
    {
        progress.Report(new InstallProgress { Percent = (int)Math.Min(99, done * 100L / Math.Max(1, total)), Message = stage + " — " + FormatBytes(done) + " / " + FormatBytes(total) });
    }

    private static string FormatBytes(long value)
    {
        return (value / 1073741824.0).ToString("0.0") + " GB";
    }
}

internal static class NetworkHelper
{
    public static bool AreServerPortsAvailable(out string problem)
    {
        TcpListener tcp = null; UdpClient udp = null;
        try
        {
            // Loopback is sufficient for detecting a conflicting wildcard listener and does not
            // trigger the Windows Firewall prompt before the user actually starts the server.
            tcp = new TcpListener(IPAddress.Loopback, 8000); tcp.Start();
            udp = new UdpClient(); udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, 8001));
            problem = null; return true;
        }
        catch (SocketException ex) { problem = "8000/8001 포트가 다른 프로그램에서 사용 중입니다 (" + ex.SocketErrorCode + ")"; return false; }
        finally { if (tcp != null) tcp.Stop(); if (udp != null) udp.Close(); }
    }

    public static List<string> GetLanIpv4Addresses()
    {
        var result = new List<string>();
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
            {
                if (item.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                string ip = item.Address.ToString();
                if (!ip.StartsWith("169.254.") && !result.Contains(ip)) result.Add(ip);
            }
        }
        return result;
    }

    public static string GetRecommendedIpv4()
    {
        List<string> values = GetLanIpv4Addresses();
        return values.FirstOrDefault(IsPrivate) ?? values.FirstOrDefault();
    }

    private static bool IsPrivate(string ip)
    {
        IPAddress parsed;
        byte[] b; if (!IPAddress.TryParse(ip, out parsed)) return false; b = parsed.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }

    public static List<IPAddress> GetBroadcastAddresses()
    {
        var list = new List<IPAddress> { IPAddress.Broadcast };
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
            {
                if (item.Address.AddressFamily != AddressFamily.InterNetwork || item.IPv4Mask == null) continue;
                byte[] ip = item.Address.GetAddressBytes(), mask = item.IPv4Mask.GetAddressBytes(), broadcast = new byte[4];
                for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | (mask[i] ^ 255));
                IPAddress address = new IPAddress(broadcast);
                if (!list.Contains(address)) list.Add(address);
            }
        }
        return list;
    }
}

internal sealed class UmoDataServer : IDisposable
{
    private readonly string dataDirectory;
    private readonly Action<string> log;
    private readonly Dictionary<string, string> localToRemote = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Regex hashTag = new Regex("!s[0-9a-fA-F]+z!", RegexOptions.Compiled);
    private CancellationTokenSource cancellation;
    private TcpListener listener;
    private UdpClient broadcaster;
    private int requestCount;
    private readonly int tcpPort;
    private readonly int udpPort;
    public bool IsRunning { get; private set; }

    public UmoDataServer(string dataDirectory, Action<string> log, int tcpPort = 8000, int udpPort = 8001)
    {
        this.dataDirectory = Path.GetFullPath(dataDirectory); this.log = log; this.tcpPort = tcpPort; this.udpPort = udpPort;
    }

    public void Start()
    {
        if (IsRunning) return;
        LoadManifestMap();
        cancellation = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Any, tcpPort); listener.Start();
        try
        {
            broadcaster = new UdpClient(); broadcaster.EnableBroadcast = true; broadcaster.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); broadcaster.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));
        }
        catch { listener.Stop(); throw; }
        IsRunning = true; PreventSleep(true);
        Task.Run((Func<Task>)AcceptLoop);
        Task.Run((Func<Task>)BroadcastLoop);
    }

    private void LoadManifestMap()
    {
        localToRemote.Clear();
        string text = File.ReadAllText(Path.Combine(dataDirectory, "RequestGetFiles.json"), Encoding.UTF8);
        foreach (Match match in Regex.Matches(text, "\\\"file\\\"\\s*:\\s*\\\"([^\\\"]+)\\\""))
        {
            string remote = match.Groups[1].Value.Replace('\\', '/');
            string local = hashTag.Replace(remote, "");
            if (!localToRemote.ContainsKey(local)) localToRemote.Add(local, remote);
        }
        text = null;
    }

    private async Task AcceptLoop()
    {
        while (IsRunning && !cancellation.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Task ignored = Task.Run(delegate { HandleClient(client); });
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (IsRunning) log("연결 수신 오류가 발생했습니다."); break; }
        }
    }

    private async Task BroadcastLoop()
    {
        byte[] data = Encoding.UTF8.GetBytes("UMO");
        while (IsRunning && !cancellation.IsCancellationRequested)
        {
            foreach (IPAddress address in NetworkHelper.GetBroadcastAddresses())
            {
                try { await broadcaster.SendAsync(data, data.Length, new IPEndPoint(address, udpPort)); } catch { }
            }
            try { await Task.Delay(1000, cancellation.Token); } catch { break; }
        }
    }

    private void HandleClient(TcpClient client)
    {
        using (client)
        {
            client.NoDelay = true; client.ReceiveTimeout = 15000; client.SendTimeout = 120000;
            try
            {
                NetworkStream stream = client.GetStream();
                string first = ReadAsciiLine(stream);
                if (string.IsNullOrEmpty(first)) return;
                string[] parts = first.Split(' ');
                if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD")) { WriteError(stream, 405, "Method Not Allowed"); return; }
                string range = null, line;
                while (!string.IsNullOrEmpty(line = ReadAsciiLine(stream))) if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) range = line.Substring(6).Trim();
                string urlPath = parts[1].Split('?')[0];
                try { urlPath = Uri.UnescapeDataString(urlPath); } catch { WriteError(stream, 400, "Bad Request"); return; }
                string file = ResolveFile(urlPath);
                if (file == null) { WriteError(stream, 404, "Not Found"); if (Interlocked.Increment(ref requestCount) <= 20) log("파일 없음: " + urlPath); return; }
                SendFile(stream, file, parts[0] == "HEAD", range);
                int count = Interlocked.Increment(ref requestCount);
                if (count <= 20 || count % 100 == 0) log("전송 " + count + "건 — " + urlPath);
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception ex) { log("요청 처리 오류: " + ex.Message); }
        }
    }

    private string ResolveFile(string urlPath)
    {
        string normalized = urlPath.Replace('\\', '/');
        if (normalized == "/RequestGetFiles.json") return SafeCandidate("RequestGetFiles.json");
        string relative = normalized.TrimStart('/');
        string candidate = SafeCandidate(relative);
        if (candidate != null && File.Exists(candidate)) return candidate;
        candidate = SafeCandidate(hashTag.Replace(relative, ""));
        if (candidate != null && File.Exists(candidate)) return candidate;
        if (candidate != null && File.Exists(candidate + ".decrypted")) return candidate + ".decrypted";
        string remote;
        if (localToRemote.TryGetValue("/" + relative, out remote))
        {
            candidate = SafeCandidate(remote.TrimStart('/'));
            if (candidate != null && File.Exists(candidate)) return candidate;
            if (candidate != null && File.Exists(candidate + ".decrypted")) return candidate + ".decrypted";
        }
        return null;
    }

    private string SafeCandidate(string relative)
    {
        string root = dataDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(dataDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static string ReadAsciiLine(Stream stream)
    {
        var bytes = new List<byte>(128); int value;
        while ((value = stream.ReadByte()) >= 0)
        {
            if (value == 10) break;
            if (value != 13) bytes.Add((byte)value);
            if (bytes.Count > 8192) throw new InvalidDataException("HTTP header too large");
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void SendFile(Stream output, string path, bool headOnly, string rangeHeader)
    {
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
        {
            long start = 0, end = file.Length - 1; bool partial = TryParseRange(rangeHeader, file.Length, out start, out end);
            long length = end >= start ? end - start + 1 : 0;
            var header = new StringBuilder();
            header.Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n");
            header.Append("Content-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\nConnection: close\r\n");
            if (partial) header.Append("Content-Range: bytes ").Append(start).Append('-').Append(end).Append('/').Append(file.Length).Append("\r\n");
            header.Append("Content-Length: ").Append(length).Append("\r\n\r\n");
            byte[] headers = Encoding.ASCII.GetBytes(header.ToString()); output.Write(headers, 0, headers.Length);
            if (headOnly) return;
            file.Position = start; byte[] buffer = new byte[1024 * 1024]; long remaining = length;
            while (remaining > 0)
            {
                int read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (read <= 0) break;
                output.Write(buffer, 0, read); remaining -= read;
            }
            output.Flush();
        }
    }

    private static bool TryParseRange(string value, long length, out long start, out long end)
    {
        start = 0; end = length - 1;
        if (string.IsNullOrEmpty(value) || !value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;
        string[] pair = value.Substring(6).Split('-'); long parsedStart, parsedEnd;
        if (pair.Length != 2 || !long.TryParse(pair[0], out parsedStart) || parsedStart < 0 || parsedStart >= length) return false;
        parsedEnd = length - 1;
        if (pair[1].Length > 0 && (!long.TryParse(pair[1], out parsedEnd) || parsedEnd < parsedStart)) return false;
        start = parsedStart; end = Math.Min(parsedEnd, length - 1); return true;
    }

    private static void WriteError(Stream stream, int status, string reason)
    {
        byte[] body = Encoding.UTF8.GetBytes(status + " " + reason);
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + reason + "\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
        stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
    }

    public void Dispose()
    {
        if (!IsRunning && cancellation == null) return;
        IsRunning = false;
        try { if (cancellation != null) cancellation.Cancel(); } catch { }
        try { if (listener != null) listener.Stop(); } catch { }
        try { if (broadcaster != null) broadcaster.Close(); } catch { }
        listener = null; broadcaster = null;
        if (cancellation != null) cancellation.Dispose(); cancellation = null;
        PreventSleep(false);
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
    private static void PreventSleep(bool enabled) { SetThreadExecutionState(enabled ? 0x80000001u : 0x80000000u); }
}
