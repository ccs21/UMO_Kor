using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class PcBuildAssistantForm : Form
{
    private const string RepositoryUrl = "https://github.com/ccs21/UMO_Kor.git";
    private const string UnityDownloadUrl = "https://download.unity3d.com/download_unity/6cd387d23174/Windows64EditorInstaller/UnitySetup64-2018.4.36f1.exe";
    private const string GitDownloadUrl = "https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.5/Git-2.55.0.5-64-bit.exe";
    private const string PythonDownloadUrl = "https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe";
    private const string DataGuideUrl = "https://umo.xele.org/getting-started/installation/install-pc/";
    private const string OfficialLoginBonusUrl = "http://umo.xele.org:8000/offcial-login-bonuses_1_Android.zip";
    private const string OfficialLoginBonusFile = "offcial-login-bonuses_1_Android.zip";
    private const string OfficialLoginBonusPackage = "offcial-login-bonuses";
    private const string OfficialLoginBonusSha256 = "2888712f6b1774542fa4a1c2d6af425ae3616c6ba2a947f81ed5a48428fba4cc";
    private static readonly string[] Requirements = { "UnityPy==1.25.3", "Pillow==12.3.0", "texture2ddecoder==1.0.6" };
    private static readonly string[] ManifestNames = { "RequestGetDB.json", "RequestGetFiles.json", "RequestMaster.json", "RequestPlayerAccount.json" };

    private readonly TabControl pages = new TabControl();
    private readonly Button back = new Button(), next = new Button();
    private readonly Label pageTitle = new Label(), pageNumber = new Label();
    private readonly Label[] state = new Label[5];
    private readonly Label[] progressText = new Label[5];
    private readonly ProgressBar[] progress = new ProgressBar[5];
    private readonly TextBox log = new TextBox();
    private readonly TextBox workspace = new TextBox(), unityPath = new TextBox(), archiveFolder = new TextBox();
    private readonly Button installPackages = new Button(), cloneBuild = new Button(), convert = new Button(), settings = new Button();
    private readonly bool[] complete = new bool[5];
    private string gitExe, pythonExe;
    private string pythonArgsPrefix = "";
    private bool busy;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PcBuildAssistantForm());
    }

    public PcBuildAssistantForm()
    {
        Text = "우타마크로스 PC 빌드 도우미";
        Font = new Font("Malgun Gothic", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(900, 690);
        MinimumSize = new Size(820, 620);
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (busy && MessageBox.Show("작업 중입니다. 지금 종료하면 진행 중인 파일이 남을 수 있습니다. 종료할까요?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) e.Cancel = true;
        };

        string defaultRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        workspace.Text = defaultRoot;
        archiveFolder.Text = Path.Combine(defaultRoot, "Archives");
        unityPath.Text = FindUnity();

        var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.FromArgb(31, 86, 132), Padding = new Padding(18, 8, 18, 8) };
        pageTitle.ForeColor = Color.White; pageTitle.Font = new Font(Font, FontStyle.Bold); pageTitle.Location = new Point(18, 10); pageTitle.Size = new Size(700, 30);
        pageNumber.ForeColor = Color.White; pageNumber.TextAlign = ContentAlignment.MiddleRight; pageNumber.Dock = DockStyle.Right; pageNumber.Width = 110;
        header.Controls.Add(pageTitle); header.Controls.Add(pageNumber); Controls.Add(header);

        pages.Dock = DockStyle.Fill; pages.Appearance = TabAppearance.FlatButtons; pages.ItemSize = new Size(0, 1); pages.SizeMode = TabSizeMode.Fixed;
        pages.SelectedIndexChanged += delegate { UpdateNavigation(); };
        pages.TabPages.Add(Page1()); pages.TabPages.Add(Page2()); pages.TabPages.Add(Page3()); pages.TabPages.Add(Page4()); pages.TabPages.Add(Page5());
        Controls.Add(pages);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(14, 12, 14, 12), BackColor = Color.WhiteSmoke };
        back.Text = "이전"; back.Size = new Size(150, 46); back.Dock = DockStyle.Left; back.Font = new Font(Font, FontStyle.Bold); back.Click += delegate { if (pages.SelectedIndex > 0) pages.SelectedIndex--; };
        next.Text = "다음"; next.Size = new Size(150, 46); next.Dock = DockStyle.Right; next.Font = new Font(Font, FontStyle.Bold);
        next.Click += delegate { if (pages.SelectedIndex < 4 && complete[pages.SelectedIndex]) pages.SelectedIndex++; };
        footer.Controls.Add(back); footer.Controls.Add(next); Controls.Add(footer);
        pages.SendToBack(); header.BringToFront(); footer.BringToFront();
        UpdateNavigation();
        Shown += async delegate { await RefreshTools(); };
    }

    private TabPage BasePage(int index, string description, out FlowLayoutPanel flow)
    {
        var page = new TabPage { BackColor = Color.White, Padding = new Padding(18) };
        flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(6) };
        flow.Controls.Add(new Label { Text = description, Width = 820, Height = 54 });
        state[index] = new Label { Text = "확인 전", Width = 820, Height = 34, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.DarkOrange };
        flow.Controls.Add(state[index]); page.Controls.Add(flow); return page;
    }

    private TabPage Page1()
    {
        FlowLayoutPanel flow; var page = BasePage(0, "빌드에 필요한 프로그램을 검사합니다. 설치 후 [새로고침]을 누르세요. Windows PC 빌드에는 Android SDK/JDK/NDK가 필요하지 않습니다.", out flow);
        flow.Controls.Add(PathRow("작업 폴더", workspace, delegate { ChooseFolder(workspace, true); archiveFolder.Text = Path.Combine(workspace.Text, "Archives"); }, 710));
        flow.Controls.Add(PathRow("Unity 2018.4.36f1", unityPath, delegate { ChooseUnity(); }, 710));
        flow.Controls.Add(LinkRow("Git for Windows 64비트", GitDownloadUrl, "Git 설치파일 받기"));
        flow.Controls.Add(LinkRow("Python 3.12.10 64비트 (Add Python to PATH 필수)", PythonDownloadUrl, "Python 설치파일 받기"));
        flow.Controls.Add(LinkRow("Unity Editor 2018.4.36f1 Windows 64비트", UnityDownloadUrl, "Unity 설치파일 받기"));
        flow.Controls.Add(LinkRow(".NET Framework 4.8 개발자 팩", "https://dotnet.microsoft.com/download/dotnet-framework/net48", ".NET 다운로드"));
        flow.Controls.Add(ActionButton("설치 시 꼭 확인할 항목", delegate { ShowInstallHelp(); return Task.FromResult(0); }, 300));
        var refresh = ActionButton("설치 상태 새로고침", async delegate { await RefreshTools(); }); flow.Controls.Add(refresh);
        return page;
    }

    private TabPage Page2()
    {
        FlowLayoutPanel flow; var page = BasePage(1, "선택한 작업 폴더에 전용 Python 가상환경(.venv)을 만들고 텍스처 변환 패키지를 설치합니다.", out flow);
        flow.Controls.Add(new Label { Text = String.Join(Environment.NewLine, Requirements), Width = 800, Height = 108, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8) });
        flow.Controls.Add(ProgressPanel(1));
        installPackages.Text = "필요 패키지 설치"; installPackages.Size = new Size(280, 48); installPackages.Font = new Font(Font, FontStyle.Bold); installPackages.Click += async delegate { await InstallRequirements(); }; flow.Controls.Add(installPackages);
        flow.Controls.Add(ActionButton("패키지 상태 새로고침", async delegate { await RefreshPackages(); }));
        return page;
    }

    private TabPage Page3()
    {
        FlowLayoutPanel flow; var page = BasePage(2, "한국어판 저장소의 develop 브랜치를 자동으로 복제하고 Unity PC 빌드 및 결과 파일 검사를 실행합니다.", out flow);
        flow.Controls.Add(new Label { Text = "소스 위치: <작업 폴더>\\UMO_Kor\r\n게임 위치: <작업 폴더>\\UMO_Kor\\Unity\\Build\\Windows\\UMO_Kor", Width = 820, Height = 58 });
        flow.Controls.Add(ProgressPanel(2));
        cloneBuild.Text = "Git 클론 및 PC 빌드"; cloneBuild.Size = new Size(300, 48); cloneBuild.Font = new Font(Font, FontStyle.Bold); cloneBuild.Click += async delegate { await CloneAndBuild(); }; flow.Controls.Add(cloneBuild);
        flow.Controls.Add(ActionButton("빌드 상태 새로고침", async delegate { await RefreshBuild(); }));
        return page;
    }

    private TabPage Page4()
    {
        FlowLayoutPanel flow; var page = BasePage(3, "두 ZIP을 아래 폴더에 넣고 [새로고침 및 자동 배치]를 누르세요. 공식 로그인 보너스 DLC도 자동으로 설치합니다.", out flow);
        flow.Controls.Add(PathRow("ZIP 보관 폴더", archiveFolder, delegate { ChooseFolder(archiveFolder, false); }, 710));
        flow.Controls.Add(LinkRow("두 ZIP 파일의 다운로드 안내", DataGuideUrl, "다운로드 안내 열기"));
        flow.Controls.Add(ActionButton("ZIP 파일 넣을 폴더 열기", delegate {
            OpenArchiveFolder();
            return Task.FromResult(0);
        }, 330));
        flow.Controls.Add(new Label { Text = "필수 파일:\r\n\r\n  UtaMacrossDataArchive.zip\r\n  UtaMacrossDataArchivePCPatch.zip", AutoSize = true, MinimumSize = new Size(820, 0), MaximumSize = new Size(820, 0), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(12), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 5, 3, 8) });
        var autoPlace = ActionButton("새로고침 및 자동 배치", async delegate { await DetectAndExtract(); }, 380);
        autoPlace.Height = 58; flow.Controls.Add(autoPlace);
        return page;
    }

    private TabPage Page5()
    {
        FlowLayoutPanel flow; var page = BasePage(4, "Android용 압축 텍스처를 Windows용 캐시로 변환하고 전체 결과를 검사합니다. 데이터 양에 따라 오래 걸립니다.", out flow);
        flow.Controls.Add(ProgressPanel(4));
        convert.Text = "텍스처 변환 및 최종 검증"; convert.Size = new Size(330, 48); convert.Font = new Font(Font, FontStyle.Bold); convert.Click += async delegate { await ConvertAndVerify(); }; flow.Controls.Add(convert);
        settings.Text = "게임 폴더 열기 및 키 설정"; settings.Size = new Size(330, 48); settings.Font = new Font(Font, FontStyle.Bold); settings.Enabled = false; settings.Click += delegate { OpenSettingsAndExit(); }; flow.Controls.Add(settings);
        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Both; log.WordWrap = false; log.Size = new Size(820, 270); flow.Controls.Add(log);
        return page;
    }

    private Control PathRow(string label, TextBox box, Action choose, int width)
    {
        var panel = new TableLayoutPanel { Width = 830, Height = 48, ColumnCount = 3, RowCount = 1, Margin = new Padding(3, 4, 3, 4) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        box.Dock = DockStyle.Fill; box.Margin = new Padding(3, 8, 8, 8); panel.Controls.Add(box, 1, 0);
        var button = new Button { Text = "찾아보기", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3) }; button.Click += delegate { choose(); }; panel.Controls.Add(button, 2, 0);
        return panel;
    }

    private Control LinkRow(string text, string url, string buttonText)
    {
        var panel = new TableLayoutPanel { Width = 830, Height = 48, ColumnCount = 2, RowCount = 1, Margin = new Padding(3, 4, 3, 4) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var button = new Button { Text = buttonText, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3) }; button.Click += delegate { OpenUrl(url); }; panel.Controls.Add(button, 1, 0);
        return panel;
    }

    private Button ActionButton(string text, Func<Task> action, int width = 280)
    {
        var button = new Button { Text = text, Width = width, Height = 48, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 5, 3, 5) };
        button.Click += async delegate { await action(); };
        return button;
    }

    private Control ProgressPanel(int index)
    {
        var panel = new TableLayoutPanel { Width = 820, Height = 72, ColumnCount = 1, RowCount = 2, Margin = new Padding(3, 5, 3, 8) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        progressText[index] = new Label { Text = "대기 중", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        progress[index] = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Style = ProgressBarStyle.Continuous, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        panel.Controls.Add(progressText[index], 0, 0); panel.Controls.Add(progress[index], 0, 1);
        return panel;
    }

    private void UpdateNavigation()
    {
        string[] names = { "개발 도구 검사", "Python 패키지", "소스 및 빌드", "게임 데이터", "텍스처 및 완료" };
        int index = pages.SelectedIndex;
        if (index < 0 || index >= names.Length) return;
        pageTitle.Text = names[index]; pageNumber.Text = (index + 1) + " / 5";
        back.Enabled = index > 0 && !busy;
        next.Visible = index < 4; next.Enabled = !busy && complete[index];
    }

    private void SetBusy(bool value)
    {
        busy = value; UseWaitCursor = value; back.Enabled = !value && pages.SelectedIndex > 0;
        next.Enabled = !value && complete[pages.SelectedIndex]; installPackages.Enabled = !value; cloneBuild.Enabled = !value; convert.Enabled = !value;
    }

    private void SetState(int index, bool ok, string text)
    {
        complete[index] = ok; state[index].Text = (ok ? "✓ " : "● ") + text; state[index].ForeColor = ok ? Color.DarkGreen : Color.DarkOrange; UpdateNavigation();
    }

    private void SetWorkingText(int index, string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<int, string>(SetWorkingText), index, text); return; }
        state[index].Text = "● " + text; state[index].ForeColor = Color.DarkOrange;
    }

    private void SetProgress(int index, int value, string text, bool marquee)
    {
        if (InvokeRequired) { BeginInvoke(new Action<int, int, string, bool>(SetProgress), index, value, text, marquee); return; }
        if (progress[index] == null || progressText[index] == null) return;
        progress[index].Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        progress[index].MarqueeAnimationSpeed = marquee ? 24 : 0;
        if (!marquee) progress[index].Value = Math.Max(0, Math.Min(100, value));
        progressText[index].Text = text;
    }

    private void SetProgressFailed(int index, string text)
    {
        SetProgress(index, 0, "오류: " + text, false);
    }

    private async Task RefreshTools()
    {
        SetBusy(true);
        try
        {
            gitExe = await FindGit();
            pythonExe = await FindPython();
            if (!File.Exists(unityPath.Text)) unityPath.Text = FindUnity();
            string unityVersion = File.Exists(unityPath.Text) ? FileVersionInfo.GetVersionInfo(unityPath.Text).FileVersion : null;
            bool unityOk = File.Exists(unityPath.Text) && !String.IsNullOrEmpty(unityVersion) && unityVersion.StartsWith("2018.4.36");
            string compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            var missing = new List<string>();
            if (String.IsNullOrEmpty(gitExe)) missing.Add("Git");
            if (String.IsNullOrEmpty(pythonExe)) missing.Add("Python 3.10 이상");
            if (!unityOk) missing.Add("Unity Editor 2018.4.36f1");
            if (!File.Exists(compiler)) missing.Add(".NET Framework C# 컴파일러");
            if (!CanWriteWorkspace()) missing.Add("쓰기 가능한 작업 폴더");
            SetState(0, missing.Count == 0, missing.Count == 0 ? "필요한 개발 도구가 모두 준비되었습니다." : "설치 또는 경로 지정 필요: " + String.Join(", ", missing));
        }
        catch (Exception e) { SetState(0, false, "검사 실패: " + e.Message); }
        finally { SetBusy(false); }
    }

    private async Task RefreshPackages()
    {
        SetBusy(true);
        try
        {
            string venv = VenvPython();
            if (!File.Exists(venv)) { SetProgress(1, 0, "설치 전", false); SetState(1, false, "전용 Python 환경이 없습니다. [필요 패키지 설치]를 누르세요."); return; }
            SetProgress(1, 90, "설치된 패키지를 확인하고 있습니다.", true);
            ProcessResult result = await Run(venv, "-c \"import importlib.metadata as m; expected={'UnityPy':'1.25.3','Pillow':'12.3.0','texture2ddecoder':'1.0.6'}; bad=[k for k,v in expected.items() if m.version(k)!=v]; assert not bad, bad; print('OK')\"", null, false);
            SetProgress(1, result.ExitCode == 0 ? 100 : 0, result.ExitCode == 0 ? "패키지 검사 완료" : "패키지 누락", false);
            SetState(1, result.ExitCode == 0, result.ExitCode == 0 ? "필요한 Python 패키지가 모두 설치되었습니다." : "패키지가 누락됐습니다. 설치를 다시 실행하세요.");
        }
        catch (Exception e) { SetProgressFailed(1, e.Message); SetState(1, false, "검사 실패: " + e.Message); }
        finally { SetBusy(false); }
    }

    private async Task InstallRequirements()
    {
        SetBusy(true); ClearLog(); SetState(1, false, "Python 전용 환경과 패키지를 설치하고 있습니다. 창을 닫지 마세요.");
        try
        {
            Directory.CreateDirectory(WorkspaceRoot());
            string venv = VenvPython();
            if (!File.Exists(venv))
            {
                if (String.IsNullOrEmpty(pythonExe)) throw new Exception("1페이지에서 Python 설치를 먼저 확인하세요.");
                SetProgress(1, 10, "1/3 Python 전용 환경 생성 중", true);
                ProcessResult create = await Run(pythonExe, PrefixPythonArgs("-m venv " + Q(Path.Combine(WorkspaceRoot(), ".venv"))), WorkspaceRoot(), true);
                if (create.ExitCode != 0) throw new Exception("가상환경 생성에 실패했습니다.");
            }
            SetProgress(1, 30, "2/3 변환 패키지 다운로드 및 설치 중", true);
            ProcessResult pip = await Run(venv, "-m pip install --disable-pip-version-check " + String.Join(" ", Requirements), WorkspaceRoot(), true);
            if (pip.ExitCode != 0) throw new Exception("패키지 설치에 실패했습니다.");
            SetProgress(1, 90, "3/3 설치 결과 검사 중", false);
            await RefreshPackagesCore();
            if (!complete[1]) throw new Exception("필요 패키지의 설치 버전을 확인하지 못했습니다.");
            SetProgress(1, 100, "설치 및 검사 완료", false);
        }
        catch (Exception e) { SetProgressFailed(1, e.Message); SetState(1, false, e.Message); ShowError(e); }
        finally { SetBusy(false); }
    }

    private async Task RefreshPackagesCore()
    {
        string venv = VenvPython();
        ProcessResult result = File.Exists(venv) ? await Run(venv, "-c \"import importlib.metadata as m; expected={'UnityPy':'1.25.3','Pillow':'12.3.0','texture2ddecoder':'1.0.6'}; bad=[k for k,v in expected.items() if m.version(k)!=v]; assert not bad, bad\"", null, false) : new ProcessResult(1, "");
        SetState(1, result.ExitCode == 0, result.ExitCode == 0 ? "필요한 Python 패키지가 모두 설치되었습니다." : "패키지 설치를 확인할 수 없습니다.");
    }

    private async Task CloneAndBuild()
    {
        SetBusy(true); ClearLog(); SetState(2, false, "소스를 준비하고 Unity PC 빌드를 진행하고 있습니다. 창을 닫지 마세요.");
        try
        {
            if (!complete[0] || !complete[1]) throw new Exception("앞 단계를 먼저 완료하세요.");
            string repo = RepoRoot(); Directory.CreateDirectory(WorkspaceRoot());
            if (!Directory.Exists(Path.Combine(repo, ".git")))
            {
                if (Directory.Exists(repo) && Directory.EnumerateFileSystemEntries(repo).Any()) throw new Exception("소스 폴더가 비어 있지 않습니다: " + repo);
                SetProgress(2, 10, "1/3 Git 저장소 복제 중", true);
                ProcessResult clone = await Run(gitExe, "clone --branch develop --single-branch " + RepositoryUrl + " " + Q(repo), WorkspaceRoot(), true);
                if (clone.ExitCode != 0) throw new Exception("Git 클론에 실패했습니다.");
            }
            else
            {
                SetProgress(2, 10, "1/3 기존 소스 확인 및 업데이트 중", true);
                ProcessResult remote = await Run(gitExe, "-C " + Q(repo) + " remote get-url origin", repo, true);
                if (remote.ExitCode != 0 || remote.Output.IndexOf("ccs21/UMO_Kor", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new Exception("선택한 폴더의 저장소가 UMO_Kor가 아닙니다. 기존 파일은 자동 변경하지 않습니다.");
                ProcessResult checkout = await Run(gitExe, "-C " + Q(repo) + " checkout develop", repo, true);
                if (checkout.ExitCode != 0) throw new Exception("develop 브랜치로 전환하지 못했습니다. 로컬 변경 사항을 확인하세요.");
                ProcessResult pull = await Run(gitExe, "-C " + Q(repo) + " pull --ff-only origin develop", repo, true);
                if (pull.ExitCode != 0) throw new Exception("업데이트하지 못했습니다. 로컬 변경 사항을 보존했으므로 직접 확인하세요.");
            }
            SetProgress(2, 35, "2/3 Unity 리소스 준비 및 PC 빌드 중", true);
            string script = Path.Combine(repo, "Tools", "Build", "Build-Windows.ps1");
            string args = "-NoProfile -ExecutionPolicy Bypass -File " + Q(script) + " -Unity " + Q(unityPath.Text);
            ProcessResult build = await Run("powershell.exe", args, repo, true);
            if (build.ExitCode != 0) throw new Exception("PC 빌드에 실패했습니다. Logs 폴더를 확인하세요.");
            SetProgress(2, 95, "3/3 빌드 결과 파일 검사 중", false);
            VerifyBuildFiles(); SetState(2, true, "Git 클론, PC 빌드와 기본 파일 검증을 통과했습니다.");
            SetProgress(2, 100, "소스 준비, 빌드 및 검사 완료", false);
        }
        catch (Exception e) { SetProgressFailed(2, e.Message); SetState(2, false, e.Message); ShowError(e); }
        finally { SetBusy(false); }
    }

    private async Task RefreshBuild()
    {
        SetBusy(true);
        try { SetProgress(2, 90, "빌드 파일 검사 중", true); await Task.Run((Action)VerifyBuildFiles); SetProgress(2, 100, "빌드 파일 검사 완료", false); SetState(2, true, "PC 빌드 파일이 확인되었습니다."); }
        catch (Exception e) { SetProgressFailed(2, e.Message); SetState(2, false, "빌드 확인 실패: " + e.Message); }
        finally { SetBusy(false); }
    }

    private async Task DetectAndExtract()
    {
        SetBusy(true); ClearLog(); SetState(3, false, "두 ZIP을 확인하고 게임 데이터를 배치하고 있습니다. 오래 걸릴 수 있습니다.");
        try
        {
            VerifyBuildFiles(); Directory.CreateDirectory(archiveFolder.Text);
            string archive = Path.Combine(archiveFolder.Text, "UtaMacrossDataArchive.zip");
            string patch = Path.Combine(archiveFolder.Text, "UtaMacrossDataArchivePCPatch.zip");
            if (!File.Exists(archive) || !File.Exists(patch)) throw new Exception("ZIP 두 개를 찾지 못했습니다. 파일명과 보관 폴더를 확인하세요.");
            await Task.Run(delegate {
                ExtractMapped(archive, "data/", GameDataRoot());
                ExtractMapped(patch, "db/", Path.Combine(GameDataRoot(), "db"));
                CopyCurrentManifests();
                VerifyDataFiles(false);
            });
            SetWorkingText(3, "공식 로그인 보너스를 자동 설치하고 있습니다.");
            await InstallOfficialLoginBonus();
            SetState(3, true, "게임 데이터와 공식 로그인 보너스 DLC를 자동 배치했습니다.");
        }
        catch (Exception e) { SetState(3, false, e.Message); ShowError(e); }
        finally { SetBusy(false); }
    }

    private async Task ConvertAndVerify()
    {
        SetBusy(true); settings.Enabled = false; ClearLog(); SetState(4, false, "Windows용 텍스처를 변환하고 있습니다. 창을 닫거나 절전 상태로 만들지 마세요.");
        try
        {
            SetProgress(4, 0, "1/3 게임 데이터 검사 중", true);
            VerifyDataFiles(false);
            string script = Path.Combine(RepoRoot(), "Tools", "Windows", "prepare_texture_cache.py");
            SetProgress(4, 1, "2/3 변환 대상 검색 및 텍스처 변환 준비 중", true);
            ProcessResult result = await Run(VenvPython(), Q(script) + " --all --workers 4 --data-root " + Q(GameDataRoot()), RepoRoot(), true, UpdateTextureProgress);
            if (result.ExitCode != 0) throw new Exception("텍스처 변환 중 오류가 발생했습니다. 마지막 로그를 확인하세요.");
            SetProgress(4, 98, "3/3 변환 보고서와 결과 파일 검사 중", false);
            VerifyDataFiles(true);
            SetState(4, true, "빌드, 데이터, 텍스처 변환 및 최종 검증을 통과했습니다."); settings.Enabled = true;
            SetProgress(4, 100, "텍스처 변환 및 최종 검증 완료", false);
        }
        catch (Exception e) { SetProgressFailed(4, e.Message); SetState(4, false, e.Message); ShowError(e); }
        finally { SetBusy(false); settings.Enabled = complete[4]; }
    }

    private void ExtractMapped(string zipPath, string prefix, string destination)
    {
        AppendLog("압축 확인: " + zipPath);
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            var entries = zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !String.IsNullOrEmpty(e.Name)).ToList();
            if (entries.Count == 0) throw new InvalidDataException("예상한 " + prefix + " 폴더가 ZIP에 없습니다.");
            int done = 0;
            foreach (ZipArchiveEntry entry in entries)
            {
                string relative = entry.FullName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                // Repository manifests are newer and are copied after extraction.
                if (relative.IndexOf(Path.DirectorySeparatorChar) < 0 && ManifestNames.Contains(relative, StringComparer.OrdinalIgnoreCase)) { done++; continue; }
                string target = Path.GetFullPath(Path.Combine(root, relative));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP 경로가 대상 폴더를 벗어납니다: " + entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target) && new FileInfo(target).Length == entry.Length) { done++; continue; }
                string temporary = target + ".umo-part";
                using (Stream input = entry.Open()) using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                if (new FileInfo(temporary).Length != entry.Length) throw new InvalidDataException("압축 해제 크기가 다릅니다: " + entry.FullName);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target); done++;
                if (done % 500 == 0)
                {
                    string progress = String.Format("{0}: {1:N0}/{2:N0}개 배치 중", Path.GetFileName(zipPath), done, entries.Count);
                    AppendLog(progress); SetWorkingText(3, progress);
                }
            }
            AppendLog(String.Format("압축 배치 완료: {0} ({1}개)", Path.GetFileName(zipPath), entries.Count));
        }
    }

    private void CopyCurrentManifests()
    {
        string source = Path.Combine(RepoRoot(), "Data"); Directory.CreateDirectory(GameDataRoot());
        foreach (string name in ManifestNames)
        {
            string file = Path.Combine(source, name); if (!File.Exists(file)) throw new FileNotFoundException("저장소 목록 파일 누락", file);
            File.Copy(file, Path.Combine(GameDataRoot(), name), true);
        }
    }

    private void VerifyBuildFiles()
    {
        string game = GameRoot();
        RequireFile(Path.Combine(game, "UMO_Kor.exe")); RequireFile(Path.Combine(game, "UMO_PC_Settings.exe"));
        RequireFile(Path.Combine(game, "UMO_Kor_Data", "Managed", "Assembly-CSharp.dll"));
        RequireFile(Path.Combine(game, "UMO_Kor_Data", "Plugins", "libvlc.dll"));
        string buildLog = Path.Combine(RepoRoot(), "Logs", "windows-build.log"); RequireFile(buildLog);
        if (File.ReadAllText(buildLog).IndexOf("UMO Korean Windows build: result=Succeeded, errors=0", StringComparison.Ordinal) < 0)
            throw new InvalidDataException("Unity 성공 기록을 찾지 못했습니다.");
    }

    private void VerifyDataFiles(bool requireCache)
    {
        VerifyBuildFiles(); string data = GameDataRoot();
        string android = Path.Combine(data, "android");
        if (!Directory.Exists(android) || !Directory.EnumerateFiles(android, "*.xab", SearchOption.AllDirectories).Any())
            throw new DirectoryNotFoundException("Data/android 게임 애셋을 찾지 못했습니다.");
        string db = Path.Combine(data, "db"); if (!Directory.Exists(db) || Directory.GetFiles(db, "*.dat").Length < 4) throw new DirectoryNotFoundException("PC 패치 Data/db 파일이 부족합니다.");
        foreach (string name in ManifestNames) RequireFile(Path.Combine(data, name));
        if (requireCache)
        {
            string report = Path.Combine(data, "WindowsCache", "last-report.json"); RequireFile(report);
            int expected = Directory.EnumerateFiles(android, "*.xab", SearchOption.AllDirectories).Count();
            string dlc = Path.Combine(data, "dlc");
            if (Directory.Exists(dlc)) expected += Directory.EnumerateFiles(dlc, "*.xab", SearchOption.AllDirectories).Count();
            VerifyTextureReport(report, expected);
        }
    }

    private async Task InstallOfficialLoginBonus()
    {
        string install = Path.Combine(GameDataRoot(), "dlc", OfficialLoginBonusPackage);
        string installedInfo = Path.Combine(install, "dlc.json");
        if (File.Exists(installedInfo) && File.ReadAllText(installedInfo).IndexOf("\"version\":1", StringComparison.Ordinal) >= 0)
        {
            AppendLog("공식 로그인 보너스 DLC가 이미 설치되어 있습니다.");
            return;
        }

        Directory.CreateDirectory(archiveFolder.Text);
        string zipPath = Path.Combine(archiveFolder.Text, OfficialLoginBonusFile);
        if (!File.Exists(zipPath) || !HashMatches(zipPath, OfficialLoginBonusSha256))
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            AppendLog("공식 로그인 보너스 DLC 다운로드 중: " + OfficialLoginBonusUrl);
            using (var client = new WebClient())
                await client.DownloadFileTaskAsync(new Uri(OfficialLoginBonusUrl), zipPath);
        }
        if (!HashMatches(zipPath, OfficialLoginBonusSha256))
            throw new InvalidDataException("공식 로그인 보너스 DLC 파일 검증에 실패했습니다.");

        string dlcRoot = Path.Combine(GameDataRoot(), "dlc");
        string temporary = Path.Combine(dlcRoot, "." + OfficialLoginBonusPackage + "-install");
        string disabled = Path.Combine(dlcRoot, "_" + OfficialLoginBonusPackage);
        if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        Directory.CreateDirectory(temporary);
        ExtractWholeZipSafely(zipPath, temporary);
        string info = Path.Combine(temporary, "dlc.json");
        if (!File.Exists(info) || File.ReadAllText(info).IndexOf("\"package_name\":\"" + OfficialLoginBonusPackage + "\"", StringComparison.Ordinal) < 0)
            throw new InvalidDataException("공식 로그인 보너스 DLC 구조가 올바르지 않습니다.");
        if (Directory.Exists(install)) Directory.Delete(install, true);
        if (Directory.Exists(disabled)) Directory.Delete(disabled, true);
        Directory.Move(temporary, install);
        AppendLog("공식 로그인 보너스 DLC 설치 및 활성화 완료");
    }

    private static bool HashMatches(string path, string expected)
    {
        if (!File.Exists(path)) return false;
        using (var sha = SHA256.Create())
        using (var input = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ExtractWholeZipSafely(string zipPath, string destination)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (String.IsNullOrEmpty(entry.Name)) continue;
                string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("DLC ZIP 경로가 대상 폴더를 벗어납니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, true);
            }
        }
    }

    private void VerifyTextureReport(string report, int expectedCount)
    {
        int statusCount = 0;
        bool started = false, ended = false;
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "converted", "cached", "not-needed", "audio-not-needed", "error" };
        using (var reader = new StreamReader(report, Encoding.UTF8, true, 65536))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (!started && trimmed.Length > 0)
                {
                    if (!trimmed.StartsWith("[", StringComparison.Ordinal)) throw new InvalidDataException("텍스처 변환 보고서의 시작 형식이 올바르지 않습니다.");
                    started = true;
                }
                if (trimmed == "]") ended = true;
                Match match = Regex.Match(line, "^\\s*\\\"status\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                if (!match.Success) continue;
                string status = match.Groups[1].Value; statusCount++;
                if (!allowed.Contains(status)) throw new InvalidDataException("알 수 없는 텍스처 변환 상태가 있습니다: " + status);
                if (status == "error") throw new InvalidDataException("변환 보고서에 실패 항목이 있습니다.");
            }
        }
        if (!started || !ended) throw new InvalidDataException("텍스처 변환 보고서가 완전하지 않습니다.");
        if (statusCount == 0) throw new InvalidDataException("텍스처 변환 보고서가 비어 있습니다.");
        if (statusCount != expectedCount)
            throw new InvalidDataException(String.Format("텍스처 변환 보고서 항목 수가 원본과 다릅니다: 보고서 {0:N0}개 / 원본 {1:N0}개", statusCount, expectedCount));
        AppendLog(String.Format("텍스처 변환 보고서 검사 완료: {0:N0}개, 오류 0", statusCount));
    }

    private void UpdateTextureProgress(string line)
    {
        Match match = Regex.Match(line ?? "", @"^\s*(\d+)\s*/\s*(\d+)\s");
        if (!match.Success) return;
        int done, total;
        if (!Int32.TryParse(match.Groups[1].Value, out done) || !Int32.TryParse(match.Groups[2].Value, out total) || total <= 0) return;
        int percent = 2 + (int)Math.Round(95.0 * done / total);
        SetProgress(4, percent, String.Format("2/3 텍스처 변환 중: {0:N0}/{1:N0} ({2}%)", done, total, percent), false);
    }

    private async Task<ProcessResult> Run(string file, string arguments, string workingDirectory, bool showOutput, Action<string> outputHandler = null)
    {
        return await Task.Run(delegate {
            var start = new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = String.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory };
            var output = new StringBuilder();
            using (var process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { output.AppendLine(e.Data); if (showOutput) AppendLog(e.Data); if (outputHandler != null) try { outputHandler(e.Data); } catch { } } };
                process.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { output.AppendLine(e.Data); if (showOutput) AppendLog(e.Data); if (outputHandler != null) try { outputHandler(e.Data); } catch { } } };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit(); process.WaitForExit();
                return new ProcessResult(process.ExitCode, output.ToString());
            }
        });
    }

    private async Task<string> FindCommand(string command, string argument)
    {
        try { ProcessResult r = await Run(command, argument, null, false); return r.ExitCode == 0 ? command : null; } catch { return null; }
    }

    private async Task<string> FindPython()
    {
        foreach (string pair in new[] { "py.exe|-3.12", "py.exe|-3", "python.exe|" })
        {
            string[] p = pair.Split('|');
            try
            {
                ProcessResult r = await Run(p[0], p[1] + " -c \"import sys; print(sys.version_info[0], sys.version_info[1])\"", null, false);
                Match m = Regex.Match(r.Output, @"(\d+)\s+(\d+)");
                if (r.ExitCode == 0 && m.Success && Int32.Parse(m.Groups[1].Value) == 3 && Int32.Parse(m.Groups[2].Value) >= 10)
                {
                    pythonArgsPrefix = p[1];
                    return p[0];
                }
            }
            catch { }
        }
        pythonArgsPrefix = "";
        return null;
    }

    private string PrefixPythonArgs(string arguments)
    {
        return String.IsNullOrWhiteSpace(pythonArgsPrefix) ? arguments : pythonArgsPrefix + " " + arguments;
    }

    private async Task<string> FindGit()
    {
        string found = await FindCommand("git.exe", "--version");
        if (!String.IsNullOrWhiteSpace(found)) return found;
        string[] candidates = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe")
        };
        foreach (string candidate in candidates)
            if (File.Exists(candidate) && (await Run(candidate, "--version", null, false)).ExitCode == 0) return candidate;
        return null;
    }

    private static string FindUnity()
    {
        string[] roots = { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", "2018.4.36f1", "Editor", "Unity.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Unity", "Hub", "Editor", "2018.4.36f1", "Editor", "Unity.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Editor", "Unity.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Unity", "Editor", "Unity.exe") };
        return roots.FirstOrDefault(File.Exists) ?? roots[0];
    }

    private void ChooseUnity()
    {
        using (var dialog = new OpenFileDialog { Filter = "Unity Editor|Unity.exe", Title = "Unity 2018.4.36f1 선택" }) if (dialog.ShowDialog(this) == DialogResult.OK) unityPath.Text = dialog.FileName;
    }

    private void ChooseFolder(TextBox target, bool create)
    {
        using (var dialog = new FolderBrowserDialog { Description = "폴더 선택", SelectedPath = Directory.Exists(target.Text) ? target.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ShowNewFolderButton = create })
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private void OpenArchiveFolder()
    {
        try
        {
            string folder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(archiveFolder.Text.Trim()));
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", Q(folder));
        }
        catch (Exception e) { ShowError(e); }
    }

    private bool CanWriteWorkspace()
    {
        string testFile = null;
        try
        {
            Directory.CreateDirectory(WorkspaceRoot());
            testFile = Path.Combine(WorkspaceRoot(), ".umo-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            if (!String.IsNullOrEmpty(testFile)) try { if (File.Exists(testFile)) File.Delete(testFile); } catch { }
            return false;
        }
    }

    private void ShowInstallHelp()
    {
        string message =
            "[Python]\r\n" +
            "• Python 3.10 이상 64비트 일반 설치 프로그램을 사용하세요.\r\n" +
            "• Add python.exe to PATH를 체크하세요.\r\n" +
            "• pip와 py launcher를 빼지 마세요.\r\n" +
            "• Microsoft Store 별칭이나 embeddable package는 사용하지 마세요.\r\n\r\n" +
            "[Unity]\r\n" +
            "• 정확히 Unity Editor 2018.4.36f1을 설치하세요.\r\n" +
            "• Windows Build Support (Mono)가 보이면 체크하세요.\r\n" +
            "• 설치 후 Unity를 한 번 실행해 로그인과 라이선스 활성화를 끝내고 닫으세요.\r\n" +
            "• 프로젝트를 최신 Unity 버전으로 업그레이드하지 마세요.\r\n\r\n" +
            "설치가 끝나면 도우미를 다시 실행하고 [설치 상태 새로고침]을 누르세요.";
        MessageBox.Show(message, "설치 시 꼭 확인할 항목", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenSettingsAndExit()
    {
        string game = GameRoot(), tool = Path.Combine(game, "UMO_PC_Settings.exe");
        try { Process.Start("explorer.exe", Q(game)); Process.Start(tool); Close(); }
        catch (Exception e) { ShowError(e); }
    }

    private void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception e) { ShowError(e); } }
    private string WorkspaceRoot() { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(workspace.Text.Trim())); }
    private string RepoRoot() { return Path.Combine(WorkspaceRoot(), "UMO_Kor"); }
    private string GameRoot() { return Path.Combine(RepoRoot(), "Unity", "Build", "Windows", "UMO_Kor"); }
    private string GameDataRoot() { return Path.Combine(GameRoot(), "Data"); }
    private string VenvPython() { return Path.Combine(WorkspaceRoot(), ".venv", "Scripts", "python.exe"); }
    private static string Q(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    private static void RequireFile(string path) { if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new FileNotFoundException("필수 파일을 찾지 못했습니다.", path); }
    private string LogFile() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UMO_PC_Build_Assistant.log"); }
    private void ClearLog()
    {
        if (InvokeRequired) { BeginInvoke((Action)ClearLog); return; }
        log.Clear();
        try { File.WriteAllText(LogFile(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 작업 시작" + Environment.NewLine, Encoding.UTF8); } catch { }
    }
    private void AppendLog(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), text); return; }
        log.AppendText(text + Environment.NewLine);
        try { File.AppendAllText(LogFile(), text + Environment.NewLine, Encoding.UTF8); } catch { }
    }
    private void ShowError(Exception e)
    {
        AppendLog("오류: " + e);
        MessageBox.Show(e.Message + "\r\n\r\n상세 로그: " + LogFile(), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private sealed class ProcessResult
    {
        public readonly int ExitCode; public readonly string Output;
        public ProcessResult(int code, string output) { ExitCode = code; Output = output; }
    }
}
