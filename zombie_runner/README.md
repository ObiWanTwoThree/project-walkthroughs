# Zombie Runner prototype

The foundation for a forward-running zombie horde shooter for iOS and Android, in the style of Last Z or Mob Control. It is built for Unity 2022.3 LTS or newer with URP, and aims for a stable 60 FPS on mid-tier phones with no GC allocation during gameplay.

The squad runs forward on its own. Drag to steer between the lane barriers, and the squad auto-targets and shoots the nearest zombie ahead. Hordes spawn in staggered, jittered clusters beyond the camera and walk toward you. They flash and get knocked back when hit, and return to the pool when they die, reach the squad, or fall behind it.

## Quick start

1. Create a project in Unity Hub from the **Universal 3D** (URP) template, on 2022.3 LTS or newer.
2. Copy `Assets/ZombieRunner` from this folder into the project's `Assets` folder.
3. Run **Tools › Zombie Runner › Build Prototype Scene**.
4. Press Play and drag with the mouse to steer. To test on a phone, switch the platform to iOS or Android in Build Settings and build. The builder already adds the scene to the build list.

The builder generates everything under `Assets/ZombieRunner/Generated`: materials, a checker ground texture, and the `Bullet` and `Zombie` prefabs. It also adds a `Zombie` layer if one doesn't exist. Then it creates `Assets/ZombieRunner/Scenes/Prototype.unity` with every reference wired up. Re-running it overwrites the generated assets in place.

## Scene layout

```
Main Camera            (0, 11, -8), rotation (42, 0, 0); CameraFollow → PlayerSquad
Directional Light
[Managers]
 ├── ObjectPooler      Bullet prefab ×150, Zombie prefab ×200
 └── PerformanceBootstrap
PlayerSquad            Tag: Player; kinematic Rigidbody; BoxCollider (trigger)
 │                     SquadMovement, SquadShooter (Zombie Layer = Zombie)
 ├── Visual            Capsule, no collider
 └── FirePoint         local (0, 1, 0.8)
[Spawners]
 └── HordeSpawner      Player Transform → PlayerSquad
Environment            TrackScroller: 3 ground segments of 60 m with lane rails, recycled forward
```

**Zombie prefab:** the root is on the `Zombie` layer and has a `CapsuleCollider` (non-trigger), a kinematic `Rigidbody` and a `ZombieAgent`. A child `Body` capsule holds the renderer, and `ZombieAgent` references it along with the flash material.

**Bullet prefab:** a mesh with a `Bullet` component and no collider.

To set the scene up by hand instead, recreate the layout above. Bullets need no collider, and zombies need a kinematic Rigidbody so the squad's trigger detects them.

## Scripts

| Script | Role |
| --- | --- |
| `Pooling/PooledBehaviour` | Base class for pooled objects. `Despawn()` is safe to call more than once. |
| `Pooling/ComponentPool<T>` | Typed pool: prewarms instances, then only toggles activation. Grows when empty and logs one warning so you can raise the prewarm size. |
| `Pooling/ObjectPooler` | Scene singleton that owns the typed `Bullets` and `Zombies` pools. |
| `Player/SquadMovement` | Constant +Z speed. Drag is normalised by screen width, clamped to ±`horizontalLimit`, and followed with `SmoothDamp`. Works with touch and mouse, on either the old Input Manager or the Input System package. |
| `Player/CameraFollow` | Locks the camera to the squad's Z and follows part of its X movement, with smoothing. |
| `Combat/SquadShooter` | `OverlapSphereNonAlloc` radar picks the nearest zombie ahead of the squad, then fires at a fixed cadence and leads the shot. |
| `Combat/Bullet` | Moves at a fixed speed and finds hits by sphere-casting along each frame's path. Returns to the pool on a hit or when its lifetime ends. |
| `Enemies/ZombieAgent` | Walks down-track and drifts toward the squad. A hit swaps in the flash material, adds a decaying knockback and lowers health. |
| `Spawning/HordeSpawner` | Keeps clusters spawned up to `lookAheadDistance` ahead of the squad. Rows are staggered, positions jittered, and each cluster shifted sideways at random within the track. |
| `World/TrackScroller` | Moves ground segments that fall behind the squad to the front, so the track never ends. |
| `Core/PerformanceBootstrap` | Sets `targetFrameRate` to 60 (mobile defaults to 30) and keeps the screen awake. |
| `Editor/PrototypeSceneBuilder` | The one-click scene builder described above. |

## Gameplay hooks

`ZombieAgent` raises two static events, which are the places to attach scoring and squad losses:

```csharp
void OnEnable()
{
    ZombieAgent.Killed += OnZombieKilled;          // score, VFX, drops
    ZombieAgent.ReachedSquad += OnZombieReachedUs; // squad reduction
}

void OnDisable()
{
    ZombieAgent.Killed -= OnZombieKilled;
    ZombieAgent.ReachedSquad -= OnZombieReachedUs;
}
```

Use a method group or a cached delegate as shown. A lambda that captures variables allocates each time you subscribe.

## Tuning

| Setting | Where | Default |
| --- | --- | --- |
| Forward speed | SquadMovement | 7.5 m/s |
| Drag feel | SquadMovement › Drag Units Per Screen Width | 10 m per screen width |
| Lane limit | SquadMovement › Horizontal Limit | ±4.25 m |
| Fire rate | SquadShooter › Fire Interval | 0.1 s (10 rounds/s) |
| Radar range | SquadShooter › Targeting Range | 16 m |
| Bullet speed, damage, lifetime | Bullet prefab | 32 m/s, 1, 1.2 s |
| Zombie health, speed | Zombie prefab | 3 HP, 2.2 m/s |
| Knockback | Zombie prefab › Impulse ÷ Damping | 2.5 ÷ 10 = 0.25 m per hit |
| Horde size | HordeSpawner › Rows, Columns | 3–6 rows × 5 columns |
| Horde spacing | HordeSpawner › Cluster Spacing Z | 30 m |

At these defaults, no more than about 90 zombies and 15 bullets are alive at once. The 200 and 150 prewarm sizes therefore leave headroom before a pool has to grow.

## Performance notes

- **No allocation in the gameplay loop.** Spawning and despawning only toggle `SetActive`. Physics queries use `OverlapSphereNonAlloc` and `SphereCast` with preallocated buffers, component lookups use `TryGetComponent`, and events are invoked with `?.Invoke`. The only allocations after load happen if a pool has to grow, and that logs a warning. To verify, open the Profiler's CPU module during a run: the GC Alloc column for `PlayerLoop` should read 0 B.
- **Few draw calls.** Every object uses the same shader (URP Simple Lit), so the SRP Batcher can batch them. The hit flash swaps a shared material rather than using a `MaterialPropertyBlock`, which would break SRP Batcher compatibility. Only the squad casts a shadow, and only the ground receives shadows.
- **Physics.** Zombies have kinematic Rigidbodies, which the physics engine handles more cheaply than moving static colliders. Bullets have no physics body at all. Kinematic-to-kinematic contacts are filtered out by default, so zombies don't generate contacts with each other.
- **Suggested URP asset settings for mid-tier phones:** HDR off, MSAA 2× or off, render scale 0.85–1.0, shadow distance around 30 m with one cascade, soft shadows off. These are starting points to tune in the profiler, not measured values.
- **Pooled objects sit at the scene root.** Unity updates many root-level transforms faster than one large moving hierarchy. Turn on **Group Under Folders** on the ObjectPooler if you want a tidy Hierarchy while debugging.
- **Domain reload.** Static state is reset through `RuntimeInitializeOnLoadMethod`, so the scripts still work with domain reload disabled in the Enter Play Mode settings.

## Changes from the original draft scripts

- **Bullets now register hits.** The draft relied on `OnTriggerEnter`, but neither the bullet nor the zombie had a Rigidbody, so the event never fired. A bullet moving about 0.5 m per frame could also pass through a target between frames. Bullets now sphere-cast along each frame's path.
- **Pool objects can't be handed out twice.** In the draft, a bullet whose lifetime ended in the same frame as a hit was queued twice, so the pool could later give one object to two callers. `PooledBehaviour.IsSpawned` now guards returns.
- **Pool growth no longer allocates on the lookup.** The draft's `pools.Find(p => ...)` closure and `transform.Find($"Pool_{tag}")` allocated each time a pool grew. Pools are now typed, so spawning does no string lookups or `GetComponent` calls.
- **Zombies that pass the squad are recycled.** In the draft they were never returned, which drained the pool.
- **The radar finds the true nearest zombie.** A 40-slot `OverlapSphereNonAlloc` buffer returns an arbitrary subset once it fills. The buffer is now 128, with an editor warning if it fills. The scan is also centred on the squad.
- **Features the spec required:** the hit flash, resolution-independent drag, a camera that follows the squad, and a 60 FPS target (mobile builds default to 30).

## Verification status

- The runtime and editor scripts compile with 0 errors against the Unity 2021.1 reference assemblies (`Unity3D.SDK` on NuGet), as C# 9 targeting netstandard2.1, using the old Input Manager path. Every API used also exists in 2022.3 LTS.
- The `ENABLE_INPUT_SYSTEM` branch in `SquadMovement` was not compiled, because the Input System package wasn't available in that environment.
- Nothing has been run inside the Unity Editor or on a device yet. Frame rate and GC figures are design targets, not measurements.
