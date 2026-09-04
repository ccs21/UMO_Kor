using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public sealed class PcSettingsForm : Form
{
    private readonly string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UMOPcSettings.FileName);
    private readonly ComboBox[] keys4 = new ComboBox[4], keys6 = new ComboBox[6], pad4 = new ComboBox[4], pad6 = new ComboBox[6];
    private ComboBox skill, assist, resolution, fps;
    private CheckedListBox skillPad;
    private CheckBox low, fullscreen;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PcSettingsForm());
    }

    public PcSettingsForm()
    {
        Text = "우타마크로스 PC 설정";
        Font = new Font("Malgun Gothic", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(790, 620);
        MinimumSize = new Size(700, 570);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14) };
        Controls.Add(panel);
        AddLabel(panel, "레인은 화면 왼쪽부터 순서대로입니다. 저장 후 게임을 재시작해 주세요.", 740);
        AddLabel(panel, "기존 세이브·데이터는 변경하지 않습니다. 게임패드는 XInput 모드로 연결하세요.", 740);
        AddBindings(panel, "4키 키보드", keys4, UMOPcSettings.KeyNames());
        AddBindings(panel, "6키 키보드", keys6, UMOPcSettings.KeyNames());
        AddBindings(panel, "4키 게임패드", pad4, UMOPcSettings.PadNames);
        AddBindings(panel, "6키 게임패드", pad6, UMOPcSettings.PadNames);
        var skillRow = Row(panel, "스킬 키보드"); skill = Combo(UMOPcSettings.KeyNames(), 140); skillRow.Controls.Add(skill);
        AddLabel(panel, "스킬 게임패드: 선택한 버튼 중 하나만 눌러도 발동합니다.", 730);
        skillPad = new CheckedListBox { Width = 730, Height = 64, MultiColumn = true, ColumnWidth = 85, CheckOnClick = true };
        skillPad.Items.AddRange(UMOPcSettings.PadNames); panel.Controls.Add(skillPad);
        var options = Row(panel, "PC 판정 보조"); assist = Combo(new[] { "원본 (0ms)", "약하게 (+15ms)", "넉넉하게 (+30ms)" }, 195); options.Controls.Add(assist);
        options = Row(panel, "화면 해상도"); resolution = Combo(new[] { "1280 x 720", "1280 x 800", "1920 x 1080" }, 170); options.Controls.Add(resolution);
        fps = Combo(new[] { "60 fps", "30 fps" }, 100); options.Controls.Add(fps);
        low = new CheckBox { Text = "저부하 (AA·그림자·이방성 필터 끄기)", Width = 360, Checked = true };
        fullscreen = new CheckBox { Text = "전체 화면", Width = 130 };
        var flags = new FlowLayoutPanel { Width = 735, Height = 35 }; flags.Controls.Add(low); flags.Controls.Add(fullscreen); panel.Controls.Add(flags);
        AddLabel(panel, "판정 보조는 앞뒤 허용 시간을 늘립니다. 점수·클리어 기록은 기존처럼 저장됩니다.", 735);
        AddLabel(panel, "리전 고 1: 1280×800 / 60fps / 저부하부터 테스트. 실제 성능은 기기에서 확인해야 합니다.", 735);
        var actions = new FlowLayoutPanel { Width = 735, Height = 42 };
        var save = new Button { Text = "저장", Width = 150, Height = 34 };
        save.Click += delegate { SaveSettings(); };
        var defaults = new Button { Text = "기본값 불러오기", Width = 180, Height = 34 };
        defaults.Click += delegate { Populate(new UMOPcSettings()); };
        actions.Controls.Add(save); actions.Controls.Add(defaults); panel.Controls.Add(actions);
        var resources = new Button { Text = "검수용 테스트 프로필 / 자원 충전", Width = 380, Height = 36 };
        resources.Click += delegate { PcTestResources.ShowDialog(this); };
        panel.Controls.Add(resources);
        try { Populate(UMOPcSettings.Load(path)); }
        catch (Exception e) { Populate(new UMOPcSettings()); MessageBox.Show("설정을 읽지 못해 기본값을 표시합니다. 기존 파일은 보존됩니다.\n" + e.Message, Text); }
    }

    private static void AddLabel(Control parent, string text, int width)
    {
        parent.Controls.Add(new Label { Text = text, Width = width, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
    }
    private static FlowLayoutPanel Row(Control parent, string label)
    {
        var row = new FlowLayoutPanel { Width = 740, Height = 37, WrapContents = false };
        row.Controls.Add(new Label { Text = label, Width = 135, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
        parent.Controls.Add(row); return row;
    }
    private static ComboBox Combo(string[] names, int width)
    {
        var combo = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(names); return combo;
    }
    private static void AddBindings(Control parent, string label, ComboBox[] boxes, string[] names)
    {
        var row = Row(parent, label);
        for (int i = 0; i < boxes.Length; i++) { boxes[i] = Combo(names, 91); row.Controls.Add(boxes[i]); }
    }
    private static void Set(ComboBox[] boxes, string[] values)
    {
        for (int i = 0; i < boxes.Length; i++) boxes[i].SelectedItem = values[i];
    }
    private static string[] Get(ComboBox[] boxes) { return boxes.Select(b => (string)b.SelectedItem).ToArray(); }
    private void Populate(UMOPcSettings config)
    {
        Set(keys4, config.Keys4); Set(keys6, config.Keys6); Set(pad4, config.Pad4); Set(pad6, config.Pad6);
        skill.SelectedItem = config.SkillKey;
        for (int i = 0; i < skillPad.Items.Count; i++) skillPad.SetItemChecked(i, Array.IndexOf(config.SkillPad, (string)skillPad.Items[i]) >= 0);
        assist.SelectedIndex = config.AssistMs / 15;
        resolution.SelectedItem = config.Width + " x " + config.Height;
        fps.SelectedIndex = config.MaxFps == 60 ? 0 : 1;
        low.Checked = config.LowGraphics; fullscreen.Checked = config.Fullscreen;
    }
    private void SaveSettings()
    {
        try
        {
            var config = new UMOPcSettings();
            config.Keys4 = Get(keys4); config.Keys6 = Get(keys6); config.Pad4 = Get(pad4); config.Pad6 = Get(pad6);
            config.SkillKey = (string)skill.SelectedItem;
            config.SkillPad = skillPad.CheckedItems.Cast<string>().ToArray();
            config.AssistMs = assist.SelectedIndex * 15;
            string[] size = ((string)resolution.SelectedItem).Split('x');
            config.Width = int.Parse(size[0]); config.Height = int.Parse(size[1]);
            config.MaxFps = fps.SelectedIndex == 0 ? 60 : 30;
            config.LowGraphics = low.Checked; config.Fullscreen = fullscreen.Checked;
            config.Save(path);
            MessageBox.Show("저장했습니다. 게임을 재시작하면 적용됩니다.\n" + path, Text);
        }
        catch (Exception e) { MessageBox.Show("저장하지 못했습니다.\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
