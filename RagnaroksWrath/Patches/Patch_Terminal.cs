using System;
using System.Globalization;
using System.Text;
using HarmonyLib;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Net;
using RavenIron.RagnaroksWrath.Systems.World;

namespace RavenIron.RagnaroksWrath.Patches
{
    /// <summary>
    /// The `wrath` console — the locked-decision prefix, finally cashed. Registered from
    /// an InitTerminal postfix (the ConsoleCommand constructor overwrites same-name
    /// entries, so the repeat call per terminal is harmless).
    ///
    /// AUTHORITY RULE, self-gated: reads answer everywhere (synced caches on a pure
    /// client, the live stores on the authority); MUTATIONS run only where the stores
    /// live — the dedicated server's own console, or a listen host. A pure client asking
    /// to mutate is refused with directions, not trusted. This deletes the
    /// stop-edit-copy-restart dance that today needed five performances.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    public static class Patch_Terminal_Wrath
    {
        private static void Postfix()
        {
            try
            {
                new Terminal.ConsoleCommand("wrath",
                    "Ragnarok's Wrath admin: wrath status | zone [x y] | zone set x y field v | " +
                    "care/harm set x y playerId v | relics | save",
                    Run);
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"wrath console: register failed: {ex.Message}");
            }
        }

        private static bool Authority => Persistence.IsLoaded;

        /// <summary>
        /// Mutations typed on a pure client forward to the server through VANILLA's own
        /// remote-command pipe (`ZNet.RemoteCommand` — decompile-verified 2026-08-26):
        /// the server checks the sender against adminlist.txt, refuses non-admins with
        /// "You are not admin", logs the admin and the exact line, and replays the
        /// command through its own console where Authority holds. Output lands on the
        /// SERVER console — the client confirms through its own reads a ring-push later.
        /// </summary>
        private static bool TryForward(Terminal.ConsoleEventArgs args)
        {
            ZNet znet = ZNet.instance;
            if (znet == null || znet.IsServer()) return false;

            string line = string.Join(" ", args.Args);
            znet.RemoteCommand(line);
            // Diagnostic breadcrumb (2026-08-26): the first live forward produced a
            // phantom usage line client-side and no audit line server-side — this names
            // the terminal and the exact line so the next repro is a fact, not a theory.
            RagnaroksWrath.Log.LogInfo(
                $"wrath: forwarded '{line}' via {args.Context?.GetType().Name ?? "?"}.");
            args.Context.AddString(
                "wrath: forwarded to the server — vanilla's admin gate applies " +
                "(non-admins are refused). Confirm with `wrath zone <x> <y>` after the " +
                "next sync (~10s); the server console carries the receipt.");
            return true;
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            try
            {
                string sub = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "help";
                switch (sub)
                {
                    case "status": Status(args); return;
                    case "zone":   Zone(args); return;
                    case "care":   Column(args, care: true); return;
                    case "harm":   Column(args, care: false); return;
                    case "relics": Relics(args); return;
                    case "save":   SaveAll(args); return;
                    case "storm":  Storm(args); return;
                    case "title":  Title(args); return;
                    default:
                        args.Context.AddString(
                            "wrath status — world overview\n" +
                            "wrath zone [x y] — one zone's full story (underfoot when omitted)\n" +
                            $"wrath zone set <x> <y> <field> <value> — fields: {WrathAdmin.ZoneFields}\n" +
                            "wrath care set <x> <y> <playerId> <value> — set a ledger column\n" +
                            "wrath harm set <x> <y> <playerId> <value>\n" +
                            "wrath relics — peaks, stones, pending, era\n" +
                            "wrath save — flush every store now\n" +
                            "wrath title — the titles you have earned and the one you wear (any player; /wrath title in chat)\n" +
                            "wrath title <name> | auto | none — wear that title, follow new ones, or wear none\n" +
                            "wrath storm — the storm settings in effect\n" +
                            "wrath storm start [dry|wet] [x z] — a storm now, where you stand when typed in game\n" +
                            "wrath storm stop — end the storm now\n" +
                            "wrath storm strike — one lightning bolt now, near a player under the storm\n" +
                            $"wrath storm set <name> <value> — live, saved to the config; names: {StormLive.AdminKeys}\n" +
                            "Mutations run on the authority; typed in-game they forward to the " +
                            "server through vanilla's admin gate (adminlist.txt).");
                        return;
                }
            }
            catch (Exception ex)
            {
                args.Context.AddString($"wrath: failed — {ex.Message}");
            }
        }

        private static void Status(Terminal.ConsoleEventArgs args)
        {
            var sb = new StringBuilder(256);
            if (Authority)
            {
                sb.Append($"condition {WorldStateSystem.Condition}, season {SeasonSystem.Current} ({SeasonSystem.Source}), ");
                sb.Append($"day {SeasonSystem.CurrentDayOrZero()}\n");
                sb.Append($"zones tracked {Persistence.TrackedZoneCount}, ");
                sb.Append($"wars {RivalrySystem.ContestedZoneCount}, ");
                sb.Append($"rivalry rows {RivalryLedger.Count}, ");
                sb.Append($"relics {RelicLedger.RelicCount} standing / {RelicLedger.PendingCount} pending, ");
                sb.Append($"era {(RelicLedger.EraArmed ? "ARMED" : "clear")}");
            }
            else
            {
                sb.Append("(pure client — synced view only)\n");
                // Season AND its source, never one without the other. Spring is index 0 and so
                // is "nothing has ever told us", so on a client the source is the only thing
                // that separates a working sync from none at all — and before 0.25.0 there was
                // no sync, so this line would have read Spring in midwinter.
                sb.Append(SeasonSystem.Source == SeasonSystem.SeasonSource.Server
                    ? $"season {SeasonSystem.Current} (synced from the server), "
                    : $"season {SeasonSystem.Current} (NOT SYNCED), ");
                sb.Append($"day {SeasonSystem.CurrentDayOrZero()}\n");
                Player local = Player.m_localPlayer;
                if (local != null)
                {
                    ZoneKey here = ZoneKey.FromWorldPos(local.transform.position);
                    sb.Append(DescribeZone(here));
                }
                else sb.Append("no local player.");
            }

            // The config layout, on both roles. ConfigMigration declared LastSummary "kept for
            // `wrath status`" and then nothing ever read it — a dead property until 2026-09-18.
            // The stamp prints unconditionally because the two commonest boots, a fresh install and
            // a file already at the current layout, set no summary at all; the summary prints
            // whenever this boot planned a migration, which is the only time there is news.
            sb.Append($"\nconfig layout v{ModConfig.ConfigVersion.Value.ToString(CultureInfo.InvariantCulture)}");
            string migration = ConfigMigration.LastSummary;
            sb.Append(string.IsNullOrEmpty(migration) ? " (nothing migrated this boot)" : $" — {migration}");
            sb.Append("\n").Append(ServerSyncSetup.Describe());

            args.Context.AddString(sb.ToString());
        }

        private static void Zone(Terminal.ConsoleEventArgs args)
        {
            // wrath zone set <x> <y> <field> <value>
            if (args.Args.Length >= 3 && args.Args[2].ToLowerInvariant() == "set")
            {
                if (!Authority)
                {
                    if (!TryForward(args))
                        args.Context.AddString("wrath: mutations run on the authority only (server console or listen host).");
                    return;
                }
                if (args.Args.Length < 7
                    || !WrathAdmin.TryParseInt(args.Args[3], out int sx)
                    || !WrathAdmin.TryParseInt(args.Args[4], out int sy)
                    || !WrathAdmin.TryParseValue(args.Args[6], out float value))
                {
                    args.Context.AddString($"usage: wrath zone set <x> <y> <field> <value>  (fields: {WrathAdmin.ZoneFields})");
                    RagnaroksWrath.Log.LogInfo(
                        $"wrath: zone-set usage rejection (authority={Authority}, " +
                        $"terminal={args.Context?.GetType().Name ?? "?"}, args='{string.Join(" ", args.Args)}').");
                    return;
                }

                var zone = new ZoneKey(sx, sy);
                ZoneState state = Persistence.Get(zone);
                if (!WrathAdmin.TrySetZoneField(state, args.Args[5], value, out ZoneState next))
                {
                    args.Context.AddString($"unknown field '{args.Args[5]}' — fields: {WrathAdmin.ZoneFields}");
                    return;
                }

                Persistence.Set(zone, next);
                // Fresh contact stamp, always: a stale stamp credits the backlog and the
                // staged value drains before anyone measures it — the runbook's own trap,
                // now impossible to forget because the tool does it for you.
                ZoneClock.MarkContact(zone);
                Persistence.Save(force: true);
                args.Context.AddString($"zone {zone}: {args.Args[5]} = {value.ToString("0.####", CultureInfo.InvariantCulture)} (fresh contact stamp, saved).");
                return;
            }

            // wrath zone [x y] — show
            ZoneKey target;
            if (args.Args.Length >= 4
                && WrathAdmin.TryParseInt(args.Args[2], out int zx)
                && WrathAdmin.TryParseInt(args.Args[3], out int zy))
            {
                target = new ZoneKey(zx, zy);
            }
            else
            {
                Player local = Player.m_localPlayer;
                if (local == null)
                {
                    args.Context.AddString("no local player — give coordinates: wrath zone <x> <y>");
                    return;
                }
                target = ZoneKey.FromWorldPos(local.transform.position);
            }

            args.Context.AddString(DescribeZone(target));
        }

        private static string DescribeZone(ZoneKey zone)
        {
            var c = CultureInfo.InvariantCulture;
            ZoneState s = ZoneSync.StateAt(zone);
            var sb = new StringBuilder(192);
            sb.Append($"zone {zone}: fert {s.Fertility.ToString("0.####", c)}, corr {s.Corruption.ToString("0.####", c)}, ");
            sb.Append($"scorch {s.Scorch.ToString("0.####", c)}, frost {s.Frost.ToString("0.####", c)}, plague {s.Plague.ToString("0.####", c)}\n");
            sb.Append($"war {ZoneSync.WarAt(zone).ToString("0.#", c)}");

            RelicLedger.Relic relic = RelicSync.RelicAt(zone);
            if (relic.Standing)
                sb.Append($", relic type {relic.Type} {(relic.Cursed ? "cursed" : "blessed")} day {relic.Day}");

            if (Authority && RivalryLedger.IsLoaded)
            {
                foreach (System.Collections.Generic.KeyValuePair<RivalryLedger.Key, RivalryLedger.Row> kv in RivalryLedger.All())
                    if (kv.Key.Zone == zone)
                        sb.Append($"\n  {kv.Key.Player}: harm {kv.Value.Harm.ToString("0.####", c)}, care {kv.Value.Care.ToString("0.####", c)}");
            }
            return sb.ToString();
        }

        private static void Column(Terminal.ConsoleEventArgs args, bool care)
        {
            if (!Authority || !RivalryLedger.IsLoaded)
            {
                if (!TryForward(args))
                    args.Context.AddString("wrath: mutations run on the authority only (server console or listen host).");
                return;
            }
            if (args.Args.Length < 7
                || args.Args[2].ToLowerInvariant() != "set"
                || !WrathAdmin.TryParseInt(args.Args[3], out int x)
                || !WrathAdmin.TryParseInt(args.Args[4], out int y)
                || !WrathAdmin.TryParseLong(args.Args[5], out long playerId)
                || !WrathAdmin.TryParseValue(args.Args[6], out float value))
            {
                args.Context.AddString($"usage: wrath {(care ? "care" : "harm")} set <x> <y> <playerId> <value>");
                return;
            }

            var zone = new ZoneKey(x, y);
            RivalryLedger.SetColumns(zone, playerId, care ? (float?)null : value, care ? value : (float?)null);
            RivalryLedger.SaveIfDirty();
            args.Context.AddString($"zone {zone}, player {playerId}: {(care ? "care" : "harm")} = {value.ToString("0.####", CultureInfo.InvariantCulture)} (saved).");
        }

        private static void Relics(Terminal.ConsoleEventArgs args)
        {
            if (!Authority || !RelicLedger.IsLoaded)
            {
                args.Context.AddString("(pure client) standing relics come from the synced cache — use `wrath zone <x> <y>`.");
                return;
            }

            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(192);
            foreach (System.Collections.Generic.KeyValuePair<ZoneKey, RelicLedger.Peaks> kv in RelicLedger.AllPeaks())
                sb.Append($"peak {kv.Key}: scorch {kv.Value.Scorch.ToString("0.####", c)}, plague {kv.Value.Plague.ToString("0.####", c)}\n");
            foreach (System.Collections.Generic.KeyValuePair<ZoneKey, RelicLedger.Relic> kv in RelicLedger.AllRelics())
                sb.Append($"stone {kv.Key}: type {kv.Value.Type} {(kv.Value.Cursed ? "cursed" : "blessed")} day {kv.Value.Day}{(kv.Value.Placed ? "" : " (unplaced)")}\n");
            foreach (System.Collections.Generic.KeyValuePair<ZoneKey, RelicLedger.Relic> kv in RelicLedger.AllPending())
                sb.Append($"pending {kv.Key}: type {kv.Value.Type}\n");
            if (RelicLedger.EraArmed) sb.Append("era: ARMED\n");
            if (sb.Length == 0) sb.Append("no peaks, no stones, no pending, era clear.");
            args.Context.AddString(sb.ToString().TrimEnd('\n'));
        }

        /// <summary>
        /// `wrath title [name|auto|none]`: a player's OWN title. The one console command every
        /// player may use, not only admins, because it can only ever touch the caller: the server
        /// decides whose title it is from the connection, never from the text. Works from chat
        /// (`/wrath title`), which needs no -console launch option. The answer comes back from the
        /// server a moment later and is printed where the command was typed.
        /// </summary>
        private static void Title(Terminal.ConsoleEventArgs args)
        {
            ZNet znet = ZNet.instance;
            if (znet == null)
            {
                args.Context.AddString("title: join a world first.");
                return;
            }
            // No local character is the server console (or a loading screen). Asked rather than
            // IsDedicated, which a build against the client reference DLLs reads as always false.
            if (Player.m_localPlayer == null)
            {
                args.Context.AddString("title: there is no character here. Each player chooses their own with /wrath title in chat.");
                return;
            }

            string choice = args.Args.Length > 2 ? string.Join(" ", args.Args, 2, args.Args.Length - 2) : "";
            if (!TitleSync.RequestChoice(choice, args.Context))
                args.Context.AddString("title: could not reach the server.");
        }

        /// <summary>
        /// `wrath storm` shows what is in effect on THIS machine; `wrath storm set` changes it. A set
        /// writes the config entry, so it is saved to the file and survives a restart, and the config
        /// change is what pushes it into the live events and out to every client (see
        /// <see cref="StormLive"/>). Mutations follow the same authority rule as the rest of the
        /// console: typed on a pure client they forward through vanilla's admin gate.
        /// </summary>
        private static void Storm(Terminal.ConsoleEventArgs args)
        {
            string verb = args.Args.Length >= 3 ? args.Args[2].ToLowerInvariant() : "";
            if (verb == "start" || verb == "stop")
            {
                StormStartStop(args, verb == "start");
                return;
            }
            if (verb == "strike")
            {
                if (!Authority)
                {
                    if (!TryForward(args))
                        args.Context.AddString("wrath: lightning is thrown on the authority only (server console or listen host).");
                    return;
                }
                FireSystem.TryForceStrike(out string said);
                args.Context.AddString("storm: " + said);
                return;
            }

            if (args.Args.Length >= 3 && args.Args[2].ToLowerInvariant() == "set")
            {
                if (!Authority)
                {
                    if (!TryForward(args))
                        args.Context.AddString("wrath: mutations run on the authority only (server console or listen host).");
                    return;
                }
                if (args.Args.Length < 5)
                {
                    args.Context.AddString($"usage: wrath storm set <name> <value>  (names: {StormLive.AdminKeys})");
                    return;
                }
                // Anything after the name is the value, so a sky name with spaces would still arrive whole.
                string value = string.Join(" ", args.Args, 4, args.Args.Length - 4);
                bool ok = StormLive.TrySet(args.Args[3], value, out string message);
                args.Context.AddString(ok ? "storm: " + message + " (saved; live now)." : "storm: " + message);
                if (ok) RagnaroksWrath.Log.LogInfo($"wrath storm set {args.Args[3]} {value}: {message}");
                return;
            }

            bool fromServer = ServerSyncSetup.Sync != null && !ServerSyncSetup.Sync.IsSourceOfTruth;
            string text = StormLive.Describe(StormLive.FromConfig(), fromServer);
            if (!Authority && !fromServer)
                text += "\n(not synced from the server yet: these are this machine's own config's values)";
            args.Context.AddString(text);
        }

        /// <summary>
        /// `wrath storm start [dry|wet] [x z]` and `wrath storm stop`. Admin only, like every
        /// mutation here. Typed by a player, the command is forwarded with THEIR position added when
        /// none was given, because the server console that replays it has no player of its own.
        /// </summary>
        private static void StormStartStop(Terminal.ConsoleEventArgs args, bool start)
        {
            if (!Authority)
            {
                ZNet znet = ZNet.instance;
                if (znet == null || znet.IsServer())
                {
                    args.Context.AddString("wrath: storms start on the authority only (server console or listen host).");
                    return;
                }

                var line = new StringBuilder(string.Join(" ", args.Args));
                Player local = Player.m_localPlayer;
                bool needsHere = start && local != null &&
                                 StormLive.TryParseStart(args.Args, 3, out _, out UnityEngine.Vector3? given) && given == null;
                if (needsHere)
                {
                    UnityEngine.Vector3 p = local.transform.position;
                    line.Append(' ').Append(p.x.ToString("0.#", CultureInfo.InvariantCulture))
                        .Append(' ').Append(p.z.ToString("0.#", CultureInfo.InvariantCulture));
                }
                znet.RemoteCommand(line.ToString());
                RagnaroksWrath.Log.LogInfo($"wrath: forwarded '{line}'.");
                args.Context.AddString("wrath: sent to the server (admins only). The banner shows when the storm starts.");
                return;
            }

            string message;
            bool ok;
            if (start)
            {
                if (!StormLive.TryParseStart(args.Args, 3, out bool? dry, out UnityEngine.Vector3? at))
                {
                    args.Context.AddString("usage: wrath storm start [dry|wet] [x z]");
                    return;
                }
                // A listen host typing it with no coordinates means "here".
                if (at == null && Player.m_localPlayer != null) at = Player.m_localPlayer.transform.position;
                ok = WeatherSystem.TryForceStart(at, dry, out message);
            }
            else ok = WeatherSystem.TryForceStop(out message);

            args.Context.AddString("storm: " + message);
        }

        private static void SaveAll(Terminal.ConsoleEventArgs args)
        {
            if (!Authority)
            {
                if (!TryForward(args))
                    args.Context.AddString("wrath: nothing to save on a pure client.");
                return;
            }
            Persistence.Save(force: true);
            HealthStore.SaveIfDirty();
            RivalryLedger.SaveIfDirty();
            RelicLedger.SaveIfDirty();
            args.Context.AddString("all four stores flushed.");
        }
    }
}
