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
    /// it: the settings snapshot and its sanitising, what a client believes the server said, and
    /// the `wrath storm set` parsing. `WeatherSystem` pushes a snapshot into the events and
    /// `StormSync` carries the server's snapshot to the clients.
    ///
    /// THE SERVER IS THE TRUTH. A client that has heard from the server uses its snapshot and
    /// ignores its own config for these keys, so every player sees the sky the owner chose without
    /// editing a file; one that has not (an older server, a join still in flight) falls back to its
    /// own config, which is what it did before this existed.
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

        // Ranges mirror the config entries' own AcceptableValueRange. A client is handed these by
        // the network, so it enforces them itself rather than trusting the sender to have.
        public const float MinDuration = 30f, MaxDuration = 3600f;
        public const float MinRange = 32f, MaxRange = 1024f;
        public const int MaxSkyNameLength = 64;

        private static Settings _fromServer;

        /// <summary>True once the server has told this machine its storm settings this session.</summary>
        public static bool HasServerValue { get; private set; }

        /// <summary>This machine's own config, as a snapshot.</summary>
        public static Settings FromConfig() => new Settings
        {
            Force = ModConfig.StormsForceWeather.Value,
            Duration = ModConfig.StormDurationSeconds.Value,
            Range = ModConfig.StormRangeMeters.Value,
            WetSky = ModConfig.StormForcedEnvironment.Value ?? "",
            DrySky = ModConfig.StormDryEnvironment.Value ?? "",
        };

        /// <summary>
        /// What this machine's events should carry. The authority's own config is the truth and is
        /// never overridden; a client uses the server's snapshot once it has one.
        /// </summary>
        public static Settings Effective(bool isAuthority)
            => (!isAuthority && HasServerValue) ? _fromServer : FromConfig();

        /// <summary>The sky an event of the given look carries: empty when no sky is forced.</summary>
        public static string SkyFor(Settings s, bool wet) => s.Force ? (wet ? s.WetSky : s.DrySky) : "";

        /// <summary>
        /// Refuses a snapshot a client should not act on, and clamps what can be clamped. A NaN or
        /// infinite number is refused outright: clamping it would pick a number nobody sent.
        /// </summary>
        public static bool TrySanitise(ref Settings s)
        {
            if (float.IsNaN(s.Duration) || float.IsInfinity(s.Duration)) return false;
            if (float.IsNaN(s.Range) || float.IsInfinity(s.Range)) return false;

            s.Duration = Math.Max(MinDuration, Math.Min(MaxDuration, s.Duration));
            s.Range = Math.Max(MinRange, Math.Min(MaxRange, s.Range));
            s.WetSky = CleanSky(s.WetSky);
            s.DrySky = CleanSky(s.DrySky);
            return true;
        }

        private static string CleanSky(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Trim();
            return name.Length > MaxSkyNameLength ? name.Substring(0, MaxSkyNameLength) : name;
        }

        /// <summary>Remember the server's snapshot. Returns false (and remembers nothing) when it is unusable.</summary>
        public static bool AcceptFromServer(Settings s)
        {
            if (!TrySanitise(ref s)) return false;
            _fromServer = s;
            HasServerValue = true;
            return true;
        }

        /// <summary>A new world session: whatever the last server said is not this server's word.</summary>
        public static void ForgetServer()
        {
            HasServerValue = false;
            _fromServer = default(Settings);
        }

        /// <summary>
        /// True when the event data differ in a way that needs pushing. Compared the way the events
        /// store them, so a float round-trip never reads as a change.
        /// </summary>
        public static bool Differs(Settings a, Settings b)
            => a.Force != b.Force
               || Math.Abs(a.Duration - b.Duration) > 0.001f
               || Math.Abs(a.Range - b.Range) > 0.001f
               || !string.Equals(a.WetSky, b.WetSky, StringComparison.Ordinal)
               || !string.Equals(a.DrySky, b.DrySky, StringComparison.Ordinal);

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
