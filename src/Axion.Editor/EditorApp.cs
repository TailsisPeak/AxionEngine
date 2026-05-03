using System;
using System.IO;
using System.Numerics;
using Axion.Core;
using Axion.Engine;
using Axion.Rendering;

namespace Axion.Editor;

/// <summary>
/// Editor host. Currently runs the game in "play mode" with a free-fly camera fallback if the
/// scene has no main camera. The visual scene-graph / inspector / project browser is roadmapped
/// (will plug in Dear ImGui in v0.2).
/// </summary>
public static class EditorApp
{
    public static void Run(string? scenePath)
    {
        Scene scene;
        if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
        {
            Log.Info($"Loading scene: {scenePath}", "Editor");
            scene = SceneSerializer.Load(scenePath);
        }
        else
        {
            Log.Info("No scene file — building a default scene.", "Editor");
            scene = BuildDefaultScene();
        }

        EnsureCamera(scene);
        EnsureLight(scene);

        Log.Info("Scene tree:", "Editor");
        DumpScene(scene);

        using var app = new AxionApp("Axion Editor", 1440, 900, scene);

        // Persistent prefs + free-fly editor camera + project browser
        var settings = EditorSettings.Load();
        var editorCam = new EditorCamera();
        var projectRoot = FindProjectRoot(scenePath) ?? AppContext.BaseDirectory;
        var browser  = new ProjectBrowser(settings, projectRoot);

        ImGuiController? imgui = null;
        EditorUI?        ui    = null;

        // Build editor UI once GL context exists.
        app.Window.OnLoadCallback += () =>
        {
            imgui = new ImGuiController(app.Window);
            ui    = new EditorUI(app, settings, editorCam, browser);
            // Apply persisted theme
            switch (settings.Theme)
            {
                case "Light":   ImGuiNET.ImGui.StyleColorsLight();   break;
                case "Classic": ImGuiNET.ImGui.StyleColorsClassic(); break;
                default:        ImGuiNET.ImGui.StyleColorsDark();    break;
            }
        };

        // Pump ImGui input + free-fly editor camera each frame.
        app.Window.OnUpdateCallback += dt =>
        {
            imgui?.Update(dt);
            ui?.TickEditorCamera(dt);
        };

        // Draw the editor overlay AFTER the 3D scene.
        app.Window.OnRenderCallback += dt =>
        {
            ui?.Draw();
            imgui?.Render();
        };

        app.Run();
        settings.Save();
    }

    private static string? FindProjectRoot(string? scenePath)
    {
        if (!string.IsNullOrEmpty(scenePath))
        {
            try { var d = Path.GetDirectoryName(Path.GetFullPath(scenePath)); if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d; }
            catch { }
        }
        // Walk up from the executable looking for a "samples" sibling — that's our likely repo root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "samples"))) return dir.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    private static Scene BuildDefaultScene()
    {
        var s = new Scene("Default");
        var cube = s.CreateGameObject("Cube");
        var mr = cube.AddComponent<MeshRendererComponent>();
        mr.SetPrimitive("cube");
        mr.Material.Color = new Vector3(0.55f, 0.78f, 0.95f);
        cube.Transform.LocalPosition = new Vector3(0, 0, 0);
        return s;
    }

    private static void EnsureCamera(Scene s)
    {
        foreach (var c in s.AllOfType<CameraComponent>()) return;
        var camGo = s.CreateGameObject("MainCamera");
        camGo.AddComponent<CameraComponent>();
        camGo.Transform.LocalPosition = new Vector3(3, 2, 5);
        camGo.Transform.LookAt(Vector3.Zero);
        Log.Info("(no camera in scene — added fallback)", "Editor");
    }

    private static void EnsureLight(Scene s)
    {
        foreach (var l in s.AllOfType<LightComponent>()) return;
        var lg = s.CreateGameObject("Sun");
        var lc = lg.AddComponent<LightComponent>();
        lc.Type = LightType.Directional;
        lg.Transform.LookAt(new Vector3(-0.4f, -1, -0.3f));
        Log.Info("(no light in scene — added fallback directional light)", "Editor");
    }

    private static void DumpScene(Scene s)
    {
        foreach (var go in s.Roots) DumpGo(go, 0);
    }

    private static void DumpGo(GameObject go, int depth)
    {
        var indent = new string(' ', depth * 2);
        var comps = string.Join(", ",
            System.Linq.Enumerable.Select(go.Components, c => c.GetType().Name));
        Console.WriteLine($"  {indent}- {go.Name}  [{comps}]");
        foreach (var ch in go.Transform.Children) DumpGo(ch.GameObject, depth + 1);
    }
}
