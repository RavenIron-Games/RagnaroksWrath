<!-- README schema v2 -->
# ⛈️ Ragnarok's Wrath

![RAGNAROK'S WRATH](https://img.shields.io/badge/RAGNAROK%27S_WRATH-THE_LAND_REMEMBERS-c9a227?style=for-the-badge&labelColor=1b1b1b)
![VERSION](https://img.shields.io/badge/VERSION-0.30.0-8b1e1e?style=for-the-badge&labelColor=1b1b1b)
![CLIENT & SERVER](https://img.shields.io/badge/CLIENT_%26_SERVER-ONE_DLL_ON_BOTH-1f4e79?style=for-the-badge&labelColor=1b1b1b)

> *The land remembers who walked it.*

### The world reacts, and it remembers.

Seasons turn, storms rage, plague takes root and creeps, fire leaves scars, farmed soil tires, the wild wages war on the blight, and the land holds grudges against the people who hurt it, then raises stone monuments where its stories end. Every zone quietly keeps score of what has happened there, across restarts, forever.

There is no tracker, no dashboard, no HUD. The world tells you itself: a sickly fog in a blighted valley, ash drifting over an old burn scar, a deer that staggers, a bush that refuses your hand specifically, runes glowing on a stone that remembers a fire.

---

![NEW IN 0.30.0](https://img.shields.io/badge/NEW_IN_0.30.0-STORMS_WITH_TEETH-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**Storms you can see, hear and feel, titles you choose, and one set of rules for everyone.**

| You | It |
|---|---|
| **Get caught in a storm** | it has its own sky now, lasts ten minutes, and throws real lightning: a branching bolt, a flash, thunder that arrives late from far away. Bolts go for the tallest tree, start fires unless it rains, and hurt anyone standing too close |
| **Earn a title** | every one is kept, and `/wrath title` lets you choose which to wear |
| **Run the server** | admins start, stop and reshape storms from the console, live: their sky, length, size and lightning |
| **Change the config** | the server's settings now reach every player on their own, through ServerSync |
| **Update** | the server and every player together: a player on any other version is refused at the door |

---

![BEFORE YOU INSTALL](https://img.shields.io/badge/BEFORE_YOU_INSTALL-READ_THIS-b8860b?style=for-the-badge&labelColor=1b1b1b)

| | If… | Then… |
|---|---|---|
| 🤝 | you play on a **server** | install it on the server **and every player**: the same DLL does both jobs. Every player must run **exactly the server's version**, or the server turns them away with a message naming both. |
| 🔥 | you install **by hand** | install **FireFront** (Raven Iron) too, and keep it current. Mod managers bring it for you. Without it, fire scars, arson and storm lightning sleep; everything else runs. |
| ⚖️ | you change the **config** | change it on the server: every player gets the server's settings at login and whenever they change, and only admins can change them from in game. Each player keeps their own visual effects. |
| ☁️ | you play single player on a **Steam Cloud** world | the land's memory lives beside the local save and does not travel with the cloud: move machines and the land starts fresh. Dedicated servers and local worlds are unaffected. |
| 🧪 | you want to know **how finished it is** | every system is live and checked on a dedicated server, most rates to four decimal places against prediction, but it is an early, evolving mod. Report anything strange: the logs are chatty in all the right places. |

---

![I](https://img.shields.io/badge/I-SEASONS-c9a227?style=for-the-badge&labelColor=1b1b1b)

**Spring, Summer, Fall, Winter: they change how the world plays, never how it looks.**

| | |
|---|---|
| 🍂 **Gameplay, not paint** | plague growth, frost, farming and how fast burnt land heals all swing with the season |
| 🔄 **One clock, never two** | runs its own clock, or uses the season from **Seasonality** or **Seasons** (shudnal) when one is installed |

---

![II](https://img.shields.io/badge/II-DEVASTATING_STORMS-1f4e79?style=for-the-badge&labelColor=1b1b1b)

**Real vanilla events, with banner, music and timer, on the wrath's own schedule.**

| | |
|---|---|
| ⛈️ **Positional** | a storm has an area: plague spreads faster inside it and nowhere else |
| ⚔️ **It feeds the war** | a storm over contested ground escalates the fight beneath it |
| 🌩️ **Its own sky** | each storm rolls a rain-lashed thunderstorm or Eikthyr's dry, crackling dark, through the game's own event override; it works alongside Seasonality, and one setting hands the sky back to your weather mod |
| ⚡ **Real lightning** | with FireFront: a jagged, branching bolt from the clouds, flickering through its return strokes, a flash that lights the trees, and thunder at the speed of sound, a crack overhead or a rumble seconds after a distant flash |
| 🌲 **It finds the tallest thing** | a bolt goes for the highest tree or ridge near where it was heading, and never lands near anything player-built |
| 🔥 **It burns and it hurts** | it starts a real fire unless it rains there, and anyone within a few metres takes lightning damage and is thrown back |
| 🎛️ **Admins steer it** | `wrath storm start`, `stop`, `strike`, and `set` the sky, length, size (a radius up to 5 km) or lightning while it rages |

<details>
<summary>📖 <b>Read the saga</b> — wet storms and dry storms</summary>

Each storm rolls wet or dry. The wet sky brings rain: its bolts strike, flash and hurt just the same, but start no fire. The dry sky is dark and thundery with no rain, and it is the warning that this storm can set the land alight. `StormDryChance` decides the mix. Changing the sky takes effect at once, on every player, even in a storm already raging.

</details>

---

![III](https://img.shields.io/badge/III-PLAGUE-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

**A spreading, curable sickness that lives in the ground, and then in you.**

| | |
|---|---|
| 🦠 **It starts on its own** | every so often an outbreak quietly takes root where people live and walk, likelier on corrupted or burnt land, carried by storms |
| 🐛 **It creeps** | it grows where players linger, spreads along its frontier, feeds on corruption, rides storms, and dies back through winter or neglect |
| 🌫️ **You see the land go wrong** | a **grey-green miasma** where it runs deep: no marker, no map icon |
| 🤒 **It gets into you** | stand in it and exposure builds over tens of minutes into a real sickness, stamina first, then healing, shown as a vanilla status icon |
| 🍯 **You can shake it** | leaving drains it, rest drains it faster, poison-resist mead slows the intake. Relogging is not a cure |
| ❄️ **The cold bites too** | on high-frost ground you are chilled where vanilla would not call it cold, and your breath fogs; a campfire or frost-resist mead answers it |

---

![IV](https://img.shields.io/badge/IV-FIRE_SCARS-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**With FireFront, a fire chars the land itself.**

| | |
|---|---|
| 🔥 **Scorch** | burning zones gain scorch, which holds back recovery and outlives the flames |
| 🌋 **Ash** | grey ash drifts over old burn scars, thinning as the land heals |
| 🕯️ **Arson has a name** | FireFront tells the wrath who lit each fire, across its whole spread |

<details>
<summary>📖 <b>Read the saga</b> — why FireFront, and what if it is missing</summary>

**FireFront owns every flame.** Ragnarok's Wrath ships no second fire simulation: it reads where FireFront is burning, records the scorch, and asks FireFront to light what storm lightning strikes. FireFront is a dependency, so mod managers install the pair together.

**Without FireFront the fire systems sleep.** An older FireFront does not error either: a feature it cannot reach is simply never reached, and the log names what it could not find, at boot or the first time it looks. Keep FireFront current for all of it.

</details>

---

![V](https://img.shields.io/badge/V-LAND_THAT_KEEPS_SCORE-b8860b?style=for-the-badge&labelColor=1b1b1b)

**Sick land acts like it. Tired land grows slow.**

| | |
|---|---|
| 🌱 **Tired fields** | fertility tires under dense crops, and crops in depleted soil take up to twice as long, so resting a field genuinely pays |
| ☠️ **Corruption** | blighted and burnt ground corrupts, and corruption feeds the plague |
| 🫐 **Barren ground** | berry bushes and mushrooms stop yielding on plagued or burnt ground |
| 🦌 **Sick wildlife** | animals stagger visibly sick |
| 🥀 **Withering crops** | crops in deeply blighted soil wither and die |
| ⭐ **Meaner spawns** | creatures born on corrupted ground come up **starred** |
| 🌍 **The world's condition** | Flourishing, Stable, Ailing, Stricken: the world judges itself and announces the turning points to everyone |

Player structures are never touched.

---

![VI](https://img.shields.io/badge/VI-THE_LAND_TAKES_SIDES-5b2c83?style=for-the-badge&labelColor=1b1b1b)

**Every act is remembered, per zone, per player.**

| | |
|---|---|
| 🌿 **Care** | planting and a healing presence book care; tended ground heals faster for its keeper and sheds their sickness sooner |
| 🔥 **Harm** | arson books harm to the arsonist by name |
| 😠 **Grudges** | grudged ground drifts harsher under your feet, refuses you berries it gives freely to others, and marks you **Ashbringer** |
| 🏅 **Standing** | regional standing earns **Warden** or **Despoiler** |
| ⚔️ **Spawn wars** | where blight and genuine care collide, the zone goes to war: the blight's spawns star harder, the wild pours in to answer, storms escalate it, and it ends one way or the other with a single line to whoever stands there |

---

![VII](https://img.shields.io/badge/VII-THE_NEMESIS-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**The creature that kills you is marked in that moment.**

| | |
|---|---|
| ⭐ **It grows** | it gains a star, and its nameplate remembers: *slayer of Nomad*, counted on repeat offences |
| 👑 **Bosses too, fairly** | a boss is marked but never gains a star: its health bar carries the name, and the fight stays the size it was built to be |
| 💾 **It remembers** | the mark lives in the creature's own body and survives server restarts. If it despawns, it got away |
| 🗡️ **The only cure** | kill it |

---

![VIII](https://img.shields.io/badge/VIII-RELICS-c9a227?style=for-the-badge&labelColor=1b1b1b)

**The world writes its own monuments.**

| | |
|---|---|
| ✨ **Blessed stones** | a great fire fully healed, a plague driven out: blessed ground heals quicker and sheds sickness faster |
| 🩸 **Cursed stones** | a war lost to the blight: cursed ground sickens you and breeds meaner things |
| 🗿 **The rare one** | the land recovering from Stricken raises one rare stone on the ground that healed the most |
| ᚱ **Glowing runes** | standing stones wear runes, gold or blood-red |
| 🔨 **Vandals** | the stones can be broken, but the land books the vandal, and it does not forget |

---

![IX](https://img.shields.io/badge/IX-TITLES-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

**Earned from what happened to you, never from tracked stats.**

| | |
|---|---|
| ⛈️ **Stormrider** · ☣️ **Plaguewalker** · ❄️ **Winterborn** | caught in a storm, stood in badly plagued ground, lived through Winter |
| 🔥 **Ashbringer** · 🌿 **Warden** · 💀 **Despoiler** | lasting harm or care to the land |

Every title you earn is kept, and you choose which one shows under your nameplate: type `/wrath title` in chat to see yours, `/wrath title <name>` to wear one, `/wrath title auto` to wear each new one as you earn it (the default), or `/wrath title none`.

---

![THE TOOLBOX](https://img.shields.io/badge/THE_TOOLBOX-QUICK_START_%C2%B7_COMMANDS_%C2%B7_HOW--TO-1b1b1b?style=for-the-badge&labelColor=8b1e1e)

**First ten minutes:** install it with your mod manager on the server and every player (FireFront comes with it) → play; there is nothing to press → type `wrath status` in the console to see the world's state → walk into a fog and feel it.

| Command | | Where it runs |
|---|---|---|
| `wrath status` | the world at a glance | anywhere |
| `wrath zone [x y]` | one zone's full story, underfoot when you leave out x and y | anywhere |
| `wrath relics` | peaks, stones, pending, era | anywhere |
| `wrath zone set x y field value` | set a zone's `fert`, `corr`, `scorch`, `frost` or `plague` | on the server (admin) |
| `wrath care set x y playerId value` · `wrath harm set x y playerId value` | set one player's care or harm in a zone | on the server (admin) |
| `wrath save` | write every store now | on the server (admin) |
| `wrath title [name · auto · none]` | the titles you have earned, and which one you wear | anyone, for their own title (`/wrath title` in chat) |
| `wrath storm` | the storm settings in effect | anywhere |
| `wrath storm start [dry · wet] [x z]` · `wrath storm stop` | a storm now, where you stand, or end the one raging | on the server (admin) |
| `wrath storm strike` | one lightning bolt now, near a player under the storm | on the server (admin) |
| `wrath storm set name value` | `sky`, `duration`, `size`, `wetsky`, `drysky`, `drychance`, `lightning`, `lightningon`: saved, and live at once | on the server (admin) |

Typed in game by an **admin**, the server commands go through vanilla's own admin gate, run where the world lives, and reply to your screen. Chat works too: start the line with `/`.

<details>
<summary>🛠️ <b>Fix a zone by hand</b></summary>

Stand in the zone and type `wrath zone` to read it, then `wrath zone set x y field value` with a value from 0 to 1. Server owners never need to hand-edit a save file, though they can: every store is plain tab-separated text.

</details>

<details>
<summary>🛠️ <b>Keep players on the same rules</b></summary>

Nothing to do: ServerSync sends the server's settings to every player at login and again whenever one changes. A player's own file is never overwritten, and it comes back when they leave the server. With `LockConfiguration` on (the default), only admins on `adminlist.txt` can change the settings from in game, for instance in a configuration manager, and their change goes to the server and out to everyone. `wrath status` says where your game's settings are coming from.

</details>

---

![MULTIPLAYER](https://img.shields.io/badge/MULTIPLAYER-ONE_WORLD%2C_ONE_MEMORY-1f4e79?style=for-the-badge&labelColor=1b1b1b)

| Works with vanilla players and servers | Needs it on the server | Needs it on both ends |
|---|---|---|
| no: install it on the server and every player | the simulation and every zone's memory · storms and outbreaks · relic stones · the world's condition and announcements · the server commands | the miasma, ash, frost breath and runes · sickness and chill · tired soil · the land's consequences · grudge-refused picking · spawn wars · the nemesis · relic auras |

**Versions must match.** The server runs the simulation, and each player's game shows what it pushes and acts where that player stands. A player without the mod, or on any other version, is refused when joining, with a message naming both versions, so update the server and every player together.

---

![COMPATIBILITY](https://img.shields.io/badge/COMPATIBILITY-PLAYS_WELL_WITH_OTHERS-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

| Mod | |
|---|---|
| **FireFront** | A listed dependency, installed with it by mod managers. It unlocks fire scars, arson by name and storm lightning. Removing it is safe: those systems simply sleep. |
| **Seasonality · Seasons** (shudnal) | Found and used by themselves: their season drives the wrath, and it runs no clock of its own. |
| **Weather mods** | A storm gives itself a sky through the game's own event override, which works alongside Seasonality. If another weather mod fights it, turn `StormsForceWeather` off and the storm leaves the sky alone. |
| **AwayFromHome** | Deliberately compatible: drift never ticks on zone load state, unattended bases never burn, and sweeps are staggered off its rescan. |

The mod adds **no prefabs, no assets, no bundles**: every visual is generated in code, and the thunder and sky flash are the game's own, on purpose.

---

![SETTINGS](https://img.shields.io/badge/SETTINGS-BEPINEX_CONFIG-b8860b?style=for-the-badge&labelColor=1b1b1b)

Two files in `BepInEx/config/`. `com.raveniron.ragnarokswrath.cfg` holds every system's on/off switch and the settings most servers change; `com.raveniron.ragnarokswrath.advanced.cfg` holds the tuning (rates, thresholds, intervals and lists) under the same section names. Every setting has a plain description in the file. Edit the files with the game or server closed, or change a setting while it runs with a configuration manager or `wrath storm set`. **The server's settings are everyone's:** they reach every player through ServerSync, except the visual effects and `VerboseLogging`, which each machine keeps for itself.

<details>
<summary>🔒 <b>On the server</b> — the server's own file decides</summary>

| Section | Setting | What it does |
|---|---|---|
| 02 - Season | `EnableSeason` · `SeasonLengthDays` · `AnnounceSeasonChange` | the season clock, its length in game days, and the message when it turns |
| 03 - Weather | `EnableWeather` · `StormMinIntervalSeconds` · `StormMaxIntervalSeconds` · `StormDurationSeconds` | Devastating Storms, the shortest and longest gap between them, and how long one lasts |
| 03 - Weather | `StormDryChance` | the chance a storm rolls dry instead of wet |
| 04 - Biome state | `EnableBiomeState` | each zone's slow drift and healing |
| 01 - General | `LockConfiguration` | only admins may change the settings from in game |
| 05 - Fire | `EnableFire` · `StormLightningEnabled` · `LightningMeanMinutes` | scorch from FireFront's fires, storm lightning, and how often a bolt falls |
| 06 - Plague | `EnablePlague` · `PlagueGenesisEnabled` · `PlagueGenesisMeanHours` | the plague, outbreaks that start on their own, and how often |
| 07 - Ecology | `EnableEcology` | land corruption |
| 10 - Consequence | `AnnounceConsequences` | the one-line message when you enter sick ground |
| 11 - Rivalry | `AnnounceContests` | announce a contested zone changing hands |
| 13 - Titles | `EnableTitle` · `AnnounceTitles` | earned titles, and the message when one is earned |
| 14 - World state | `EnableWorldState` | the world's overall condition |
| 15 - Visuals | `EnableZoneSync` | send zone state to players, which the fog, frost breath and ash need |

</details>

<details>
<summary>🖥️ <b>On each player's own game</b> — the gameplay ones come from the server</summary>

**Read on each player's game, and sent there from the server:**

| Section | Settings |
|---|---|
| **03 - Weather** | `StormsForceWeather` · `StormForcedEnvironment` · `StormDryEnvironment` (a storm's own sky, wet and dry; changes at once, even mid-storm) |
| **05 - Fire** | `LightningHurtsPlayers` · `LightningDamage` · `LightningHurtRadiusMeters` (who a bolt hurts, and how badly) |
| **08 - Farming** | `EnableFarming` |
| **09 - Health** | `EnableHealth` · `FrostChillEnabled` |
| **10 - Consequence** | `EnableConsequence` · `ConsequenceBarren` · `ConsequenceEmpower` · `ConsequenceSicken` · `ConsequenceWither` |
| **11 - Rivalry** | `EnableRivalry` · `EnableNemesis` · `NemesisMaxLevel` |
| **12 - Relic** | `EnableRelic` |

**Yours alone:**

| Section | Settings |
|---|---|
| **15 - Visuals** | `PlagueFogEnabled` · `FrostBreathEnabled` · `ScorchAshEnabled` · `RelicRunesEnabled` · `LightningBoltsEnabled` |
| **01 - General** | `VerboseLogging` (a log line for every pass of every system, on whichever machine turns it on) |

</details>

---

![CREDITS](https://img.shields.io/badge/CREDITS-THANK_YOU-1b1b1b?style=for-the-badge&labelColor=c9a227)

**Needs:** [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) · **FireFront** by Raven Iron

*A world-simulation mod by Raven Iron.* Every Raven Iron mod is free, and stays free: all of it, always. Nothing is held back for patrons, and nothing ever will be. If you would like to help cover server hosting and test hardware: [ravenirongames.com](https://ravenirongames.com/) · [Patreon](https://www.patreon.com/cw/RavenIronGames) · [Discord](https://discord.gg/AGKDEurAVa), a channel per mod, and where the testing happens.
