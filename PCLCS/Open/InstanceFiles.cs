// SPDX-License-Identifier: MIT
using System.IO;
using System.IO.Compression;

namespace PCLCS.Open;

public static class InstanceFiles {
    public static string Import(string source, string targetDirectory, bool world) {
        Directory.CreateDirectory(targetDirectory);
        var name = Path.GetFileName(source);
        if (!world) {
            var destination = Path.Combine(targetDirectory, name);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("已存在同名资源，请先重命名。");
            var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(source, staging); File.Move(staging, destination); }
            finally { if (File.Exists(staging)) File.Delete(staging); }
            return destination;
        }
        var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(source));
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("已存在同名存档，请先重命名。");
        var temp = Path.Combine(targetDirectory, ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            using (var zip = ZipFile.OpenRead(source)) {
                long total = 0;
                if (zip.Entries.Count > 200000) throw new InvalidDataException("压缩包文件数量过多。");
                foreach (var entry in zip.Entries) {
                    total = checked(total + entry.Length);
                    if (total > 16L * 1024 * 1024 * 1024) throw new InvalidDataException("存档解压大小超过 16 GiB。");
                    var normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.GetFullPath(Path.Combine(temp, normalized));
                    if (!full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || normalized.Contains(':')) throw new InvalidDataException("压缩包包含不安全的路径。");
                    if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("不支持压缩包中的符号链接。");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(full); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    entry.ExtractToFile(full, false);
                }
            }
            var root = temp;
            if (!File.Exists(Path.Combine(root, "level.dat"))) {
                var folders = Directory.GetDirectories(root);
                if (folders.Length == 1 && File.Exists(Path.Combine(folders[0], "level.dat"))) root = folders[0];
                else throw new InvalidDataException("压缩包中没有可识别的 level.dat 存档。");
            }
            Directory.Move(root, target);
            return target;
        } finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
    public static void Export(string source, string destination) {
        var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            if (Directory.Exists(source)) {
                var root = Path.GetFullPath(source) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(destination).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("导出位置不能位于存档内部。");
                ZipFile.CreateFromDirectory(source, staging, CompressionLevel.Optimal, true);
            } else File.Copy(source, staging);
            if (File.Exists(destination)) File.Replace(staging, destination, destination + ".bak"); else File.Move(staging, destination);
        } finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}

/// <summary>Small bounded NBT codec. Preserve every unknown tag when editing servers.dat.</summary>
public static class ServerList {
    private sealed class Node {
        public byte Type;
        public string Name = "";
        public byte[] Payload = [];
        public List<Node> Children = [];
        public byte ElementType;
    }
    public sealed class Entry { public string Name { get; set; } = ""; public string Address { get; set; } = ""; public int Index { get; set; } }
    public static List<Entry> Read(string path) {
        var root = Load(path);
        var servers = root.Children.FirstOrDefault(n => n.Name == "servers" && n.Type == 9);
        return servers?.Children.Select((n, i) => new Entry { Name = Get(n, "name"), Address = Get(n, "ip"), Index = i }).ToList() ?? [];
    }
    public static void Add(string path, string name, string address) {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) || address.Any(char.IsWhiteSpace)) throw new ArgumentException("请输入服务器名称和有效地址。");
        var root = Load(path);
        var servers = root.Children.FirstOrDefault(n => n.Name == "servers" && n.Type == 9);
        if (servers == null) { servers = new Node { Type = 9, Name = "servers", ElementType = 10 }; root.Children.Add(servers); }
        if (servers.ElementType != 10) throw new InvalidDataException("服务器列表格式无效。");
        var node = new Node { Type = 10 };
        node.Children.Add(Text("name", name)); node.Children.Add(Text("ip", address));
        servers.Children.Add(node); Save(path, root);
    }
    public static void Remove(string path, int index) {
        var root = Load(path);
        var servers = root.Children.First(n => n.Name == "servers" && n.Type == 9);
        servers.Children.RemoveAt(index); Save(path, root);
    }
    private static string Get(Node n, string name) { var tag = n.Children.FirstOrDefault(x => x.Name == name && x.Type == 8); return tag == null ? "" : Decode(tag.Payload.Skip(2).ToArray()); }
    private static Node Text(string name, string value) {
        var bytes = Encode(value);
        if (bytes.Length > 65535) throw new ArgumentException("服务器名称或地址过长。");
        return new Node { Type = 8, Name = name, Payload = new[] { (byte)(bytes.Length >> 8), (byte)bytes.Length }.Concat(bytes).ToArray() };
    }
    private static Node Load(string path) {
        if (!File.Exists(path)) return new Node { Type = 10 };
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("servers.dat 过大。");
        using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
        var type = reader.ReadByte(); if (type != 10) throw new InvalidDataException("servers.dat 根标签格式无效。");
        var name = ReadString(reader); return ReadNode(reader, type, name, 0);
    }
    private static byte[] Bytes(BinaryReader r, int count) {
        if (count < 0 || count > 32 * 1024 * 1024) throw new InvalidDataException("NBT 长度无效。");
        var bytes = r.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return bytes;
    }
    private static int Int(BinaryReader r) { var b = Bytes(r, 4); return b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]; }
    private static void Int(BinaryWriter w, int n) { w.Write(new[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n }); }
    // Java NBT strings use DataOutput.writeUTF (modified UTF-8, including surrogate pairs).
    private static byte[] Encode(string value) {
        using var stream = new MemoryStream();
        foreach (var c in value) {
            if (c is >= '\u0001' and <= '\u007f') stream.WriteByte((byte)c);
            else if (c <= '\u07ff') { stream.WriteByte((byte)(0xc0 | c >> 6)); stream.WriteByte((byte)(0x80 | c & 63)); }
            else { stream.WriteByte((byte)(0xe0 | c >> 12)); stream.WriteByte((byte)(0x80 | c >> 6 & 63)); stream.WriteByte((byte)(0x80 | c & 63)); }
        }
        if (stream.Length > 65535) throw new InvalidDataException("NBT 字符串过长。");
        return stream.ToArray();
    }
    private static string Decode(byte[] bytes) {
        var result = new StringBuilder();
        for (int i = 0; i < bytes.Length;) {
            var first = bytes[i++];
            if (first < 0x80) { result.Append((char)first); continue; }
            int Tail() { if (i >= bytes.Length || (bytes[i] & 0xc0) != 0x80) throw new InvalidDataException("NBT 字符串编码无效。"); return bytes[i++] & 63; }
            if ((first & 0xe0) == 0xc0) result.Append((char)((first & 31) << 6 | Tail()));
            else if ((first & 0xf0) == 0xe0) { int second = Tail(); result.Append((char)((first & 15) << 12 | second << 6 | Tail())); }
            else throw new InvalidDataException("NBT 字符串编码无效。");
        }
        return result.ToString();
    }
    private static string ReadString(BinaryReader r) { var a = r.ReadByte(); var b = r.ReadByte(); return Decode(Bytes(r, a << 8 | b)); }
    private static void WriteString(BinaryWriter w, string s) { var bytes = Encode(s); w.Write((byte)(bytes.Length >> 8)); w.Write((byte)bytes.Length); w.Write(bytes); }
    private static Node ReadNode(BinaryReader r, byte type, string name, int depth) {
        if (depth > 64) throw new InvalidDataException("NBT 嵌套过深。");
        var n = new Node { Type = type, Name = name };
        if (type == 9) {
            n.ElementType = r.ReadByte(); var count = Int(r);
            if (count < 0 || count > 100000 || (count > 0 && n.ElementType == 0)) throw new InvalidDataException("NBT 列表长度无效。");
            for (int i = 0; i < count; i++) n.Children.Add(ReadNode(r, n.ElementType, "", depth + 1));
        } else if (type == 10) {
            for (int i = 0; ; i++) { var childType = r.ReadByte(); if (childType == 0) break; if (i > 100000) throw new InvalidDataException(); n.Children.Add(ReadNode(r, childType, ReadString(r), depth + 1)); }
        } else {
            int size;
            if (type == 8) { var a = r.ReadByte(); var b = r.ReadByte(); n.Payload = new[] { a, b }.Concat(Bytes(r, a << 8 | b)).ToArray(); return n; }
            if (type == 7 || type == 11 || type == 12) {
                var count = Int(r); size = checked(count * (type == 7 ? 1 : type == 11 ? 4 : 8));
                using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms); Int(writer, count); writer.Write(Bytes(r, size)); n.Payload = ms.ToArray(); return n;
            }
            size = type switch { 1 => 1, 2 => 2, 3 or 5 => 4, 4 or 6 => 8, _ => throw new InvalidDataException("未知 NBT 标签。") };
            n.Payload = Bytes(r, size);
        }
        return n;
    }
    private static void WriteNode(BinaryWriter w, Node n) {
        if (n.Type == 9) { w.Write(n.ElementType); Int(w, n.Children.Count); foreach (var child in n.Children) WriteNode(w, child); }
        else if (n.Type == 10) { foreach (var child in n.Children) { w.Write(child.Type); WriteString(w, child.Name); WriteNode(w, child); } w.Write((byte)0); }
        else w.Write(n.Payload);
    }
    private static void Save(string path, Node root) {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms); w.Write((byte)10); WriteString(w, root.Name); WriteNode(w, root);
        OpenPreferences.AtomicWrite(path, ms.ToArray());
    }
}
