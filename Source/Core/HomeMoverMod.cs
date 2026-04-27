using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RimWorld;
using Verse;

namespace HomeMover
{
    public class HomeMoverMod : Mod
    {
        private static FieldInfo _rootdir = typeof(ModContentPack).GetField(
            "rootDirInt",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        public static DateTime CreationTime;
        public static HomeMoverSetting Setting { get; private set; }

        private static List<string> queuedLogs = new List<string>();
        private static bool readyToLog = false;

        // Deduplication: suppress identical messages logged within this many ticks.
        private const int DebugLogThrottleTicks = 300; // ~5 seconds at 60 tps
        private static readonly Dictionary<string, int> _lastLoggedTick = new Dictionary<string, int>();

        public HomeMoverMod(ModContentPack content)
            : base(content)
        {
            Setting = GetSettings<HomeMoverSetting>();
            CreationTime = (_rootdir.GetValue(this.Content) as DirectoryInfo).CreationTimeUtc;

            readyToLog = true;
            FlushQueuedLogs();

            Log.Message("[HomeMover] Mod initialized.");
        }

        public override string SettingsCategory() => UIText.Label.TranslateSimple();

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Setting.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Safe debug logger: respects dev mode and mod setting.
        /// Identical messages are throttled to once per 300 ticks to prevent log spam.
        /// </summary>
        public static void DebugLog(string msg)
        {
            if (!readyToLog)
            {
                queuedLogs.Add(msg);
                return;
            }

            if (!Prefs.DevMode || !(Setting?.enableDebugLogging ?? false))
                return;

            int now = Current.Game?.tickManager?.TicksGame ?? 0;
            if (_lastLoggedTick.TryGetValue(msg, out int lastTick) && now - lastTick < DebugLogThrottleTicks)
                return;

            _lastLoggedTick[msg] = now;
            Log.Message($"[HomeMover] {msg}");
        }

        private static void FlushQueuedLogs()
        {
            if (Prefs.DevMode && (Setting?.enableDebugLogging ?? false))
            {
                foreach (var msg in queuedLogs)
                {
                    Log.Message($"[HomeMover] {msg}");
                }
            }
            queuedLogs.Clear();
        }
    }
}
