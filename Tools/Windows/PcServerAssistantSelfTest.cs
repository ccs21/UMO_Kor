using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading;

internal static class PcServerSelfTest
{
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "umo-pc-server-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string archive = Path.Combine(root, "archive.zip"), patch = Path.Combine(root, "patch.zip"), data = Path.Combine(root, "data");
            CreateArchive(archive, true); CreateArchive(patch, false);
            DataInstaller.Install(archive, patch, data, new ConsoleProgress());
            Assert(File.ReadAllText(Path.Combine(data, "android", "foo.xab")) == "hello-main", "main extraction");
            Assert(File.ReadAllText(Path.Combine(data, "db", "sd!sabcdefz!.dat")) == "database", "patch extraction");

            int tcpPort = FindTcpPort(); int udpPort = FindUdpPort();
            using (var server = new UmoDataServer(data, delegate(string value) { Console.WriteLine(value); }, tcpPort, udpPort))
            {
                server.Start(); Thread.Sleep(200);
                Assert(Request(tcpPort, "/android/foo!sabcdefz!.xab", null).EndsWith("hello-main"), "dehashed asset request");
                Assert(Request(tcpPort, "/db/sd.dat", null).EndsWith("database"), "manifest mapped request");
                string ranged = Request(tcpPort, "/android/foo!sabcdefz!.xab", "bytes=1-3");
                Assert(ranged.Contains("206 Partial Content") && ranged.EndsWith("ell"), "range request");
            }
            Console.WriteLine("SELF TEST PASSED"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(root).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void CreateArchive(string path, bool main)
    {
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            if (main)
            {
                string json = "{\"files\":[{\"file\":\"/android/foo!sabcdefz!.xab\"},{\"file\":\"/db/sd!sabcdefz!.dat\"}]}" + new string(' ', 1000001);
                Write(zip, "data/RequestGetFiles.json", json); Write(zip, "data/android/foo.xab", "hello-main");
            }
            else Write(zip, "db/sd!sabcdefz!.dat", "database");
        }
    }

    private static void Write(ZipArchive zip, string name, string value)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(value);
    }

    private static int FindTcpPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0); listener.Start();
        try { return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; } finally { listener.Stop(); }
    }

    private static int FindUdpPort()
    {
        using (var client = new UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0))) return ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Port;
    }

    private static string Request(int port, string path, string range)
    {
        using (var client = new TcpClient("127.0.0.1", port))
        using (NetworkStream stream = client.GetStream())
        {
            string request = "GET " + path + " HTTP/1.1\r\nHost: localhost\r\n" + (range == null ? "" : "Range: " + range + "\r\n") + "Connection: close\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(request); stream.Write(bytes, 0, bytes.Length);
            using (var output = new MemoryStream()) { stream.CopyTo(output); return Encoding.UTF8.GetString(output.ToArray()); }
        }
    }

    private static void Assert(bool condition, string name) { if (!condition) throw new Exception("Failed: " + name); }
    private sealed class ConsoleProgress : IProgress<InstallProgress> { public void Report(InstallProgress value) { } }
}
