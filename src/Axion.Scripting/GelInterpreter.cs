using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace Axion.Scripting;

// ───────────────────────────────────────────────────────────────────────────────
// Tree-walking interpreter for Gel and Silica AST. Runs scripts directly — no
// C# transpile, no Roslyn. Errors stay in the source language with original
// line numbers from the AST.
//
// Differences from the IDE port: this version has a *reflection fallback* in
// EvalMember / AssignTo / EvalCall so live .NET objects (Transform,
// GameObject, Scene, …) can be exposed straight to Gel without dictionary
// wrappers, and method calls like `transform.Translate(vec3(0,1,0))` resolve
// to real members.
// ───────────────────────────────────────────────────────────────────────────────

public sealed class GelRuntimeException : Exception
{
    public int Line { get; }
    public GelRuntimeException(string msg, int line) : base(msg) { Line = line; }
}

public sealed class GelEnv
{
    public readonly GelEnv? Parent;
    public readonly Dictionary<string, object?> Vars = new();
    public GelEnv(GelEnv? parent = null) { Parent = parent; }

    public object? Get(string name)
    {
        for (var e = this; e != null; e = e.Parent)
            if (e.Vars.TryGetValue(name, out var v)) return v;
        throw new GelRuntimeException($"undefined: {name}", 0);
    }
    public bool TryGet(string name, out object? val)
    {
        for (var e = this; e != null; e = e.Parent)
            if (e.Vars.TryGetValue(name, out val)) return true;
        val = null; return false;
    }
    public void DeclareLocal(string name, object? value) => Vars[name] = value;
    public bool AssignExisting(string name, object? value)
    {
        for (var e = this; e != null; e = e.Parent)
            if (e.Vars.ContainsKey(name)) { e.Vars[name] = value; return true; }
        return false;
    }
}

public sealed class GelFuncValue
{
    public List<string> Params = new();
    public List<Stmt> Body = new();
    public GelEnv Closure = null!;
    public string Name = "";
}

internal sealed class GelReturnSignal : Exception { public object? Value; public GelReturnSignal(object? v) { Value = v; } }
internal sealed class GelBreakSignal : Exception { }
internal sealed class GelContinueSignal : Exception { }
public  sealed class GelQuitSignal : Exception { public string? Reason; public GelQuitSignal(string? r) { Reason = r; } }

public sealed class GelInterpreter
{
    private readonly TextWriter _out;
    public  readonly GelEnv Global = new();

    public GelInterpreter(TextWriter output) { _out = output; }

    /// <summary>Hoist function declarations + run top-level statements.</summary>
    public void Run(ProgramNode prog)
    {
        foreach (var s in prog.Body)
            if (s is FuncDecl fd)
                Global.DeclareLocal(fd.Name, new GelFuncValue { Name = fd.Name, Params = fd.Params, Body = fd.Body, Closure = Global });

        try
        {
            foreach (var s in prog.Body)
                if (s is not FuncDecl) Exec(s, Global);
        }
        catch (GelQuitSignal q)
        {
            if (!string.IsNullOrEmpty(q.Reason)) _out.WriteLine($"[quit] {q.Reason}");
        }
    }

    /// <summary>Look up a function by name in the global env. Returns null if missing.</summary>
    public GelFuncValue? FindFunc(params string[] names)
    {
        foreach (var n in names)
            if (Global.TryGet(n, out var v) && v is GelFuncValue fv) return fv;
        return null;
    }

    public object? CallFunction(GelFuncValue fv, object?[] args, int line = 0)
    {
        var inner = new GelEnv(fv.Closure);
        for (int i = 0; i < fv.Params.Count; i++)
            inner.DeclareLocal(fv.Params[i], i < args.Length ? args[i] : null);
        try { ExecBlock(fv.Body, inner); }
        catch (GelReturnSignal rs) { return rs.Value; }
        return null;
    }

    // ── Statements ─────────────────────────────────────────────────────────────
    private void Exec(Stmt s, GelEnv env)
    {
        switch (s)
        {
            case CommentStmt: return;
            case ImportStmt: return;
            case VarDecl v:
                {
                    var val = v.Value != null ? Eval(v.Value, env) : null;
                    if (v.IsGlobal) Global.DeclareLocal(v.Name, val);
                    else env.DeclareLocal(v.Name, val);
                    return;
                }
            case Assign a:
                {
                    var val = Eval(a.Value, env);
                    AssignTo(a.Target, val, env);
                    return;
                }
            case ExprStmt es: Eval(es.Expr, env); return;
            case ReturnStmt r:
                {
                    object? v = r.Values.Count == 0 ? null : Eval(r.Values[0], env);
                    throw new GelReturnSignal(v);
                }
            case BreakStmt: throw new GelBreakSignal();
            case ContinueStmt: throw new GelContinueSignal();
            case CrashStmt c: throw new GelQuitSignal(c.Reason != null ? Stringify(Eval(c.Reason, env)) : null);
            case IfStmt i:
                {
                    if (Truthy(Eval(i.Cond, env))) ExecBlock(i.Then, new GelEnv(env));
                    else
                    {
                        bool matched = false;
                        foreach (var (ec, eb) in i.Elifs)
                            if (Truthy(Eval(ec, env))) { ExecBlock(eb, new GelEnv(env)); matched = true; break; }
                        if (!matched && i.Else != null) ExecBlock(i.Else, new GelEnv(env));
                    }
                    return;
                }
            case WhileStmt w:
                while (Truthy(Eval(w.Cond, env)))
                {
                    try { ExecBlock(w.Body, new GelEnv(env)); }
                    catch (GelBreakSignal) { break; }
                    catch (GelContinueSignal) { continue; }
                }
                return;
            case LoopStmt lp:
                while (true)
                {
                    try { ExecBlock(lp.Body, new GelEnv(env)); }
                    catch (GelBreakSignal) { break; }
                    catch (GelContinueSignal) { continue; }
                }
                return;
            case ForStmt f:
                {
                    var loopEnv = new GelEnv(env);
                    if (f.Init != null) Exec(f.Init, loopEnv);
                    while (f.Cond == null || Truthy(Eval(f.Cond, loopEnv)))
                    {
                        try { ExecBlock(f.Body, new GelEnv(loopEnv)); }
                        catch (GelBreakSignal) { goto endFor; }
                        catch (GelContinueSignal) { /* fallthrough */ }
                        if (f.Step != null) Exec(f.Step, loopEnv);
                    }
                    endFor: return;
                }
            case ForEachStmt fe:
                {
                    var iter = Eval(fe.Iterable, env);
                    foreach (var item in ToEnumerable(iter, fe.Line))
                    {
                        var inner = new GelEnv(env);
                        inner.DeclareLocal(fe.VarName, item);
                        try { ExecBlock(fe.Body, inner); }
                        catch (GelBreakSignal) { break; }
                        catch (GelContinueSignal) { continue; }
                    }
                    return;
                }
            case FuncDecl fd:
                env.DeclareLocal(fd.Name, new GelFuncValue { Name = fd.Name, Params = fd.Params, Body = fd.Body, Closure = env });
                return;
            case MatchStmt m:
                {
                    var subj = Eval(m.Subject, env);
                    foreach (var (pat, body) in m.Cases)
                    {
                        var pv = Eval(pat, env);
                        if (Equal(subj, pv)) { ExecBlock(body, new GelEnv(env)); return; }
                    }
                    if (m.Else != null) ExecBlock(m.Else, new GelEnv(env));
                    return;
                }
        }
    }

    private void ExecBlock(List<Stmt> body, GelEnv env)
    {
        foreach (var st in body) Exec(st, env);
    }

    private void AssignTo(Expr target, object? val, GelEnv env)
    {
        switch (target)
        {
            case VarRef vr:
                if (!env.AssignExisting(vr.Name, val)) env.DeclareLocal(vr.Name, val);
                return;
            case IndexExpr ix:
                {
                    var t = Eval(ix.Target, env);
                    var k = Eval(ix.Index, env);
                    if (t is List<object?> list && k is double dn) { int i = (int)dn; while (list.Count <= i) list.Add(null); list[i] = val; return; }
                    if (t is Dictionary<string, object?> dict) { dict[Stringify(k)] = val; return; }
                    throw new GelRuntimeException("cannot index-assign to that target", target.Line);
                }
            case MemberExpr me:
                {
                    var t = Eval(me.Target, env);
                    if (t is Dictionary<string, object?> dict) { dict[me.Member] = val; return; }
                    if (t != null && TrySetReflective(t, me.Member, val)) return;
                    throw new GelRuntimeException($"cannot assign member .{me.Member}", target.Line);
                }
        }
        throw new GelRuntimeException("invalid assignment target", target.Line);
    }

    // ── Expressions ────────────────────────────────────────────────────────────
    private object? Eval(Expr e, GelEnv env)
    {
        switch (e)
        {
            case NumLit n:  return double.Parse(n.Value, CultureInfo.InvariantCulture);
            case StrLit s:  return UnquoteString(s.Value);
            case BoolLit b: return b.Value;
            case NullLit:   return null;
            case VarRef v:  return env.Get(v.Name);
            case ListLit ll: return ll.Items.Select(x => Eval(x, env)).ToList();
            case RangeExpr r:
                {
                    var a = ToDouble(Eval(r.Start, env)); var b = ToDouble(Eval(r.End, env));
                    var list = new List<object?>();
                    if (a <= b) for (double i = a; i < b; i++) list.Add(i);
                    else for (double i = a; i > b; i--) list.Add(i);
                    return list;
                }
            case UnaryOp u:
                {
                    var v = Eval(u.Operand, env);
                    return u.Op switch
                    {
                        "-" => -ToDouble(v),
                        "!" => !Truthy(v),
                        _ => throw new GelRuntimeException($"bad unary {u.Op}", u.Line)
                    };
                }
            case BinOp b: return EvalBin(b, env);
            case CallExpr c: return EvalCall(c, env);
            case MemberExpr m: return EvalMember(m, env);
            case IndexExpr ix:
                {
                    var t = Eval(ix.Target, env); var k = Eval(ix.Index, env);
                    if (t is List<object?> list && k is double dn) { int i = (int)dn; return (i >= 0 && i < list.Count) ? list[i] : null; }
                    if (t is string str && k is double sn) { int i = (int)sn; return (i >= 0 && i < str.Length) ? str[i].ToString() : ""; }
                    if (t is Dictionary<string, object?> dict) return dict.TryGetValue(Stringify(k), out var v) ? v : null;
                    throw new GelRuntimeException("cannot index value", ix.Line);
                }
        }
        throw new GelRuntimeException($"unknown expr {e.GetType().Name}", e.Line);
    }

    private object? EvalBin(BinOp b, GelEnv env)
    {
        if (b.Op == "&&") return Truthy(Eval(b.Left, env)) && Truthy(Eval(b.Right, env));
        if (b.Op == "||") return Truthy(Eval(b.Left, env)) || Truthy(Eval(b.Right, env));

        var l = Eval(b.Left, env); var r = Eval(b.Right, env);
        switch (b.Op)
        {
            case "+":
                if (l is string || r is string) return Stringify(l) + Stringify(r);
                return ToDouble(l) + ToDouble(r);
            case "-": return ToDouble(l) - ToDouble(r);
            case "*": return ToDouble(l) * ToDouble(r);
            case "/": return ToDouble(l) / ToDouble(r);
            case "%": return ToDouble(l) % ToDouble(r);
            case "^": return Math.Pow(ToDouble(l), ToDouble(r));
            case "==": return Equal(l, r);
            case "!=": return !Equal(l, r);
            case "<":  return ToDouble(l) <  ToDouble(r);
            case ">":  return ToDouble(l) >  ToDouble(r);
            case "<=": return ToDouble(l) <= ToDouble(r);
            case ">=": return ToDouble(l) >= ToDouble(r);
        }
        throw new GelRuntimeException($"bad op {b.Op}", b.Line);
    }

    private object? EvalCall(CallExpr c, GelEnv env)
    {
        // Dotted built-in fast-path: e.g. file.read(...)  →  look up "file.read"
        if (c.Callee is MemberExpr me && me.Target is VarRef tv)
        {
            var key = $"{tv.Name}.{me.Member}";
            if (env.TryGet(key, out var direct) && direct is Func<object?[], object?> bf)
                return bf(c.Args.Select(a => Eval(a, env)).ToArray());
        }

        // Method call on a real .NET object: target.Method(args)
        if (c.Callee is MemberExpr mm)
        {
            var t = Eval(mm.Target, env);
            var args = c.Args.Select(a => Eval(a, env)).ToArray();
            if (t is Dictionary<string, object?> dict && dict.TryGetValue(mm.Member, out var dv) && dv is Func<object?[], object?> df)
                return df(args);
            if (t != null && t is not Dictionary<string, object?> && t is not string && t is not List<object?>)
            {
                if (TryInvokeReflective(t, mm.Member, args, out var res)) return res;
            }
            // Fall through to generic callee resolution (will likely fail with a clean error)
        }

        var callee = Eval(c.Callee, env);
        var fnArgs = c.Args.Select(a => Eval(a, env)).ToArray();
        return CallValue(callee, fnArgs, c.Line);
    }

    private object? CallValue(object? callee, object?[] args, int line)
    {
        if (callee is Func<object?[], object?> fn) return fn(args);
        if (callee is GelFuncValue fv) return CallFunction(fv, args, line);
        throw new GelRuntimeException("value is not callable", line);
    }

    private object? EvalMember(MemberExpr me, GelEnv env)
    {
        if (me.Target is VarRef tv)
        {
            var key = $"{tv.Name}.{me.Member}";
            if (env.TryGet(key, out var v)) return v;
        }
        var target = Eval(me.Target, env);
        if (target is Dictionary<string, object?> dict && dict.TryGetValue(me.Member, out var dv)) return dv;
        if (target is string s)
        {
            return me.Member switch
            {
                "length" => (double)s.Length,
                _ => throw new GelRuntimeException($"unknown txt member .{me.Member}", me.Line)
            };
        }
        if (target is List<object?> list)
        {
            return me.Member switch
            {
                "length" => (double)list.Count,
                _ => throw new GelRuntimeException($"unknown array member .{me.Member}", me.Line)
            };
        }
        if (target != null && TryGetReflective(target, me.Member, out var res)) return res;
        throw new GelRuntimeException($"cannot access .{me.Member}", me.Line);
    }

    // ── Reflection bridge for live .NET objects ────────────────────────────────
    private const BindingFlags _bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase;

    private static bool TryGetReflective(object target, string member, out object? value)
    {
        var t = target.GetType();
        var p = t.GetProperty(member, _bf);
        if (p != null) { value = Wrap(p.GetValue(target)); return true; }
        var f = t.GetField(member, _bf);
        if (f != null) { value = Wrap(f.GetValue(target)); return true; }
        value = null; return false;
    }

    private static bool TrySetReflective(object target, string member, object? raw)
    {
        var t = target.GetType();
        var p = t.GetProperty(member, _bf);
        if (p != null && p.CanWrite) { p.SetValue(target, Coerce(raw, p.PropertyType)); return true; }
        var f = t.GetField(member, _bf);
        if (f != null) { f.SetValue(target, Coerce(raw, f.FieldType)); return true; }
        return false;
    }

    private static bool TryInvokeReflective(object target, string member, object?[] args, out object? result)
    {
        var t = target.GetType();
        var methods = t.GetMethods(_bf).Where(m => string.Equals(m.Name, member, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var m in methods)
        {
            var ps = m.GetParameters();
            // Allow trailing optional args
            if (args.Length > ps.Length) continue;
            var coerced = new object?[ps.Length];
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < args.Length) coerced[i] = Coerce(args[i], ps[i].ParameterType);
                else if (ps[i].HasDefaultValue) coerced[i] = ps[i].DefaultValue;
                else { ok = false; break; }
            }
            if (!ok) continue;
            result = Wrap(m.Invoke(target, coerced));
            return true;
        }
        result = null; return false;
    }

    /// <summary>Coerce a Gel runtime value into the requested .NET parameter type.</summary>
    private static object? Coerce(object? v, Type want)
    {
        if (v == null)
        {
            if (want.IsValueType && Nullable.GetUnderlyingType(want) == null)
                return Activator.CreateInstance(want);
            return null;
        }
        if (want.IsAssignableFrom(v.GetType())) return v;
        if (want == typeof(float))   return (float)ToDouble(v);
        if (want == typeof(double))  return ToDouble(v);
        if (want == typeof(int))     return (int)ToDouble(v);
        if (want == typeof(long))    return (long)ToDouble(v);
        if (want == typeof(short))   return (short)ToDouble(v);
        if (want == typeof(byte))    return (byte)ToDouble(v);
        if (want == typeof(bool))    return Truthy(v);
        if (want == typeof(string))  return Stringify(v);
        if (want == typeof(Vector3)) return ToVector3(v);
        if (want == typeof(Vector2)) return ToVector2(v);
        if (want == typeof(Vector4))
        {
            if (v is Dictionary<string, object?> d)
                return new Vector4(F(d, "x"), F(d, "y"), F(d, "z"), F(d, "w"));
        }
        if (want.IsEnum && v is string es) return Enum.Parse(want, es, ignoreCase: true);
        // Last-ditch: try Convert.ChangeType
        try { return Convert.ChangeType(v, want, CultureInfo.InvariantCulture); }
        catch { return v; }
    }

    private static Vector3 ToVector3(object? v) => v switch
    {
        Vector3 vv => vv,
        Vector2 v2 => new Vector3(v2, 0),
        Dictionary<string, object?> d => new Vector3(F(d, "x"), F(d, "y"), F(d, "z")),
        List<object?> list when list.Count >= 3 => new Vector3((float)ToDouble(list[0]), (float)ToDouble(list[1]), (float)ToDouble(list[2])),
        _ => Vector3.Zero
    };

    private static Vector2 ToVector2(object? v) => v switch
    {
        Vector2 vv => vv,
        Vector3 v3 => new Vector2(v3.X, v3.Y),
        Dictionary<string, object?> d => new Vector2(F(d, "x"), F(d, "y")),
        List<object?> list when list.Count >= 2 => new Vector2((float)ToDouble(list[0]), (float)ToDouble(list[1])),
        _ => Vector2.Zero
    };

    private static float F(Dictionary<string, object?> d, string k) => d.TryGetValue(k, out var v) ? (float)ToDouble(v) : 0f;

    /// <summary>Make a CLR value friendlier for Gel: floats become doubles for arithmetic.</summary>
    private static object? Wrap(object? v) => v switch
    {
        float f => (double)f,
        int i   => (double)i,
        long l  => (double)l,
        short s => (double)s,
        byte b  => (double)b,
        _ => v
    };

    // ── Helpers ────────────────────────────────────────────────────────────────
    public static double ToDouble(object? v) => v switch
    {
        double d => d,
        float f  => f,
        int i    => i,
        long l   => l,
        bool b   => b ? 1 : 0,
        string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : double.NaN,
        null     => 0,
        _        => double.NaN
    };

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0,
        float f => f != 0,
        int i => i != 0,
        string s => s.Length > 0,
        _ => true
    };

    public static bool Equal(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a is double da && b is double db) return da == db;
        if (a is string sa && b is string sb) return sa == sb;
        return a.Equals(b);
    }

    public static string Stringify(object? v) => v switch
    {
        null => "none",
        bool b => b ? "true" : "false",
        double d => d == Math.Floor(d) && !double.IsInfinity(d) ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture),
        string s => s,
        Vector3 v3 => $"({v3.X}, {v3.Y}, {v3.Z})",
        Vector2 v2 => $"({v2.X}, {v2.Y})",
        List<object?> list => "[" + string.Join(", ", list.Select(Stringify)) + "]",
        _ => v.ToString() ?? ""
    };

    private static IEnumerable<object?> ToEnumerable(object? v, int line)
    {
        if (v is List<object?> list) return list;
        if (v is string s) return s.Select(c => (object?)c.ToString());
        if (v is System.Collections.IEnumerable en) return en.Cast<object?>();
        throw new GelRuntimeException("value is not iterable", line);
    }

    private static string UnquoteString(string raw)
    {
        if (raw.Length < 2) return raw;
        var inner = raw.Substring(1, raw.Length - 2);
        var sb = new StringBuilder();
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                char c = inner[++i];
                sb.Append(c switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '\\' => '\\', '"' => '"', '\'' => '\'', _ => c });
            }
            else sb.Append(inner[i]);
        }
        return sb.ToString();
    }
}
