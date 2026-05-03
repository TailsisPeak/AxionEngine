using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Axion.Core;
using Axion.Engine;

namespace Axion.Editor;

/// <summary>
/// Project / file-system browser panel. Shows the contents of the project root, lets the
/// user double-click any source file to open it (in the configured external editor or the
/// OS default), and double-click .json scenes to load them into the editor.
/// </summary>
public sealed class ProjectBrowser
{
    private readonly EditorSettings _settings;
    public string Root { get; private set; }
    public string? Selected { get; private set; }

    public event Action<string>? OnOpenScene;

    private static readonly HashSet<string> SourceExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gel", ".sil", ".blocks", ".cs", ".txt", ".md", ".glsl", ".vert", ".frag", ".hlsl",
    };

    public ProjectBrowser(EditorSettings settings, string initialRoot)
    {
        _settings = settings;
        Root = !string.IsNullOrEmpty(_settings.LastProjectFolder) && Directory.Exists(_settings.LastProjectFolder)
            ? _settings.LastProjectFolder
            : initialRoot;
    }

    public void SetRoot(string path)
    {
        if (!Directory.Exists(path)) return;
        Root = path;
        _settings.LastProjectFolder = path;
        _settings.Save();
    }

    public void Draw()
    {
        if (!ImGui.Begin("Project")) { ImGui.End(); return; }

        // Toolbar: root path + change-folder
        string r = Root;
        ImGui.SetNextItemWidth(-90);
        if (ImGui.InputText("##root", ref r, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            SetRoot(r);
        ImGui.SameLine();
        if (ImGui.Button("Up"))
        {
            var p = Directory.GetParent(Root)?.FullName;
            if (p != null) SetRoot(p);
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) { /* tree re-walks every frame */ }

        ImGui.Separator();
        if (!Directory.Exists(Root)) { ImGui.TextDisabled("Folder not found."); ImGui.End(); return; }

        ImGui.BeginChild("tree");
        try { DrawDirectory(new DirectoryInfo(Root)); }
        catch (Exception ex) { ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), ex.Message); }
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawDirectory(DirectoryInfo dir)
    {
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (dir.FullName == Root) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        bool open = ImGui.TreeNodeEx($"\uF07B {dir.Name}##{dir.FullName}", flags);
        if (!open) return;

        DirectoryInfo[] subs;
        FileInfo[]      files;
        try
        {
            subs  = dir.GetDirectories().Where(d => (d.Attributes & FileAttributes.Hidden) == 0 && d.Name != "bin" && d.Name != "obj").ToArray();
            files = dir.GetFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).ToArray();
        }
        catch { ImGui.TreePop(); return; }

        Array.Sort(subs,  (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Array.Sort(files, (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var s in subs) DrawDirectory(s);
        foreach (var f in files) DrawFile(f);
        ImGui.TreePop();
    }

    private void DrawFile(FileInfo f)
    {
        var icon = f.Extension.ToLowerInvariant() switch
        {
            ".gel"    => "[G]",
            ".sil"    => "[S]",
            ".blocks" => "[B]",
            ".json"   => "[J]",
            ".cs"     => "[C#]",
            ".png" or ".jpg" or ".jpeg" or ".bmp" => "[img]",
            ".wav" or ".ogg" or ".mp3" => "[snd]",
            _ => "    ",
        };
        bool selected = Selected == f.FullName;
        if (ImGui.Selectable($"  {icon} {f.Name}##{f.FullName}", selected, ImGuiSelectableFlags.AllowDoubleClick))
        {
            Selected = f.FullName;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                OpenFile(f.FullName);
        }
        if (ImGui.BeginPopupContextItem($"ctx_{f.FullName}"))
        {
            if (ImGui.MenuItem("Open"))             OpenFile(f.FullName);
            if (ImGui.MenuItem("Open in OS shell")) ShellOpen(f.FullName);
            if (ImGui.MenuItem("Show in folder"))   ShowInFolder(f.FullName);
            if (ImGui.MenuItem("Copy path"))        ImGui.SetClipboardText(f.FullName);
            ImGui.EndPopup();
        }
    }

    public void OpenFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        // Scene file → load it into the editor
        if (ext == ".json" && IsLikelyScene(path))
        {
            try { OnOpenScene?.Invoke(path); Log.Info($"Opened scene {Path.GetFileName(path)}", "Editor"); }
            catch (Exception ex) { Log.Exception(ex, "OpenScene"); }
            return;
        }

        // Source files → external editor (configured) or OS default
        if (SourceExt.Contains(ext))
        {
            if (!string.IsNullOrEmpty(_settings.ExternalEditorPath) && File.Exists(_settings.ExternalEditorPath))
            {
                try { Process.Start(new ProcessStartInfo(_settings.ExternalEditorPath, $"\"{path}\"") { UseShellExecute = true }); }
                catch (Exception ex) { Log.Exception(ex, "OpenInEditor"); }
                return;
            }
        }

        ShellOpen(path);
    }

    private static bool IsLikelyScene(string path)
    {
        try
        {
            using var sr = new StreamReader(path);
            char[] buf = new char[256];
            int n = sr.Read(buf, 0, buf.Length);
            var head = new string(buf, 0, n);
            return head.Contains("\"name\"") || head.Contains("\"roots\"") || head.Contains("\"gameObjects\"");
        }
        catch { return false; }
    }

    private static void ShellOpen(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Exception(ex, "ShellOpen"); }
    }

    private static void ShowInFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (dir != null) Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            }
        }
        catch (Exception ex) { Log.Exception(ex, "ShowInFolder"); }
    }
}
