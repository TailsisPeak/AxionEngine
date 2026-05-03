using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Axion.Core;

namespace Axion.Scripting;

/// <summary>
/// Registers stdlib + live engine bindings (transform, gameObject, scene, time,
/// input, log, random, vec3, find, destroy, instantiate) into a Gel interpreter
/// env. Engine bindings are tied to a specific host <see cref="Behavior"/> so
/// `transform` and `gameObject` resolve to the script's own object.
/// </summary>
public static class EngineGelStdlib
{
    public static void Register(GelEnv g, TextWriter output, Behavior host)
    {
        Func<object?[], object?> Fn(Func<object?[], object?> f) => f;

        // ── Console / output ───────────────────────────────────────────────────
        g.DeclareLocal("print", Fn(a => { output.WriteLine(string.Join(" ", a.Select(GelInterpreter.Stringify))); return null; }));
        g.DeclareLocal("typeof", Fn(a => TypeNameOf(a.Length == 0 ? null : a[0])));
        g.DeclareLocal("quit",   Fn(a => throw new GelQuitSignal(a.Length > 0 ? GelInterpreter.Stringify(a[0]) : null)));

        // ── Math ───────────────────────────────────────────────────────────────
        g.DeclareLocal("sqrt",   Fn(a => Math.Sqrt(GelInterpreter.ToDouble(a[0]))));
        g.DeclareLocal("abs",    Fn(a => Math.Abs(GelInterpreter.ToDouble(a[0]))));
        g.DeclareLocal("floor",  Fn(a => Math.Floor(GelInterpreter.ToDouble(a[0]))));
        g.DeclareLocal("ceil",   Fn(a => Math.Ceiling(GelInterpreter.ToDouble(a[0]))));
        g.DeclareLocal("round",  Fn(a => Math.Round(GelInterpreter.ToDouble(a[0]))));
        g.DeclareLocal("clamp",  Fn(a => Math.Clamp(GelInterpreter.ToDouble(a[0]), GelInterpreter.ToDouble(a[1]), GelInterpreter.ToDouble(a[2]))));
        g.DeclareLocal("sin",    Fn(a => Math.Sin(GelInterpreter.ToDouble(a[0]) * Math.PI / 180)));
        g.DeclareLocal("cos",    Fn(a => Math.Cos(GelInterpreter.ToDouble(a[0]) * Math.PI / 180)));
        g.DeclareLocal("tan",    Fn(a => Math.Tan(GelInterpreter.ToDouble(a[0]) * Math.PI / 180)));
        g.DeclareLocal("pow",    Fn(a => Math.Pow(GelInterpreter.ToDouble(a[0]), GelInterpreter.ToDouble(a[1]))));
        g.DeclareLocal("min",    Fn(a => Math.Min(GelInterpreter.ToDouble(a[0]), GelInterpreter.ToDouble(a[1]))));
        g.DeclareLocal("max",    Fn(a => Math.Max(GelInterpreter.ToDouble(a[0]), GelInterpreter.ToDouble(a[1]))));
        g.DeclareLocal("pi",     Math.PI);
        g.DeclareLocal("euler",  Math.E);

        // ── Random ─────────────────────────────────────────────────────────────
        var rng = new Random();
        g.DeclareLocal("random.value", Fn(_ => rng.NextDouble()));
        g.DeclareLocal("random.range", Fn(a => rng.NextDouble() * (GelInterpreter.ToDouble(a[1]) - GelInterpreter.ToDouble(a[0])) + GelInterpreter.ToDouble(a[0])));
        g.DeclareLocal("random.int",   Fn(a => (double)rng.Next((int)GelInterpreter.ToDouble(a[0]), (int)GelInterpreter.ToDouble(a[1]))));
        g.DeclareLocal("random.sign",  Fn(_ => rng.Next(2) == 0 ? -1d : 1d));

        // ── Vec constructors (return real System.Numerics so engine APIs accept directly) ──
        g.DeclareLocal("vec3", Fn(a => (object)new Vector3(
            a.Length > 0 ? (float)GelInterpreter.ToDouble(a[0]) : 0,
            a.Length > 1 ? (float)GelInterpreter.ToDouble(a[1]) : 0,
            a.Length > 2 ? (float)GelInterpreter.ToDouble(a[2]) : 0)));
        g.DeclareLocal("vec2", Fn(a => (object)new Vector2(
            a.Length > 0 ? (float)GelInterpreter.ToDouble(a[0]) : 0,
            a.Length > 1 ? (float)GelInterpreter.ToDouble(a[1]) : 0)));

        // ── Live engine bindings ───────────────────────────────────────────────
        // Stored as live .NET objects — the interpreter's reflection bridge reads
        // them so `transform.Position`, `transform.Rotate(...)`, `gameObject.Name`,
        // `scene.Find("X")` all work directly on the real types. Both lowercase
        // (Gel-idiomatic) and PascalCase (C#-idiomatic, what samples already use)
        // names are registered so existing scripts authored against the
        // CSharpEmitter surface keep running unchanged.
        g.DeclareLocal("transform",  host.Transform);
        g.DeclareLocal("Transform",  host.Transform);
        g.DeclareLocal("gameObject", host.GameObject);
        g.DeclareLocal("GameObject", host.GameObject);
        g.DeclareLocal("self",       host);

        // Vec.V(x,y,z) / Vec.V(x,y) constructors mirror Axion.Core.Vec.
        g.DeclareLocal("Vec.V", Fn(a => a.Length switch
        {
            >= 3 => (object)new Vector3((float)GelInterpreter.ToDouble(a[0]), (float)GelInterpreter.ToDouble(a[1]), (float)GelInterpreter.ToDouble(a[2])),
            2    => (object)new Vector2((float)GelInterpreter.ToDouble(a[0]), (float)GelInterpreter.ToDouble(a[1])),
            _    => (object)Vector3.Zero,
        }));
        g.DeclareLocal("Vec.Forward", Vector3.UnitZ * -1);
        g.DeclareLocal("Vec.Back",    Vector3.UnitZ);
        g.DeclareLocal("Vec.Up",      Vector3.UnitY);
        g.DeclareLocal("Vec.Down",    Vector3.UnitY * -1);
        g.DeclareLocal("Vec.Right",   Vector3.UnitX);
        g.DeclareLocal("Vec.Left",    Vector3.UnitX * -1);
        g.DeclareLocal("Vec.Zero",    Vector3.Zero);
        g.DeclareLocal("Vec.One",     Vector3.One);

        // scene — late-bound via callables so they track scene swaps and we
        // never have to take a hard dependency on Axion.Engine from Axion.Scripting.
        // The scene is reached through the host's own GameObject, which the
        // engine wires up during AddComponent / scene load.
        Func<Scene?> currentScene = () => host.GameObject?.Scene;

        g.DeclareLocal("scene.find",   Fn(a => currentScene()?.Find(GelInterpreter.Stringify(a[0]))));
        g.DeclareLocal("scene.create", Fn(a =>
        {
            var s = currentScene(); if (s == null) return null;
            var name = a.Length > 0 ? GelInterpreter.Stringify(a[0]) : "GameObject";
            var go = new GameObject(name);
            s.Add(go);
            return go;
        }));
        g.DeclareLocal("scene.remove", Fn(a =>
        {
            var s = currentScene();
            if (s != null && a.Length > 0 && a[0] is GameObject g0) s.Remove(g0);
            return null;
        }));
        g.DeclareLocal("find",        Fn(a => currentScene()?.Find(GelInterpreter.Stringify(a[0]))));
        g.DeclareLocal("destroy",     Fn(a => { if (a.Length > 0 && a[0] is GameObject go) currentScene()?.Remove(go); return null; }));
        g.DeclareLocal("instantiate", Fn(a =>
        {
            var s = currentScene(); if (s == null) return null;
            var name = a.Length > 0 ? GelInterpreter.Stringify(a[0]) : "GameObject";
            var go = new GameObject(name);
            s.Add(go);
            return go;
        }));

        // ── time.* / Time.* (callable getters so values are live each frame) ──
        Func<object?[], object?> tDelta   = _ => (double)Time.DeltaTime;
        Func<object?[], object?> tElapsed = _ => (double)Time.ElapsedTime;
        Func<object?[], object?> tFrames  = _ => (double)Time.FrameCount;
        Func<object?[], object?> tScale   = _ => (double)Time.TimeScale;
        Func<object?[], object?> tFixed   = _ => (double)Time.FixedDelta;
        g.DeclareLocal("time.delta",        tDelta);
        g.DeclareLocal("time.deltaTime",    tDelta);
        g.DeclareLocal("time.elapsed",      tElapsed);
        g.DeclareLocal("time.elapsedTime",  tElapsed);
        g.DeclareLocal("time.frame",        tFrames);
        g.DeclareLocal("time.frames",       tFrames);
        g.DeclareLocal("time.scale",        tScale);
        g.DeclareLocal("time.fixedDelta",   tFixed);
        g.DeclareLocal("Time.DeltaTime",    tDelta);
        g.DeclareLocal("Time.ElapsedTime",  tElapsed);
        g.DeclareLocal("Time.FrameCount",   tFrames);
        g.DeclareLocal("Time.TimeScale",    tScale);
        g.DeclareLocal("Time.FixedDelta",   tFixed);

        // ── log.* / Log.* (route to engine logger) ─────────────────────────────
        var tag = host.GameObject?.Name ?? "GelScript";
        Func<object?[], object?> lInfo  = a => { Log.Info (string.Join(" ", a.Select(GelInterpreter.Stringify)), tag); return null; };
        Func<object?[], object?> lWarn  = a => { Log.Warn (string.Join(" ", a.Select(GelInterpreter.Stringify)), tag); return null; };
        Func<object?[], object?> lError = a => { Log.Error(string.Join(" ", a.Select(GelInterpreter.Stringify)), tag); return null; };
        g.DeclareLocal("log.info",  lInfo);
        g.DeclareLocal("log.warn",  lWarn);
        g.DeclareLocal("log.error", lError);
        g.DeclareLocal("Log.Info",  lInfo);
        g.DeclareLocal("Log.Warn",  lWarn);
        g.DeclareLocal("Log.Error", lError);

        // ── input.* / Input.* (live polling) ───────────────────────────────────
        Func<object?[], object?> iKey       = a => Input.GetKey(ParseKey(a));
        Func<object?[], object?> iKeyDown   = a => Input.GetKeyDown(ParseKey(a));
        Func<object?[], object?> iKeyUp     = a => Input.GetKeyUp(ParseKey(a));
        Func<object?[], object?> iAxis      = a => (double)Input.GetAxis(GelInterpreter.Stringify(a[0]));
        g.DeclareLocal("input.key",         iKey);
        g.DeclareLocal("input.keyDown",     iKeyDown);
        g.DeclareLocal("input.keyPressed",  iKeyDown);
        g.DeclareLocal("input.keyUp",       iKeyUp);
        g.DeclareLocal("input.keyReleased", iKeyUp);
        g.DeclareLocal("input.axis",        iAxis);
        g.DeclareLocal("input.pointer.x",   Fn(_ => (double)Input.MousePosition.X));
        g.DeclareLocal("input.pointer.y",   Fn(_ => (double)Input.MousePosition.Y));
        g.DeclareLocal("input.pointer.dx",  Fn(_ => (double)Input.MouseDelta.X));
        g.DeclareLocal("input.pointer.dy",  Fn(_ => (double)Input.MouseDelta.Y));
        g.DeclareLocal("input.scroll",      Fn(_ => (double)Input.ScrollDelta));
        g.DeclareLocal("Input.GetKey",      iKey);
        g.DeclareLocal("Input.GetKeyDown",  iKeyDown);
        g.DeclareLocal("Input.GetKeyUp",    iKeyUp);
        g.DeclareLocal("Input.GetAxis",     iAxis);
    }

    private static Axion.Core.Key ParseKey(object?[] a)
    {
        if (a.Length == 0) return default;
        var s = GelInterpreter.Stringify(a[0]);
        if (Enum.TryParse<Axion.Core.Key>(s, ignoreCase: true, out var k)) return k;
        return default;
    }

    private static string TypeNameOf(object? v) => v switch
    {
        null => "none",
        bool => "bool",
        double d => d == Math.Floor(d) ? "int" : "decimal",
        string => "txt",
        Vector3 => "vec3",
        Vector2 => "vec2",
        List<object?> => "array",
        Dictionary<string, object?> => "group",
        GelFuncValue => "func",
        _ => v.GetType().Name
    };
}
