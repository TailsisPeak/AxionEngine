using System;
using System.IO;
using System.Text.Json;
using Axion.Core;

namespace Axion.Editor;

/// <summary>
/// Persistent editor preferences. Saved to %AppData%/Axion/editor-settings.json
/// (or ~/.config/Axion/editor-settings.json on Linux/macOS).
/// </summary>
public sealed class EditorSettings
{
    public string Theme              { get; set; } = "Dark";        // Dark | Light | Classic
    public string ExternalEditorPath { get; set; } = "";            // empty = use OS default
    public float  CameraMoveSpeed    { get; set; } = 5f;
    public float  CameraFastMultiplier { get; set; } = 3f;
    public float  CameraLookSensitivity { get; set; } = 0.15f;
    public bool   ShowFpsOverlay     { get; set; } = true;
    public bool   ShowGrid           { get; set; } = true;
    public string LastProjectFolder  { get; set; } = "";

    private static string SettingsPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(root)) root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(root, "Axion");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "editor-settings.json");
        }
    }

    public static EditorSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<EditorSettings>(json);
                if (s != null) return s;
            }
        }
        catch (Exception ex) { Log.Warn($"Could not load settings: {ex.Message}", "Editor"); }
        return new EditorSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            Log.Info($"Settings saved → {SettingsPath}", "Editor");
        }
        catch (Exception ex) { Log.Warn($"Could not save settings: {ex.Message}", "Editor"); }
    }
}
