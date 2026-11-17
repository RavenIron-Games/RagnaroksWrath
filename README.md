# Ragnarok's Wrath

> *The land remembers who walked it.*

A Valheim world-simulation mod by **Raven Iron**. The world reacts and remembers: seasons turn, storms rage, plague takes root and creeps, fire leaves scars, farmed soil tires, the wild wages war on the blight, the land holds grudges against the people who hurt it — and raises stone monuments where its stories end. Every zone quietly keeps score of what has happened there, across restarts, forever. One DLL — install on server and clients.

**Not** a tracker, dashboard, or HUD mod. There is no UI. The world tells you itself: a sickly fog in a blighted valley, ash drifting over an old burn scar, a deer that staggers, a bush that refuses your hand specifically, runes glowing on a stone that remembers a fire.

---

## What it does today

### 🍂 Seasons
Spring, Summer, Fall, Winter on a configurable clock — driving **gameplay**, never visuals: fire risk, plague growth, frost, farming yield all swing with the season. Runs its own clock, or defers automatically to **Seasonality** (RustyMods) when installed — no second clock, no conflict.

### ⛈ Devastating Storms
Real vanilla events — banner, music, timer — fired on our schedule, positional and local: plague spread rises **inside the storm's area** and nowhere else, and a storm over contested ground escalates the war beneath it. Each storm brings its own sky — a rain-lashed thunderstorm or Eikthyr's dry, crackling dark — through the game's own event override, which works alongside Seasonality (one toggle hands the sky back to your weather mod). Admins can start one where they stand, end it, and change its sky, length and size (a radius of up to 5 km) while it rages, all from the console.

**Lightning, as real as we could make it** (with FireFront): a jagged, branching bolt from the clouds, flickering through its return strokes, with a flash that lights the trees around it and thunder that arrives at the speed of sound — a crack overhead, a rumble seconds later from a distant strike. Bolts go for the tallest tree or ridge nearby, hurt and throw back anyone standing too close, and set real fires — unless it rains there, where they strike just the same but start nothing. A bolt never lands near anything player-built.

### 🦠 Plague — in the land, and in you
A spreading, curable sickness that lives in the ground — and it starts on its own: every so often (rarer than daily by default, likelier on corrupted or burnt land, carried by storms) an outbreak quietly takes root where people live and walk. It grows where players linger, creeps along its frontier, feeds on corruption, rides storms — and dies back through winter or neglect. Clients see a **grey-green miasma** where it runs deep: no marker, no map icon, just the land looking wrong.

Stand in it and it gets into **you**: exposure builds over tens of minutes into a real sickness — stamina first, then healing — shown as a vanilla status icon, never a HUD. Leaving drains it; rest drains it faster; poison-resist mead slows the intake. Relogging is not a cure. High-frost ground chills you where vanilla wouldn't — a campfire or frost-resist mead answers it, and your breath fogs in the cold air.

### 🔥 Fire scars (with FireFront)
**FireFront** (Raven Iron) ships as a dependency — mod managers install the pair together — and fires char the land itself: burning zones accrue Scorch, which suppresses recovery and outlives the flames — and gray **ash drifts** over old burn scars, thinning as the land heals. Installing by hand? You need both mods, and which FireFront you have decides how much works: **0.17.2+** for burn scars at all, **0.17.3+** to attribute arson to whoever lit it, **0.18.0+** for storm lightning to be able to set anything alight (**0.21.0+** unless the storm sky is forced, because each bolt asks FireFront whether it rains where it would land), and **1.0.2+** to blame each fire on whoever lit it when several burn at once (an older FireFront names one igniter for the whole map, so the blame stays near that player's own fire). An older FireFront does not error — the feature is simply never reachable (per-fire blame instead falls back as above) — so the log names whichever of these it cannot find, at boot or the first time it reaches for it. Without FireFront the system sleeps; Ragnarok's Wrath ships no second fire simulation.

### 🌱 Land that keeps score
Fertility tires under dense crops — and **tired fields grow slow**: crops in depleted soil take up to double the time, so resting a field genuinely pays. Blighted and burnt ground corrupts, corruption feeds the plague, and the world derives its own condition — Flourishing, Stable, Ailing, Stricken — announcing the turning points to everyone.

### 💀 Consequences
Sick land acts like it: berry bushes and mushrooms stop yielding on plagued or burnt ground, wildlife staggers visibly sick, crops in deeply blighted soil wither and die, and creatures born on corrupted ground come up **starred**. Player structures are never touched.

### ⚔️ The land takes sides
Every act is remembered, per zone, per player: planting and healing presence book **care**; arson books **harm** to the arsonist by name (FireFront identifies who lit it — even across the fire's whole spread). Grudged ground drifts harsher under your feet, refuses you berries it gives freely to others, and marks you — **Ashbringer** — while tended ground heals faster for its keeper and sheds their sickness sooner. Regional standing earns **Warden** or **Despoiler**. And where blight and genuine care collide, the zone goes to **war**: the blight's spawns star harder, the wild pours in to answer, storms escalate it, and the war ends one way or the other with a single line to whoever stands there.

### 🐗 The nemesis
The creature that kills you is marked in that moment: it gains a star, and its nameplate remembers — *slayer of Nomad*, counted on repeat offenses. A boss is marked but never gains a star: its health bar carries the name, and the fight stays the size it was built to be. The mark lives in the creature's own body and survives server restarts. If it despawns, it got away. Kill it for the only cure.

### 🗿 Relics — the world writes its own monuments
Where a story completes, a stone rises: a great fire fully healed, a plague driven out — blessed. A war lost to the blight — cursed. The land recovering from Stricken raises one rare stone on the ground that healed the most. Standing stones carry real auras (blessed ground heals quicker and sheds sickness faster; cursed ground sickens you and breeds meaner things) and wear **glowing runes** — gold or blood-red. The stones can be broken, but the land books the vandal, and it does not forget.

### 🏅 Titles
Earned from what happened to you, never from tracked stats: **Stormrider**, **Plaguewalker**, **Winterborn**, **Ashbringer**, **Warden**, **Despoiler**. Every title you earn is kept, and you choose which one shows under your nameplate: type `/wrath title` in chat to see yours, `/wrath title <name>` to wear one, `auto` to wear each new one as you earn it (the default), or `none`.

### 🛠 The `wrath` console
`wrath status`, `wrath zone`, `wrath relics`, `wrath storm` answer anywhere; `wrath zone set`, `wrath care set`, `wrath harm set`, `wrath save` run on the server — and typed in-game by an **admin**, they forward through vanilla's own admin gate, execute where the world lives, and reply to your screen. Admins also drive the weather: `wrath storm start [dry|wet]`, `wrath storm stop`, `wrath storm strike` (one bolt, now), and `wrath storm set <sky|duration|size|lightning|…> <value>`, saved to the config and live at once. `wrath title` is for every player, and only ever changes your own. Server owners never need to hand-edit a save file again (though they can: every store is plain tab-separated text).

---

## Install

Drop `RagnaroksWrath.dll` into `BepInEx/plugins/` on the **server and every client**. The same DLL does both jobs: the server runs the simulation, clients render what it pushes and act where they stand.

Config is two files in `BepInEx/config/`: `com.raveniron.ragnarokswrath.cfg` holds every gameplay system's on/off switch and the handful of settings most servers change, and `com.raveniron.ragnarokswrath.advanced.cfg` holds the tuning — every rate, threshold and aura. **[docs/CONFIG.md](docs/CONFIG.md)** walks through all of them. Upgrading from 0.27.x moves your settings over on the first start, values kept, and backs the old file up beside them.

**The server's settings are every player's settings** (ServerSync). Many gameplay settings act on each player's own game — sickness and chill, the exposure tiers, tired soil and the crop list, every land consequence, grudges and spawn wars, the nemesis, relic stones, lightning damage — so the server sends its own values to every player at login and whenever they change. A player's own file is never overwritten; it comes back when they leave. With `LockConfiguration` on (the default) only admins can change the settings from in game, and their change goes out to everyone. The visual effects stay each player's own choice.

**Every player must run exactly the server's version.** A player without Ragnarok's Wrath, or on any other version, is refused at the door with a message naming both versions — update the server and every player together.

## Plays well with

- **Seasonality** — detected and deferred to automatically (GUID verified against their source)
- **FireFront** — a listed dependency (installed automatically by mod managers); unlocks fire scarring, arson attribution, and storm lightning. Removing it is safe: those systems simply sleep.
- **AwayFromHome** — deliberately compatible: drift never ticks on zone load state, unattended bases never burn, and sweeps are staggered off its rescan

## Known limitations

- On a **Steam Cloud** singleplayer world, the mod's world-state files live beside the local save and do not travel with the cloud — move machines and the land starts a fresh memory. Dedicated servers and local worlds are unaffected.
- The mod adds **no prefabs, no assets, no bundles** — every visual is generated in code, and the thunder and sky flash are the game's own. This is deliberate and load-bearing; it will not change.

## Early build

Every system is live and dedicated-server-verified — most rates to four decimal places against prediction — but this is an early, evolving mod. Report anything strange: the logs are chatty in all the right places.

---

## Support Raven Iron

Every Raven Iron mod is free, and stays free — all of it, always. Nothing is held
back for patrons, and nothing ever will be.

If you'd like to help cover server hosting and test hardware:

- **Website** — <https://ravenirongames.com>
- **Patreon** — <https://www.patreon.com/cw/RavenIronGames>
- **Discord** — <https://discord.gg/AGKDEurAVa> — a channel per mod, and where the
  testing happens
