using System;
using System.Globalization;
using RavenIron.RagnaroksWrath.Config;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// The storm settings that are BAKED INTO a vanilla event, and so need pushing to it after a
    /// change: the forced sky, the duration and the range. Everything else a storm reads
    /// (lightning rate, plague multiplier, the dry chance) is read from config at the moment it is
    /// used and was always live.
    ///
    /// WHY THIS EXISTS. `WeatherSystem.RegisterStormEvent` copied these three values into the two
    /// `RandomEvent` objects once, when the world loaded, so an edit meant a full restart on EVERY
    /// machine (each client bakes its own copy from its own config; vanilla replicates only the
    /// event's NAME). This type is the pure half of making them live, in Core so the harness pins
    /// it: the settings snapshot, which sky each look wears, which keys need a push, and the
    /// `wrath storm` parsing. `WeatherSystem` pushes a snapshot into the events on every change.
    ///
    /// THE SERVER IS THE TRUTH through ServerSync (see ServerSyncSetup). On a client these config
    /// entries hold the server's values once the handshake has delivered them, and their arrival
    /// raises SettingChanged like any edit, so the same push follows it. A storm-only sync
    /// (StormSync) did this job briefly before ServerSync replaced it, and is gone.
    /// </summary>
    public static class StormLive
    {
        public struct Settings
        {
            public bool Force;
            public float Duration;
            public float Range;
            public string WetSky;
            public string DrySky;
        }

        public const int MaxSkyNameLength = 64;

        /// <summary>This machine's config, as a snapshot: on a synced client, the server's values.</summary>
        public static Settings FromConfig() => new Settings
        {
            Force = ModConfig.StormsForceWeather.Value,
            Duration = ModConfig.StormDurationSeconds.Value,
            Range = ModConfig.StormRangeMeters.Value,
            WetSky = ModConfig.StormForcedEnvironment.Value ?? "",
            DrySky = ModConfig.StormDryEnvironment.Value ?? "",
        };

        /// <summary>The sky an event of the given look carries: empty when no sky is forced.</summary>
        public static string SkyFor(Settings s, bool wet) => s.Force ? (wet ? s.WetSky : s.DrySky) : "";

        private static string CleanSky(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Trim();
            return name.Length > MaxSkyNameLength ? name.Substring(0, MaxSkyNameLength) : name;
        }

        /// <summary>The config keys whose value is baked into an event, so a change to one needs a push.</summary>
        public static bool IsBakedKey(string key)
            => key == "StormsForceWeather" || key == "StormDurationSeconds" || key == "StormRangeMeters"
               || key == "StormForcedEnvironment" || key == "StormDryEnvironment";

        // ---- the `wrath storm` console ---------------------------------------------------------

        public const string AdminKeys = "sky, duration, range, wetsky, drysky, drychance, lightning, lightningon";

        /// <summary>One line per setting, as the console prints them.</summary>
        public static string Describe(Settings effective, bool fromServer)
        {
            var c = CultureInfo.InvariantCulture;
            return
                $"storm sky forced: {(effective.Force ? "ON" : "off")} (wet '{effective.WetSky}', dry '{effective.DrySky}', " +
                $"dry chance {ModConfig.StormDryChance.Value.ToString("0.##", c)})\n" +
                $"storm lasts {effective.Duration.ToString("0.#", c)}s, reaches {effective.Range.ToString("0.#", c)}m" +
                (fromServer ? " (as the server set them)" : "") + "\n" +
                $"lightning {(ModConfig.StormLightningEnabled.Value ? "ON" : "off")}, about one bolt every " +
                $"{ModConfig.LightningMeanMinutes.Value.ToString("0.##", c)} min under a dry sky";
        }

        /// <summary>
        /// Change one storm setting by its console name. The value is written through the config
        /// entry, so BepInEx saves it to the file (it survives a restart) and raises SettingChanged,
        /// which is what pushes it into the live events and out to the clients.
        ///
        /// Reads the value back and reports THAT, because the entry clamps into its range rather
        /// than refusing: "duration 99999" lands as 3600, and saying "set to 99999" would be a lie.
        /// </summary>
        public static bool TrySet(string name, string text, out string message)
        {
            message = null;
            string key = (name ?? "").Trim().ToLowerInvariant();
            text = (text ?? "").Trim();
            if (text.Length == 0) { message = "give a value after the name."; return false; }

            switch (key)
            {
                case "sky":
                case "force":
                    if (!TryParseSwitch(text, out bool on)) { message = "sky takes on/off (or true/false)."; return false; }
                    ModConfig.StormsForceWeather.Value = on;
                    message = "StormsForceWeather = " + (ModConfig.StormsForceWeather.Value ? "on" : "off");
                    return true;
                case "lightningon":
                    if (!TryParseSwitch(text, out bool bolts)) { message = "lightningon takes on/off (or true/false)."; return false; }
                    ModConfig.StormLightningEnabled.Value = bolts;
                    message = "StormLightningEnabled = " + (ModConfig.StormLightningEnabled.Value ? "on" : "off");
                    return true;
                case "duration":
                    return SetNumber(ModConfig.StormDurationSeconds, "StormDurationSeconds", text, out message);
                case "range":
                    return SetNumber(ModConfig.StormRangeMeters, "StormRangeMeters", text, out message);
                case "drychance":
                    return SetNumber(ModConfig.StormDryChance, "StormDryChance", text, out message);
                case "lightning":
                    return SetNumber(ModConfig.LightningMeanMinutes, "LightningMeanMinutes", text, out message);
                case "wetsky":
                    ModConfig.StormForcedEnvironment.Value = CleanSky(text);
                    message = "StormForcedEnvironment = '" + ModConfig.StormForcedEnvironment.Value + "'";
                    return true;
                case "drysky":
                    ModConfig.StormDryEnvironment.Value = CleanSky(text);
                    message = "StormDryEnvironment = '" + ModConfig.StormDryEnvironment.Value + "'";
                    return true;
                default:
                    message = $"unknown storm setting '{name}' - settings: {AdminKeys}";
                    return false;
            }
        }

        /// <summary>
        /// The words after `wrath storm start`: an optional look (dry or wet) and optional x z,
        /// in either order. Nothing at all is valid and means "roll the look, pick the place".
        /// Refuses a lone number, a third number, NaN, or any other word, rather than guessing.
        /// The storm's area is a circle on the ground (XZ only), so the height is left at 0.
        /// </summary>
        public static bool TryParseStart(string[] args, int from, out bool? dry, out UnityEngine.Vector3? at)
        {
            dry = null;
            at = null;
            var numbers = new System.Collections.Generic.List<float>(2);
            for (int i = from; args != null && i < args.Length; i++)
            {
                string word = (args[i] ?? "").Trim().ToLowerInvariant();
                if (word.Length == 0) continue;
                if (word == "dry") { if (dry.HasValue) return false; dry = true; continue; }
                if (word == "wet") { if (dry.HasValue) return false; dry = false; continue; }
                if (!WrathAdmin.TryParseValue(word, out float n)) return false;
                numbers.Add(n);
            }
            if (numbers.Count == 1 || numbers.Count > 2) return false;
            if (numbers.Count == 2) at = new UnityEngine.Vector3(numbers[0], 0f, numbers[1]);
            return true;
        }

        private static bool SetNumber(BepInEx.Configuration.ConfigEntry<float> entry, string label, string text, out string message)
        {
            if (!WrathAdmin.TryParseValue(text, out float value)) { message = label + " takes a number."; return false; }
            entry.Value = value;
            float landed = entry.Value;
            message = label + " = " + landed.ToString("0.###", CultureInfo.InvariantCulture) +
                      (Math.Abs(landed - value) > 1e-4f ? " (clamped into its allowed range)" : "");
            return true;
        }

        private static bool TryParseSwitch(string text, out bool value)
        {
            switch (text.ToLowerInvariant())
            {
                case "on": case "true": case "1": case "yes": value = true; return true;
                case "off": case "false": case "0": case "no": value = false; return true;
                default: value = false; return false;
            }
        }
    }
}
