using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
class TestPcResources
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static Dictionary<string, object> Obj(object value) { return (Dictionary<string, object>)value; }
    static void Main(string[] args)
    {
        string source = File.ReadAllText(args[0]); // read only: supplied profile or fixture
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
        string updated = PcTestResources.RefillJson(source);
        var before = serializer.Deserialize<Dictionary<string, object>>(source);
        var after = serializer.Deserialize<Dictionary<string, object>>(updated);
        var oldCommon = Obj(before["common"]); var newCommon = Obj(after["common"]);
        Assert(Convert.ToInt32(newCommon["have_uc"]) >= 99999999, "UC cap");
        foreach (string key in new[] { "have_uc", "grow_item", "epi_item", "cos_item", "val_item" }) newCommon[key] = oldCommon[key];
        var oldProducts = Obj(Obj(before["user_data"])["products"]);
        var newProducts = Obj(Obj(after["user_data"])["products"]);
        foreach (object value in (IEnumerable)newProducts["currencies"])
        {
            var item = Obj(value);
            if (Convert.ToInt32(item["id"]) == 1001) Assert(Convert.ToInt32(item["paid"]) + Convert.ToInt32(item["free"]) >= 9999, "Crystal cap");
        }
        newProducts["currencies"] = oldProducts["currencies"];
        Assert(serializer.Serialize(before) == serializer.Serialize(after), "Unrelated progression changed");
        Assert(PcTestResources.RefillJson(updated) == updated, "Not idempotent");
        // Test real copy/backup operations only in an isolated scratch root.
        string root = Path.Combine(Path.GetTempPath(), "umo-resource-test-" + Guid.NewGuid().ToString("N"));
        string original = Path.Combine(root, "Profiles", "123456789");
        Directory.CreateDirectory(original); Directory.CreateDirectory(Path.Combine(root, "SaveData"));
        File.WriteAllText(Path.Combine(original, "data.json"), source);
        File.WriteAllText(Path.Combine(root, "SaveData", "123456789_save.bin"), "local-save-fixture");
        bool refused = false;
        try { PcTestResources.Refill(root, "123456789"); } catch (InvalidOperationException) { refused = true; }
        Assert(refused, "Must refuse original");
        string id = PcTestResources.CreateTestCopy(root, "123456789");
        string backup = PcTestResources.Refill(root, id);
        Assert(File.ReadAllText(Path.Combine(original, "data.json")) == source, "Original changed");
        Assert(File.ReadAllText(Path.Combine(backup, "Profile", "data.json")) == source, "Backup mismatch");
        Assert(File.ReadAllText(Path.Combine(root, "Profiles", id, "data.json")) == updated, "Refill mismatch");
        string originalBackup = PcTestResources.Refill(root, "123456789", true);
        Assert(File.ReadAllText(Path.Combine(original, "data.json")) == updated, "Original opt-in refill failed");
        Assert(File.ReadAllText(Path.Combine(originalBackup, "Profile", "data.json")) == source, "Original backup mismatch");
        Assert(File.ReadAllText(Path.Combine(originalBackup, "data-before.json")) == source, "Atomic replacement backup mismatch");
        Assert(File.ReadAllText(Path.Combine(originalBackup, "123456789_save.bin")) == "local-save-fixture", "Binary backup mismatch");
        Assert(File.ReadAllText(Path.Combine(root, "SaveData", "123456789_save.bin")) == "local-save-fixture", "Local save changed");
        string repeatedBackup = PcTestResources.Refill(root, "123456789", true);
        Assert(repeatedBackup != originalBackup, "Repeated backup overwrote prior backup");
        Assert(File.ReadAllText(Path.Combine(repeatedBackup, "Profile", "data.json")) == updated, "Repeat backup mismatch");
        Assert(!File.Exists(Path.Combine(original, PcTestResources.Marker)), "Original converted into test profile");
        Console.WriteLine("PASS: resource caps, unrelated fields, idempotence, opt-in original refill, unique backups, unchanged local save and profile ID. Fixture: " + root);
    }
}
