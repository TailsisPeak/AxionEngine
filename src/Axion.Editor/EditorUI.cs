using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Axion.Core;
using Axion.Engine;
using Axion.Rendering;

namespace Axion.Editor;

/// <summary>
/// The Axion Editor's panel system. Renders the menu bar, toolbar, hierarchy, inspector,
/// and console as Dear ImGui windows on top of the 3D viewport.
/// </summary>
public enum ViewMode { Scene, Game }

public sealed class EditorUI
{
    private readonly AxionApp        _app;
    private readonly EditorSettings  _settings;
    private readonly EditorCamera    _editorCam;
    private readonly ProjectBrowser  _browser;
    private GameObject? _selected;
    private string _saveAsPath = "scene.json";
    private string _statusMessage = "";
    private float  _statusTimer;
    private ViewMode _mode = ViewMode.Scene;
    private bool   _showSettings;
    private bool   _showAbout;

    private readonly List<LogEntry> _logBuffer = new();
    private readonly object _logLock = new();
    private bool _autoScrollLog = true;

    public EditorUI(AxionApp app, EditorSettings settings, EditorCamera editorCam, ProjectBrowser browser)
    {
        _app       = app;
        _settings  = settings;
        _editorCam = editorCam;
        _browser   = browser;
        _browser.OnOpenScene += path =>
        {
            try
            {
                _app.LoadSceneFile(path);
                _selected = null;
                Status($"Loaded {Path.GetFileName(path)}");
            }
            catch (Exception ex) { Log.Exception(ex, "LoadScene"); }
        };

        Log.OnLog += entry => { lock (_logLock) { _logBuffer.Add(entry); if (_logBuffer.Count > 1000) _logBuffer.RemoveRange(0, 200); } };
        // seed with history
        foreach (var e in Log.History) _logBuffer.Add(e);
        ApplyMode();
        Log.Info($"Axion Editor v{Program.Version} ready.", "Editor");
    }

    public bool IsPlaying => _mode == ViewMode.Game;
    public ViewMode Mode => _mode;

    public void Draw()
    {
        DrawMenuBar();
        DrawToolbar();
        DrawHierarchy();
        DrawInspector();
        DrawConsole();
        DrawScenePanel();
        _browser.Draw();
        if (_showSettings) DrawSettings();
        if (_showAbout)    DrawAbout();
        DrawStatusBar();

        if (_statusTimer > 0) _statusTimer -= Time.DeltaTime;
    }

    /// <summary>Called by EditorApp every update tick BEFORE ImGui consumes input.</summary>
    public void TickEditorCamera(float dt)
    {
        if (_mode != ViewMode.Scene) return;
        var io = ImGui.GetIO();
        bool capturedByUi = io.WantCaptureMouse || io.WantCaptureKeyboard;
        _editorCam.MoveSpeed       = _settings.CameraMoveSpeed;
        _editorCam.FastMultiplier  = _settings.CameraFastMultiplier;
        _editorCam.LookSensitivity = _settings.CameraLookSensitivity;
        _editorCam.Update(dt, capturedByUi);
    }

    private void Status(string msg) { _statusMessage = msg; _statusTimer = 3f; }

    /// <summary>Sync AxionApp's camera-override + simulation flags to current view mode.</summary>
    private void ApplyMode()
    {
        if (_mode == ViewMode.Scene)
        {
            _app.Simulating = false;
            _app.CameraOverride = () =>
            {
                float aspect = _app.Window.ClientSize.X / (float)Math.Max(1, _app.Window.ClientSize.Y);
                return _editorCam.GetMatrices(aspect);
            };
        }
        else
        {
            _app.Simulating = true;
            _app.CameraOverride = null;
        }
    }

    // ── Menu Bar ────────────────────────────────────────────────────────────
    private void DrawMenuBar()
    {
        if (!ImGui.BeginMainMenuBar()) return;

        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New Scene"))   { _app.LoadScene(new Scene("Untitled")); _selected = null; Status("New scene"); }
            if (ImGui.MenuItem("Save Scene"))  { try { SceneSerializer.Save(_app.Scene, _saveAsPath); Status($"Saved {_saveAsPath}"); } catch (Exception ex) { Log.Exception(ex, "SaveScene"); } }
            ImGui.Separator();
            ImGui.SetNextItemWidth(220);
            ImGui.InputText("##saveas", ref _saveAsPath, 260);
            ImGui.Separator();
            if (ImGui.MenuItem("Quit")) _app.Window.Close();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("GameObject"))
        {
            if (ImGui.MenuItem("Create Empty"))     CreateEmpty("GameObject");
            ImGui.Separator();
            if (ImGui.MenuItem("3D Object/Cube"))   CreatePrimitive("Cube",   "cube");
            if (ImGui.MenuItem("3D Object/Sphere")) CreatePrimitive("Sphere", "sphere");
            if (ImGui.MenuItem("3D Object/Plane"))  CreatePrimitive("Plane",  "quad");
            ImGui.Separator();
            if (ImGui.MenuItem("Light/Directional")) CreateLight(LightType.Directional, "Directional Light");
            if (ImGui.MenuItem("Light/Point"))       CreateLight(LightType.Point,       "Point Light");
            if (ImGui.MenuItem("Camera"))            CreateCamera();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Component") && _selected != null)
        {
            if (ImGui.MenuItem("Mesh Renderer"))      _selected.AddComponent<MeshRendererComponent>();
            if (ImGui.MenuItem("Light"))              _selected.AddComponent<LightComponent>();
            if (ImGui.MenuItem("Camera"))             _selected.AddComponent<CameraComponent>();
            if (ImGui.MenuItem("Rigidbody"))          _selected.AddComponent<RigidbodyComponent>();
            if (ImGui.MenuItem("Audio Source"))       _selected.AddComponent<AudioSourceComponent>();
            if (ImGui.MenuItem("Script (Gel/Silica)"))_selected.AddComponent<ScriptComponent>();
            if (ImGui.MenuItem("Animator"))           _selected.AddComponent<AnimatorComponent>();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Settings…"))                _showSettings = true;
            if (ImGui.MenuItem("Reset Editor Camera"))      _editorCam.FrameOrigin();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Window"))
        {
            ImGui.MenuItem("Hierarchy",  null, true,  false);
            ImGui.MenuItem("Inspector",  null, true,  false);
            ImGui.MenuItem("Console",    null, true,  false);
            ImGui.MenuItem("Scene",      null, true,  false);
            ImGui.MenuItem("Project",    null, true,  false);
            ImGui.Separator();
            if (ImGui.MenuItem("Settings…")) _showSettings = true;
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("About Axion…")) _showAbout = true;
            if (ImGui.MenuItem("Open samples folder"))
            {
                var samples = FindSamplesDir();
                if (samples != null) try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(samples) { UseShellExecute = true }); } catch { }
            }
            if (ImGui.MenuItem("Open settings file in OS"))
            {
                try
                {
                    var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var dir = Path.Combine(root, "Axion");
                    if (Directory.Exists(dir))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
                } catch { }
            }
            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    private static string? FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "samples");
            if (Directory.Exists(p)) return p;
        }
        return null;
    }

    // ── Toolbar ─────────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X, vp.WorkPos.Y));
        ImGui.SetNextWindowSize(new Vector2(vp.WorkSize.X, 36));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                  | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings;
        if (ImGui.Begin("##toolbar", flags))
        {
            // View-mode toggle: Scene (free-fly editor cam) vs Game (uses scene's MainCamera)
            bool sceneSel = _mode == ViewMode.Scene;
            bool gameSel  = _mode == ViewMode.Game;
            if (ImGui.RadioButton("Scene", sceneSel) && !sceneSel) { _mode = ViewMode.Scene; ApplyMode(); Status("Scene view"); }
            ImGui.SameLine();
            if (ImGui.RadioButton("Game",  gameSel)  && !gameSel)  { _mode = ViewMode.Game;  ApplyMode(); Status("Game view (playing)"); }
            ImGui.SameLine(); ImGui.Dummy(new Vector2(16, 0)); ImGui.SameLine();

            if (ImGui.Button(_mode == ViewMode.Game ? "▶ Playing" : "▶ Play"))   { _mode = ViewMode.Game;  ApplyMode(); }
            ImGui.SameLine();
            if (ImGui.Button("⏸ Pause"))   { _app.Simulating = false; Status("Paused"); }
            ImGui.SameLine();
            if (ImGui.Button("■ Stop"))    { _mode = ViewMode.Scene; ApplyMode(); Status("Stopped"); }

            ImGui.SameLine(); ImGui.Dummy(new Vector2(20, 0)); ImGui.SameLine();
            ImGui.Text($"Scene: {_app.Scene.Name}");
            if (_settings.ShowFpsOverlay)
            {
                ImGui.SameLine(); ImGui.Dummy(new Vector2(20, 0)); ImGui.SameLine();
                ImGui.Text($"FPS: {1f / MathF.Max(0.0001f, Time.DeltaTime):F0}");
            }
            ImGui.SameLine(); ImGui.Dummy(new Vector2(20, 0)); ImGui.SameLine();
            if (ImGui.SmallButton("Settings")) _showSettings = true;
        }
        ImGui.End();
    }

    // ── Hierarchy ───────────────────────────────────────────────────────────
    private void DrawHierarchy()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X, vp.WorkPos.Y + 36), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280, vp.WorkSize.Y * 0.55f), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Hierarchy"))
        {
            if (ImGui.Button("+ Create"))   ImGui.OpenPopup("create_go");
            if (ImGui.BeginPopup("create_go"))
            {
                if (ImGui.MenuItem("Empty"))  { CreateEmpty("GameObject"); ImGui.CloseCurrentPopup(); }
                if (ImGui.MenuItem("Cube"))   { CreatePrimitive("Cube",   "cube");   ImGui.CloseCurrentPopup(); }
                if (ImGui.MenuItem("Sphere")) { CreatePrimitive("Sphere", "sphere"); ImGui.CloseCurrentPopup(); }
                if (ImGui.MenuItem("Plane"))  { CreatePrimitive("Plane",  "quad");   ImGui.CloseCurrentPopup(); }
                ImGui.Separator();
                if (ImGui.MenuItem("Directional Light")) { CreateLight(LightType.Directional, "Directional Light"); ImGui.CloseCurrentPopup(); }
                if (ImGui.MenuItem("Camera")) { CreateCamera(); ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete") && _selected != null)
            {
                _app.Scene.Remove(_selected);
                _selected = null;
            }

            ImGui.Separator();
            foreach (var root in _app.Scene.Roots.ToList())
                DrawHierarchyNode(root);
        }
        ImGui.End();
    }

    private void DrawHierarchyNode(GameObject go)
    {
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (go.Transform.ChildCount == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (_selected == go)              flags |= ImGuiTreeNodeFlags.Selected;

        bool open = ImGui.TreeNodeEx($"{go.Name}##{go.Id}", flags);
        if (ImGui.IsItemClicked()) _selected = go;
        if (open)
        {
            foreach (var ch in go.Transform.Children.ToList()) DrawHierarchyNode(ch.GameObject);
            ImGui.TreePop();
        }
    }

    // ── Inspector ───────────────────────────────────────────────────────────
    private void DrawInspector()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - 340, vp.WorkPos.Y + 36), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(340, vp.WorkSize.Y - 36 - 200), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Inspector"))
        {
            if (_selected == null) { ImGui.TextDisabled("No GameObject selected."); ImGui.End(); return; }

            // Header: Name + Active
            string n = _selected.Name; bool active = _selected.Active;
            ImGui.Checkbox("##act", ref active); _selected.Active = active;
            ImGui.SameLine();
            if (ImGui.InputText("Name", ref n, 64)) _selected.Name = n;
            ImGui.Separator();

            foreach (var c in _selected.Components.ToList())
            {
                ImGui.PushID(c.GetHashCode());
                if (c is Transform tr)
                {
                    if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        var p = tr.LocalPosition; if (DrawV3("Position", ref p)) tr.LocalPosition = p;
                        var r = tr.LocalEulerAngles; if (DrawV3("Rotation", ref r)) tr.LocalEulerAngles = r;
                        var s = tr.LocalScale;    if (DrawV3("Scale",    ref s)) tr.LocalScale    = s;
                    }
                }
                else
                {
                    bool open = ImGui.CollapsingHeader(c.GetType().Name, ImGuiTreeNodeFlags.DefaultOpen);
                    if (ImGui.BeginPopupContextItem("comp_ctx"))
                    {
                        if (ImGui.MenuItem("Remove component")) _selected.RemoveComponent(c);
                        ImGui.EndPopup();
                    }
                    if (open) DrawComponentReflected(c);
                }
                ImGui.PopID();
            }

            ImGui.Separator();
            if (ImGui.Button("Add Component", new Vector2(-1, 0))) ImGui.OpenPopup("add_comp");
            if (ImGui.BeginPopup("add_comp"))
            {
                if (ImGui.MenuItem("Mesh Renderer")) _selected.AddComponent<MeshRendererComponent>();
                if (ImGui.MenuItem("Light"))         _selected.AddComponent<LightComponent>();
                if (ImGui.MenuItem("Camera"))        _selected.AddComponent<CameraComponent>();
                if (ImGui.MenuItem("Rigidbody"))     _selected.AddComponent<RigidbodyComponent>();
                if (ImGui.MenuItem("Audio Source"))  _selected.AddComponent<AudioSourceComponent>();
                if (ImGui.MenuItem("Script"))        _selected.AddComponent<ScriptComponent>();
                if (ImGui.MenuItem("Animator"))      _selected.AddComponent<AnimatorComponent>();
                ImGui.EndPopup();
            }
        }
        ImGui.End();
    }

    private static bool DrawV3(string label, ref Vector3 v)
    {
        return ImGui.DragFloat3(label, ref v, 0.05f);
    }

    private void DrawComponentReflected(Component c)
    {
        var t = c.GetType();
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            if (p.Name is "GameObject" or "Transform" or "Scene" or "Name" or "ActiveInHierarchy") continue;

            object? val;
            try { val = p.GetValue(c); } catch { continue; }
            DrawProperty(p, c, val);
        }
    }

    private void DrawProperty(PropertyInfo p, object owner, object? val)
    {
        var name = p.Name;
        if (val is float f && p.CanWrite)        { if (ImGui.DragFloat(name, ref f, 0.05f)) p.SetValue(owner, f); }
        else if (val is int i && p.CanWrite)     { if (ImGui.DragInt(name, ref i)) p.SetValue(owner, i); }
        else if (val is bool b && p.CanWrite)    { if (ImGui.Checkbox(name, ref b)) p.SetValue(owner, b); }
        else if (val is string s && p.CanWrite)  { if (ImGui.InputText(name, ref s, 256)) p.SetValue(owner, s); }
        else if (val is Vector3 v3 && p.CanWrite){ if (ImGui.DragFloat3(name, ref v3, 0.05f)) p.SetValue(owner, v3); }
        else if (val is Vector2 v2 && p.CanWrite){ if (ImGui.DragFloat2(name, ref v2, 0.05f)) p.SetValue(owner, v2); }
        else if (val is Enum e && p.CanWrite)
        {
            var names = Enum.GetNames(p.PropertyType);
            int idx = Array.IndexOf(names, e.ToString());
            if (ImGui.Combo(name, ref idx, names, names.Length)) p.SetValue(owner, Enum.Parse(p.PropertyType, names[idx]));
        }
        else { ImGui.LabelText(name, val?.ToString() ?? "null"); }
    }

    // ── Console ─────────────────────────────────────────────────────────────
    private void DrawConsole()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + 280, vp.WorkPos.Y + vp.WorkSize.Y - 200), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(vp.WorkSize.X - 280 - 340, 200), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Console"))
        {
            if (ImGui.Button("Clear")) { lock (_logLock) _logBuffer.Clear(); }
            ImGui.SameLine();
            ImGui.Checkbox("Auto-scroll", ref _autoScrollLog);
            ImGui.Separator();
            ImGui.BeginChild("scroll", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            lock (_logLock)
            {
                foreach (var entry in _logBuffer)
                {
                    var col = entry.Level switch
                    {
                        LogLevel.Error => new Vector4(1f, 0.4f, 0.4f, 1),
                        LogLevel.Warn  => new Vector4(1f, 0.85f, 0.4f, 1),
                        LogLevel.Trace => new Vector4(0.6f, 0.6f, 0.6f, 1),
                        _              => new Vector4(0.85f, 0.85f, 0.9f, 1),
                    };
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    var src = entry.Source != null ? $"{entry.Source}: " : "";
                    ImGui.TextWrapped($"[{entry.Level}] {src}{entry.Message}");
                    ImGui.PopStyleColor();
                }
            }
            if (_autoScrollLog && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1) ImGui.SetScrollHereY(1f);
            ImGui.EndChild();
        }
        ImGui.End();
    }

    // ── Scene panel (info + editor cam state) ───────────────────────────────
    private void DrawScenePanel()
    {
        ImGui.SetNextWindowSize(new Vector2(260, 230), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Scene"))
        {
            ImGui.TextDisabled($"View: {_mode}");
            if (_mode == ViewMode.Scene)
                ImGui.TextWrapped("Hold RMB to look. WASD = move, QE = down/up, Shift = fast, wheel = FOV.");
            ImGui.Separator();
            ImGui.Text($"Roots: {_app.Scene.Roots.Count}");
            int total = 0; foreach (var _ in _app.Scene.EnumerateAll()) total++;
            ImGui.Text($"GameObjects: {total}");
            ImGui.Text($"Lights:  {_app.Scene.AllOfType<LightComponent>().Count()}");
            ImGui.Text($"Meshes:  {_app.Scene.AllOfType<MeshRendererComponent>().Count()}");
            ImGui.Text($"Bodies:  {_app.Scene.AllOfType<RigidbodyComponent>().Count()}");
            ImGui.Separator();
            var p = _editorCam.Position;
            ImGui.Text($"Cam: {p.X:0.00}, {p.Y:0.00}, {p.Z:0.00}");
            ImGui.Text($"Yaw {_editorCam.Yaw * Mathf.Rad2Deg:0}°  Pitch {_editorCam.Pitch * Mathf.Rad2Deg:0}°  FOV {_editorCam.Fov:0}°");
            if (ImGui.Button("Reset Cam")) _editorCam.FrameOrigin();
            ImGui.SameLine();
            if (ImGui.Button("Frame Selected") && _selected != null)
            {
                _editorCam.Position = _selected.Transform.Position + new Vector3(3, 2, 4);
            }
        }
        ImGui.End();
    }

    // ── Settings window ─────────────────────────────────────────────────────
    private void DrawSettings()
    {
        ImGui.SetNextWindowSize(new Vector2(440, 360), ImGuiCond.FirstUseEver);
        bool open = _showSettings;
        if (ImGui.Begin("Settings", ref open))
        {
            ImGui.TextDisabled("Preferences are saved to %AppData%/Axion/editor-settings.json");
            ImGui.Separator();

            // Theme
            string[] themes = { "Dark", "Light", "Classic" };
            int themeIdx = Math.Max(0, Array.IndexOf(themes, _settings.Theme));
            if (ImGui.Combo("Theme", ref themeIdx, themes, themes.Length))
            {
                _settings.Theme = themes[themeIdx];
                ApplyTheme(_settings.Theme);
            }

            // External editor
            string ext = _settings.ExternalEditorPath;
            if (ImGui.InputText("External code editor", ref ext, 512)) _settings.ExternalEditorPath = ext;
            ImGui.TextDisabled("Path to e.g. notepad++.exe, code.exe, or Silica Gel IDE. Leave empty to use OS default.");

            ImGui.Separator();
            ImGui.Text("Editor camera");
            float mv = _settings.CameraMoveSpeed;
            if (ImGui.SliderFloat("Move speed",     ref mv, 0.5f, 30f)) _settings.CameraMoveSpeed = mv;
            float fm = _settings.CameraFastMultiplier;
            if (ImGui.SliderFloat("Fast multiplier", ref fm, 1f, 10f)) _settings.CameraFastMultiplier = fm;
            float ls = _settings.CameraLookSensitivity;
            if (ImGui.SliderFloat("Look sensitivity", ref ls, 0.02f, 1f)) _settings.CameraLookSensitivity = ls;

            ImGui.Separator();
            bool fps = _settings.ShowFpsOverlay; if (ImGui.Checkbox("Show FPS in toolbar", ref fps)) _settings.ShowFpsOverlay = fps;
            bool grid = _settings.ShowGrid;      if (ImGui.Checkbox("Show grid (placeholder)", ref grid)) _settings.ShowGrid = grid;

            ImGui.Separator();
            if (ImGui.Button("Save"))   { _settings.Save(); Status("Settings saved"); }
            ImGui.SameLine();
            if (ImGui.Button("Close"))  open = false;
        }
        ImGui.End();
        _showSettings = open;
    }

    private void DrawAbout()
    {
        ImGui.SetNextWindowSize(new Vector2(380, 200), ImGuiCond.Always);
        bool open = _showAbout;
        if (ImGui.Begin("About Axion", ref open, ImGuiWindowFlags.NoResize))
        {
            ImGui.Text($"Axion Engine — v{Program.Version}");
            ImGui.Separator();
            ImGui.TextWrapped("A C# 3D engine with three interchangeable scripting languages: Gel, Silica, and Blocks.");
            ImGui.Spacing();
            ImGui.TextDisabled(".NET 8  •  OpenTK 4  •  BepuPhysics 2  •  Dear ImGui");
            ImGui.Spacing();
            if (ImGui.Button("Close")) open = false;
        }
        ImGui.End();
        _showAbout = open;
    }

    private static void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case "Light":   ImGui.StyleColorsLight();   break;
            case "Classic": ImGui.StyleColorsClassic(); break;
            default:        ImGui.StyleColorsDark();    break;
        }
    }

    private void DrawStatusBar()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X, vp.WorkPos.Y + vp.WorkSize.Y - 22));
        ImGui.SetNextWindowSize(new Vector2(vp.WorkSize.X, 22));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                  | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings;
        if (ImGui.Begin("##status", flags))
        {
            ImGui.Text(_statusTimer > 0 ? _statusMessage : $"Axion Editor v{Program.Version}  |  {_mode}{(_app.Simulating ? "" : " (paused)")}");
        }
        ImGui.End();
    }

    // ── Object creation helpers ─────────────────────────────────────────────
    private void CreateEmpty(string name)
    {
        var go = _app.Scene.CreateGameObject(name);
        _selected = go; Status($"Created {name}");
    }

    private void CreatePrimitive(string name, string prim)
    {
        var go = _app.Scene.CreateGameObject(name);
        var mr = go.AddComponent<MeshRendererComponent>();
        mr.SetPrimitive(prim);
        _selected = go; Status($"Created {name}");
    }

    private void CreateLight(LightType type, string name)
    {
        var go = _app.Scene.CreateGameObject(name);
        var lc = go.AddComponent<LightComponent>();
        lc.Type = type;
        _selected = go; Status($"Created {name}");
    }

    private void CreateCamera()
    {
        var go = _app.Scene.CreateGameObject("Camera");
        go.AddComponent<CameraComponent>();
        go.Transform.LocalPosition = new Vector3(0, 1, 5);
        go.Transform.LookAt(Vector3.Zero);
        _selected = go; Status("Created Camera");
    }
}
