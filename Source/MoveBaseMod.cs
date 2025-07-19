using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Verse;
using HarmonyLib;
using RimWorld;

namespace MoveBase
{
    public class MoveBaseMod : Mod
    {
        private static FieldInfo _rootdir = typeof(ModContentPack).GetField("rootDirInt", BindingFlags.NonPublic | BindingFlags.Instance);

        public static DateTime CreationTime;
        public static MoveBaseSetting Setting { get; private set; }

        private static List<string> queuedLogs = new List<string>();
        private static bool readyToLog = false;

        public MoveBaseMod(ModContentPack content) : base(content)
        {
            Setting = GetSettings<MoveBaseSetting>();
            CreationTime = (_rootdir.GetValue(this.Content) as DirectoryInfo).CreationTimeUtc;

            readyToLog = true;
            FlushQueuedLogs();

            Log.Message("[MoveBase] Mod initialized.");
        }

        public override string SettingsCategory() => "Move Base";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Setting.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Safe debug logger: respects dev mode and mod setting.
        /// </summary>
        public static void DebugLog(string msg)
        {
            if (!readyToLog)
            {
                queuedLogs.Add(msg);
                return;
            }

            if (Prefs.DevMode && (Setting?.enableDebugLogging ?? false))
            {
                Log.Message($"[MoveBase] {msg}");
            }
        }

        private static void FlushQueuedLogs()
        {
            if (Prefs.DevMode && (Setting?.enableDebugLogging ?? false))
            {
                foreach (var msg in queuedLogs)
                {
                    Log.Message($"[MoveBase] {msg}");
                }
            }
            queuedLogs.Clear();
        }
    }
}

