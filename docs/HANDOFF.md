# Session handoff — 2026-09-25 (0.29.0: the wild answers a spawn war again)

**0.29.0** is PR #13 (`Patch_SpawnWar`, `ContestWildMaxSpawned` retired at config version 3,
verified in-game on Valheim 1.0.16; CLAUDE.md's Current state has the numbers) plus the version bump,
the BepInExPack pin moved to 5.4.2351, and `docs/CONFIG.md` regenerated. 0.28.0 and FireFront 1.0.2
went live on Hexium 2026-09-24, so the 0.28.0 section below is done except its website update.
Package from a fresh clone of the merge commit; upload is RavenIron's to do. FireFront is already
at 1.0.2 on the store, so there is no upload order this time.

Still open: the website's RW page (0.28.0's lightning in rain and two config files, 0.29.0's
wild answer); the nameplate RENDER check, reported working 2026-09-24 but not yet written up
(which title, whose plate, which server); revprobe of the other four mods' shipped binaries on
1.0.16; the FireFront 1.0.2 GitHub pre-release its changelog promises.

# Session handoff — 2026-09-24 (0.28.0: two config files, lightning checks the rain, PR #9 folded in)

Read `CLAUDE.md` first, then this. The 2026-09-23 handoff below is superseded; its OPEN BUG section
is fixed in 0.28.0 and verified in-game.

## The one-line version

**0.28.0** is three pieces of work in one release: the rain fix (storm lightning asks FireFront
whether it rains where each bolt would land), the config layout (one 148-setting file becomes a
42-setting main file and a 104-setting advanced file, every value carried), and PR #9 (a separate
session's code review of 0.27.5, branch `fix/review-2026-09-24`). The combined build was tested in
one in-game session on Storm10 with FireFront 1.0.2, and both the migration and the merge had
adversarial reviews.

## Where things stand

- **Local branch `release/0.28.0`** = `fix/review-2026-09-24` (PR #9, open, unmerged) plus the 0.28.0
  work, UNCOMMITTED at the time of writing. `stash@{0}` ("0.28.0 work before integrating PR 9") holds
  the 0.28.0 work as first written on `release/0.27.6`; drop it once 0.28.0 is merged. `release/0.27.6`
  never shipped: its number was folded into 0.28.0, and nothing public says 0.27.6.
- **PR #8** (`docs/0.27.5-audit`, CHANGELOG + CLAUDE.md) is still open and NOT in `release/0.28.0`. Merging
  it and this one will conflict in those two files; take both sides.
- **The store**: Hexium serves RW **0.27.4** (the fixed 0.27.5 zip in `dist\` was never uploaded, and
  0.28.0 supersedes it) and FireFront **1.0.1**. 0.28.0 pins **FireFront 1.0.2**, which is not on the
  store yet: **upload FireFront 1.0.2 first**, confirm
  `valheim.hexium.gg/api/experimental/package/RavenIronStudios/FireFront/1.0.2/` answers 200, then RW.
  BepInExPack 5.4.2351 appeared on 2026-09-24; the manifest still says 5.4.2350, which Hexium resolves
  to the current one anyway.
- **Verified in-game, 2026-09-24, Storm10, Valheim 1.0.15, FireFront 1.0.2** (details in CLAUDE.md's
  Current state): 3 of 3 bolts withheld under `fireweather force Rain`, a strike and 5 ground fires
  under `force Clear`, five strikes under a natural clear sky after `reset`. The migration: 146 of 146 values
  on Storm10, on a copy of it (Storm28), and in the player's own client config; second boot a no-op;
  an interrupted migration rebuilt by hand behaved as designed.
- **The docs check corrected the docs, and one constant.** `IsRainingAt` arrived in FireFront **0.21.0**, not
  0.20.3 (`RainFireFrontVersion`, the boot warning and every doc); 45 settings, not 25, are read on each
  player's own game (every Consequence setting among them); wind feeds nothing at all now; `.v0.bak`
  means "last written by 0.27.0 or earlier" (the stamp arrived in 0.27.1, which was never uploaded). These landed after PR #10 merged, in the docs PR.
- **Tests: 477/477** (0.28.0's 440 plus PR #9's). apiprobe: **110 of 110** surfaces resolve against the
  1.0.15 server assemblies.
- **Storm10**: stopped, left on the 0.28.0 build with FireFront 1.0.2 and its PRODUCTION storm settings,
  now in the two-file layout (compared by name against the pre-test file: 146 of 146). Beside them:
  `RagnaroksWrath.dll.0.27.5.bak` (a 0.27.5 build from `0bfcf8c`, not the packaged one), `RagnaroksWrath.dll.0.27.5-live-20260924.bak` (the
  PR #9 test build the other session had there), `com.raveniron.ragnarokswrath.cfg.pre-028test-20260924`
  (the v1 production file), `.v1.bak` (written by the migration), `.v2-raintest-20260924` (the test
  settings, v2 layout).
- **The `testing` Gale profile** runs the same 0.28.0 build (DLL + repo manifest), its config migrated;
  the PR #9 test build and its v1 config are kept beside them as `.0.27.5-live-20260924.bak` and
  `.pre-028test-20260924`.
- **Storm28** (`C:\Users\donfr\ValheimServers\Storm28`, port 2479, 2.1 GB) is a disposable Storm10 copy
  made so this session would not collide with the FireFront session. Stopped. Delete it when convenient.

## What was learned, in the order it cost something

1. **Another session had a release in flight, and nothing on this branch showed it.** It surfaced only
   because Storm10's RW DLL named a commit (`0.27.5+ddd36d7`) this branch did not have. `git worktree list`
   then found `_wt\RagnaroksWrath-release` on `fix/review-2026-09-24` with thirteen commits, a changelog
   and a FireFront 1.0.2 pin. **Before building a release, list every branch by date and every worktree.**
   The integration was a stash, a branch from their tip, and a pop: one conflict (the manifest's pin),
   six clean text merges, and a five-reviewer semantic check that found no damage.
2. **A stop routine that breaks every `valheim_server.exe` takes down another session's server.** Storm28
   was stopped twice, cleanly, by something other than this session. Stop a server by its own
   `ExecutablePath`, never by process name.
3. **The migration's failure paths needed a second review.** The first 0.28.0 tests modelled only a
   locked file failing before any write. The review found five defects, all on failure paths: a
   destination already holding an edited value was overwritten, a failed save truncated the file, a
   failed advanced save still saved the main file, an unreadable file ran the boot on defaults and
   saved them, and the backup note could deny a backup existed. Each is now a CLAUDE.md trap and a test
   that fails when its fix is reverted.
4. **`fireweather force <Env>` is the instrument for weather on a dedicated server.** FireFront's own admin
   command sets the server's `m_debugEnv`, which vanilla's override path honours even headless (the
   server's own sky read 'Rain'), and which `IsRainingAt` replays. Relayed from any admin client.
5. **`tools/config-guide/check-migrated.js` proves a migration by name.** It compares an old file with the
   two new ones key by key. It is how every "146 of 146" above was measured, and how Storm10's
   restored settings were checked.

## Not done

- **Commit, push, PR, package** — waiting on the word. Package from a FRESH CLONE of the pushed commit
  (two clones must give the same md5), then audit the zip.
- **The website** (RavenIron-website): the RW page stopped promising "never in rain" on 2026-09-23. Once
  0.28.0 is live it can say it again, with the FireFront 0.21.0 caveat, and it should mention the two
  config files.
- **The `SeasonSystem: initial season resolved as Spring` boot line is premature** on an established world;
  the next tick corrects it (`Spring -> Winter`). Seen on 0.27.x too. Cosmetic.

# Session handoff — 2026-09-23 (0.27.3 and 0.27.4: bosses stop levelling, and the FireFront pin moves)

Read `CLAUDE.md` first, then this. The 2026-09-18 handoff below is SUPERSEDED but kept, and two
of its statements are WRONG: that dependency strings are "minimums that resolve forward" (see the
dependency trap at the top of CLAUDE.md's Known traps), and that with no sky forced "real weather
decides" (see the open bug below).

## The one-line version

The owner reported a twice-marked Queen was almost unbeatable; **0.27.3** marks bosses but never
levels them. The owner then reported that installing 0.27.3 pulled FireFront 0.21.2; **0.27.4**
moves the pin to FireFront 1.0.0 and changes no code.

## Where things stand

- **0.27.4 is merged** (PR #5, merge commit `0bfcf8c`) **and LIVE on Hexium** since 19:31Z. The API
  lists it with `RavenIronStudios-FireFront-1.0.0`, so a mod manager now fetches FireFront 1.0.0. Its
  zip was built from a fresh clone at `395bfbd`. Nothing in it is code: manifest pins, the three
  version sites, README and docs. 0.27.3 (PR #4, `3cda48d`, zip from `41b0738`) went up earlier
  the same day.
- **Hexium REPACKS uploads.** The CDN file (`cdn.hexium.gg/upload/732/<version>.zip`) is 32–37 bytes
  smaller than the zip we built, for 0.27.2, 0.27.3 and 0.27.4 alike, and its etag is not our md5.
  Check an upload with a HEAD request against that offset; never expect a byte match.
- **The website matches the store** (RavenIron-website `180dc90`, live on ravenirongames.com within a
  minute): the RW, FireFront, Undertow, The Raven's Call and Where The Crow Flies pages were redrafted
  against each mod's shipped source and every changed claim checked by a second reader. The RW page no
  longer promises "never in rain" (see the open bug), and the 0.24.0 news post carries a dated
  correction about the rain gate.
- **Storm10 now runs RW 0.27.4 + FireFront 1.0.0** (replaced DLLs kept beside them as
  `RagnaroksWrath.dll.0.27.3.bak` and `FireFront.dll.0.24.0.bak`). STOPPED. Tartarus is hosted on
  bamf, not from this install.

## What was learned, in the order it cost something

1. **The nemesis death hook runs on the VICTIM'S CLIENT.** Its `Nemesis:` lines are in the client's
   LogOutput (the Gale profile's), never the server's. The first monitor watched the server and saw
   nothing.
2. **Package from a fresh clone, never from this folder.** Line endings are source bytes under
   `DeterministicSourcePaths`, the repo has no `.gitattributes`, and the working tree drifts (Git
   Bash's `sed -i` and most tools write LF; a Git for Windows checkout writes CRLF). A package built
   in place could be reproduced by nobody. Two fresh clones of the same commit build byte-identical
   DLLs. A cross-repo fix is offered as a separate task.
3. **A dependency pin is what gets installed.** See CLAUDE.md. Hexium lists the current
   BepInExPack whatever the zip says, and does that for no other dependency.
4. **An independent audit of the 0.27.3 zip caught three false sentences in my own changelog** before
   upload (the reproducibility claim, "the largest health pool in the game", and a comment passed off
   as test coverage). Run one on every release; it is cheap next to a wrong changelog on the store.

## OPEN BUG, found 2026-09-23 while updating the website: with no sky forced, a dedicated server's lightning ignores rain

`StormsForceWeather` is OFF by default. In that case `LightningStrike.SkyAllows` falls back to
`EnvMan.IsWet()`, and 0.27.2's changelog says "real weather decides". **On a dedicated server it does
not.** Decompiled from 1.0.15 (`EnvMan.UpdateEnvironment`): after the override check, the biome
weather roll returns early when `Utils.GetMainCamera()` is null, which it always is headless. So
`m_currentEnv` never leaves `Awake`'s `GetDefaultEnv()`, and `IsWet()` reads that default's
`m_isWet` (false for Clear) for the life of the process. Result: under the default config, a storm's
bolt can land in natural rain on every dedicated server. The README's "never in rain" (live on the
Hexium listing) is false there; a listen host is fine, because its `EnvMan` really runs.

**Not fixed, because the fix is a design call.** The obvious path: FireFront 1.0.0 already solved
exactly this problem. `FireFront.Utils.ValheimBridge.IsRainingAt(Vector3)` (public static, shipped
since FireFront 0.20.x, present in 1.0.0) replays vanilla's per-period, per-biome-sector weather roll
for a position instead of trusting headless `EnvMan`. Lightning already requires FireFront, so asking
it for the strike position costs no new dependency. It would be a FOURTH reflected surface: resolve it
lazily like the other three, log once if absent, and ask FireFront to document it as a cross-mod
contract. Until then, the no-forced-sky branch should not be described as honest anywhere.

## Not done, deliberately or for want of a word

- **A `wrath nemesis` recovery command** — designed, not built. `NextLevel` never demotes, so a boss
  levelled under 0.27.2 that is still alive keeps its level. A freshly summoned boss starts clean.
- **Cairn and RavenEye still say BepInExPack 5.4.2333** in their manifests; Hexium rewrites it, so
  it is cosmetic until their next release.
- **The Tartarus server on bamf** needs RW 0.27.4 and FireFront 1.0.0 at its next update; what it
  runs now was not checked from here.
- **Players who installed 0.27.3 through a mod manager may still have FireFront 0.21.2.** Updating RW
  does not necessarily upgrade a dependency that is already installed.
- **README: "FireFront 0.18.0+ for storm lightning" is conservative**, not proven necessary (FireFront's
  0.17.3 build already has `IgniteGroundNear`). Left alone; nothing older than 0.18.4 is on the store.

# Session handoff — 2026-09-18 (0.27.2: the two-sky storm shipped, and the gate under it never worked)

Read `CLAUDE.md` first, then this. The 2026-08-27 handoff below is SUPERSEDED but kept.

## The one-line version

The session set out to add random wet/dry storm looks. The looks work and are verified. Building
the test to prove it found that **the mechanic the looks were supposed to drive — "rain suppresses
lightning" — had never worked on a dedicated server, in any version.** That is 0.27.2.

## Where things stand

- **Repo `main` at 0.27.2, ALL PUSHED** (`dbae891`). 370/370 off-game tests. Binds clean against
  Valheim **1.0.15**.
- **`dist\RavenIron-RagnaroksWrath-0.27.2.zip` is built and verified — NOT UPLOADED.** The store
  still serves **0.27.0**. 0.27.1 was built and never uploaded, so 0.27.2 carries BOTH the config
  migration fixes and the lightning fix. This is the one mod that was ready to upload as of this
  session; see "Upload status" below.
- **`libs\` is on the 1.0.15 publicized set.** The owner regenerated
  `valheim_Data\Managed\publicized_assemblies` by hand at 11:40 that day; `fetch-libs.ps1` was then
  run in ALL SEVEN repos. Note this INVERTS the 1.0.7-era hazard — for once the in-game folder is
  the current copy, not the stale trap. Do not learn "the in-game folder is stale" as a rule; the
  rule is "let the per-file staleness guard decide".

## THE BUG, because it will be tempting to re-introduce

`FireSystem.TryLightning` gated on `EnvMan.IsWet()`. On a headless dedicated server that value is
not merely wrong, **it is frozen**, and two independent vanilla local-player gates cause it:

- `RandEventSystem.GetEnvOverride()` reads `m_activeEvent`, set only on a branch behind
  `(bool)Player.m_localPlayer`.
- `EnvMan.UpdateEnvironment`'s biome-roll fallback returns early when `Utils.GetMainCamera()` is null.

Both are permanently true headless, so the server never applies the forced storm sky AND never
rolls its own weather either. `IsWet()` sits at whatever `EnvSetup` `Awake` flagged as default —
observed as `Clear` — for the entire process lifetime.

**The fix is `LightningStrike.SkyAllows(forcedSky, stormIsDry, envIsWet)`**: with a sky forced, the
rolled look decides (correct on the authority by construction, because the authority is what rolled
it); with no sky forced, real weather decides, exactly as before. `FireSystem` short-circuits
`IsWet()` away entirely in the forced case. A LISTEN HOST was never affected — it has a local
player, so the override resolves and old and new agree.

**CLAUDE.md's 2026-08-27 claim that a wet look "suppresses lightning via the rain gate" is
corrected in place.** That run most likely watched the frozen default happen to agree with the roll.
A green in-game verification that never tested the negative case is not a verification.

## How it was verified, and why the second phase was the important one

Two phases on Storm10, ONE config key apart, same binary:

| phase | config | storms | bolts |
|---|---|---|---|
| 1 | `StormDryChance = 0` | 4 wet (incl. one RESUMED across a restart) | **0** |
| 2 | `StormDryChance = 1` | 1 dry | **1**, into a tree, spread to ground fire, scorch banked |

Four silent wet storms is a ~0.16% outcome under the old gate — but **it is also exactly what an
inert `FireSystem` would produce**, and nothing logs per tick. Phase 2 is the control that tells
those apart. If you ever re-verify this, do not skip it.

Seven tests pin `SkyAllows`; **two were proven to fail against the old code** by reverting it and
re-running, per the working agreement.

## Instruments — this is the transferable part

- **`devcommands` then `env`** in the client console prints `Environment: EnvSetup: ThunderStorm.`
  or `Eikthyr.` — an authoritative client-side read of the storm sky, and the first time this
  project has had one. Vanilla's own `env` is `onlyServer:true, isCheat:true` and is useless to a
  joined client; ServerDevcommands' override is what makes it work, and only for an adminlist player.
- **The vanilla `Wet` status icon** is the no-console equivalent, and it is exact rather than
  approximate: `EnvMan.IsWet()` returns `s_isWet` ← `GetCurrentEnvironment()?.m_isWet`, and
  `m_isWet` is the ONLY `EnvSetup` field feeding the `_Wet` shader global. Wetness in the gameplay
  sense and in the visible sense are the same boolean by construction.
- **"Is there thunder?" discriminates NOTHING.** `Thunder.cs` contains zero references to `EnvMan`,
  `IsWet` or any environment name. Both looks have thunder. A question was wasted on this.
- **The `storm began` log line now names WHOSE sky it is reporting.** It used to say
  `sky is 'Clear'` while every client stood in a thunderstorm — that line is the single reason this
  bug survived a previous verification.
- Wetness ramps over `m_wetTransitionDuration`, **15s default**. A storm shorter than ~30s cannot be
  trusted to show its sky; an early attempt at 30s duration would have produced a false negative.

## Valheim 1.0.15 (live 2026-09-18) — a NULL RESULT, recorded on purpose

Found by reading a client's console banner mid-session, not by noticing an update. Full drill run
the same hour: **93/93 apiprobe surfaces resolve**; revprobe says every BUILT and every SHIPPED
binary for all seven mods binds clean; network version still **40**; `Version.Player` **46** and
`Version.World` **41** unmoved, so no save migration. Every member this project reaches for is
unchanged. **Write null results down** — a release needing no change is the one nobody re-checks,
which is how Undertow 0.5.1 and RavenEye 0.1.0 stayed broken on Hexium for two days after 1.0.7.

## Upload status

> ✅ **UPDATED 2026-09-19 — everything is published and every version matches.** The table below
> this block is the 2026-09-18 snapshot, kept because it is what that session actually saw; it is
> now stale in every row. Live figures read from Hexium's own API (`/api/v1/package/` on the
> Valheim subdomain) rather than from a listing page, whose search does not filter.
>
> | mod | live on Hexium | local | state |
> |---|---|---|---|
> | RagnaroksWrath | 0.27.2 | 0.27.2 | current |
> | FireFront | 0.21.7 | 0.21.7 | current |
> | Undertow | 0.7.2 | 0.7.2 | current |
> | Cairn | 0.8.0 | 0.8.0 | current |
> | RavenEye | 0.2.0 | 0.2.0 | current |
> | ValkyriesCargo | 0.1.4 | 0.1.4 | current |
> | TheRavensCall | 1.3.0 | 1.3.0 | current |
> | WhereTheCrowFlies | 1.1.3 | 1.1.3 | current |
>
> **Nothing is owed to the store.** Two notes worth carrying:
>
> - **Undertow went 0.6.0 → 0.7.2 in one step.** 0.7.0 and 0.7.1 were built and never published,
>   so every existing installation receives the drift lines AND the config migration together.
>   That is also the first time any of this family's migrations runs on a config file belonging to
>   somebody who is not the owner — on the stamp-only path, since all three of Undertow's ledger
>   tables are empty by measurement.
> - **This mod's `manifest.json` still pins `RavenIronStudios-FireFront-0.21.2`, and the note below
>   explaining why is now out of date.** It was pinned there because 0.21.2 was the newest version
>   that EXISTED on the store; 0.21.7 has existed since 2026-09-18. The pin remains CORRECT either
>   way, because dependency strings are minimums that resolve forward — but the constraint that
>   forced it is gone, so bump it or leave it on the merits rather than on that reasoning.

## Upload status at the end of the 2026-09-18 session (nothing was uploaded THEN)

| mod | live on Hexium | local | verdict |
|---|---|---|---|
| **RagnaroksWrath** | 0.27.0 | **0.27.2** | READY — packaged, verified, pushed |
| FireFront | 0.21.2 | 0.21.4 | Zip repackaged with the README fix, but **0.21.4 owes an in-game run** per its own HEAD commit |
| Undertow | 0.6.0 | 0.7.1 | Zip binds clean, but its state is a concurrent session's to vouch for |
| Cairn / RavenEye / ValkyriesCargo / TheRavensCall / WhereTheCrowFlies | — | — | already current |

`manifest.json` now pins `RavenIronStudios-FireFront-0.21.2`, **not** 0.21.4. The Hexium API says
0.21.2 is the newest version that actually EXISTS on the store; pinning a local-only version would
name something the store cannot resolve. Dependency strings are minimums and resolve forward, so
this stays correct whether or not 0.21.4 ships.

## Storm10, the verification server

`C:\Users\donfr\ValheimServers\Storm10`, port **2477**, world `Storm10`, own `-savedir`, `public 0`.
STOPPED and RESTORED to production config at end of session (300s storms every 1–3h, lightning mean
15, `VerboseLogging` false). The test config is kept beside it as
`com.raveniron.ragnarokswrath.cfg.posttest-20260918` if this needs running again — restoring that
gives 90s storms every 60–120s with a 1-minute lightning mean and `StormAvoidBaseMeters 0`.

**Clamp traps found while setting that up**, all silent: `StormMinIntervalSeconds` floors at **60**,
`StormMaxIntervalSeconds` at **120**, `StormDurationSeconds` at **30**. Values below those do not
refuse, they clamp, and the file keeps claiming what you typed.

**Config edits only take while the process is STOPPED.** BepInEx's `ConfigFile.Reload()` runs from
the constructor only, so a hand edit to a running server's cfg is invisible to it.

## Traps this session walked into, so the next one need not

- **The BepInEx log APPENDS across boots.** Twice a grep of it produced a confident, well-formed,
  wrong answer. Always anchor to the LAST `BepInEx ... - valheim_server` banner line and discard
  everything above it.
- **`strings | grep` is not a version reader.** It grabs the first version-shaped literal and
  reported `1.0.0` for a 1.113.0 mod. Use
  `[Reflection.AssemblyName]::GetAssemblyName($dll).Version`.
- **The csproj `<Version>` bump does NOT touch `PluginVersion`**, the hardcoded `BepInPlugin`
  constant — which is what every log line and every other mod reads. A boot announced `0.27.1`
  while running 0.27.2 code. `package.ps1`'s three-way guard catches it at package time; the boot
  banner catches it sooner.
- **Gale HARDLINKS profile files to its package cache** (link count 2). `cp` over one in place
  writes THROUGH and corrupts the cached package for every profile using it. `rm` first, then copy.
  Recorded in auto-memory under `ravenrest-gale-profiles`.
- **`python` on this machine is the Microsoft Store stub** and hangs. Use node or the edit tools.
- **`2>&1` on a native exe in PowerShell** yields exit 255 on success. Do not redirect.
- **`git push` needs `env -u GITHUB_TOKEN`** — a scopeless `GITHUB_TOKEN` in the environment
  overrides the keyring and 403s git-over-HTTPS while leaving `gh api` working, so it looks
  intermittent rather than broken. The real fix is clearing that variable, which is the owner's.

## What is NOT done

1. **Upload 0.27.2 to Hexium.** Owner's action; nothing blocks it.
2. **`wrath status` says nothing about weather or storms.** Zero matches for `Weather|Storm` in the
   terminal patch — which is why this whole session was log archaeology. The smallest honest fix is
   one line in `Status()` reading the CALLING machine's own `EnvMan`
   (`EnvMan.instance?.GetCurrentEnvironment()?.m_name`), needing no networking, because `EnvMan` is
   a local singleton on every machine. It would have exposed this bug in seconds: server says
   `Clear`, client says `ThunderStorm`. **Do NOT reuse `WeatherSystem.CurrentEnvironment`** — that
   is authority-only (`WorldTick` gates every system on `IsSimulationAuthority`) and reads `""`
   forever on a pure client, the same shape as the `SeasonSystem.Current` bug `SeasonSync` exists
   to fix. Needs its own in-game run; a clean build proves nothing about `EnvMan` member access.
3. **The storm-look roll's RATE is unverified.** Variety is proven (both looks rolled and rendered);
   that the split matches `StormDryChance` would need ~100+ storms and was not attempted.
4. **`docs/BACKLOG.md` task 16** — softening the boot warning that still calls Seasonality a
   conflict — remains open and untouched.

# Session handoff — 2026-08-27 (THE ROADMAP IS COMPLETE; the EA rehearsal begins)

## PUBLISHED — 2026-08-27, evening

**Both mods are LIVE on Hexium (hexium.gg) under team `RavenIronStudios`:**
`RavenIronStudios-FireFront-0.18.4` and `RavenIronStudios-RagnaroksWrath-0.24.0` (the
dependency chain resolving for real — RW's manifest minimum 0.17.3 satisfied by the
published 0.18.4). RW ships the owner's wolf icon; FireFront the burning shield. Uploaded
private-first via the owner's browser. **Thunderstore is NOT a channel** — owner's call,
2026-09-03: nothing has ever been uploaded there and nothing will be. Hexium is the only
store. The zip is still built to Thunderstore's package *format*, which is what Hexium
consumes; format and channel are different things, and these docs conflated them for a week.
Packaging facts and the store-team-vs-GitHub-org distinction live in item 3 of "What
remains" below and in auto-memory (`distribution-targets`). The items below this line
predate the publish and describe the road to it.

For the next session picking this up cold. Read `CLAUDE.md` first, then this; `docs/BACKLOG.md`
carries per-task verification detail, `docs/reference/README.md` gates the engine sheets. The
2026-08-26 handoff below this section is SUPERSEDED but kept — it is the log of how phase D's
horn mystery became two days that finished the mod.

## Where things stand (v0.22.3 everywhere, 256/256 off-game, all pushed)

- **Every roadmap task (0–14) is built and verified.** Since the last handoff: phase D
  verified at every link (two decompiled engine facts: the pheromone max override widens the
  GATE not the BUDGET, and GetNrOfInstances counts the whole loaded area — so the war ships as
  REFILL PRESSURE, locked decision); the resolution edge observed exactly once (the half-life
  config floors at 1h — stage care just above threshold instead); phase E nemesis built and
  proven on its strong claim (ZDO-key mark survives ZDOID regeneration — a starred boar
  remembers Nomad through restarts); RelicSystem verified through TWO full stone lifecycles
  (consecration, retry-until-confirmed placement, desecration billed to the vandal, rune
  columns 0.18.0); the wrath console (0.19.0) with REMOTE admin mutations riding vanilla's
  own pipe (0.21.x — output relays back to the invoking admin's screen); the farming consumer
  (0.20.0) verified by a two-turnip race; genesis, VersionSync and config parity (0.22.0).
- **THE ANNOUNCEMENT LAYER WAS NEVER CONNECTED and now is (0.22.1–0.22.3).** Two lifelong
  bugs stacked: a dedicated server holds ZERO Player instances even with players online
  (measured; the Jotunn sheet's §6 is WRONG — noted in the reference README), and zone
  centres sit at y=0 so 3D distance checks lost every hillside player. ToPlayersNear now
  routes remote players by character ZDO + ShowMessage RPC, distances XZ-planar. The flip
  announcement was SEEN on screen — the first zone-local line this mod ever delivered from
  headless. Every pre-0.22.3 "accepted unobserved" Centre line was in fact undeliverable;
  the next organic war/stone re-verifies them free.
- **EA prep done:** README rewritten for the full mod; fresh-world smoke test passed (15
  systems from zero, no noise); Seasonality GUID verified from their source; a 0.19.0-era
  store zip exists in dist\ (STALE — repack at the shipping version).

## The two worlds (one shared BepInEx config — remember that)

- **`Dedicated` (uid 4690126) is the STAGING world.** Scarred, instrumented, fast to stage
  via `wrath zone set` / `care set` / `harm set` (fresh contact stamps automatic). Cleaned
  2026-08-27: phantom rival 999 zeroed, owner's care at (2,1) restored to 0.35. STANDING
  GUARDS: do NOT cure the outbreak at (0,-1) — its plague peak is armed for a future organic
  stone and the ground is guarded world state.
- **`Saga` (uid 18446744073473864156) is the HONEST PLAYTHROUGH** — the EA rehearsal. Booted
  fresh 2026-08-27 08:47, zero errors, genesis at 3h mean (per-install config, so Dedicated
  inherits 3h at its next boot — fine for a staging world). `start_saga_world.bat` in the
  server root launches it; the old `start_headless_server.bat` launches Dedicated; same
  port, one at a time. NO STAGING on Saga, ever — its whole value is that nobody helped it.

## What remains before the store button

1. **The Skadi evening, on Saga**: multi-peer ring fan-out (the one genuinely untested
   surface — same code per peer, but never run with two), player-nameplate render (nemesis
   plate already argues equivalence), nemesis owned-elsewhere skip line. Skadi's Gale
   profile needs RW at the shipping version (0.23.x — storm lightning and the FireFront
   dependency landed AND VERIFIED LIVE 2026-08-27, see backlog task 15) + FireFront
   0.17.3; VersionSync will tattle inside a minute if the versions differ. TEST STAGING
   STILL LIVE on both owner installs: `LightningMeanMinutes = 1` and `VerboseLogging =
   true` (restore 15 / false before the Skadi evening; `StormsForceWeather = true` +
   `StormForcedEnvironment = Eikthyr` are KEEPERS — the owner's chosen look, verified
   compatible with lightning).
2. **Repack** at the shipping version: `tools\package.ps1` (refuses on version disagreement).
3. **FireFront IS PUBLISHED (2026-08-27)** — the dependency prerequisite is CLEARED. Team
   name on BOTH stores is **`RavenIronStudios`** (not RavenIron — the repo org and the
   store team differ), so RW's manifest dependency reads
   `RavenIronStudios-FireFront-0.17.3` and ONE zip serves both stores. Upload day taught
   three packaging facts, now baked into BOTH repos' `tools\package.ps1`: Hexium rejects
   PS 5.1 `Compress-Archive` zips outright ("No manifest.json found"), requires the DLL
   under `plugins/` (BepInEx layout) not at the zip root, and .NET Framework's
   `CreateFromDirectory` writes spec-invalid backslash entry names — entries are now
   written by hand with forward slashes. FireFront also gained its full store packaging
   that day: player README (dev log preserved at `docs/DEVLOG.md`), CHANGELOG, icon
   (owner's art at the required 256x256), manifest, and the three-version-homes guard,
   which caught csproj still saying 0.17.2 on its very first run.
4. **Hexium (hexium.gg) is a second distribution target** (owner's call, 2026-08-27).
   Confirmed against hexium.gg/packaging: it takes Thunderstore-compatible zips with the
   exact root files `package.ps1` already stages (manifest.json, icon.png 256x256,
   README.md, CHANGELOG.md optional) — **the same dist zip uploads to both stores
   verbatim**. Their quirks: dependency strings are MINIMUM versions (managers install
   that or newer), BepInExPack_Valheim dependencies are auto-stripped on upload,
   `name` allows only letters/digits/underscores (ours complies), description caps at
   256 chars (ours complies), each version number uploads once, optional `faq/` folder
   of Markdown files, >5 dependencies auto-tags a modpack. FireFront must be published
   on Hexium too for the dependency to resolve there — same prerequisite as item 3,
   per store.

## Ops facts the next session will want (also in auto-memory)

- Graceful server stop: injected CTRL_BREAK works, CTRL_C is ignored (helper script pattern
  in the session scratchpad; the user's console window Ctrl+Break also works). Start:
  direct exe with `$env:SteamAppId = "892970"` — `cmd /c` bat launches fail from the tool.
- The server's BepInEx LogOutput.log captures ZERO Unity/ZLog lines — vanilla audit output
  (e.g. `Remote admin ... executed command`) exists only in the console window. Absence in
  the file proves nothing.
- The permission classifier blocks writing into worlds_local — prepare edits in the
  scratchpad and hand the user one copy command. Reading is fine. (Mostly obsolete now:
  the wrath console does live edits without file dances.)
- BepInEx clamps AND persists out-of-range config values at boot — after a clamp the file
  no longer holds what you wrote.

---

# (SUPERSEDED) Session handoff — 2026-08-26 (updated through phase D, ~13:50)

For the next session picking this up cold. Read `CLAUDE.md` first (house rules, locked
decisions, verified-state ledger), then this for the operational state the docs don't carry.
`docs/BACKLOG.md` has per-task detail; `docs/reference/README.md` gates the engine fact
sheets; `docs/zone-clock-ownership.md` is the architecture decision everything drift-shaped
obeys.

## Where things stand

- **Backlog tasks 0–12: done.** RW at **0.10.0** (151/151 off-game tests), deployed AND
  verified by strings on both sides (Gale client profile + dedicated server — Program Files
  IS copyable from PowerShell when the server is stopped; the old "cannot write there"
  memory was circumstantial). FireFront at **0.17.2**. Both repos pushed under **RavenIron**.
- **Task 12 (ConsequenceSystem) VERIFIED LIVE same day** — all five checks by the owner:
  one-line announcement (once), withered/refusing pickables, slowed wildlife, starred
  spawns at expected rarity, both negative controls held. Crop withering recorded
  unobserved (plant a turnip in the outbreak for the quick half). Deploy near-miss to
  remember: a DLL built BEFORE the version bump shipped with task-12 code and a 0.9.0
  label — the strings audit caught it; identify builds by content, always.
- **Task 11 (HealthSystem) VERIFIED LIVE end to end** — accrual to four decimals, tiers
  felt (after the 0.8.1 step fix — read that backlog entry for the lesson: assert what the
  player was PROMISED, not what the function computes), relog/restart persistence, decay to
  through-zero row removal, the chill with its campfire gate, frost breath with its roof
  gate. Unobserved, accepted: tier-3 line, live mead/rested rate change.
- **0.8.2 flush-fix verification PENDING:** needs a player to get exposed, then a server
  stop — the health ledger's mtime must land beside Dedicated.db's instead of up to 60s
  earlier. Today's stops had an empty ledger, so it has never been observed doing its job.
- **PHASE D (spawn war) BUILT at 0.15.0 and STAGED, verification IN FLIGHT at handoff:**
  contested = blight >= 0.5 AND total zone care >= 0.3; storms x2 the intensity (rule 4's
  breadcrumb cashed); blight side rides the task-12 star surface x(1+bonus x intensity);
  wild side is vanilla's OWN pheromone machinery (`SE_Stats.m_pheromoneTarget` — the Bog
  Witch mead fields, public, read by UpdateSpawnList) via invisible TTL'd "war horn" SEs
  on players standing contested ground. Resolution at the contested->uncontested edge
  (wild wins if blight broke, blight wins if care faded), ONE Centre line. Wire is now
  ...zone_state3 (war intensity per zone in the ring). THE OUTBREAK (0,-1) IS STAGED AS
  WAR GROUND: care hand-set to 0.5 (backup `.prephased`). **HORN DISCREPANCY RESOLVED
  2026-08-26 14:34, client chain VERIFIED LIVE at 0.15.1 both ends:** the silence had two
  boring causes stacked — the owner was standing in (1,-1), one zone EAST of the war (the
  contact-tick block in the zone store proved it), and the server run of the moment was a
  13:36 build of 0.15.0 that PREDATED the war-state edge log, so its silence proved
  nothing (audit the instrument). On the 0.15.1 restart the server logged `war state: 1
  contested zone(s)` first tick, and once the owner walked into (0,-1) proper the client
  logged `war intensity 1.0 underfoot.` then `3 war horn(s) ready for contested ground.`
  — server war state -> ring push -> client cache -> horn build, every link observed. The
  "horns sounding" audio was never ours; the horns are silent SEs.
  **ENGINE FACT, decompile-read 2026-08-26 (SpawnSystem.UpdateSpawnList body):** vanilla's
  `m_pheromoneMaxInstanceOverride` widens the instance-cap GATE but NOT the group-size
  BUDGET — the spawn-count line computes `m_maxSpawned - currentCount` from the spawner's
  RAW `m_maxSpawned`, ignoring the override. So pheromones can never push a population
  above vanilla's stock cap; they only refill toward it faster (and `GetNrOfInstances`
  at range 0 counts the WHOLE loaded area, not the zone). Observed live: `Spawned Deer
  x 0` — a line only reachable when a pheromone raised the gate past ambient while the
  raw-cap arithmetic zeroed the group. That line is also PROOF the horn's prefab-
  reference equality holds and the override applies: without a pheromone the pass breaks
  before logging. Also: Hare's spawner is Mistlands-tagged, biome-gated out before
  pheromones are consulted — in Meadows only Deer/Boar can ever answer the horn. Configs
  raised to ContestWildSpawnChance=100 / ContestWildMaxSpawned=15 both ends (the max
  override is gate-only given the quirk). The war therefore reads as REFILL PRESSURE:
  visible only when local wildlife is below vanilla's cap — hunt the ambient deer down,
  then horns refill at 100% chance from 40-80m out. Whether refill pressure is enough
  wild-side teeth, or phase D needs its own modest spawn budget, is a DESIGN DECISION
  for Raven Iron — the "no spawn patch at all" intent has now met vanilla's ceiling.
  **WILD SIDE VERIFIED LIVE 2026-08-26 15:18 (0.15.2):** the 0.15.2 war census (verbose-
  gated, 60s, logs per-target instances loaded + within 200m — the numbers vanilla's
  budget actually sees) showed the truth in one line: 6 deer and 8 boar ALREADY within
  200m, invisible in the fog — the population was above vanilla's caps the whole time,
  "no animals came" was "the animals were already here". Owner culled deer 6 -> 2;
  next attempt logged `Spawned Deer x 2`. Boar stayed `x 0` at 5 loaded >= its raw cap —
  the negative control proving the gate-not-budget engine fact in the same breath (and
  tamed boar COUNT: a pen near war ground permanently mutes the boar horn, same vanilla
  behavior that stops wild boar near pens). Full loop: war computed -> synced -> horns
  -> pheromone -> vanilla spawner -> budget open -> creatures spawned. Census stays in
  the shipping code behind VerboseLogging.
  **RESOLUTION EDGE VERIFIED server-side 2026-08-26 15:59:** `war in (0,-1) resolved:
  Blight.` + `war state: 0 contested zone(s)`, exactly once. THE HALF-LIFE ROUTE ABOVE
  IS WRONG — `RivalryHalfLifeHours` has an AcceptableValueRange floor of 1h (deliberate:
  "0 would disable decay"), BepInEx silently clamps AND persists the clamped value back
  into the cfg. Working route, used live: stop server, edit the war zone's ledger care
  to just ABOVE threshold (0.31 vs 0.30), restart — war re-derives, decay crosses in
  minutes, edge fires mid-session. TWO LESSONS BAKED IN: (1) the owner reported seeing
  the Centre line ~6 min BEFORE the edge fired; the ledger disproved it (care 0.3033,
  still above threshold, decay monotonic) — an expected announcement will be "seen"
  early; trust the store over the eyewitness. A title (Ashbringer) landed at the false
  sighting's timestamp. (2) decay ran ~3x slower than pure math predicts and it is NOT
  a bug: the owner standing in the fog kept the zone contacted, drift healed plague/
  scorch on contact, healing booked care to them — the defender holds ground by standing
  on it. Expect slow care fades wherever a player camps damaged ground. Ledger NOT
  restored from `.prephased` (that predates the afternoon's real history — Ashbringer's
  harm, the tending care); post-test ledger kept, half-life 48h rebound, fresh boot
  correctly derives NO war at care 0.297.
  **DESIGN DECIDED 2026-08-26 (0.15.3): REFILL PRESSURE.** Raven Iron chose vanilla's
  machinery as-is over a mod-owned spawn budget; ContestWildSpawnChance=100 and
  ContestWildMaxSpawned=15 are the shipped defaults. Locked-decisions row added to
  CLAUDE.md. Phase D is CLOSED. 0.15.3 built but NOT yet deployed to the live server
  (running 0.15.1) or client (0.15.2) — the live cfgs already carry 100/15 explicitly,
  so only fresh installs are affected; deploy at the next natural restart.
  Player-side sighting of the true resolution Centre line:
  owner could NOT confirm (the only confident sighting was the disproven early one).
  ACCEPTED UNOBSERVED on component evidence — the Centre pipe is live-verified since
  v0.2.2 (storm announcements seen on screen, same MessageFeed.ToPlayersNear path) and
  the call site's execution is logged with a player in the area. To observe it properly
  someday: HalfLife=1 (valid, no clamp), care=0.31, restart, watch deliberately —
  expect ~5-10 min, healing-presence income stretches pure-decay math ~3x. A dead-ledger edit CANNOT test resolution: a
  restart re-derives war from the store, so care edited below 0.3 just means no war and
  no edge (the Winterborn shrug — UpdateWar's own comment). Live route instead: set
  `RivalryHalfLifeHours = 0.05` in the server cfg, restart, stand within 64m of (0,-64);
  war re-derives (care 0.51), decays past 0.3 in ~2.3 min, edge fires mid-session ->
  expect exactly ONE Centre line "The blight has claimed this ground." + server log `war
  in (0,-1) resolved: Blight.` + `war state: 0 contested zone(s)`. Then restore half-life
  48 and the ledger from `.prephased`. Do not cure the outbreak to force a wild win — it
  is guarded world state.
- **Task 13 PHASES A, B COMPLETE and live-verified; PHASE C BUILT (RW 0.14.1, FireFront
  0.17.3):** the ledger with all three writers proven (A); the grudge with three teeth
  verified — Ashbringer, the personal pick refusal, and the drift tooth measured at
  **0.01750/h observed vs 0.01750/h predicted, EXACT**, after the first window was
  contaminated by the credit-on-contact backlog (the runbook's own trap; re-baseline
  after the backlog clears, save-to-save). Phase C (dominance, mercies x1.25 zone /
  x1.5 sickness, Warden/Despoiler, flip voice) is harness-pinned (180+) and deployed;
  its in-game bits await play: mercy rate measurement, a 3-zone title, and the flip
  announcement which STRUCTURALLY requires two players. Phase D CLOSED 2026-08-26 (see
  above). PHASE E VERIFIED LIVE 2026-08-26 at 0.16.0: a boar killed Nomad, took its star
  and slayer line (owner's eyes), and KEPT BOTH through a full server bounce — the
  ZDO-key mark surviving ZDOID regeneration is the design's strong claim, observed.
  TASK 13 COMPLETE, all five phases. Both ends deployed at 0.16.0. Task 14 (RelicSystem,
  the capstone) is the last system in the mod.
  Scorch ash (0.14.x) verified by eye — burn scars visibly dust now, fading with healing.
  Deferred with cause: wildlife-flee and hostiles-seek-you (no acceptance criteria,
  BaseAI is the riskiest surface — own pass, own gates).
- **Version note:** client runs 0.14.1; server runs 0.14.0 until its next stop (the
  0.14.1 delta is client-only ash tuning). FireFront 0.17.3 both sides.
- **Task 14 specced, not built** (owner's calls recorded; Relic is the capstone, after 13).
- **Remaining, unnumbered:** task 13 phases B–E, task 14; the storm-gust emitter
  (`Visuals\ParticleKit` is the substrate); farming's growth/yield consumer; the nameplate
  RENDER check (needs a second player); the crop-wither slow half (a turnip stands in the
  outbreak at blight 1.0 — it should be visibly unhealthy now and die at grow time);
  package + store upload (now 0.11.1 + FireFront 0.17.3 as a pair) — SUPERSEDED, both shipped to Hexium 2026-08-27.

## The live world (Dedicated, uid 4690126)

Genuine state, not test residue — do not wipe:

- An outbreak centred on zone (0,-1) at plague **~1.0** with corruption **0.7** (raised
  from ~0.30 by store edit for the task 12 empowerment test, kept as genuine state) —
  the zone now carries ALL FOUR consequence flags, and the corruption boost makes its
  plague effectively incurable by neglect. Winter or a config cure drains the plague;
  nothing but time off drains corruption. Seeded neighbours sit below the 0.15 floor.
- **A cold scar at zone (1,0), frost ~0.74** — staged for the chill/breath test and KEPT
  deliberately (owner's call): breath fogs there, the chill bites, and it only drains while
  someone stands in it. `.prefrost`/`.pretask12` zone-store backups sit beside it.
- **A burn scar across zones (1,-1)/(1,0)/(0,-1)/(1,-2)** — the owner's own arson test
  (2026-08-26): a beech lit south of the scar spread four zones before the extinguish key
  and a server cycle killed it. Scorch ~0.21 in (1,-1), and the rivalry ledger bills
  775624 exactly 0.2664 harm for it. Genuine history now — do not clean it up.
- **The rivalry ledger** `ragnarokswrath_rivalry_4690126.dat`: 775624 carries care across
  a dozen zones (tending + healing presence) and the arson harm above. Both columns decay
  at a 48h half-life.
- Titles ledger: `ragnarokswrath_titles_4690126.dat` — Nomad (775624) = Plaguewalker.
- Health ledger `ragnarokswrath_health_4690126.dat`: header-only right now (Nomad recovered
  fully; the row through-zero-deleted itself, which is correct).
- Five turnips in zone (-1,0) tiring the soil; scorch from the fire test healing slowly.
- Stores live in `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local\`, plain TSV,
  hand-editable (a supported write path — plague AND frost were both seeded that way; stamp
  the contact column with fresh `DateTime.UtcNow.Ticks` when editing, or the backlog credits
  the elapsed gap and drains your edit on first contact).

## Runbook (the part that cost round-trips to learn)

- **Deploy targets.** Client: `%APPDATA%\com.kesomannen.gale\valheim\profiles\Default\BepInEx\plugins\RagnaroksWrath\`
  (the user launches through GALE — the Steam folder loads nothing). Server:
  `C:\Program Files (x86)\Steam\steamapps\common\Valheim dedicated server\BepInEx\plugins\RagnaroksWrath\`.
  FireFront and ServerDevcommands are installed both sides too. **A running game locks its
  DLL** — ask the user to quit before copying; verify a deploy by `strings` on the copied
  file, never by trusting the cp.
- **Bump the version on every deploy** (Plugin const + csproj together; `package.ps1` enforces
  manifest agreement) — it's the only way to know which build a log came from.
- **Server start** (background, output to a scratch log):
  `cd <server dir> && SteamAppId=892970 ./valheim_server.exe -nographics -batchmode -name "My server" -port 2456 -world "Dedicated" -password "secret" -crossplay`
  Every restart mints a NEW join code — grep the console log for `registered with join code`
  and hand it to the user each time. Stop via `taskkill` (graceful first), then confirm
  `Dedicated.db` mtime moved — check the save, not the process.
- **Logs.** Server: `<server dir>\BepInEx\LogOutput.log` (recreated each boot — watches on it
  die across restarts; re-arm). Client: Gale profile `BepInEx\LogOutput.log`.
- **Verification pattern that works:** background `until`-loop watches on the log and on the
  store file; the zone store is a 120s-autosave SNAPSHOT, so absence in the file means "not
  saved yet", not "not happening". Predict the number before reading it — every verified rate
  so far matched prediction once credit-on-contact backlogs were accounted (contact stamps
  keep accruing across server downtime; first contact pays the backlog).
- **Config edits** need a server restart to take effect; BepInEx rewrites the cfg on exit, so
  don't edit a client's cfg while its game runs. `VerboseLogging` gates the per-tick lines —
  currently OFF both sides.
- **Tooling quirks of this machine:** `python` is the Store stub (`dnread.py` dead — use
  `ilspycmd`, installed globally; ALWAYS decompile before designing against a game API, and
  read bodies, not signatures). Bash here has perl; PowerShell doesn't. PowerShell 5.1:
  no `&&`, and `2>&1` on native exes fakes failures. gh CLI is installed and authed as
  RavenIron; repo-local git email is ravenirongames@gmail.com in both repos.

## Cross-repo contract (do not break silently)

`FireManager.CollectActiveFirePositions(List<Vector3>)` in FireFront is resolved by RW via
reflection. Renaming or re-signing it in FireFront disarms RW's Scorch with only a per-tick
warning on the RW side. It is comment-documented at both ends.
