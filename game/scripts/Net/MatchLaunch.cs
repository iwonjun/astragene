using System;
using Godot;
using RtsGame.Sim.Commands;

namespace RtsGame.Net;

public enum MatchMode { Local, Network, Replay }

/// <summary>Scene-to-scene match configuration. The lobby fills it; match/replay scenes read it once.</summary>
public static class MatchLaunch
{
    public const string DefaultMap = "res://game/maps/duel.map";
    public static MatchMode Mode { get; set; } = MatchMode.Local;
    public static int LocalPlayer { get; set; }
    public static int[] Factions { get; set; } = { 0, 1 };
    public static int[] Players { get; set; } = { 0, 1 };
    public static ulong Seed { get; set; } = 1;
    public static int InputDelay { get; set; } = 3;
    public static string MapPath { get; set; } = DefaultMap;
    public static NetworkSession? Session { get; set; }
    public static ReplayLog? Replay { get; set; }
    public static string ReplayOut { get; set; } = "";
    /// <summary>-1 no opponent, 0 easy, 1 medium, 2 hard; the computer opponent of local matches.</summary>
    public static int AiDifficulty { get; set; } = -1;

    public static void ResetLocal()
    {
        Mode = MatchMode.Local; LocalPlayer = 0; Factions = new[] { 0, 1 }; Players = new[] { 0, 1 }; Seed = 1; InputDelay = 3;
        MapPath = DefaultMap; AiDifficulty = -1; Session?.Close(); Session = null; Replay = null;
    }

    /// <summary>Command line value such as <c>--replay-out=user://soak.agr</c> after the <c>--</c> separator.</summary>
    public static string Arg(string name, string fallback = "")
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--" + name + "=", StringComparison.Ordinal)) return arg.Substring(name.Length + 3);
        return fallback;
    }
    public static bool Flag(string name) => Array.IndexOf(OS.GetCmdlineUserArgs(), "--" + name) >= 0;

    public static string SaveReplay(ReplayLog log, string path = "")
    {
        if (string.IsNullOrEmpty(path)) path = ReplayOut;
        if (string.IsNullOrEmpty(path)) path = $"user://replays/replay_{DateTime.Now:yyyyMMdd_HHmmss}.agr";
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir()); DirAccess.MakeDirRecursiveAbsolute("user://replays");
        var bytes = log.Encode();
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write)) { if (file == null) return ""; file.StoreBuffer(bytes); }
        using (var last = FileAccess.Open("user://replays/last.agr", FileAccess.ModeFlags.Write)) last?.StoreBuffer(bytes);
        return path;
    }
}
