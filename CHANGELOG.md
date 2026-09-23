# Changelog

## 0.27.3

**A boss that kills you keeps the story and never gains a level.** The nemesis mark stars up the
creature that killed you — and nothing ever excluded bosses from that. A Queen who killed the same
player twice reached `NemesisMaxLevel` 3 and, in the owner's words on 2026-09-23, was *almost
unbeatable*. They beat her anyway; the next owner might not.

- **Why a level is catastrophic on a boss specifically.** Vanilla scales health LINEARLY —
  `SetLevel` → `SetupMaxHealth` → `SetMaxHealth(GetMaxHealthBase() * level)` — so level 3 is three
  times a health pool that was already the largest in the game, and per-level attack damage lands
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
  the test pinning non-demotion now says so.
- **Not changed, deliberately: tamed creatures.** They looked like the same class of bug and are
  not — vanilla's `MonsterAI.SetTarget` structurally refuses to let a tame target the player who
  damaged it (`!attacker.IsPlayer() || !m_character.IsTamed()`), so an `IsTamed()` guard would be
  code defending against something the engine already prevents.
- **Not changed: the other `SetLevel`.** `Patch_Consequence`'s `CreatureSpawner.Spawn` postfix is
  the nest and bone-pile path, already gated on `m_maxLevel < 2`, and boss altars never go through
  it. Checked rather than assumed, because fixing one of two identical hazards is how this returns.
- **Verified in-game on a dedicated server before release.** Eikthyr killed the same player twice
  and stayed at level 1 while his boss health bar read `slayer of TestNomad x2`; a greydwarf killed
  them twice and climbed 1 → 2 → 3, stopping at the cap. The greydwarf is the half that matters —
  a guard that suppressed every level-up would make the boss row look identical.

**Also in this release: the DLL no longer carries the build machine's folders.** Every build
through 0.27.2 embedded an absolute path to its debug symbols in the DLL, and that path included
the user name of the machine it was built on. The build now maps the repository root to a neutral
prefix, so neither the DLL nor its symbols name any local folder. The compiled code is unchanged.
A side effect worth knowing if you compare binaries: the DLL's contents now follow the commit it
was built from rather than the folder it was checked out into, so two clean builds of the same
commit match. The 0.27.3 DLL is built from the commit that adds this entry.

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
    warns and moves on — so the one line the owner reads could claim a value was moved that
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
