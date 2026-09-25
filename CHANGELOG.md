# Changelog

## 0.29.0

**The wild answers a spawn war again.** On contested ground, wildlife (deer, boar and hares by
default) refills quickly while the war lasts, as it did before Valheim 1.0. Nothing needs doing to
upgrade.

### The wild's answer, working again

Since Valheim 1.0 (Ragnarok's Wrath 0.27.0), the wild side of a spawn war did nothing. The mod raised
wildlife spawns through the game's own attraction mechanism, the one the Bog Witch's meads use, and
Valheim 1.0 stopped reading the two values that mechanism relied on. The war still starred hostile
spawns harder and still resolved, but no extra animals came.

- **In a contested zone, every animal on the wildlife list spawns at `ContestWildSpawnChance`**
  (default 100%) instead of the game's own lower chance. It never lowers the game's chance, and 0
  turns it off.
- **The game's own limit on how many can stand there is untouched**, so a war refills hunted-out
  ground quickly and never crowds it past what the game would allow. The first spawn check after
  you arrive, which makes up for time away, rolls at the game's own chance, because at 100% that
  catch-up could overshoot the limit. Raids and other event spawns are never affected.
- **It is read on the game of the player standing there**, like the rest of a war's local effects,
  so every player needs the setting in their own config.
- **`ContestWildMaxSpawned` is retired.** It never added an animal even when the game read it, and
  nothing reads it now. The first start removes its line and says so in the log. Your
  `ContestWildSpawnChance` stays exactly as you set it.

### Packaging

- Tested on Valheim 1.0.16. The BepInExPack dependency is now 5.4.2351; FireFront stays at 1.0.2.

## 0.28.0

**The config is two readable files now, with every value kept. Storm lightning checks for rain where
it would strike. And a code review of 0.27.5 fixed a world's state leaking into the next one you
load, storms ending raids, and titles swapping back and forth.** Nothing needs doing to upgrade: the
first start moves your settings and says so in the log.

### Two config files, every value kept

The single `com.raveniron.ragnarokswrath.cfg` had grown to 148 settings in 18 sections, numbered in
the order they were added. It is now two files:

- **`com.raveniron.ragnarokswrath.cfg`** holds 42 settings: every gameplay system's on/off switch and
  the few settings most servers change, such as storm timing and look, storm lightning, how often
  outbreaks start, the nemesis, announcements and the visual effects.
- **`com.raveniron.ragnarokswrath.advanced.cfg`** holds the other 104: rates, thresholds, intervals
  and lists.
- **Both files use the same 15 sections, `01 - General` to `15 - Visuals`**, one per system, so a
  setting sits under the same heading as its switch.
- **Every description is rewritten** in plain terms: what the setting does, in what unit, and, for
  the ones each player's own game reads, that it does.
- **[docs/CONFIG.md](docs/CONFIG.md) is a guide to all of them**, section by section, and marks the
  gameplay settings each player's own game reads: sickness and chill, the exposure tiers, tired soil and the crop list, every land consequence
  with its thresholds and the wildlife list, grudges and spawn wars, the nemesis, and relic stones. Give every player the
  server's values for those. The visual effects are each player's own choice.
- **Two settings are removed, because neither ever did anything.** `StormFireRiskMultiplier` and
  `StormWindMultiplier` only ever fed a log line.
- **The wind settings moved into `03 - Weather`** in the advanced file. The mod reads the wind, but
  since the two retired settings went, nothing uses it.

**Upgrading.** The first start of 0.28.0 moves every setting to its new place with its value. It
keeps the old file beside the new ones as `com.raveniron.ragnarokswrath.cfg.v1.bak` (`.v0.bak` if
it was last written by 0.27.0 or earlier, before config files recorded a layout version), and logs one line
naming what moved and what was removed. `wrath status` shows
the layout version the files are at. **Going back to an older version? Restore that backup first.**
An older version cannot read the new layout and would start from its defaults.

**An interrupted move is safe.** If a file is locked or the disk fills, the mod leaves your files as
they were and moves them on the next start:

- A save that fails part-way puts the file back exactly as it was, because BepInEx empties a config
  file before writing it.
- The main file is written only after the advanced one has saved, so no setting is ever lost
  between the two.
- If a setting turns up in both its old and its new place with different values (left behind by an
  interrupted earlier start and edited since), the new place's value is kept, unless it is only the
  shipped default. The log names both values either way.
- If the mod cannot read the file at all, it writes nothing to either file and says, in the log and
  in `wrath status`, that settings may be at their defaults until the next start.

**Verified on a dedicated server (Valheim 1.0.15).**

- A server config in the 0.27.x layout, with custom values, moved on the first start with all 146
  of its values unchanged: 145 settings moved to new places, and one (`AnnounceTitles`) was
  already in its place, since its section kept its name. A second start changed nothing.
- A player's own config, last written by 0.27.5, moved the same way when they launched the game
  on 0.28.0.
- An interrupted move, rebuilt by hand under real BepInEx: a value edited in the new place was kept
  with a warning, and a default left there gave way to the old value.

### Storm lightning checks for rain where it would strike

**On a dedicated server, storm lightning struck in the rain.** That was the default setup, with
`StormsForceWeather` off. The game only works out the weather for a machine that has a camera, so a
dedicated server's own weather reading never changes from the value it started with, and the rain
check read that. The README promised "never in rain".

- **Each bolt now asks FireFront whether it is raining where the bolt would land.** FireFront works
  out the weather for any position the same way the game does for a player standing there. If it
  rains there, or FireFront cannot say, the bolt does not fall.
- **This needs FireFront 0.21.0 or later** when no storm sky is forced. With an older FireFront the
  log warns at startup, and no bolt falls unless `StormsForceWeather` is on.
- **With `StormsForceWeather` on, nothing changes.** The storm's own sky decides, as it has since
  0.27.2.
- **Verified on a dedicated server (Valheim 1.0.15, FireFront 1.0.2).** With the server's weather
  forced to rain, all three bolts a storm rolled were withheld, each logged as "FireFront reads rain
  there". Forced clear, the next bolt struck about 30 m from the player and started a ground fire.

### From a code review of 0.27.5

One save file gains an optional column: the titles file marks a player who has earned Winterborn
this winter with a `W`, so the award stays once per winter across a restart. Older builds ignore the
column and drop the mark on their next save.

- **Each world keeps its own state.** Going back to the main menu and then starting or hosting
  another world kept the first world's zone drift, titles, grudges, sickness and relic stones in
  memory. They carried on in the second world at the same map positions, and the next autosave
  wrote them over the second world's own files. Now a world's state is saved and cleared when you
  leave it, and the next world loads its own. A dedicated server runs one world per start and was
  never affected.
- **Joining a server after playing a local world reads the server's state.** The same leftover
  data made a player who went on to join a server see their local world's plague, frost, grudges
  and relic auras instead of the server's. The same fix covers it.
- **A storm no longer ends another event.** Valheim runs one event at a time, and starting a storm
  ended whatever was running: a raid on a base, or Valkyrie's Cargo's merchant visit. A storm that
  comes due during another event now waits, and starts once that event is over. If the storm event
  is missing from the game's list, the storm no longer starts at all (the log says why), where
  before it ended the running event and announced a storm that never came. One side effect: a
  vanilla raid pauses while no player is near it, so a raid everyone walked away from can hold
  storms back until someone returns to it or the game replaces it with another event.
- **Titles no longer swap back and forth.** Stormrider, Plaguewalker and Winterborn were awarded
  again on every title check while their condition held. Two that held together (plague in winter,
  a storm over plague) swapped every check, each swap announced to everyone. Every title is now
  earned once, when its condition starts. Winterborn's clock now starts again at zero each winter,
  so it is earned once per winter, after that winter's full stretch online; before, a server up
  through two winters awarded it the moment the second began. Titles earned at the same moment
  give one announcement.
- **Arson blame stays with the arsonist's own fire.** FireFront used to report a single igniter for
  the whole map, so while one player's fire burned, every other fire anywhere was booked as their
  harm.
  - With a FireFront that reports who lit each fire (1.0.2 or later), each fire's harm goes
    to the player who lit it, or the fire it spread from. Natural fires and lightning blame nobody,
    and a storm no longer holds its lightning back while someone's fire burns.
  - With FireFront 1.0.1 or older, harm is booked only in the zones next to where that player's
    fire has spread. A fire elsewhere on the map is no longer blamed on them. A fire started right
    next to theirs, while theirs still burns, can still be.
- **Autosaves stay quick on old worlds.** Saving the zone file compared every visited zone against
  every stored one, a pause on the server that grew with the world's age. It is now a direct lookup.
- **Nearby messages are rate-limited per player.** One shared limit meant a message to one player
  could silently swallow another player's one-time message elsewhere on the map, such as a relic's
  story or the line a player gets on first reaching barren or festering ground.
- **Relic and zone messages are checked.** The server drops relic and zone messages from a client
  that claims to be someone else. Clients accept relic and zone updates only from the server. A
  report that a relic stone broke counts only from a player near it, a report that one was raised
  only from the player the server asked to raise it, and the breaker is blamed only when they are
  near the stone.
- **A mod manager now installs FireFront 1.0.2 with it** (was 1.0.0). It is the first FireFront
  that reports who lit each fire. Installing by hand, an older FireFront now names itself at boot,
  with what it still does and what needs the update.

## 0.27.5

**A storm now blows over whether or not anyone is near it.** Every storm was registered as a vanilla
event that stops its clock while no player is within its range. So a storm that everyone walked away
from, or logged off under, never reached its end. It stayed on the server indefinitely, blocked every
later storm, and kept counting against the whole world's condition.

- **Why it froze.** Vanilla adds no time to an event registered with `m_pauseIfNoPlayerInArea` while
  nobody is inside its area (Valheim 1.0.15, decompiled). This mod reads "a storm event exists" as "a
  storm is on", so a frozen storm was a storm forever: the scheduler waited on it, the world's
  condition kept its storm burden, and every client kept receiving it. The storm's clock now runs
  whoever is near. Valkyrie's Cargo hit the same flag on its merchant visit and made the same fix.
- **A storm already stuck in a world clears itself.** The save keeps only a storm's name, time and
  position and takes everything else from the running build, so the first boot on 0.27.5 resumes a
  frozen storm and it runs out the time it had left. No migration, and nobody has to go and find it.
- **Verified on a dedicated server (Valheim 1.0.15) in three steps, each a control for the next:**
  - On 0.27.4, a player walked out of a 180-second storm and stayed online, far from it. Five
    minutes after it began, it had not ended; the player then logged off, and it still had not
    ended when the server was stopped.
  - That frozen storm was saved, and the server booted on 0.27.5 with nobody online. It came back
    from the save and ended within two and a half minutes.
  - On 0.27.5, a player walked out of a new 180-second storm and stayed online, far from it. It
    ended 181 seconds after it began.
- **A storm's start is now reported the moment it starts,** not on the next weather tick. Now that a
  storm runs out unwatched, the shortest allowed storm (30 s) could otherwise begin and end between two
  ticks of the slowest allowed weather interval (60 s), and never be logged, announced as passed, or
  seen by anything that reacts to storms.
- **`StormDurationSeconds` is now time from the start, in game seconds.** Its description said vanilla
  paused it while nobody was near. Only a single-player pause stops it now, as it stops every clock
  in the game. The key and its default are unchanged, so no configured value changes; the first boot
  only rewrites that setting's description in the config file.

## 0.27.4

**Installing Ragnarok's Wrath now brings FireFront 1.0.0, not 0.21.2.** The manifest has named
FireFront as a dependency since 0.23.0, and it named `RavenIronStudios-FireFront-0.21.2`: the
newest FireFront the store carried when that line was last set. Hexium's packaging notes call a
dependency version a minimum, so it looked harmless to leave behind. It was not: on 2026-09-23 a
mod manager installing 0.27.3 fetched exactly FireFront 0.21.2, a version from before FireFront's
1.0 release, instead of the 1.0.0 the store offers.

- **No code changed.** Only the version number moves.
- **FireFront 1.0.0 carries everything this mod reaches for.** It was checked two ways:
  - Decompiling the 1.0.0 that Hexium serves. A mod manager's own download of it is
    byte-identical to the uploaded package. It has `FireFront.Fire.FireManager` with its static
    `Instance`, `CollectActiveFirePositions(List<Vector3>)`, `CurrentFireIgniterPlayerId` and
    `IgniteGroundNear(Vector3, float)`, one overload each, under the unchanged plugin GUID.
  - Booting a dedicated server on this build with FireFront 1.0.0. The bridge resolved on its
    first pass and raised no warning in the minute the server ran.
- **The BepInExPack line moves from 5.4.2333 to 5.4.2350.** That changes the file, not what
  installs. For that one dependency Hexium lists the current pack whatever the zip says: 5.4.2350
  for the live 0.27.3, whose zip says 5.4.2333, and the same for this studio's other mods. It does
  not do that for FireFront.
- **An older FireFront still works if you install by hand.** The README says which FireFront
  each feature needs. The manifest line only decides what a mod manager fetches.
- **The README now matches 0.27.3 on bosses.** It said every creature that kills you gains a
  star; a boss is marked but never does.

## 0.27.3

**A boss that kills you keeps the story and never gains a level.** The nemesis mark stars up the
creature that killed you — and nothing ever excluded bosses from that. A Queen who killed the same
player twice reached `NemesisMaxLevel` 3 and became close to unbeatable.

- **Why a level is catastrophic on a boss specifically.** Vanilla scales health LINEARLY —
  `SetLevel` → `SetupMaxHealth` → `SetMaxHealth(GetMaxHealthBase() * level)` — so level 3 is three
  times a health pool that was already sized for a boss fight, and per-level attack damage lands
  on top. On an ordinary creature the same arithmetic is a fair fight; on a boss it is a wall.
- **Bosses are still marked.** The kill count still climbs and the plate still reads
  `slayer of <name> x2`, because the mark was never the problem. `EnemyHud` builds the boss health
  bar's name from `Character.GetHoverName` (the same method our decorating postfix appends to), so
  a boss now wears the story of the fight without the arithmetic. `Character.m_boss` is plain
  prefab data that nothing writes at runtime, so `IsBoss()` is answerable on the victim's client
  the moment the killer resolves.
- **Lowering `NemesisMaxLevel` was never a fix and could not have been.** `NemesisMark.NextLevel`
  refuses to demote by design, so a cap lowered today leaves every creature already marked exactly
  where it is. That is why this is a "never level a boss" change rather than a tuning change, and
  a comment on the test that pins non-demotion now says so. The boss gate itself was verified in
  the game (below); the test harness does not cover it.
- **Not changed, deliberately: tamed creatures.** They looked like the same class of bug and are
  not — vanilla's `MonsterAI.SetTarget` structurally refuses to let a tame target the player who
  damaged it (`!attacker.IsPlayer() || !m_character.IsTamed()`), so an `IsTamed()` guard would be
  code defending against something the engine already prevents.
- **Not changed: the other `SetLevel`.** `Patch_Consequence`'s `CreatureSpawner.Spawn` postfix is
  the nest and bone-pile path, already gated on `m_maxLevel < 2`, and boss altars never go through
  it. Checked rather than assumed, because fixing one of two identical hazards is how this returns.
- **Verified in-game on a dedicated server before release.** Eikthyr killed the same player twice
  and stayed at level 1 while his boss health bar carried the slayer mark with its count of two; a
  greydwarf killed them twice and climbed 1 → 2 → 3, stopping at the cap. The greydwarf is the half that matters —
  a guard that suppressed every level-up would make the boss row look identical.

**Also in this release: the DLL no longer carries the build machine's folders.** Every build
through 0.27.2 embedded an absolute path to its debug symbols in the DLL, and that path included
the user name of the machine it was built on. The build now maps the repository root to a neutral
prefix, so neither the DLL nor its symbols name any local folder. The compiled code is unchanged.
A side effect worth knowing if you compare binaries: the DLL's contents now follow the source it
was built from rather than the folder it was built in. Line endings count as source, so two builds
of the same commit match only when their checkouts used the same line endings. The 0.27.3 package
was built from a fresh clone with Git for Windows' default settings, which is what reproduces it.

## 0.27.2

**On a dedicated server, a storm's rolled look decided nothing.** The wet storm was supposed to
soak and only the dry one to burn — and on a dedicated server lightning ignored which one had
rolled and struck at the full configured rate under both. Found live on 2026-09-18 by watching a
bolt land one log line after the mod had announced that rain suppresses lightning, while the
connected client showed the vanilla `Wet` status and visible rainfall.

- **The rain gate was asking a machine that does not have weather.** `FireSystem` gated on
  `EnvMan.IsWet()`, and on a headless dedicated server that value is not merely wrong, it is
  frozen. Two independent local-player gates in vanilla see to it:
  `RandEventSystem.GetEnvOverride()` reads `m_activeEvent`, which is only ever set on a branch
  behind `(bool)Player.m_localPlayer`; and `EnvMan.UpdateEnvironment`'s biome-roll fallback
  returns early when `Utils.GetMainCamera()` is null. Both are always true headless, so the
  server never applies the forced sky AND never rolls its own weather either — `IsWet()` stays
  at whatever `EnvSetup` `Awake` flagged as default for the whole process lifetime. That is why
  the server logged `sky is 'Clear'` under a forced `ThunderStorm` all session and it read as
  normal: it was not a stale value, it was the only value that machine will ever have.
- **The fix gates on the rolled look.** New `LightningStrike.SkyAllows(forcedSky, stormIsDry,
  envIsWet)`: with a sky forced, the rolled look is the answer, and it is right on the authority by
  construction because the authority is what rolled it. With no sky forced the storm imposes
  nothing, real weather decides, and behaviour is exactly as before. Seven tests pin it; two of
  them fail against the old code.
- **What was actually observed, and what was only possible.** Observed live, twice in two wet
  storms: a WET-rolled storm drew a bolt, because the frozen default read as not-wet. The mirror
  case — a DRY storm silently refusing to strike because that frozen default read as WET — was
  NOT observed and cannot occur on a server whose default is dry; it is reachable on a listen
  host, where `EnvMan` genuinely runs. Both directions are pinned by tests regardless, because
  the gate should not be consulting that value at all when a sky is forced.
- **The storm log line stopped reporting a sky it cannot see.** `storm began` said
  `sky is 'Clear'` while every client was in a thunderstorm. It now names whose sky the value is:
  with a forced look it reports what clients see AND says this machine's own sky is not the
  storm's; with no forced look it says so plainly.
- **FireFront is NOT affected, contrary to a first reading during the same session.** Its
  heartbeat's `raining 0/0` was misread here as a failed rain check; `RainingBurnersForStatus()`
  returns `wet + "/" + _burning.Count`, so `0/0` simply meant nothing was burning at that instant.
  FireFront already resolves the event through `RandEventSystem.GetCurrentRandomEvent()` — the
  server-authoritative `m_randomEvent`, not the local-player-gated `GetEnvOverride()` — and
  carries its own replica of the weather roll that does not depend on `Utils.GetMainCamera()`.
  It had this right before we did.

## 0.27.1

**Six fixes to the config migration 0.27.0 shipped, one of which could have changed a live world
in silence.** They were found by an adversarial review of Undertow's port of this same code, which
went looking for what the original had got wrong rather than for what it had got right. Nothing
about storms, seasons or zones changed; this is entirely about protecting the settings you already
have.

- **The migration compared config keys ignoring case; BepInEx does not.** Its `ConfigDefinition`
  is ordinal and case-sensitive, so `stormdrychance` and `StormDryChance` are two different keys to
  it — one binds, the other is ignored. A backfill's whole safety is asking whether a key is
  absent, so a config carrying one mis-cased line answered "present", SKIPPED the 0.27.0 backfill,
  quietly took the new `StormDryChance` of 0.5, and then stamped itself as migrated. Exactly the
  silent change to a running world the migration exists to prevent. Now ordinal, and pinned.
- **The version stamp could move DOWN.** Roll back to an older build for an afternoon and it
  rewrote the stamp to its own version, so rolling forward again replayed rungs that had already
  run — against values you had chosen in the meantime, which a rebase cannot tell from an old
  default. The stamp is now a high-water mark.
- **A backfill that did not land reported success.** BepInEx swallows a value it cannot parse and
  leaves the setting alone, which made the migration's own error handling unreachable code; and it
  CLAMPS an out-of-range value rather than refusing, which moves the setting to something nobody
  asked for. Both were silent, and the stamp made them permanent. The value is now read back and
  the mod says, by name, when it stored something other than what it intended.
- **A hand-edited negative `ConfigVersion` froze the boot** for eighteen seconds while the
  migration counted up from it. Treated as 0 now, which is what such a file is.
- **A key named by two migration steps was judged twice** against the same unchanged file, so one
  setting could be reported and reset once per step. The first step that matches now owns it.
- **`ConfigVersion` no longer carries an allowed range.** BepInEx clamps silently, so a ceiling
  would one day have refused the stamp and turned this into a migration that re-ran on every boot.

**`wrath status` now reports the config layout version**, and the migration's own line when that
boot migrated anything. The mod had been keeping that summary for the console since 0.27.0 and
nothing ever read it.

**Harness 324 → 343.** Ten deliberate breakages applied to the shipping source, ten caught. Two
assertions that could not fail were found and fixed: one stated the case-insensitivity that turned
out to be false, and one "a second boot does not migrate again" was passing because the test's
config file was never actually stamped, so it migrated a second time and looked identical.

- **Two more corrections, found by auditing the sibling ports (same day).**
  - **`wrath status` reported the plan's INTENT, not what happened.** The summary is written
    before a single step runs, and a rebase row naming a key this build no longer binds
    warns and moves on — so the one line a server owner reads could claim a value was moved that
    was never found, with the only contradiction a warning hundreds of log lines earlier.
    Refusals now correct the summary, and the count is per boot.
  - Ragnarok's Wrath retires no config key, so the retirement-failure gate the two sibling
    mods gained the same day has nothing here to guard and was deliberately NOT ported.
    Dead machinery reads as a feature that works.

  Harness 343 → 345, and seven mutations of the migration are each caught by a named
  assertion.

## 0.27.0

- **Valheim 1.0.7 support. This release REQUIRES it, and does not run on 0.2x.** The 1.0
  release moved several things the mod stands on, and every one of them was a clean compile
  and a dead mod in-game:
  - `World.GetWorldSavePath` was deleted. The drift store now resolves through
    `SaveSystem.GetWorldsSaveRootPath`, which is the same method rehoused — same body, same
    `/worlds_local`, and the world `.db` still sits in it, so your existing store file is
    found exactly where it always was. **No save migration, nothing to move.**
  - Zone ids became the short-backed `Vector2s`. `ZoneKey` keeps its own `int` fields, so the
    on-disk zone format is byte-for-byte what it was and every zone's drift history carries
    over untouched. Four new tests pin the conversion as lossless.
  - `ZDOMan.FindSectorObjects` swapped its radius integers for a `SimulationDistance`. The
    homestead scan behind `StormAvoidBaseMeters` now passes the value that reproduces the old
    3x3 sweep exactly, so storms hold and break on precisely the same ground as before.
  - `SpawnSystem.GetNrOfInstances(GameObject)` was removed; the verbose war census now calls
    the ranged overload with the same arguments the deleted one used internally.
  - **The one that would have taken the whole mod down at boot:** 1.0.7 deleted the
    `levelUpMultiplier` parameter that Empower's Harmony patch bound to. A prefix declaring a
    parameter its target no longer has does not fail quietly — Harmony throws while patching.
    The hook moved to `SpawnSystem.GetLevelUpChance`, which is where 1.0 hoisted that
    calculation, and it is a better fit: multiplying the returned chance is exactly what the
    old argument did, only `SpawnSystem.Spawn` calls that overload so fixed spawners are still
    counted once, and it is now a result-decorating postfix at default priority.
- **Every compiler-invisible dependency on the game is now verified, not assumed.**
  `tools\apiprobe` resolves all 50 Harmony patch targets, reflection lookups and
  private-member accesses across this mod and FireFront against the real 1.0.7 assemblies.
  Run it after any Valheim update; a clean build proves nothing about any of them.
- **Requires FireFront 0.20.0 or newer**, and says so at boot if it finds an older one. On
  1.0.7 an older FireFront cannot resolve its own save path, and the symptom of that shows up
  on this side of the bridge as scorch that looks broken.
- No gameplay, balance or config change. Same defaults, same simulation, same numbers.

## 0.26.1

- **The `StormsForceWeather` warning was wrong about Seasonality, and is now an
  information line.** Turning the storm look on used to log a warning that it
  "conflicts with Seasonality and any other weather mod" and should be off unless you
  run none. Verified in-game with Seasonality 3.8.0 installed, and by reading both
  sides: the storm sky goes through vanilla's own event override, which the engine
  consults *before* the biome weather list, and Seasonality only rewrites that list.
  The two coexist; the forced sky shows for the storm's duration and Seasonality's
  returns when it ends. The boot line and the setting's description now say so, and
  keep one honest caveat: a weather mod that patches the override path itself may
  still win. Also spelled out where it was missing: the value must match on every
  client and is read at game launch, so a reconnect after editing it changes nothing.
- No gameplay or simulation change. Same defaults, same storms.

## 0.26.0

- **Storms stop crying wolf over homesteads.** A Devastating Storm anchors on a
  player — and when that player was safe at their base, everyone still got the big
  "A devastating storm gathers." announcement for a storm that couldn't deliver:
  lightning refuses to strike near anything player-built, and cleared base ground
  gives fire nothing to eat. Storms now anchor only on players out in the **wild**
  (farther than `StormAvoidBaseMeters`, default 30, from anything with a builder
  stamp). If everyone online is behind their own walls, an overdue storm quietly
  holds — and breaks the moment somebody steps outside. Set the new option to 0 for
  the old behavior.

## 0.25.0

- **The season now reaches clients.** It never did: `SeasonSystem` only ever ran on the
  simulation authority, so on a dedicated server every connected client believed it was
  spring for the whole session, forever. Nothing in this mod looked wrong — every
  gameplay consumer of the season (fire risk, plague growth, farming yield, frost)
  already lived on the server — but `wrath status` typed on a client reported spring in
  midwinter, and any *other* mod asking us what season it is got the same wrong answer
  on the machine where it mattered. The server now broadcasts the season to everyone on
  its own ten-second cadence: absolute, unconditional, four bytes, so a player who joins
  mid-session is right within a tick and a dropped packet heals itself.
- `wrath status` names where the season came from, on both sides. On a client that is
  the only way to tell a working sync from none at all, because "spring" is equally what
  you see when nothing has ever told you anything.
- No behaviour changes on a single-player world or a listen host, where the authority and
  the player were always the same process.

## 0.24.0

- **Seasons (shudnal) is now a recognised season source.** Until now only Seasonality
  (RustyMods) was detected; anyone running shudnal's Seasons got a second, disagreeing
  season clock from us driving fire risk, plague growth and farming yield. We now defer
  to whichever of the two is installed (they declare themselves mutually incompatible,
  so it is never both) and run our own clock only when neither is present. shudnal's
  season is read by reflection from its own state — not its global keys, which are off
  by default and renameable — and, as always, consumed as gameplay state only: their
  mod owns everything you see.

## 0.23.1

- Config guidance from the first live lightning session: the storm-look setting's
  default environment (`ThunderStorm`) is rainy, and rain rightly suppresses lightning
  fires — so look and bolts were mutually exclusive. Both descriptions now point at
  vanilla's dry storm (`Eikthyr`) for owners who want the sky *and* the fire. No
  behavior changed.

## 0.23.0

- **Storms strike.** A Devastating Storm over your head can now land a bolt of
  lightning nearby — and the fire it starts is real, handled entirely by FireFront's
  own simulation. Bolts are rare (about one storm in three at defaults), only ever
  land near an online player, never fall in rain, and never within 30m of anything
  player-built: the storm menaces the wild, not the homestead. Lightning fires are
  natural — nobody is booked for the sky's work. Everything is config: the rate, the
  landing ring, the standoff, or off entirely.
- **FireFront is now a listed dependency.** Mod managers install the pair together,
  so the fire half of the world can no longer be silently missing. Installing by
  hand still works both ways — without FireFront the fire systems just sleep — and
  the log now tattles at boot if your FireFront is too old for what this version
  expects (0.17.2 for fire memory, 0.17.3 for arson attribution).

## 0.22.1 – 0.22.3

- **Zone announcements actually arrive now.** Two lifelong delivery bugs stacked: a
  dedicated server could never reach remote players with zone-local lines, and the
  distance check lost anyone standing on high ground. Every zone-local message —
  war resolutions, consecrations, contest flips — now routes to each player properly
  and measures distance flat. If you never saw a centre-screen line before, this is
  why.

## 0.22.0

- **Plagues begin on their own.** Until now every outbreak needed an admin's hand —
  which meant a fresh public world could run forever without the blight arc ever
  starting. Genesis fixes that: every so often (about twice a day of played time by
  default, config-tunable) sickness quietly takes root in ground players actually
  touch — likelier on corrupted or burnt land, carried by storms. The seed is
  invisible until it grows; outbreaks are discovered, never announced. All the old
  containment rules still hold, and the old admin instruments still work.
- **Version mismatches are loud now.** A client running a different mod version than
  the server gets one warning — log and corner message — instead of features silently
  not showing. Mismatched pairs still fail safe; now they also fail audibly.
- README: keep the config identical on server and clients — several client-side gates
  read local values.

## 0.21.0

- **`wrath` from the comfort of F5.** Mutations typed in-game now forward to the server
  through vanilla's own remote-command pipe: the server checks you against
  adminlist.txt, refuses everyone else with "You are not admin", logs the admin and the
  exact line, and runs the command where the stores live. Reads still answer locally;
  confirm a forwarded edit with `wrath zone <x> <y>` a sync later.

## 0.20.1

- The farming boot line stopped promising what 0.20.0 already delivered.

## 0.20.0

- **Tired fields grow slow.** Fertility depletion — written by the farming sweep since
  0.6.0 — is finally felt: crops in depleted soil take longer to grow, up to double on
  fully exhausted ground (linear, configurable, off at 1). Resting a field now
  genuinely pays, closing the loop the depletion writer opened thirteen versions ago.
  Wild trees and bushes owe farmland's memory nothing — only the crop list pays.

## 0.19.0

- **The `wrath` console.** `wrath status`, `wrath zone [x y]`, `wrath zone set`,
  `wrath care/harm set`, `wrath relics`, `wrath save`. Reads answer everywhere (a pure
  client sees the synced view); mutations run only where the stores live — the server's
  own console or a listen host. Zone edits stamp fresh contact automatically, so the
  credit-on-contact backlog can never again eat a staged value before it is measured.
  Retires the stop-edit-copy-restart dance that one verification day performed five times.

## 0.18.0

- **Runes on the stones.** Standing relics now wear their nordic design: fehu, algiz,
  gebo and thurisaz rise slowly around every consecrated stone — gold where the ground
  is blessed, a dull red where it is cursed. Drawn in code, stroke by stroke, like
  everything else this mod renders: no assets, no textures touched, the fourth emitter
  on the same template as the fog, the frost and the ash.

## 0.17.2

- Arm the relic wire where a SOLO client actually runs (ConsequenceEffects' loop, the
  ZoneSync pattern) — the nameplate path only fires rendering someone else's plate.

## 0.17.1

- The stone that never rose: 0.17.0's first consecration was recorded perfectly and its
  placement request vanished into a client that had never armed its handler — pure
  clients tick no world systems, the nameplate-patch lesson, relearned. Clients now arm
  the relic wire on the render path, and placement is fire-and-forget no longer: the
  server retries until a client confirms the stone stands, the confirmation is
  persisted, and asking twice never builds twice. A 0.17.0 ledger row loads unconfirmed,
  so the lost first monument raises itself on the next visit.

## 0.17.0

- **Consecrated places.** The world now writes its own monuments: where a story
  completes, a stone rises. A great fire fully healed, a plague driven through zero —
  blessed. A spawn war resolved — blessed if the wild took the ground back, cursed if
  the blight claimed it. And rarest of all, the land recovering from Stricken raises
  one stone on the ground whose healing defined the era.
- Blessed ground heals quicker and sheds sickness faster while you stand on it; cursed
  ground sulks, sickens you faster, and breeds slightly meaner things. The stone speaks
  once to whoever arrives — and it can be broken, which lifts the aura, but the land
  books the vandal into its ledger. A desecrated site can earn a new stone the next
  time its story peaks and completes.

## 0.16.0

- **The nemesis.** The creature that kills you is marked in that moment: it climbs a
  level (up to two stars) and its nameplate remembers — *slayer of Nomad*, counted on
  repeat offenses. The mark lives in the creature's own body and travels with the world
  save; a nemesis that despawns got away. Kill it for the only cure.

## 0.15.3

- **The wild side's design is settled: refill pressure.** On contested ground the war
  keeps the wildlife topped up and keen — spawn chance 100% and a widened attempt gate
  (now the defaults) — but it deliberately never crowds a population past vanilla's own
  caps. The engine's pheromone override widens the gate, not the budget; we ship what
  the engine honestly supports rather than bolting on a second spawner.

## 0.15.2

- War census (verbose only): every minute on contested ground, log how many of each
  horn target are loaded and how many stand within 200m — the exact numbers vanilla's
  spawn budget sees. Added because "no animals came" turned out to mean "six deer and
  eight boar were already here, hidden in the fog".

## 0.15.1

- War-state edge logs on both sides: the server logs every change in the contested-zone
  count, the client logs once when it first sees war underfoot. Instrument before
  guessing — the difference between "the server never computed a war" and "the wire
  lost it" should never cost a round-trip again.

## 0.15.0

- **The spawn war.** A blighted zone that people genuinely fight for — sick past the line
  AND tended past its own — becomes CONTESTED ground, and both sides answer: the blight's
  spawns come up starred at doubled odds, while the wild surges to its defenders through
  the game's own spawn-attraction machinery (more deer, boar and hare answering the war
  horn near anyone standing their ground). A Devastating Storm overhead escalates the
  whole thing.
- Wars end when one side's drift wins: the land healing past the line is the wild's
  victory, the tending fading while blight stands is the blight's — announced once, to
  whoever is there to hear it. Dangerous ground that resolves itself.

## 0.14.1

- Ash made properly visible (bigger, darker, denser, tighter) — 0.14.0's motes were
  arithmetic-invisible at real scar scorch.

## 0.14.0

- **Ash over the burn scars.** Zones the fire marked now show it: gray ash motes drift
  down over ground whose scorch runs high, thinning as the land heals — the memory of
  fire, visible, on the same clock as the healing itself. FireFront's living flames and
  its permanent dirt-paint are separate and untouched.

## 0.13.0

- **The land takes sides.** Every shaped zone now remembers who shaped it MOST: its
  dominant carer and its dominant despoiler, floor-gated so nobody wins ground they barely
  touched, hysteresis-held so the crown doesn't flap.
  - The dominant carer's presence heals the zone 25% faster, and sickness leaves them 50%
    faster on their own ground — the world's favour, earned in the ledger.
  - Holding three zones' memory earns **Warden** (care) or **Despoiler** (harm) under your
    nameplate.
  - When a zone genuinely changes hands between two rivals who both shaped it, the ground
    says so to whoever stands there — one line per actual taking; walkovers and fades pass
    in silence, and a solo world never hears this voice at all.

## 0.12.0

- **The land holds grudges.** Zones remember who hurt them (net of who tended them), and
  react to that person specifically:
  - Ground you wronged **drifts harsher under your feet** — healing up to halved, rot up
    to doubled, scaling with the grudge. Your friends walk the same ground untroubled.
  - Past a threshold, **its pickables refuse your hand** — "the land remembers what you
    did here" — while anyone else picks freely.
  - The worst offenders wear it: the **Ashbringer** title lands when any zone's grudge
    crosses the line, announced like every title.
- Grudges fade (48-hour half-life) and tending genuinely mollifies — care offsets harm
  point for point. The atonement loop is real: heal what you burned and the land forgets.
- Wire note: the zone sync now carries your personal grudge per zone (renamed RPC; server
  and clients update together, as ever).

## 0.11.1

- **Arson has a name now.** With FireFront 0.17.3+, the scorch a fire burns into each zone
  is booked as harm to whoever lit it — spread fires inherit their arsonist, natural and
  creature fires book nobody. With an older FireFront, attribution stays quietly dormant
  and fire scars accrue exactly as before. The ledger's harm column has its first writer;
  grudges come next.

## 0.11.0

- **The world keeps score (first ledger).** Every zone now remembers who tended it: planting
  crops books care to the planter (once per plant, ever), and standing by while damaged land
  heals books care to those present. Both fade over days — the world forgives on a long
  enough timeline. Nothing reads the ledger yet; grudges, contests and the spawn war build
  on it in coming releases. Stored beside the other world files, plain text, admin-editable.

## 0.10.0

- **The land pushes back.** Four consequences of drift, all reversible by curing the land,
  none of them ever touching player structures:
  - Plagued or scorched ground goes **barren** — berries, mushrooms and thistle refuse the
    hand, with a withered hover line saying why.
  - Corrupted ground breeds **starred enemies** — better odds on vanilla's own level-up
    roll, so the danger map follows the drift map. Wildlife is never starred.
  - Plagued zones **sicken wildlife** — deer and boar visibly slow, and recover on their
    own once the plague (or the animal) is gone.
  - Badly blighted soil **kills crops** — plants turn unhealthy and die at grow time,
    through the game's own machinery. Cure the land, replant, farm again.
- Each affected zone announces itself once — one line, the first time you stand in it —
  never per bush, never per deer.

## 0.9.0

- **Frost breath.** On land whose frost has drifted high, your breath fogs — a soft puff
  every few seconds, denser the colder the ground. It starts BELOW the chill threshold, so
  the land shows its cold before it bites, the way plague fogs before it sickens. Purely
  visual, procedural, local-only; a roof keeps it off you.

## 0.8.2

- **Shutdown no longer loses the last minute of sickness.** The exposure ledger saved on a
  60-second cadence but was missing from the shutdown flush, so a clean server stop or world
  exit could quietly drop up to a minute of exposure drift. Found live (it cost 0.02); the
  ledger now flushes alongside the zone store.

## 0.8.1

- **The sickness can now actually be felt.** In 0.8.0 the penalties ramped up from nothing at
  the moment a tier was announced, so "a sickness takes root in you" came with a 2% stamina
  penalty — invisible in the hands. Crossing a tier now bites at once (stamina ×0.85 the
  instant it takes hold, deepening as exposure climbs) and keeps ramping from there.
- **The sickness icon carries a readout.** While it is getting worse, the icon shows how far
  gone you are; once you are off plagued ground it becomes a real countdown to clean, in the
  game's own timer format — and it shortens when you are rested.

## 0.8.0

- **Plague sickness.** Standing on plagued ground now builds exposure on YOU — slowly, over
  tens of minutes. Stamina fails first, then healing; the sickness weakens but can never
  kill. It shows as a status icon (vanilla's own bar) and fades away from blighted land —
  faster rested, slower to take hold with poison resistance. Leaving and rejoining is not a
  cure: exposure is persisted per player, world-scoped, in the same admin-editable format as
  everything else.
- **The chill.** Land deep in frost now bites players the weather alone would not — unless
  they carry frost resistance, stand by a fire, or shelter. Never lethal, never Freezing.

## 0.7.1

First packaged build. Everything below verified live on a dedicated server.

- **Zone sync + plague miasma.** The server now pushes each player the zone state around
  them (absolute snapshots, self-healing on packet loss), and clients render a procedural
  grey-green fog in zones past the plague threshold. Fresh seeds stay invisible — the fog is
  how you *discover* a zone has turned, not a minimap.
- **Titles.** Stormrider, Plaguewalker, Winterborn — earned from world events, shown under
  nameplates, persisted world-scoped in an admin-editable ledger. Announced once, never spam.
- **World condition.** The land judges itself from total burden (Flourishing / Stable /
  Ailing / Stricken) and announces only real turning points — transitions are
  hysteresis-guarded so they cannot flap.
- **Ecology.** Sustained plague or scorch corrupts the ground under it, and corruption feeds
  plague growth — the first feedback loop. Severable by config.
- **Farming.** Dense crops tire a zone's soil over time; rest heals it. (Growth/yield effects
  read from this in a future build.)
- **Plague.** Grows where players linger, spreads zone-to-zone along its frontier, rides
  storms, dies back through winter or neglect; a cure that reaches zero is permanent.
- **Fire scars.** With FireFront installed, burning zones accrue Scorch that outlives the
  flames and slows the land's recovery. Without it, the system sleeps.
- **Devastating Storms.** Real vanilla events on our schedule; fire risk, wind and plague
  spread rise inside the storm's area only. The sky is never forced (default).
- **Seasons.** Gameplay-only clock with Seasonality auto-deferral.
- **Persistence.** World-scoped, atomic, fail-safe stores: corrupt files quarantine loudly
  instead of vanishing silently, values clamp on read as well as write, and everything is
  plain tab-separated text.
