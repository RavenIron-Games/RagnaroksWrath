# Configuring Ragnarok's Wrath

Ragnarok's Wrath keeps its settings in two files in `BepInEx/config/`:

- **`com.raveniron.ragnarokswrath.cfg`** holds every gameplay system's on/off switch and the few settings most servers actually change: storm timing and look, storm lightning, how often outbreaks start, the nemesis, announcements, and the visual effects.
- **`com.raveniron.ragnarokswrath.advanced.cfg`** holds the tuning: rates, thresholds, intervals and lists. Each setting sits under the same section name as its switch in the main file.

Every setting ships with a working default, and most servers never need to open the advanced file. Close the game or server before editing either file; changes take effect the next time it starts.

### Server, players, or both

The world simulation runs on the server, so the server's files decide nearly everything. Many gameplay settings act on each player's own game, because that is where the effect happens: sickness and chill, the exposure tiers, tired soil and the crop list, every land consequence with its thresholds and the wildlife list, grudge-refused picking, the wild side of a spawn war, the nemesis, and relic stones. **Give every player the same values the server has for those**, or players on the same world will see different rules. They are marked below as *read on each player's own game*.

The visual effects (plague fog, frost breath, scorch ash, relic runes) are the opposite: each player's own choice, affecting only their screen.

### Upgrading from 0.27.x or older

The first start of 0.28.0 moves every setting into this layout and keeps its value. The old single file is backed up beside the new one as `com.raveniron.ragnarokswrath.cfg.v1.bak` (`.v0.bak` if it was last written by 0.27.0 or earlier, before config files recorded a layout version), and the log says what moved. Two settings that never did anything are removed: `StormFireRiskMultiplier` and `StormWindMultiplier`. `wrath status` shows which layout version the files are at.

If you ever go back to an older version of the mod, restore that backup first: older versions cannot read the new layout and would start from their defaults.

## Sections

- [01 - General](#01---general)
- [02 - Season](#02---season)
- [03 - Weather](#03---weather)
- [04 - Biome state](#04---biome-state)
- [05 - Fire](#05---fire)
- [06 - Plague](#06---plague)
- [07 - Ecology](#07---ecology)
- [08 - Farming](#08---farming)
- [09 - Health](#09---health)
- [10 - Consequence](#10---consequence)
- [11 - Rivalry](#11---rivalry)
- [12 - Relic](#12---relic)
- [13 - Titles](#13---titles)
- [14 - World state](#14---world-state)
- [15 - Visuals](#15---visuals)

## 01 - General

Logging and housekeeping. Only `VerboseLogging` is worth touching on most servers: turn it on to watch every system work, then off again.

In `com.raveniron.ragnarokswrath.cfg`:

- **`VerboseLogging`** (on/off, default `false`): Prints a detailed log line for every pass of every system, instead of just summaries. Turn it on to see the mod actually working, or while chasing down a problem.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`TickBudgetMs`** (number, default `2`, 0.25 to 16): Milliseconds of work this mod may do each frame, across every system combined. Raise it only if systems visibly fall behind on a server with room to spare. Work that doesn't fit in the budget carries over to the next frame rather than being skipped, so drift simply runs a little behind schedule rather than losing time. Most servers never need to change this.
- **`MaxCreditSeconds`** (number, default `86400`, 60 to 2592000): Caps how much real time, in seconds, a zone can catch up on drift the moment someone visits it. Stops a zone left alone for months from getting months of built-up change all at once. Applies to every kind of drift this mod tracks per zone - plague, fire scorch, corruption and the rest - each time a zone goes from unvisited to visited.
- **`AutosaveIntervalSeconds`** (number, default `120`, 0 to 3600): Seconds between writes of the world's drift data to disk. A save always happens on shutdown regardless, so this only limits how much a crash could lose. Set to 0 to turn off the periodic writes. Writing is skipped whenever nothing has actually changed, so a short interval costs nothing extra on a quiet server.
- **`MessageMinIntervalSeconds`** (number, default `8`, 0 to 300): Minimum seconds between the on-screen messages this mod shows, so a burst of nearby events cannot spam the screen at once. Doesn't apply to server-wide announcements such as a storm arriving or a season changing. Each player has their own timer, so one player's messages never hold back another's. A message to the same player sooner than this after their last one is dropped, not delayed.

## 02 - Season

The season clock. Seasons here are gameplay state only: they shape fire, plague, frost and farming, and never change what the world looks like. With Seasonality or Seasons (shudnal) installed, their season is used and this mod runs no clock of its own.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableSeason`** (on/off, default `true`): Turns season tracking on or off. The season it tracks feeds fire risk, plague growth, farming yield and frost buildup elsewhere in this mod.
- **`SeasonLengthDays`** (whole number, default `7`, 1 to 120): In-game days per season, when this mod is running its own season clock. Ignored completely if Seasonality or Seasons (shudnal) is installed - their season is used instead.
- **`AnnounceSeasonChange`** (on/off, default `true`): Shows an on-screen message when the season changes. Automatically skipped if Seasonality or Seasons (shudnal) is installed, since they already show the player the season.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`SeasonIntervalSeconds`** (number, default `10`, 1 to 300): Seconds between season checks, and how often the server tells clients the current season. A player who joins mid-session is right within one of these.

## 03 - Weather

Devastating Storms: real vanilla events with a banner, music and timer, fired on this mod's schedule, that raise plague spread inside their area. By default a storm leaves the sky to the game or your weather mod; `StormsForceWeather` gives it a storm sky of its own. The wind settings in the advanced file change nothing yet: the mod reads the wind, and no system uses it.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableWeather`** (on/off, default `true`): Turns weather tracking and Devastating Storms on or off. A storm brings an on-screen banner, gameplay effects across its area, and a chance of lightning.
- **`StormMinIntervalSeconds`** (number, default `3600`, 60 to 86400): Shortest real-world gap allowed between storms, in seconds. No new storm can begin until at least this long has passed since the last one ended.
- **`StormMaxIntervalSeconds`** (number, default `10800`, 120 to 172800): Longest real-world gap between storms, in seconds. The chance of a storm starting climbs from zero at the minimum gap to certain by this point.
- **`StormDurationSeconds`** (number, default `300`, 30 to 3600): How long a Devastating Storm lasts once it starts, in game seconds. It runs its full course and ends on schedule whether or not any player is nearby. Only pausing the whole game in singleplayer stops this clock, the same as it stops every other clock in the game.
- **`StormsForceWeather`** (on/off, default `false`): Gives a storm its own stormy sky instead of the world's weather. Left off, this mod never touches the sky, avoiding a fight with another weather mod. Every player needs the same value and a full restart to see it. The sky comes from StormForcedEnvironment (wet) or StormDryEnvironment (dry) for the length of the storm, through the same mechanism vanilla boss events use. Verified to work alongside Seasonality without conflict, though a different weather mod that forces the sky itself may still win. The sky is chosen when Valheim starts, so reconnecting after changing this does nothing - everyone needs to fully restart the game.
- **`StormForcedEnvironment`** (text, default `ThunderStorm`): The sky shown during a storm that rolls wet, used only when StormsForceWeather is on. ThunderStorm is rainy, and rain stops storm lightning from striking.
- **`StormDryEnvironment`** (text, default `Eikthyr`): The sky shown during a storm that rolls dry, used only when StormsForceWeather is on. Eikthyr is dark and thundery with no rain, so lightning can strike. Set this to the same value as StormForcedEnvironment if you would rather every storm looked the same again.
- **`StormDryChance`** (number, default `0.5`, 0 to 1): Chance that a storm rolls dry rather than wet, decided once when it starts. 0 makes every storm wet and 1 makes every storm dry, with values in between giving each a proportional chance. Only visible when StormsForceWeather is on - with it off, both kinds run under the world's real sky and this roll changes nothing anyone can see. A dry sky is the warning that a storm can start fires.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`EnableWind`** (on/off, default `true`): Turns wind tracking on or off. It reads the game's own wind for other systems to use later - nothing currently changes if you turn it off. Wind is read and cached here for future gameplay use, such as directional fire spread; today nothing reads it, so it changes nothing in the world.
- **`WeatherIntervalSeconds`** (number, default `5`, 1 to 60): Seconds between weather checks - how quickly a storm's start or end is noticed. Kept low on purpose, since this only reads state rather than computing anything heavy.
- **`StormRangeMeters`** (number, default `96`, 32 to 1024): Radius of a storm's effect, in metres. The on-screen banner and every gameplay effect of the storm use this same distance, so they always agree on where it reaches.
- **`StormPlagueSpreadMultiplier`** (number, default `1.5`, 0 to 10): How much faster plague spreads inside a Devastating Storm. 1.5 means plague spreads 50% faster within the storm's range than it does outside it.
- **`StormAvoidBaseMeters`** (number, default `30`, 0 to 64): Storms will not anchor within this many metres of anything player-built. If every online player is that close to their base, the storm holds off until someone steps into the wild. Set to 0 to turn this off and let storms anchor anywhere, including right on top of a base.
- **`WindIntervalSeconds`** (number, default `5`, 1 to 60): Seconds between wind readings taken from the game. Wind is only ever read here, never changed.

## 04 - Biome state

The slow memory of each area: frost builds up in cold seasons, and damage heals back toward normal. Drift only runs where players are, both ways, so an area nobody visits neither worsens nor recovers.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableBiomeState`** (on/off, default `true`): Turns biome state on or off — the zone-by-zone fertility, corruption, plague, scorch and frost that slowly change and heal where players go. Fire raises scorch, and the plague system below starts outbreaks and spreads them into new zones; this system does the everyday drift on top of that — including growing plague already present, using the settings under Plague, as well as healing every field. Frost also builds on its own in cold seasons and thaws in warm ones, with no event needed to start it.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`BiomeStateIntervalSeconds`** (number, default `30`, 5 to 600): How often, in seconds, zone fertility, corruption, scorch and frost are recalculated. Lower reacts to players faster; higher costs less. If you also run AwayFromHome, avoid a value that is a multiple of 60 seconds — its own scan runs every 60 seconds by default, and two heavy passes landing together can cause a stutter neither mod alone explains.
- **`BiomeContactRadiusZones`** (whole number, default `1`, 0 to 3): How many zones around each player count as their presence, for drift and plague spread. 0 is only their own zone; 1 covers the surrounding 3x3. Raising this multiplies the number of zones processed per player each pass — lower it first if a busy server needs to save performance.
- **`BiomeMaxZonesPerTick`** (whole number, default `64`, 1 to 1024): Most zones updated in a single drift pass. Anything left over continues on the next pass, so nothing is skipped, only delayed. This is a safety limit for a busy server with many players; work already scales with players present rather than world size, so it rarely needs changing.
- **`BiomeRecoveryPerHour`** (number, default `0.02`, 0 to 1): How fast fertility, corruption, plague, scorch and frost heal per hour, on a 0 to 1 scale. At the default, fully damaged land heals in about 50 hours. Healing is worked out from the real time since a zone was last visited and applied when a player returns to it, so a zone nobody revisits keeps its damage waiting rather than healing on its own.
- **`BiomeFrostPressurePerHour`** (number, default `0.015`, 0 to 1): How fast frost builds up per hour in cold seasons, before the season's own cold multiplier. Set to 0 to stop frost building at all.

## 05 - Fire

The bridge to FireFront, which owns every flame. This mod reads FireFront's fires and turns them into lasting scorch on the land, and during a storm it can call a bolt of lightning that FireFront then ignites. Without FireFront installed, this section does nothing.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableFire`** (on/off, default `true`): Turns fire memory on or off — burned zones gain scorch that fades over time. Needs FireFront installed; does nothing without it. FireFront, a separate mod, simulates the fire itself; this only reads where it is burning and records the scorch, which heals on its own over time. It also gates the storm lightning below, since that needs FireFront too.
- **`StormLightningEnabled`** (on/off, default `true`): Turns storm lightning on or off: a rare bolt during a Devastating Storm that can start a fire nearby, never in rain. Needs FireFront installed with its own fire spread enabled, or nothing happens. A bolt only ever lands near an online player, never within the homestead standoff below, and never where it is raining — including a storm forced to look rainy. If storms are forced to a wet look, set a dry environment there instead, or lightning will never strike.
- **`LightningMeanMinutes`** (number, default `15`, 1 to 600): Average minutes between lightning bolts while a storm holds at least one player under a dry sky. Higher makes strikes rarer. Against the default 5-minute storm duration, the default here means roughly one storm in three produces a bolt — storms are meant to threaten fire, not guarantee it.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`FireScorchIntervalSeconds`** (number, default `10`, 2 to 120): How often, in seconds, burning zones gain scorch. Only matters while FireFront is installed.
- **`FireScorchPerMinute`** (number, default `0.02`, 0 to 1): Scorch added per minute to a zone with any fire burning in it — the same rate whether one fire burns there or several. At the default, continuous burning fully chars a zone in about 50 minutes. This does not scale with the number of fires — a bigger fire already covers more zones, so counting fires too would double the effect. Scorch heals on its own over time, more slowly than it builds here.
- **`LightningRingMinMeters`** (number, default `15`, 0 to 50): Closest a lightning bolt can land to the player it strikes near. Kept well clear of point-blank, so a strike is a threat, never a targeted hit.
- **`LightningRingMaxMeters`** (number, default `40`, 10 to 60): Farthest a lightning bolt can land from the player it strikes near. Kept close enough that whoever it lands near can still hear it.
- **`LightningStandoffMeters`** (number, default `30`, 0 to 64): No lightning bolt lands within this distance of anything player-built — pieces and planted crops alike. A blocked bolt is simply lost, not rerolled.
- **`LightningIgniteRadiusMeters`** (number, default `2.5`, 0.5 to 8): Ground radius set alight at a lightning strike, in metres. Kept small on purpose — one bolt starts one fire, and the weather and land decide what it becomes.

## 06 - Plague

Outbreaks: plague takes root on its own, grows in the areas it holds, and spreads to neighbouring ones. Like all drift it only changes where players are, so an area nobody visits keeps whatever it has.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnablePlague`** (on/off, default `true`): Turns plague on or off — sickness that spreads between zones, grows or heals with the seasons, and can sicken players who linger in it. Existing plague does not freeze when this is off: biome state keeps growing it in already-infected zones (using the settings under Plague) as well as healing it, on its own schedule. Turning this off only stops new outbreaks appearing and stops spread into new zones.
- **`PlagueGenesisEnabled`** (on/off, default `true`): Lets plague start on its own — a rare roll seeds sickness on ground players visit, more likely where it is corrupted or burnt, and more during storms. Off means outbreaks only start by admin command. The seed is too faint to notice until it has grown, so a new outbreak only shows up in the server log at first, not to players standing on it.
- **`PlagueGenesisMeanHours`** (number, default `12`, 0.5 to 500): Average real hours of played time between new outbreaks starting on clean ground. Blighted ground shortens this by up to five times; a storm overhead shortens it further.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`PlagueSpreadIntervalSeconds`** (number, default `60`, 10 to 600): How often, in seconds, an infected zone can spread plague to a clean neighbour. Growth and healing within an already-sick zone run on their own separate pace.
- **`PlagueGrowthPerHour`** (number, default `0.03`, 0 to 1): How much plague grows per hour in an already-infected zone a player has visited, before the season's own multiplier. Whether a zone can be cured depends on this against the biome-state recovery rate: at the defaults, plague grows faster than it heals in every season except winter, when it heals faster than it grows. Set to 0 to stop plague growing at all.
- **`PlagueCorruptionBoost`** (number, default `1`, 0 to 4): How strongly corrupted ground speeds up plague growth there. At the default, fully corrupted ground doubles how fast plague grows.
- **`PlagueSpreadThreshold`** (number, default `0.5`, 0.05 to 1): Plague level a zone must reach before it can infect its neighbouring zones, on a 0 to 1 scale. A fresh outbreak starts well below this and only climbs while players keep visiting it, so the sickness only spreads as far as people actually go.
- **`PlagueSeedAmount`** (number, default `0.05`, 0.01 to 0.5): Plague level a zone starts at the moment it is newly infected, on a 0 to 1 scale.
- **`PlagueSpreadChance`** (number, default `0.25`, 0 to 1): Chance, checked on each spread pass, that a given neighbouring zone catches plague — rolled once per zone even if several infected zones border it.
- **`PlagueMaxSpreadsPerTick`** (whole number, default `16`, 1 to 256): Most zones plague can spread into in a single pass, as a safety limit against a large outbreak all rolling successfully at once.

## 07 - Ecology

Blight: land that stays badly plagued or scorched slowly becomes corrupted, and corrupted land breeds stronger creatures (see Consequence).

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableEcology`** (on/off, default `true`): Turns land corruption on or off. Corruption is the lasting scar heavy plague or fire damage leaves behind, and left unchecked it feeds back into faster plague growth.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`EcologyIntervalSeconds`** (number, default `60`, 10 to 600): How often, in seconds, the game checks blighted zones for new corruption. Lower checks more often for a small extra cost; it does not change how fast corruption itself builds.
- **`EcologyCorruptionPerHour`** (number, default `0.01`, 0 to 1): Base rate for how fast corruption builds in a zone whose plague or scorch has reached its threshold; the actual rate climbs further as plague or scorch gets worse. Set to 0 to stop corruption entirely. Right at the threshold a zone corrupts at only a quarter of this rate; it climbs towards several times this rate on badly plagued or burnt land. Corruption then feeds back into how fast plague grows in the same zone, so a corrupted outbreak gets harder to cure the longer it is left unchecked.
- **`EcologyPlagueThreshold`** (number, default `0.3`, 0.05 to 1): Plague level at which a zone's land begins to corrupt, feeding a slow build-up that outlasts the outbreak itself. A zone only needs to clear one of the two thresholds — plague or scorch — to start corrupting, whichever is worse for that zone; it does not need both at once.
- **`EcologyScorchThreshold`** (number, default `0.3`, 0.05 to 1): Scorch (fire-damage) level at which a zone's land begins to corrupt, feeding the same slow build-up that plague damage does.

## 08 - Farming

Tired soil: crops wear out the land they grow on, and crops on worn-out land take longer to grow. The slowdown itself happens on each player's own game.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableFarming`** (on/off, default `true`): Turns crop soil fatigue on or off. Heavily planted zones tire out and grow crops more slowly until the land is given a rest. Read on each player's own game, so give every player the same value.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`FarmingIntervalSeconds`** (number, default `45`, 10 to 600): Seconds between sweeps that count how many crops are growing in each zone. Kept off a round number on purpose so it does not land on the same moment as AwayFromHome's own scan. One full sweep works through every configured crop type in turn, one type per interval, so a world with several crop types can take a few minutes to finish a full round; soil depletion for the whole round is applied only once it completes.
- **`FarmingDepletionPerCropHour`** (number, default `0.002`, 0 to 0.5): How much each standing crop tires its zone's soil per hour. At the default, a field of 25 crops fully tires the land in about 20 hours of real playtime; resting the field lets it recover.
- **`FarmingGrowthSlowdownAtFull`** (number, default `2`, 1 to 5): How much longer crops take to grow on fully tired soil, as a multiplier on grow time; 1 turns the slowdown off. Applied on players' own games, so set the same value on every player's game. This is read by whichever player's own game currently owns a given plant, which can shift between nearby players over a session. Set the same value on every player's game, or the same field may grow at different speeds depending on who is closest to it.
- **`FarmingCropPrefabs`** (text, default `sapling_carrot,sapling_turnip,sapling_onion,sapling_barley,sapling_flax,sapling_seedcarrot,sapling_seedturnip,sapling_seedonion,sapling_jotunpuffs,sapling_magecap`): Comma-separated list of crop names counted as farmland. Only these are checked for soil depletion and slower growth on tired soil; anything not listed is unaffected. Read on each player's own game, so give every player the same value. These are the game's own internal names for each plant, not their display names. If Valheim renames or adds a crop, add its internal name here or the mod will quietly count it as zero matches — turn on verbose logging to see how many of each crop are actually being found.

## 09 - Health

Plague exposure and frost chill. Standing on plagued ground builds exposure, and exposure slows stamina and then health regeneration; high frost does the same through chill. Both only ever weaken regen, never kill. The regen effects are read on each player's own game.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableHealth`** (on/off, default `true`): Turns plague sickness and frost chill on or off. Standing on tainted or bitterly cold ground weakens stamina and health regen until the player leaves or recovers. Read on each player's own game, so give every player the same value.
- **`FrostChillEnabled`** (on/off, default `true`): Turns frost chill on or off: high zone frost slows stamina and health regen where vanilla would not call it cold. Fire, shelter and frost resistance cancel it. Read on each player's own game.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`HealthIntervalSeconds`** (number, default `5`, 1 to 60): How often, in seconds, online players are checked for plague exposure. Lower catches someone stepping into an outbreak sooner, at a small extra cost.
- **`ExposureMinutesToMax`** (number, default `30`, 5 to 240): Minutes of standing on fully plagued ground before a player's sickness reaches its worst. Ground that is only partly tainted builds sickness proportionally slower. The time here applies only to fully plagued ground; on ground at half that plague level, sickness builds at roughly half the speed. Nothing builds up at all on ground where the plague is too faint to notice.
- **`ExposureRecoveryMinutes`** (number, default `20`, 2 to 240): Minutes for a player's sickness to clear fully once they leave plagued ground, from its worst back to none. Each player's own game also uses this value to estimate the 'time until clean' it shows them. The server's copy is what actually controls recovery speed; a player whose own copy differs only sees a misleading countdown, not a different real recovery rate.
- **`ExposureRestedRecoveryMultiplier`** (number, default `2`, 1 to 10): How much faster sickness clears while the player has the game's own Rested status. At the default, being Rested roughly doubles recovery speed.
- **`ExposurePoisonResistMultiplier`** (number, default `0.5`, 0 to 1): How much slower sickness builds up while poison-resistant, from any mead, gear or food. At the default, protection halves how fast exposure builds.
- **`ExposureTier1`** (number, default `0.25`, 0.01 to 1): Exposure level at which the sickness first appears: the status icon shows and stamina regen starts to suffer. Set the same value on every player's game. There are three tiers in total, each its own setting: this one starts the sickness and a stamina penalty, a second tier adds a health regen penalty on top, and a third only changes when the worst-case message is shown on screen. All three read from the same shared exposure level but are applied by each player's own game.
- **`ExposureTier2`** (number, default `0.5`, 0.01 to 1): Exposure level at which health regen also starts to suffer, on top of the stamina penalty from the first tier. Set the same value on every player's game.
- **`ExposureTier3`** (number, default `0.8`, 0.01 to 1): Exposure level at which the sickness is announced as being at its worst. Only changes that announcement — the regen penalties already ramp smoothly past this point. Set the same value on every player's game.
- **`SicknessStaminaRegenAtTier1`** (number, default `0.85`, 0.05 to 1): Stamina regen multiplier the instant the first sickness tier is crossed, so the penalty is felt right away rather than easing in unnoticed. Set the same value on every player's game. The penalty jumps straight to this value the moment the first tier is crossed, then eases further down towards the 'at maximum' setting as exposure keeps climbing. Setting this to 1 makes the first tier felt as no penalty at all, only announced.
- **`SicknessStaminaRegenAtMax`** (number, default `0.3`, 0.05 to 1): Stamina regen multiplier at full exposure, easing down from the first tier's penalty as exposure climbs. Set the same value on every player's game. With every related setting at its shipped default, this produces roughly x0.85 stamina regen at 25% exposure, x0.67 at 50%, and x0.45 at 80% — the table the mod was tuned against.
- **`SicknessHealthRegenAtTier2`** (number, default `0.8`, 0.05 to 1): Health regen multiplier the instant the second sickness tier is crossed — the wound half of the sickness arriving after the stamina fails. Set the same value on every player's game.
- **`SicknessHealthRegenAtMax`** (number, default `0.38`, 0.05 to 1): Health regen multiplier at full exposure, easing down from the second tier's penalty as exposure climbs. Set the same value on every player's game. With every related setting at its shipped default, this produces roughly x0.80 health regen at 50% exposure and x0.55 at 80%.
- **`FrostChillThreshold`** (number, default `0.5`, 0.05 to 1): Zone frost level at which the chill effect takes hold of a player standing in it. Set the same value on every player's game.
- **`ChillStaminaRegenMultiplier`** (number, default `0.8`, 0.05 to 1): Stamina regen multiplier while chilled. Set the same value on every player's game.
- **`ChillHealthRegenMultiplier`** (number, default `0.7`, 0.05 to 1): Health regen multiplier while chilled. Set the same value on every player's game.

## 10 - Consequence

What the land's state does to the world: barren berries and mushrooms on plagued or burned ground, starred spawns on corrupted ground, sickened wildlife, and withering crops. Player-built structures are never touched. All of this happens on each player's own game, so these settings must match the server's.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableConsequence`** (on/off, default `true`): Turns land consequences on or off: barren pickables, tougher spawns, sickened wildlife and dying crops on badly plagued, scorched or corrupted ground. Read on each player's own game, so give every player the same value.
- **`ConsequenceBarren`** (on/off, default `true`): Stops berries, mushrooms and other pickables from being harvested on badly plagued or scorched ground, with an in-world message explaining why. Read on each player's own game, so give every player the same value.
- **`ConsequenceEmpower`** (on/off, default `true`): Gives hostile creatures a better chance of spawning as a stronger, starred variant on badly corrupted ground. Passive wildlife is never affected. Read on each player's own game, so give every player the same value.
- **`ConsequenceSicken`** (on/off, default `true`): Slows and sickens passive wildlife (deer, boars, hares) standing on plagued ground. The effect wears off once the animal leaves or the plague clears. Read on each player's own game, so give every player the same value.
- **`ConsequenceWither`** (on/off, default `true`): Kills crops planted in badly blighted soil once they would otherwise finish growing. Replanting after the land recovers is the fix. Read on each player's own game, so give every player the same value.
- **`AnnounceConsequences`** (on/off, default `true`): Shows a one-line message the first time a player enters a zone with barren ground, tougher spawns, sickness or dying crops. Sent once per zone, per session.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`ConsequenceIntervalSeconds`** (number, default `10`, 2 to 120): Seconds between checks that announce a zone's consequences to players near it. The effects themselves apply continuously regardless of this setting — it only paces the announcement.
- **`BarrenPlagueThreshold`** (number, default `0.4`, 0.05 to 1): Plague level in a zone at or above which pickables there stop yielding anything. Read on each player's own game, so give every player the same value.
- **`BarrenScorchThreshold`** (number, default `0.5`, 0.05 to 1): Scorch level in a zone at or above which pickables there stop yielding anything — ash bears nothing. Read on each player's own game, so give every player the same value.
- **`SickenPlagueThreshold`** (number, default `0.4`, 0.05 to 1): Plague level in a zone at or above which passive wildlife there starts to sicken. Read on each player's own game, so give every player the same value.
- **`SickenSpeedPenalty`** (number, default `0.35`, 0 to 0.9): How much slower sickened wildlife moves, as a fraction of its normal speed — for example, 0.5 means half speed. This only slows animals; it never kills them. Read on each player's own game, so give every player the same value.
- **`EmpowerCorruptionThreshold`** (number, default `0.5`, 0.05 to 1): Corruption level in a zone above which hostile spawns start getting better odds of coming up stronger. Read on each player's own game, so give every player the same value.
- **`EmpowerLevelUpMultiplierAtFull`** (number, default `6`, 1 to 10): How much better the odds of a stronger spawn get on fully corrupted ground, as a multiplier on the game's own level-up chance. Read on each player's own game, so give every player the same value. The game's base chance is roughly 10% per level, so a multiplier of 6, the default, means about 60% of eligible spawns come up starred on fully corrupted ground. The game's own per-creature star caps still apply on top of this.
- **`CropWitherBlightThreshold`** (number, default `0.6`, 0.05 to 1): Blight level (whichever is worse, plague or corruption) at which planted crops wither and die outright. Read on each player's own game, so give every player the same value. This is only the kill line — a blighted crop looks unhealthy but survives until it would otherwise finish growing, then dies outright. Slower growth on tired, overworked soil is a separate Farming setting that responds to how hard the land has been farmed, not to plague or corruption.
- **`WildlifePrefabs`** (text, default `Deer,Boar,Hare`): Comma-separated list of creature names counted as passive wildlife — these can sicken from plague but are never turned into a stronger spawn by corruption. Read on each player's own game, so give every player the same value. The default covers Valheim's Deer, Boar and Hare. Add other passive animals by their in-game object name, separated by commas, with no extra spaces needed.

## 11 - Rivalry

Who helps and who harms each area. Healing damaged land and tending crops earns care; starting fires earns harm. Enough of either lets a player hold ground, earn titles like Warden or Despoiler, or earn a grudge that makes the land refuse them. Blighted ground can become contested war ground. The creature that kills a player becomes their nemesis. Several of these act on each player's own game.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableRivalry`** (on/off, default `true`): Turns the rivalry system on or off. It tracks who helps or harms each area, feeding grudges, contested ground, and titles that reward or shame players for how they treat the land. Read on each player's own game, so give every player the same value. Also gates the nemesis-marking and contested-ground effects, even when their own settings are on — though those particular checks look at each player's own copy of this switch, not just the server's. Switching it off simply stops updating the ledger — whatever grudges or standings are already in the save stay there, frozen, until it's turned back on.
- **`AnnounceContests`** (on/off, default `true`): Announce contest outcomes to nearby players: an area changing hands between two rivals, or a spawn war on contested ground finally resolving. Only fires when both rivals have genuinely shaped that ground — ground claimed by default, with no real rival, changes hands silently.
- **`EnableNemesis`** (on/off, default `true`): Turns nemesis marking on or off: the creature that kills a player is marked, levelled up and named for who it slew. Read from the killed player's own game, so give every player the same value. The mark is permanent and saved with the world; only the creature despawning removes it. A boss that kills a player is marked the same way but never gains a level, regardless of the cap below. This check runs on the killed player's own game, so turn it on for every player too — the server's copy alone will not mark anything for someone whose own client has it off.
- **`NemesisMaxLevel`** (whole number, default `3`, 1 to 5): Highest level a creature can reach by killing players (level 3 is two stars). Bosses are marked but never levelled. Read from the killed player's own game, so give every player the same value. Lowering this after a creature has already climbed past it does not demote it; marks and levels already earned in the save are never taken back. A boss is marked but never levelled, regardless of this cap. Like EnableNemesis above, this value comes from whichever player the creature just killed, so set it the same in every player's own config — the server's copy is never consulted.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`RivalryIntervalSeconds`** (number, default `30`, 5 to 300): How often the rivalry system re-checks grudges, care, and tending progress, in seconds. Kept off the same cadence as the Farming section's interval (45s) and AwayFromHome's own scan (60s) so their heavier passes don't land on the same tick.
- **`RivalryHalfLifeHours`** (number, default `48`, 1 to 720): Real hours for a player's recorded harm and care in an area to fade by half. Lower makes the land forgive faster; higher makes both grudges and goodwill linger longer. At the default 48-hour half-life, harm or care from two days ago counts for half as much, and from four days ago a quarter — so both grudges and goodwill fade on their own rather than needing to be cleared by hand.
- **`CarePerHealedPoint`** (number, default `1`, 0 to 10): Credit booked to players nearby when damaged land heals, split among everyone whose presence covers it — it offsets grudges and counts toward the Warden title. At the default, fully curing a plague-ridden area earns everyone who was present one point of care between them, split evenly.
- **`TendingCarePerPlant`** (number, default `0.05`, 0 to 1): Credit booked to a crop's planter, once per plant ever planted — replanting the same spot again earns nothing extra. Uses the same crop list as the Farming section. Each plant is credited exactly once, ever, not on every harvest, so restarting the server or replanting the same spot cannot be farmed for extra credit.
- **`ArsonHarmPerScorchPoint`** (number, default `1`, 0 to 10): Blame booked against whoever started a fire, per point of scorch it burns into an area — fully charring one area blames them one point at the default. Needs FireFront installed at a version that identifies who struck the match (0.17.3 or newer); with an older FireFront the land still scorches as normal, but nobody is blamed for it. Storms and wild fires never blame a player.
- **`GrudgeScale`** (number, default `1`, 0 to 10): How strongly a player's harm to an area, minus any care they've since given it, turns into a grudge against them specifically. Higher turns the land against wrongdoers faster. At the default of 1.0, fully burning or corrupting an area earns the full grudge against whoever did it; tending the same ground afterwards genuinely lessens it. Whoever's grudge is worst in an area slows that area's own recovery and speeds its decay for everyone standing there, not just the grudged player — only the personal pick refusal and the Ashbringer title single that player out.
- **`GrudgePickRefuse`** (number, default `0.25`, 0.05 to 1): Grudge level at which an area's berries and mushrooms refuse to be picked by the player who earned it — everyone else can still pick them. Checked in each player's own game. Tend the land, or simply wait for the grudge to fade (the half-life setting above), to be forgiven and pick there again. This threshold is read from the picking player's own game, not the server's — for it to apply consistently, set it the same way in every player's copy too.
- **`AshbringerGrudge`** (number, default `0.5`, 0.05 to 1): Grudge level, in a player's single worst area, needed to earn the Ashbringer title. Currently only fire damage builds a grudge, so this effectively tracks how badly a player has burned the world. Like every title, only the most recently earned one shows.
- **`CareDominanceFloor`** (number, default `0.2`, 0.05 to 5): Minimum care a player needs in an area before the land can remember them as its carer — below this, nobody holds that ground. Being an area's remembered carer grants the recovery and sickness-recovery bonuses below, counts toward the Warden title, and is who gets named if the ground later flips to favouring someone else, when contest announcements are on.
- **`HarmDominanceFloor`** (number, default `0.2`, 0.05 to 5): Minimum harm a player needs in an area before the land can remember them as its dominant despoiler. Being an area's remembered despoiler counts toward the Despoiler title, and is who gets named if the ground later flips to fearing someone else more, when contest announcements are on.
- **`ContestHysteresis`** (number, default `0.15`, 0 to 1): How far a challenger's care or harm must exceed the current holder's before they take over an area (0.15 = 15% higher). Stops ground flickering between two close rivals. Applies only once a zone already has a holder; an empty zone is claimed by whoever qualifies first, with no announcement.
- **`MercyRecoveryBonus`** (number, default `0.25`, 0 to 2): Extra healing speed for an area while its remembered carer is nearby (0.25 = 25% faster recovery). Speeds up the area's actual recovery for everyone nearby, not just its carer — it only needs the carer (whoever the land remembers best; see the care floor above) to be nearby to switch on.
- **`MercySicknessBonus`** (number, default `0.5`, 0 to 3): Faster recovery from plague sickness while standing on ground you're remembered as the carer of (0.5 = 50% faster). Only speeds recovery once sickness has already taken hold — it does not slow how fast sickness builds up in the first place.
- **`WardenZonesHeld`** (whole number, default `3`, 1 to 64): Number of areas a player must be remembered as the carer of, at once, to earn the Warden title.
- **`DespoilerZonesHeld`** (whole number, default `3`, 1 to 64): Number of areas a player must be remembered as the dominant harmer of, at once, to earn the Despoiler title.
- **`ContestBlightThreshold`** (number, default `0.5`, 0.1 to 1): How plagued or corrupted an area must be, whichever is worse, before it can become contested war ground. On its own, blight past this line is just sick land. It only becomes a war, with wildlife surging and star odds rising, once players are also actively tending it, past the care threshold below.
- **`ContestCareThreshold`** (number, default `0.3`, 0.05 to 5): Total care, summed across everyone, a blighted area needs before it counts as actively contested rather than simply lost.
- **`StormContestMultiplier`** (number, default `2`, 1 to 5): How much fiercer a contested area's war gets while a Devastating Storm passes over it. 1 turns this off. Feeds directly into the star-odds bonus below and the wildlife surge, so a stormy war spawns tougher, more numerous enemies than a calm one.
- **`ContestStarBonus`** (number, default `1`, 0 to 5): Extra chance for hostile spawns to come up starred, per point of war intensity, on contested ground. Read from the nearby player's own game, not the server's. At the default of 1.0, ordinary contested ground doubles the star chance, and a storm-escalated war (see the storm multiplier above) triples it. Stacks with the separate corruption-based star bonus in the Consequence settings. Like those settings, this one is checked on whichever player's game is nearby when the spawn happens, so set it the same in every player's own config.
- **`ContestWildSpawnChance`** (number, default `100`, 0 to 100): How likely nearby wildlife is to spawn while a player stands on contested ground, as a percentage. Applied from that player's own game, not the server's. At the default of 100, wildlife spawns as often near contested ground as the game's own rules allow — normally far less often. Works through the game's usual wildlife-attracting mechanism, so real numbers still depend on the wildlife cap below. Every player needs this set in their own config; the server's copy is never read for it.
- **`ContestWildMaxSpawned`** (whole number, default `15`, 1 to 20): Cap on concurrent animals of each wildlife type while a spawn war is underway, applied from each player's own game, not the server's. This only speeds up how fast wildlife refills toward the game's own population limit for that creature — it cannot push animal numbers past what the game would normally allow, war or no war. Every player needs this set in their own config too; the server's copy is never read for it.

## 12 - Relic

Standing stones the world raises by itself where a story ends: a fire fully healed, a plague cured, a spawn war resolved, the world recovering from Stricken. Blessed stones speed healing nearby, cursed ones slow it. The player standing nearby when a stone rises chooses its shape, and cursed ground's extra starred spawns are decided on each player's own game.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableRelic`** (on/off, default `true`): Turns relic stones on or off. Zones where a fire fully heals, a plague is cured, or a spawn war resolves can raise a lasting blessed or cursed landmark that changes how fast the land heals and how tough creatures nearby become. Read on each player's own game, so give every player the same value.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`RelicIntervalSeconds`** (number, default `30`, 5 to 600): Seconds between checks for a zone whose story has just completed and is ready to raise a stone.
- **`FireRelicPeakThreshold`** (number, default `0.5`, 0.05 to 1): How badly a zone must have scorched before healing it fully afterwards raises a blessed stone there. The peak is remembered for however long it takes to heal — there is no time limit between the burn and the full recovery that completes the story and raises the stone.
- **`PlagueRelicPeakThreshold`** (number, default `0.5`, 0.05 to 1): How bad a zone's plague must have gotten before curing it fully afterwards raises a blessed stone there. Works the same way as the scorch threshold above: the peak stays remembered until the zone heals all the way to clean, however long that takes.
- **`RelicBlessedRecoveryMult`** (number, default `1.25`, 1 to 3): Multiplies how fast a damaged zone heals while blessed ground stands there. Above 1 speeds up recovery.
- **`RelicCursedRecoveryMult`** (number, default `0.8`, 0.25 to 1): Multiplies how fast a damaged zone heals while cursed ground stands there. Below 1 slows recovery down.
- **`RelicBlessedExposureDrainMult`** (number, default `1.5`, 1 to 3): How much faster plague sickness fades from a player standing on blessed ground. It only speeds up healing off the sickness, never shields against catching it in the first place.
- **`RelicCursedExposureAccrualMult`** (number, default `1.25`, 1 to 3): How much faster plague sickness builds up on a player standing on cursed ground.
- **`RelicCursedStarBonus`** (number, default `0.25`, 0 to 2): Extra chance for hostile spawns to come up starred while standing on cursed ground, stacking with the corruption bonus and any active spawn war. Read on each player's own game, so give every player the same value.
- **`RelicVandalHarm`** (number, default `0.5`, 0 to 5): How much blame is booked against a player who destroys a relic stone. Has no effect unless rivalry is also turned on.
- **`RelicPrefabCandidates`** (text, default `highstone,widestone`): Comma-separated names of existing game objects to try, in order, for the relic stone's shape — the first one the game recognises is used. Keep this the same on every player's copy of the config for a consistent result. Placement happens on whichever player is standing nearby when a stone is due to rise, using that player's own local copy of this list, so a mismatched list between players could raise a different-looking stone (or none) depending on who happens to be there. This mod adds no objects of its own; if every name in the list fails to resolve, no stone appears and the log names this setting.

## 13 - Titles

Titles players earn and wear on their nameplate: Stormrider, Plaguewalker, Winterborn, and the rivalry titles.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableTitle`** (on/off, default `true`): Turns earned titles on or off — names shown under a player's nameplate for what they have done in the world, such as getting caught in a storm or walking into a badly plagued zone. Current titles: Stormrider (caught in a storm), Plaguewalker (stood in a badly plagued zone) and Winterborn (enough time online through Winter). With Rivalry also on, players can additionally earn Ashbringer, Warden or Despoiler for lasting harm or care to the land.
- **`AnnounceTitles`** (on/off, default `true`): Announces a newly earned title to everyone on the server. Titles are rare by design, so this should not spam chat.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`TitleIntervalSeconds`** (number, default `10`, 2 to 120): Seconds between checks of online players for newly earned titles.
- **`WinterbornSeconds`** (number, default `1800`, 60 to 86400): Seconds a player must be online during Winter to earn the Winterborn title. Restarting the server resets everyone's progress toward it.

## 14 - World state

The world's overall condition, from Flourishing to Stricken, worked out from how much plague, scorch and corruption the land carries, with an active storm adding to it.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableWorldState`** (on/off, default `true`): Turns the world's overall condition on or off — whether the land as a whole is judged Flourishing, Ailing or Stricken, announced when it changes.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`WorldStateIntervalSeconds`** (number, default `30`, 10 to 600): Seconds between recalculations of the world's overall condition (Flourishing, Ailing or Stricken).
- **`WorldFlourishingBurden`** (number, default `0.25`, 0 to 10): Total burden — a combined score of plague, corruption, scorch, soil tiredness and frost across the world — at or below which the land counts as Flourishing. Burden adds up every tracked zone's plague, corruption, scorch, soil tiredness and frost, weighted by how serious each is (plague counts most, frost least). It is a running total, not a percentage, so a bigger or more damaged world naturally shows a higher number — set the three World state thresholds together rather than alone.
- **`WorldAilingBurden`** (number, default `4`, 0.5 to 100): Total burden at which the land turns Ailing. Keep this above the Flourishing threshold, since burden is the same combined score described there.
- **`WorldStrickenBurden`** (number, default `12`, 1 to 500): Total burden at which the land is judged Stricken, the worst condition. Keep it comfortably above the Ailing threshold. Once crossed, burden has to drop a good way back below a threshold — about 15% — before the land is announced as recovering past it, so a small dip in either direction cannot flip the announcement back and forth.
- **`WorldStormBurden`** (number, default `1`, 0 to 20): Extra burden added to the total while a Devastating Storm is active, so a stormy moment can nudge the land's judged condition worse.

## 15 - Visuals

What each player sees. The server pushes the state of nearby areas to players (`EnableZoneSync`), and each player chooses which effects to draw on their own screen. Nothing here changes gameplay.

In `com.raveniron.ragnarokswrath.cfg`:

- **`EnableZoneSync`** (on/off, default `true`): Turns zone-state syncing on or off. It feeds the plague fog, frost breath and scorch-ash visuals; a connecting player loses all three when it's off, though a player hosting their own game keeps seeing them regardless.
- **`PlagueFogEnabled`** (on/off, default `true`): Client-side: shows a low, grey-green mist over plagued ground on your own screen. Every player chooses this for themselves; purely visual.
- **`FrostBreathEnabled`** (on/off, default `true`): Client-side: fogs your character's breath on land whose cold has built up, as an early warning before the chill effect itself sets in.
- **`ScorchAshEnabled`** (on/off, default `true`): Client-side: drifts grey ash over badly burned ground on your own screen, thinning as the land heals. Separate from FireFront's own flames and scorched-ground marks, which are unaffected by this setting.
- **`RelicRunesEnabled`** (on/off, default `true`): Client-side: shows rune glyphs rising around standing relic stones on your own screen — gold on blessed ground, red on cursed.

In `com.raveniron.ragnarokswrath.advanced.cfg`:

- **`ZoneSyncIntervalSeconds`** (number, default `10`, 2 to 120): Seconds between zone-state updates sent to each connected player. Lowering it makes the plague fog, frost breath and ash visuals catch up to real changes sooner, at the cost of more frequent small network pushes.
- **`ZoneSyncRadiusZones`** (whole number, default `2`, 1 to 4): How many zones out from each player's position are sent to them each push. A larger radius covers a bigger area around the player but sends more data per push.
- **`PlagueFogDensity`** (number, default `1`, 0 to 4): Client-side: how thick the plague mist looks on your screen. Lightly plagued ground never shows fog regardless of this setting, so a fresh outbreak stays hidden until it has properly taken hold.
- **`FrostBreathFloor`** (number, default `0.3`, 0.05 to 1): Client-side: how much zone frost is needed before your breath starts to fog. Kept below the chill effect's own threshold so you see the warning before the cold actually bites.
- **`ScorchAshDensity`** (number, default `1`, 0 to 4): Client-side: how much ash drifts over burned ground on your screen. Lightly scorched ground never shows ash regardless of this setting.

## Meta

`ConfigVersion` records which layout the files are at. The mod sets it; do not edit it by hand, or an update to the files may be skipped or applied twice.
