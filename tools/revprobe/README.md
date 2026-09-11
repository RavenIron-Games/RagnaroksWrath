# revprobe — does the SHIPPED binary still bind against the game?

```powershell
dotnet build tools\revprobe\RevProbe.csproj -v q --nologo
.\tools\revprobe\bin\Debug\net10.0\RevProbe.exe "<Valheim>\valheim_Data\Managed" <dll-or-folder> [...]
```

Point it at a mod DLL — one from `dist\`, one pulled out of a published zip, one sitting in a Gale
profile — and it reports every reference that no longer resolves. Exit code 0 means the binary
binds clean.

## Why this is not apiprobe

`tools\apiprobe` asks *does today's game still have what our SOURCE reaches for?* It checks a
hand-kept list and it is the right tool before shipping.

This one asks the opposite question, and it is exhaustive rather than hand-kept: *does the binary
we already shipped still bind?* It opens the DLL's own metadata, walks its `TypeReference` and
`MemberReference` tables, keeps everything scoped to `assembly_valheim`, `assembly_utils` or
`Splatform`, and resolves each one by name **and full signature** against the live game assemblies.

The gap between those two questions is where Undertow 0.5.1 and RavenEye 0.1.0 sat for two days.

## What happened on 2026-09-11

Valheim went 0.2x → 1.0.7 → 1.0.12. Three mods had compile breaks, were ported, and shipped.
**Undertow and RavenEye compiled clean against 1.0.7 and so were never re-packaged** — and that is
exactly the trap. A clean source build proves the source matches today's game. It says nothing
about a DLL compiled in August, because .NET binds a call by its exact signature at *runtime*.

Both shipped binaries were broken, by one reference each:

```
MISSING METHOD  Terminal+ConsoleCommand..ctor(String, String, ConsoleEvent, bool×5,
                                              ConsoleOptionsFetcher, bool×3)
    exists instead: .ctor(String, String, ConsoleEvent, bool×6, ConsoleOptionsFetcher, bool×3)
```

1.0.7 added a bool. Nothing in either repo asked to be fixed, no build failed, and the store kept
serving both. **Run this over the published artefact after every Valheim update, not just over
the source.** A mod you did not have to change is the one nobody re-checks.

Note the severity is not obviously "one console command stops working": see the comment at
`Undertow\Commands\WakeConsole.cs`, which records the measured behaviour that Mono resolves a
member when the method is JIT'd rather than when the line runs, so the enclosing method throws on
entry and never reaches its own `try`.

## What it cannot see

It is a metadata check, so it is blind to anything not written in IL as a reference:

- **Reflection by string** — `AccessTools`, `GetMethod`, `GetField`, and Harmony patch targets.
  That is apiprobe's half of the job; run both.
- **A constant folded in at compile time.** 1.0.7 renumbered `FileHelpers.FileSource` into a
  `[Flags]` enum and moved `Local` from 1 to 2. A binary that passed the symbol has the old
  *number* baked in, and no reference resolution will ever notice.
- **A method body that changed behaviour without changing its signature.**

Two smaller honesty notes in the output: generic methods are matched by name and arity rather than
by full signature and are reported as `skipped`, never as misses; and a run that reports problems
in *everything* means the tool is wrong, so check it against a freshly built DLL first.
