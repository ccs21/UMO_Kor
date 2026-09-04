using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Offline, opt-in test-copy editor. Never changes profile selection or unlock flags.
public static class PcTestResources
{
    public const string Marker = "umo-test-profile.txt";
    private static JavaScriptSerializer Serializer()
    {
        return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
    }
    private static Dictionary<string, object> Obj(object value) { return (Dictionary<string, object>)value; }
    public static string RefillJson(string source)
    {
        var json = Serializer().Deserialize<Dictionary<string, object>>(source);
        var common = Obj(json["common"]);
        common["have_uc"] = Math.Max(Convert.ToInt32(common["have_uc"]), 99999999);
        string[] groups = { "grow_item", "epi_item", "cos_item", "val_item" };
        int[] caps = { 99999, 9999, 9999, 99999 };
        for (int i = 0; i < groups.Length; i++)
        {
            var items = (IEnumerable)common[groups[i]];
            foreach (object value in items)
            {
                var item = Obj(value);
                if (Convert.ToInt32(item["id"]) <= 0) throw new InvalidDataException("잘못된 소재 ID");
                item["cnt"] = Math.Max(Convert.ToInt32(item["cnt"]), caps[i]);
            }
        }
        var products = Obj(Obj(json["user_data"])["products"]);
        var currencies = (IList)products["currencies"];
        Dictionary<string, object> crystal = null;
        foreach (object value in currencies)
            if (Convert.ToInt32(Obj(value)["id"]) == 1001) crystal = Obj(value);
        if (crystal == null)
        {
            crystal = new Dictionary<string, object> { { "id", 1001 }, { "free", 0 }, { "paid", 0 } };
            // Deserializer returns an expandable ArrayList on .NET Framework.
            var expanded = new ArrayList(currencies);
            expanded.Add(crystal);
            products["currencies"] = expanded;
        }
        // Normal crystal acquisition limit is below 10,000 (AMOCLPHDGBP).
        // Preserve existing free currency and never lower an already higher balance.
        crystal["paid"] = Math.Max(Convert.ToInt32(crystal["paid"]), Math.Max(0, 9999 - Convert.ToInt32(crystal["free"])));
        return Serializer().Serialize(json);
    }
    public static void EnsureGameClosed()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string n = process.ProcessName;
                if (n.Equals("UMO_Kor", StringComparison.OrdinalIgnoreCase) || n.Equals("UtaMacross", StringComparison.OrdinalIgnoreCase) || n.Equals("Unity", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("게임과 Unity Editor를 먼저 종료해 주세요. 실행 중에는 세이브를 변경하지 않습니다.");
            }
        }
    }
    private static string Profile(string root, string id)
    {
        int number;
        if (!int.TryParse(id, out number) || number < 100000000 || number >= 900000000 || id != number.ToString())
            throw new InvalidDataException("일반 프로필 ID가 아닙니다.");
        return Path.Combine(Path.GetFullPath(root), "Profiles", id);
    }
    private static void CopyDirectory(string source, string dest)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("링크 폴더는 복사하지 않습니다.");
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("링크 파일은 복사하지 않습니다.");
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), false);
        }
        foreach (var sub in Directory.GetDirectories(source)) CopyDirectory(sub, Path.Combine(dest, Path.GetFileName(sub)));
    }
    public static string CreateTestCopy(string root, string sourceId)
    {
        EnsureGameClosed();
        string source = Profile(root, sourceId);
        // Validate before creating anything, but keep the copied balances unchanged.
        RefillJson(File.ReadAllText(Path.Combine(source, "data.json")));
        int id = 600000000;
        while (Directory.Exists(Profile(root, id.ToString())) || File.Exists(Path.Combine(root, "SaveData", id + "_save.bin")))
        {
            id++;
            if (id >= 900000000) throw new IOException("빈 테스트 프로필 ID가 없습니다.");
        }
        string dest = Profile(root, id.ToString());
        // Publish only after the complete copy is ready; failed copies remain recoverable.
        string pending = dest + ".copy-" + Guid.NewGuid().ToString("N");
        CopyDirectory(source, pending);
        string save = Path.Combine(root, "SaveData", sourceId + "_save.bin");
        if (File.Exists(save)) File.Copy(save, Path.Combine(root, "SaveData", id + "_save.bin"), false);
        File.WriteAllText(Path.Combine(pending, Marker), "source=" + sourceId + "\ncreated=" + DateTime.UtcNow.ToString("o"));
        Directory.Move(pending, dest);
        return id.ToString();
    }
    public static string Refill(string root, string id)
    {
        EnsureGameClosed();
        string profile = Profile(root, id);
        if (!File.Exists(Path.Combine(profile, Marker))) throw new InvalidOperationException("테스트 복사본에서만 충전할 수 있습니다. 먼저 테스트 복사를 눌러 주세요.");
        string file = Path.Combine(profile, "data.json");
        string source = File.ReadAllText(file);
        string updated = RefillJson(source);
        string backup = Path.Combine(root, "PcTestBackups", id, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(profile, Path.Combine(backup, "Profile"));
        string save = Path.Combine(root, "SaveData", id + "_save.bin");
        if (File.Exists(save)) File.Copy(save, Path.Combine(backup, id + "_save.bin"), false);
        string temp = file + ".refill-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, updated);
        if (RefillJson(File.ReadAllText(temp)) != updated) throw new IOException("저장 검증 실패");
        EnsureGameClosed();
        if (File.ReadAllText(file) != source) throw new IOException("검사 중 세이브가 변경되어 중단했습니다.");
        File.Replace(temp, file, Path.Combine(backup, "data-before.json"));
        return backup;
    }

    public static void ShowDialog(IWin32Window owner)
    {
        using (var form = new Form { Text = "검수용 자원 충전 — PC 테스트 복사본 전용", Width = 730, Height = 365, Font = new System.Drawing.Font("Malgun Gothic", 10) })
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            form.Controls.Add(panel);
            panel.Controls.Add(new Label { Width = 680, Height = 70, Text = "게임을 종료한 뒤 사용하세요. 원본은 그대로 두고 복사한 프로필만 충전합니다.\n가정석 9,999 / UC 99,999,999 / 강화·발키리 소재 99,999 / 에피소드·의상 소재 9,999\n기존 초과 수량은 유지합니다. 튜토리얼·랭크·의상·플레이트 보유 상태는 해금하지 않습니다." });
            var root = new TextBox { Width = 675, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "UtaMacross", "UtaMacross") };
            panel.Controls.Add(root);
            var choices = new ComboBox { Width = 675, DropDownStyle = ComboBoxStyle.DropDownList };
            panel.Controls.Add(choices);
            Action reload = delegate {
                choices.Items.Clear();
                string folder = Path.Combine(root.Text, "Profiles");
                if (!Directory.Exists(folder)) return;
                foreach (var dir in Directory.GetDirectories(folder).OrderBy(x => x))
                {
                    int id;
                    if (int.TryParse(Path.GetFileName(dir), out id) && id < 900000000 && File.Exists(Path.Combine(dir, "data.json")))
                        choices.Items.Add(Path.GetFileName(dir) + (File.Exists(Path.Combine(dir, Marker)) ? " [테스트]" : " [원본]"));
                }
                if (choices.Items.Count > 0) choices.SelectedIndex = 0;
            };
            var buttons = new FlowLayoutPanel { Width = 680, Height = 43 };
            panel.Controls.Add(buttons);
            var refresh = new Button { Text = "목록 새로고침", Width = 150, Height = 35 };
            var copy = new Button { Text = "선택 프로필 테스트 복사", Width = 235, Height = 35 };
            var refill = new Button { Text = "소재·재화 최대 충전", Width = 230, Height = 35 };
            buttons.Controls.AddRange(new Control[] { refresh, copy, refill });
            Action<Action> safely = action => { try { action(); } catch (Exception e) { MessageBox.Show(form, e.Message, "작업 중단", MessageBoxButtons.OK, MessageBoxIcon.Warning); } };
            refresh.Click += delegate { safely(reload); };
            copy.Click += delegate { safely(delegate {
                if (choices.SelectedItem == null) return;
                string id = CreateTestCopy(root.Text, choices.Text.Split(' ')[0]);
                reload(); choices.SelectedItem = id + " [테스트]";
                MessageBox.Show(form, "테스트 복사본: " + id + "\n충전 후 게임의 프로필 선택 화면에서 이 번호를 선택하세요. 원본 선택 상태는 변경하지 않았습니다.");
            }); };
            refill.Click += delegate { safely(delegate {
                if (choices.SelectedItem == null) return;
                if (MessageBox.Show(form, choices.Text + "에 자원을 충전할까요? 기존 상태를 먼저 백업합니다.", "자원 충전", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                string backup = Refill(root.Text, choices.Text.Split(' ')[0]);
                MessageBox.Show(form, "충전했습니다. 게임에서 해당 테스트 프로필을 선택하세요.\n백업: " + backup);
            }); };
            panel.Controls.Add(new Label { Width = 675, Height = 55, Text = "경로는 Profiles와 SaveData 폴더가 들어 있는 저장 루트입니다.\n매번 충전 전 PcTestBackups에 백업합니다. 앱/Android 세이브에는 적용하지 마세요." });
            safely(reload);
            form.ShowDialog(owner);
        }
    }
}
