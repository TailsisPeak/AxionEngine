# Axion — Scripting Engine API

This is the same surface documented in
[`gel-lang/docs/engine-api.md`](https://github.com/TailsisPeak/Gel-lang/blob/main/docs/engine-api.md),
restated here for the engine codebase.  Everything below is what the
**CSharpEmitter** lowers to when a Gel/Silica/Blocks script is compiled
into a `Behavior` subclass via `ScriptHost`.

## Lifecycle

```gel
func start() {  }    # → public override void Start()
func loop()  {  }    # → public override void Update()   (alias: update)
func fixed() {  }    # → public override void FixedUpdate()
```

If no entry point is declared, top-level statements become the body of
`Start()`.

## `transform`  —  this.Transform

```
transform.position           ↔ Transform.Position           (Vector3)
transform.rotation           ↔ Transform.Rotation           (Quaternion)
transform.scale              ↔ Transform.LocalScale         (Vector3)
transform.localPosition      ↔ Transform.LocalPosition
transform.eulerAngles        ↔ Transform.EulerAngles
transform.forward/right/up   ↔ Transform.Forward / Right / Up   (read-only)
transform.parent / children  ↔ Transform.Parent / Children
transform.translate(x,y,z)   ↔ Position += new Vector3(x,y,z)
transform.rotate(x,y,z)      ↔ EulerAngles += new Vector3(x,y,z)
transform.setPosition(x,y,z) ↔ Position = new Vector3(x,y,z)
transform.setScale(x,y,z)    ↔ LocalScale = new Vector3(x,y,z)
transform.lookAt(target)     ↔ Transform.LookAt(target)
```

## `gameObject` / `self`

```
gameObject.name      ↔ GameObject.Name        (string, read/write)
gameObject.active    ↔ GameObject.Active      (bool)
gameObject.transform ↔ GameObject.Transform
self                 ↔ this
```

## `time`  —  Axion.Core.Time

```
time.delta / time.deltaTime   ↔ Time.DeltaTime    (float, seconds)
time.elapsed / time.elapsedTime ↔ Time.ElapsedTime
time.frame / time.frames      ↔ Time.FrameCount   (long)
time.scale                    ↔ Time.TimeScale    (float, default 1)
time.fixedDelta               ↔ Time.FixedDelta   (float, default 1/60)
```

## `input`  —  Axion.Core.Input

Polling-style:
```
input.keyDown("space")        ↔ Input.GetKeyDown(Key.Space)
input.keyPressed("a")         ↔ Input.GetKeyDown(Key.A)
input.keyReleased("escape")   ↔ Input.GetKeyUp(Key.Escape)
input.axis("Horizontal")      ↔ Input.GetAxis("Horizontal")
```

Mouse (read via the `mouse` root):
```
mouse.position    ↔ Input.MousePosition  (Vector2)
mouse.delta       ↔ Input.MouseDelta
mouse.scroll      ↔ Input.ScrollDelta
```

Console / blocking input (`input.line`, `input.int`, `input.bool`,
`input.confirm`, `input.key`) is also available — see the Gel input-system
docs for the full list.

## `scene`  —  the active scene

```
scene.find("Player")     ↔ AxionApp.Instance.Scene.Find("Player")     → GameObject?
scene.create("Bullet")   ↔ AxionApp.Instance.Scene.CreateGameObject("Bullet")
scene.remove(go)         ↔ AxionApp.Instance.Scene.Remove(go)
find(name)               ↔ scene.find(name)
instantiate(name)        ↔ scene.create(name)
destroy(go)              ↔ scene.remove(go)
```

## `log`  —  Axion.Core.Log

```
log.info(msg)   ↔ Log.Info(msg)
log.warn(msg)   ↔ Log.Warn(msg)
log.error(msg)  ↔ Log.Error(msg)
```

Bare `print(...)` also lowers to `Log.Info(...)` so console-style scripts
work unchanged.

## `random`  —  RNG (`System.Random.Shared`)

```
random.value()        ↔ (float)Random.Shared.NextDouble()
random.range(a, b)    ↔ uniform float in [a, b)
random.int(a, b)      ↔ Random.Shared.Next(a, b)
random.sign()         ↔ -1 or +1
```

## `vec3` / `vec2`

```
vec3(x, y, z)   ↔ new System.Numerics.Vector3((float)x, (float)y, (float)z)
vec2(x, y)      ↔ new System.Numerics.Vector2((float)x, (float)y)
```

## Example

```gel
# samples/spinner.gel — drop this on any GameObject
func start() {
    log.info("spinner ready on " + gameObject.name)
}

func loop() {
    transform.rotate(0, 90 * time.delta, 0)

    if input.keyDown("space") {
        transform.translate(0, 1, 0)
    }
}
```
