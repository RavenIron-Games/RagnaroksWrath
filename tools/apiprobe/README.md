# apiprobe — does the game still have the members we reach for?

```powershell
dotnet build tools\apiprobe\Probe.csproj -v q --nologo
.\tools\apiprobe\bin\Debug\net10.0\Probe.exe "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed"
```

Exit code 0 means every surface resolved. Non-zero names the ones that did not, and prints
the overloads that *do* exist so a miss suggests its own replacement.

## Why this exists

Run it after every Valheim update, before shipping anything.

A mod's dependencies on the game come in two kinds. The compiler checks one of them. The
other — Harmony patch targets, `AccessTools` lookups, `GetMethod`/`GetField` by string, and
direct access to members that are only public in our *publicized* reference assemblies —
is invisible to it. Those fail at runtime by returning `null`, and null usually means a
feature quietly switches itself off rather than throwing.

Valheim 1.0.7 (2026-09-09) made the case for this tool on its own. Alongside the breaks that
did fail to compile, it silently killed:

- `ZoneSystem.m_instance`, renamed to `s_instance`. FireFront's water level read as -10000,
  so ground fire stopped treating water as a firebreak. Fire crossed rivers. One debug line.
- `Character.AddFireDamage(float)`, which gained a `short variant`. Fire stopped hurting
  anything at all.
- `ZDOMan.FindSectorObjects`, retyped to `Vector2s` and `SimulationDistance`. The scan that
  finds burnable objects returned nothing.
- `SEMan.AddStatusEffect`, whose fourth parameter changed type *and* which gained a fifth.
- `TerrainComp.PaintCleared`, restructured into a settings object.
- `Player.Message`, `Player.PlacePiece`, `TerrainComp.Save`, `TreeLog.Destroy` — each gained
  a parameter. A default argument still changes the signature, so an explicit types lookup
  stops matching.

Every one of those built cleanly.

A Harmony patch target is the loud exception: if it goes missing, Harmony throws while
patching and can take the whole mod down at boot. 1.0.7 deleted the `levelUpMultiplier`
parameter that Ragnarok's Wrath's Empower patch bound to, which would have done exactly that.

## Maintaining it

The list in `Program.cs` is hand-kept and covers Ragnarok's Wrath and FireFront. When you add
a reflection lookup or a Harmony patch, add a line here. An entry that is merely *missing*
from this file is the one that will bite, so the list earning its keep depends on that habit.

`MetadataLoadContext` inspects the assemblies without executing them, so no Unity runtime is
needed and this runs anywhere the game files exist — including a build agent.
