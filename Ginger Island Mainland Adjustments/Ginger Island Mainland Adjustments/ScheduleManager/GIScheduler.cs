﻿#if DEBUG
using System.Diagnostics;
using System.Runtime;
#endif

using System.Text;

using AtraBase.Toolkit;
using AtraBase.Toolkit.Extensions;

using AtraCore;

using AtraCore.Framework.Caches;
using AtraCore.Framework.ReflectionManager;

using AtraShared.Schedules.DataModels;
using AtraShared.Utils;
using AtraShared.Utils.Extensions;

using GingerIslandMainlandAdjustments.AssetManagers;
using GingerIslandMainlandAdjustments.CustomConsoleCommands;
using GingerIslandMainlandAdjustments.ScheduleManager.DataModels;

using Microsoft.Xna.Framework;

using StardewModdingAPI.Utilities;

using StardewValley.Locations;

namespace GingerIslandMainlandAdjustments.ScheduleManager;

/// <summary>
/// Class that handles scheduling if the <see cref="ModConfig.UseThisScheduler"/> option is set.
/// </summary>
internal static class GIScheduler
{
    #region delegates

    private static readonly Lazy<Func<NPC, string, string, List<string>>> GetLocationRouteLazy = new(() =>
        typeof(NPC).GetCachedMethod("getLocationRoute", ReflectionCache.FlagTypes.InstanceFlags)
        .CreateDelegate<Func<NPC, string, string, List<string>>>());

    #endregion

    private static readonly int[] TIMESLOTS = new int[] { 1200, 1400, 1600 };

    /// <summary>
    /// The starting point where NPCs staged at the saloon start.
    /// </summary>
    private static readonly Point SaloonStart = new(8, 11);

    #region groups

    /// <summary>
    /// Dictionary of possible island groups. Null is a cache miss.
    /// </summary>
    /// <remarks>Use the getter, which will automatically grab from fake asset.</remarks>
    private static Dictionary<string, HashSet<NPC>>? islandGroups = null;

    /// <summary>
    /// Dictionary of possible explorer groups. Null is a cache miss.
    /// </summary>
    /// <remarks>Use the getter, which will automatically grab from fake asset.</remarks>
    private static Dictionary<string, HashSet<NPC>>? explorerGroups = null;

    /// <summary>
    /// Gets the current group headed off to the island.
    /// </summary>
    /// <remarks>null means no current group.</remarks>
    internal static string? CurrentGroup { get; private set; }

    /// <summary>
    /// Gets the current visiting group.
    /// </summary>
    /// <remarks>Used primarily for setting group-based dialogue...</remarks>
    internal static HashSet<NPC>? CurrentVisitingGroup { get; private set; }

    /// <summary>
    /// Gets the name of the current adventure group.
    /// </summary>
    internal static string? CurrentAdventureGroup { get; private set; }

    #endregion

    #region individuals

    /// <summary>
    /// Gets the current adventure group.
    /// </summary>
    internal static HashSet<NPC>? CurrentAdventurers { get; private set; }

    /// <summary>
    /// Gets the current bartender.
    /// </summary>
    internal static NPC? Bartender { get; private set; }

    /// <summary>
    /// Gets the current musician.
    /// </summary>
    internal static NPC? Musician { get; private set; }

    #endregion

    /// <summary>
    /// Gets island groups. Will automatically load if null.
    /// </summary>
    private static Dictionary<string, HashSet<NPC>> IslandGroups
        => islandGroups ??= AssetLoader.GetCharacterGroup(SpecialGroupType.Groups);

    /// <summary>
    /// Gets explorer groups. Will automatically load if null.
    /// </summary>
    private static Dictionary<string, HashSet<NPC>> ExplorerGroups
        => explorerGroups ??= AssetLoader.GetCharacterGroup(SpecialGroupType.Explorers);

    /// <summary>
    /// Clears the cached values for this class.
    /// </summary>
    internal static void ClearCache()
    {
        islandGroups = null;
        explorerGroups = null;
    }

    /// <summary>
    /// Deletes references to the current group at the end of the day.
    /// </summary>
    internal static void DayEndReset()
    {
        CurrentGroup = null;
        CurrentVisitingGroup = null;
        CurrentAdventureGroup = null;
        CurrentAdventurers = null;
    }

    /// <summary>
    /// Generates schedules for everyone.
    /// </summary>
    internal static void GenerateAllSchedules()
    {
        Game1.netWorldState.Value.IslandVisitors.Clear();
        if (Game1.getLocationFromName("IslandSouth") is not IslandSouth island || !island.resortRestored.Value
            || !island.resortOpenToday.Value || Game1.IsRainingHere(island)
            || Utility.isFestivalDay(Game1.Date.DayOfMonth, Game1.Date.Season)
            || (Game1.Date.DayOfMonth >= 15 && Game1.Date.DayOfMonth <= 17 && Game1.IsWinter))
        {
            return;
        }

        Globals.ModMonitor.DebugOnlyLog("GI schedules being generated by mod.");

#if DEBUG
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif
        Random random = RandomUtils.GetSeededRandom(3, "atravita.GingerIslandMainlandAdjustments");

        (HashSet<NPC> explorers, string explorerGroupName) = GenerateExplorerGroup(random);
        if (explorers.Count > 0)
        {
            Globals.ModMonitor.DebugOnlyLog($"Found explorer group: {string.Join(", ", explorers.Select((NPC npc) => npc.Name))}.");
            IslandNorthScheduler.Schedule(random, explorers, explorerGroupName);
        }

        // Resort capacity set to zero, can skip everything else.
        if (Globals.Config.Capacity == 0 && (Globals.SaveDataModel is null || Globals.SaveDataModel.NPCsForTomorrow.Count == 0))
        {
            IslandSouthPatches.ClearCache();
            GIScheduler.ClearCache();
#if DEBUG
            stopwatch.Stop();
            Globals.ModMonitor.Log($"GI Scheduler did not need to run, took {stopwatch.Elapsed.TotalMilliseconds:F2} ms anyways", LogLevel.Info);
#endif
            return;
        }

        List<NPC> visitors = GenerateVisitorList(random, Globals.Config.Capacity, explorers);
        Dictionary<string, string> animationDescriptions = Globals.GameContentHelper.Load<Dictionary<string, string>>("Data/animationDescriptions");

        GIScheduler.Bartender = SetBartender(visitors);
        GIScheduler.Musician = SetMusician(random, visitors, animationDescriptions);

        List<GingerIslandTimeSlot> activities = AssignIslandSchedules(random, visitors, animationDescriptions);
        Dictionary<NPC, string> schedules = RenderIslandSchedules(random, visitors, activities);

        foreach ((NPC npc, string schedule) in schedules)
        {
            if (ScheduleUtilities.ParseMasterScheduleAdjustedForChild2NPC(npc, schedule))
            {
                Globals.ModMonitor.DebugLog($"Calculated island schedule for {npc.Name}");
                npc.islandScheduleName.Value = "island";
                Game1.netWorldState.Value.IslandVisitors[npc.Name] = true;
                ConsoleCommands.IslandSchedules[npc.Name] = schedules[npc];
            }
            else
            {
                npc.islandScheduleName.Value = string.Empty;
            }
        }

        IslandSouthPatches.ClearCache();
        GIScheduler.ClearCache();

#if DEBUG
        stopwatch.Stop();
        Globals.ModMonitor.LogTimespan("Schedule generation", stopwatch);

        if (Context.IsSplitScreen && Context.ScreenId != 0)
        {
            return;
        }

        Globals.ModMonitor.Log($"Current memory usage {GC.GetTotalMemory(false):N0}", LogLevel.Alert);
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        Globals.ModMonitor.Log($"Post-collection memory usage {GC.GetTotalMemory(true):N0}", LogLevel.Alert);
#endif
    }

    /// <summary>
    /// Yields a group of valid explorers.
    /// </summary>
    /// <param name="random">Seeded random.</param>
    /// <returns>An explorer group (of up to three explorers), or an empty hashset if there's no group today.</returns>
    private static (HashSet<NPC> group, string groupname) GenerateExplorerGroup(Random random)
    {
        if (random.OfChance(Globals.Config.ExplorerChance))
        {
            List<string> explorerGroups = ExplorerGroups.Keys.ToList();
            if (explorerGroups.Count > 0)
            {
                CurrentAdventureGroup = explorerGroups[random.Next(explorerGroups.Count)];
                CurrentAdventurers = ExplorerGroups[CurrentAdventureGroup].Where(IslandSouth.CanVisitIslandToday).Take(3).ToHashSet();
                return (CurrentAdventurers, CurrentAdventureGroup);
            }
        }
        return (new HashSet<NPC>(), string.Empty); // just return an empty hashset.
    }

    /// <summary>
    /// Gets the visitor list for a specific day. Explorers can't be visitors, so remove them.
    /// </summary>
    /// <param name="random">Random to use to select.</param>
    /// <param name="capacity">Maximum number of people to allow on the island.</param>
    /// <param name="explorers">Hashset of explorers.</param>
    /// <returns>Visitor List.</returns>
    /// <remarks>For a deterministic island list, use a Random seeded with the uniqueID + number of days played.</remarks>
    private static List<NPC> GenerateVisitorList(Random random, int capacity, HashSet<NPC> explorers)
    {
        CurrentGroup = null;
        CurrentVisitingGroup = null;
        CurrentAdventureGroup = null;
        CurrentAdventurers = null;

        List<NPC> visitors = new(capacity);
        HashSet<NPC> valid_visitors = new(64); // this is probably an undercount, but better than 4.

        // For some reason, Utility.GetAllCharacters searches the farm too.
        foreach (NPC npc in NPCHelpers.GetNPCs())
        {
            if (npc is not null && IslandSouth.CanVisitIslandToday(npc) && !explorers.Contains(npc))
            {
                valid_visitors.Add(npc);
            }
        }

        if (Globals.SaveDataModel is not null)
        {
            foreach (string npcname in Globals.SaveDataModel.NPCsForTomorrow)
            {
                NPC? npc = NPCCache.GetByVillagerName(npcname);
                if (npc is null)
                {
                    Globals.ModMonitor.Log($"{npcname} could not be located.", LogLevel.Warn);
                    continue;
                }

                visitors.Add(npc);
                if (!valid_visitors.Contains(npc))
                {
                    Globals.ModMonitor.Log($"{npcname} queued for Island DESPITE exclusion!", LogLevel.Warn);
                }
            }
            Globals.SaveDataModel.NPCsForTomorrow.Clear();
        }

        if (random.OfChance(Globals.Config.GroupChance))
        {
            List<string> groupkeys = new(IslandGroups.Count);
            foreach (string key in IslandGroups.Keys)
            {
                // Filter out groups where one member can't make it or are too big
                // Except for spouses, we'll just randomly pick until we hit the capacity later.
                if ((IslandGroups[key].Count <= capacity - visitors.Count || key == "allSpouses")
                    && IslandGroups[key].All(valid_visitors.Contains))
                {
                    groupkeys.Add(key);
                }
            }

            if (groupkeys.Count > 0)
            {
                CurrentGroup = Utility.GetRandom(groupkeys, random);
                Globals.ModMonitor.DebugOnlyLog($"Group {CurrentGroup} headed to Island.", LogLevel.Debug);

                HashSet<NPC>? group = IslandGroups[CurrentGroup];
                if (CurrentGroup == "allSpouses" && group.Count > capacity)
                {
                    group = group.OrderBy((_) => Singletons.Random.Next()).Take(capacity).ToHashSet();
                }

                visitors.AddRange(group);
                CurrentVisitingGroup = group;
                valid_visitors.ExceptWith(visitors);
            }
        }

        // Add Gus (even if we go over capacity, he has a specific standing spot).
        if (NPCCache.GetByVillagerName("Gus") is NPC gus && !visitors.Contains(gus) && valid_visitors.Contains(gus)
            && Globals.Config.GusDayAsShortString().Equals(Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth), StringComparison.OrdinalIgnoreCase)
            && random.OfChance(Globals.Config.GusChance))
        {
            Globals.ModMonitor.DebugOnlyLog($"Forcibly adding Gus.");
            visitors.Add(gus);
            valid_visitors.Remove(gus);
        }

        // Prevent children and anyone with the neveralone exclusion from going alone.
        int kidsremoved = valid_visitors.RemoveWhere((NPC npc) => npc.Age == NPC.child
            && (!IslandSouthPatches.Exclusions.TryGetValue(npc, out string[]? exclusions) || !exclusions.Contains("freerange")));
        int neveralone = valid_visitors.RemoveWhere((NPC npc) => IslandSouthPatches.Exclusions.TryGetValue(npc, out string[]? exclusions)
            && exclusions.Contains("neveralone"));

        if (Globals.Config.DebugMode)
        {
            Globals.ModMonitor.Log($"Excluded {kidsremoved} kids and {neveralone} never alone villagers from the valid villagers list");
        }

        if (visitors.Count < capacity)
        {
            Globals.ModMonitor.DebugOnlyLog($"{capacity} not yet reached, attempting to add more.", LogLevel.Debug);
            visitors.AddRange(valid_visitors.OrderBy(a => random.Next()).Take(capacity - visitors.Count));
        }

        {
            // If George in visitors, add Evelyn.
            if (visitors.Any((NPC npc) => npc.Name.Equals("George", StringComparison.OrdinalIgnoreCase))
                && visitors.All((NPC npc) => !npc.Name.Equals("Evelyn", StringComparison.OrdinalIgnoreCase))
                && NPCCache.GetByVillagerName("Evelyn") is NPC evelyn)
            {
                // counting backwards to avoid kicking out a group member.
                for (int i = visitors.Count - 1; i >= 0; i--)
                {
                    if (!visitors[i].Name.Equals("Gus", StringComparison.OrdinalIgnoreCase) && !visitors[i].Name.Equals("George", StringComparison.OrdinalIgnoreCase))
                    {
                        Globals.ModMonitor.DebugOnlyLog($"Replacing one visitor {visitors[i].Name} with Evelyn");
                        visitors[i] = evelyn;
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < visitors.Count; i++)
        {
            visitors[i].scheduleDelaySeconds = Math.Min(i * 0.4f, 7f);
        }

        {
            // set schedule Delay for George and Evelyn so they arrive together (in theory)?
            if (visitors.FirstOrDefault((NPC npc) => npc.Name.Equals("George", StringComparison.OrdinalIgnoreCase)) is NPC george
                && visitors.FirstOrDefault((NPC npc) => npc.Name.Equals("Evelyn", StringComparison.OrdinalIgnoreCase)) is NPC evelyn)
            {
                george.scheduleDelaySeconds = 7f;
                evelyn.scheduleDelaySeconds = 6.8f;
            }
        }

        Globals.ModMonitor.DebugOnlyLog($"{visitors.Count} visitors: {string.Join(", ", visitors.Select((NPC npc) => npc.Name))}");
        IslandSouthPatches.ClearCache();

        return visitors;
    }

    /// <summary>
    /// Returns either Gus if he's visiting, or a valid bartender from the bartender list.
    /// </summary>
    /// <param name="visitors">List of possible visitors for the day.</param>
    /// <returns>Bartender if it can find one, null otherwise.</returns>
    private static NPC? SetBartender(List<NPC> visitors)
    {
        NPC? bartender = visitors.Find(static (NPC npc) => npc.Name.Equals("Gus", StringComparison.OrdinalIgnoreCase));

        // Gus not visiting, go find another bartender
        if (bartender is null)
        {
            HashSet<NPC> bartenders = AssetLoader.GetSpecialCharacter(SpecialCharacterType.Bartender);
            if (bartenders.Count > 0)
            {
                bartender = visitors.Where(bartenders.Contains).OrderBy(_ => Singletons.Random.Next()).FirstOrDefault();
            }
        }
        if (bartender is not null)
        {
            bartender.currentScheduleDelay = 0f;
        }
        return bartender;
    }

    /// <summary>
    /// Returns a possible musician. Prefers Sam.
    /// </summary>
    /// <param name="random">The seeded random.</param>
    /// <param name="visitors">List of visitors.</param>
    /// <param name="animationDescriptions">Animation descriptions dictionary (pass this in to avoid rereading it).</param>
    /// <returns>Musician if it finds one.</returns>
    private static NPC? SetMusician(Random random, List<NPC> visitors, Dictionary<string, string> animationDescriptions)
    {
        NPC? musician = null;
        if (animationDescriptions.ContainsKey("sam_beach_towel"))
        {
            musician = visitors.Find(static (NPC npc) => npc.Name.Equals("Sam", StringComparison.OrdinalIgnoreCase));
        }
        if (musician is null || random.OfChance(0.25))
        {
            HashSet<NPC> musicians = AssetLoader.GetSpecialCharacter(SpecialCharacterType.Musician);
            if (musicians.Count > 0)
            {
                musician = visitors.Where((NPC npc) => musicians.Contains(npc) && animationDescriptions.ContainsKey($"{npc.Name.ToLowerInvariant()}_beach_towel"))
                                   .OrderBy(_ => Singletons.Random.Next()).FirstOrDefault() ?? musician;
            }
        }
        if (musician is not null && !musician.Name.Equals("Gus", StringComparison.OrdinalIgnoreCase))
        {
            musician.currentScheduleDelay = 0f;
            Globals.ModMonitor.DebugOnlyLog($"Found musician {musician.Name}", LogLevel.Debug);
            return musician;
        }
        return null;
    }

    /// <summary>
    /// Assigns everyone their island schedules for the day.
    /// </summary>
    /// /// <param name="random">Seeded random.</param>
    /// <param name="visitors">List of visitors.</param>
    /// <param name="animationDescriptions">the animations description dictionary.</param>
    /// <returns>A list of filled <see cref="GingerIslandTimeSlot"/>s.</returns>
    private static List<GingerIslandTimeSlot> AssignIslandSchedules(Random random, List<NPC> visitors, Dictionary<string, string> animationDescriptions)
    {
        Dictionary<NPC, string> lastactivity = new(visitors.Count);
        List<GingerIslandTimeSlot> activities = TIMESLOTS.Select((i) => new GingerIslandTimeSlot(i, Bartender, Musician, random, visitors)).ToList();

        foreach (GingerIslandTimeSlot activity in activities)
        {
            lastactivity = activity.AssignActivities(lastactivity, animationDescriptions);
        }

        return activities;
    }

    /// <summary>
    /// Takes a list of activities and renders them as proper schedules.
    /// </summary>
    /// <param name="random">Seeded random.</param>
    /// <param name="visitors">List of visitors.</param>
    /// <param name="activities">List of activities.</param>
    /// <returns>Dictionary of NPC->raw schedule strings.</returns>
    private static Dictionary<NPC, string> RenderIslandSchedules(Random random, List<NPC> visitors, List<GingerIslandTimeSlot> activities)
    {
        Dictionary<NPC, string> completedSchedules = new(visitors.Count);
        int saloon_offset = 0;

        StringBuilder sb = StringBuilderCache.Acquire();

        foreach (NPC visitor in visitors)
        {
            sb.Clear();

            if (Globals.Config.StageFarNpcsAtSaloon)
            {
                try
                {
                    List<string>? maplist = GetLocationRouteLazy.Value(visitor, visitor.DefaultMap, "IslandSouth");
                    if (maplist is null || maplist.Count > 8)
                    {
                        Globals.ModMonitor.Log($"{visitor.Name} has a long way to travel, so staging them at the Saloon.");
                        new SchedulePoint(
                            random: random,
                            npc: visitor,
                            map: "Saloon",
                            time: 0,
                            point: new(SaloonStart.X + ++saloon_offset, SaloonStart.Y)).AppendToStringBuilder(sb);
                        sb.Append('/');
                    }
                }
                catch (Exception ex)
                {
                    Globals.ModMonitor.LogError($"checking if visitor has a long way to travel.", ex);
                }
            }

            bool should_dress = IslandSouth.HasIslandAttire(visitor);
            if (should_dress)
            {
                new SchedulePoint(
                    random: random,
                    npc: visitor,
                    map: "IslandSouth",
                    time: 1150,
                    point: IslandSouth.GetDressingRoomPoint(visitor),
                    animation: "change_beach",
                    isarrivaltime: true).AppendToStringBuilder(sb);
                sb.Append('/');
            }

            for (int i = 0; i < activities.Count; i++)
            {
                GingerIslandTimeSlot activity = activities[i];
                if (activity.Assignments.TryGetValue(visitor, out SchedulePoint? schedulePoint))
                {
                    if (i == 0 && !should_dress)
                    {
                        schedulePoint.IsArrivalTime = true;
                    }
                    schedulePoint.AppendToStringBuilder(sb);
                    sb.Append('/');
                }
            }

            if (should_dress)
            {
                new SchedulePoint(
                    random: random,
                    npc: visitor,
                    map: "IslandSouth",
                    time: 1730,
                    point: IslandSouth.GetDressingRoomPoint(visitor),
                    animation: "change_normal",
                    isarrivaltime: true).AppendToStringBuilder(sb);
                sb.Append('/');
            }

            sb.AppendCorrectRemainderSchedule(visitor);

            completedSchedules[visitor] = string.Join('/', sb.ToString());
            Globals.ModMonitor.DebugOnlyLog($"For {visitor.Name}, created island schedule {completedSchedules[visitor]}");
        }

        StringBuilderCache.Release(sb);
        return completedSchedules;
    }
}