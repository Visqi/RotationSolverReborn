﻿using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;
using RotationSolver.Basic.Configuration.Timeline;
using RotationSolver.UI;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace RotationSolver.Updaters;
internal static partial class RaidTimeUpdater
{
    internal static readonly Dictionary<uint, string> PathForRaids = [];

    private static readonly Dictionary<uint, TimelineItem[]> _savedTimeLines = [];
    private static readonly Queue<(DateTime, ITimelineItem)> _addedItems = new();

    public static string GetLink(uint id)
    {
        if (!PathForRaids.TryGetValue(id, out var path)) return string.Empty;
        return "https://github.com/xpdota/event-trigger/tree/master/timelines/src/main/resources/timeline/" + path;
    }

    internal static void UpdateTimeline()
    {
        if (!Service.Config.Timeline.TryGetValue(Svc.ClientState.TerritoryType, out var timeline)) return;

        while (_addedItems.TryPeek(out var addedItem))
        {
            if (DateTime.Now - addedItem.Item1 < TimeSpan.FromSeconds(5)) break;
            _addedItems.Dequeue();
        }

        foreach (var item in DataCenter.TimelineItems)
        {
            var time = item.Time - DataCenter.RaidTimeRaw;

            if (time < 0) continue;
            if (!timeline.TryGetValue(item.Time, out var items)) continue;


            var validItems = items.Where(i => !_addedItems.Any(added => added.Item2 == i) && i.InPeriod(item));

            foreach (var item2 in validItems.OfType<ActionTimelineItem>())
            {
                var act = DataCenter.RightNowRotation?.AllBaseActions.FirstOrDefault(a => (ActionID)a.ID == item2.ID);

                if (act == null) continue;

                DataCenter.AddCommandAction(act, Service.Config.SpecialDuration);
                _addedItems.Enqueue((DateTime.Now, item2));

#if DEBUG
                Svc.Log.Debug($"Added the action {act} to timeline.");
#endif
            }

            foreach (var item2 in validItems.OfType<StateTimelineItem>())
            {
                DataCenter.SpecialType = item2.State;

                _addedItems.Enqueue((DateTime.Now, item2));
#if DEBUG
                Svc.Log.Debug($"Added the state {item2.State} to timeline.");
#endif
            }
        }
    }

    internal static async void EnableAsync()
    {
        Svc.DutyState.DutyWiped += DutyState_DutyWiped;
        Svc.DutyState.DutyCompleted += DutyState_DutyWiped;

        using var client = new HttpClient();
        var message = await client.GetAsync("https://raw.githubusercontent.com/xpdota/event-trigger/master/timelines/src/main/resources/timelines.csv");

        if (!message.IsSuccessStatusCode)
        {
            return;
        }

        var str = await message.Content.ReadAsStringAsync();

        foreach (var pair in str.Split('\n'))
        {
            var items = pair.Split(',');
            if (items.Length < 2) continue;

            if (!uint.TryParse(items[0], out var id)) continue;
            var name = items[1][1..^1];
            PathForRaids[id] = name;
        }

        Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
        ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);

        Svc.GameNetwork.NetworkMessage += GameNetwork_NetworkMessage;
        Svc.Chat.ChatMessage += Chat_ChatMessage;
    }

    internal static void Disable() 
    {
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.DutyState.DutyWiped -= DutyState_DutyWiped;
        Svc.DutyState.DutyCompleted -= DutyState_DutyWiped;
        Svc.GameNetwork.NetworkMessage -= GameNetwork_NetworkMessage;
        Svc.Chat.ChatMessage -= Chat_ChatMessage;
    }

    private static void DutyState_DutyWiped(object? sender, ushort e)
    {
        DataCenter.RaidTimeRaw = -1;
    }

    private static void Chat_ChatMessage(Dalamud.Game.Text.XivChatType type, uint senderId, ref Dalamud.Game.Text.SeStringHandling.SeString sender, ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
    {
        var name = GetNameFromObjectId(senderId);

#if DEBUG
        //Svc.Log.Debug(sender.TextValue.ToString());
#endif

        foreach (var item in DataCenter.TimelineItems)
        {
            if (item.IsShown) continue;
            if (item.Time < DataCenter.RaidTimeRaw) continue;
            if (item.Type is not TimelineType.GameLog) continue;

            var typeString = ((uint)type).ToString("X4");
            if (!item["code", typeString]) continue;
            if (!item["name", name]) continue;
            if (!item["line", message.TextValue]) continue;

            item.UpdateRaidTimeOffset();
            break;
        }
    }

    private static void GameNetwork_NetworkMessage(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, Dalamud.Game.Network.NetworkMessageDirection direction)
    {
        if (direction != Dalamud.Game.Network.NetworkMessageDirection.ZoneDown) return;
        OpCode op = (OpCode)opCode;

        switch (op)
        {
            case OpCode.SystemLogMessage:
                OnSystemLogMessage(dataPtr);
                break;
            case OpCode.ActorControlTarget:
            case OpCode.ActorControlSelf:
            case OpCode.ActorControl:
                OnActorControl(dataPtr);
                break;

            case OpCode.Effect:
                OnEffect(dataPtr, targetActorId);
                break;

            case OpCode.ActorCast:
                OnCast(dataPtr, targetActorId);
                break;
        }
    }

    private static void OnCast(IntPtr dataPtr, uint targetActorId)
    {
        var name = GetNameFromObjectId(targetActorId);

        foreach (var item in DataCenter.TimelineItems)
        {
            if (item.IsShown) continue;
            if (item.Time < DataCenter.RaidTimeRaw) continue;
            if (item.Type is not TimelineType.StartsUsing) continue;

            if (!item["id", ReadUshort(dataPtr, 0)]) continue;
            if (!item["source", name]) continue;

            item.UpdateRaidTimeOffset();
            break;
        }
    }

    static DateTime _actionUpdateTime = DateTime.MinValue;
    private static void OnEffect(IntPtr dataPtr, uint targetActorId)
    {
        var name = GetNameFromObjectId(targetActorId);

        foreach (var item in DataCenter.TimelineItems)
        {
            if (item.IsShown) continue;
            if (item.Time < DataCenter.RaidTimeRaw) continue;
            if (item.Type is not TimelineType.Ability) continue;

            if (!item["id", ReadUint(dataPtr, 28)]) continue;
            if (!item["source", name]) continue;

            if (DateTime.Now - _actionUpdateTime < TimeSpan.FromSeconds(2)) continue;

            item.UpdateRaidTimeOffset();
            _actionUpdateTime = DateTime.Now;
            break;
        }
    }

    private static void OnActorControl(IntPtr dataPtr)
    {
        foreach (var item in DataCenter.TimelineItems)
        {
            if (item.IsShown) continue;
            if (item.Time < DataCenter.RaidTimeRaw) continue;
            if (item.Type is not TimelineType.ActorControl) continue;

            if (!item["command", ReadUint(dataPtr, 8)]) continue;
            if (!item["data0", ReadUshort(dataPtr, 12)]) continue;
            if (!item["data1", ReadUshort(dataPtr, 14)]) continue;
            if (!item["data2", ReadUshort(dataPtr, 16)]) continue;
            if (!item["data3", ReadUshort(dataPtr, 18)]) continue;

            item.UpdateRaidTimeOffset();
            break;
        }
    }

    private static void OnSystemLogMessage(IntPtr dataPtr)
    {
        foreach (var item in DataCenter.TimelineItems)
        {
            if (item.IsShown) continue;
            if (item.Time < DataCenter.RaidTimeRaw) continue;
            if (item.Type is not TimelineType.SystemLogMessage) continue;

            if (!item["id", ReadUint(dataPtr, 4)]) continue;
            if (!item["param0", ReadUint(dataPtr, 8)]) continue;
            if (!item["param1", ReadUint(dataPtr, 12)]) continue;
            if (!item["param2", ReadUint(dataPtr, 16)]) continue;

            item.UpdateRaidTimeOffset();
            break;
        }
    }

    private unsafe static ushort ReadUshort(IntPtr dataPtr, int offset)
    {
        return *(ushort*)(dataPtr + offset);
    }

    private unsafe static uint ReadUint(IntPtr dataPtr, int offset)
    {
        return *(uint*)(dataPtr + offset);
    }

    private static string GetNameFromObjectId(uint id)
    {
        var obj = Svc.Objects.SearchById(id);
        var nameId = obj is BattleChara battle ? battle.NameId : 0;
        var name = Svc.Data.GetExcelSheet<BNpcName>()?.GetRow(nameId)?.Singular.RawString;

        if (!string.IsNullOrEmpty(name)) return name;

#if DEBUG
        return "Failed to find the NPC name!";
#else
        return obj?.Name.TextValue ?? string.Empty;
#endif
    }

    private static async void ClientState_TerritoryChanged(ushort id)
    {
        if (PathForRaids.ContainsKey(id))
        {
            RotationConfigWindow._territoryId = id;
        }
        try
        {
            DataCenter.TimelineItems = await GetRaidAsync(id) ?? [];
        }
        catch (Exception e)
        {
            Svc.Log.Warning(e, "Failed to update the raid timeline!");
        }
    }

    private static readonly List<ushort> _downloading = [];
    internal static TimelineItem[] GetRaidTime(ushort id)
    {
        if (_savedTimeLines.TryGetValue(id, out var value)) return value;
        if (!PathForRaids.TryGetValue(id, out var path)) return [];
        if (_downloading.Contains(id)) return [];

        _downloading.Add(id);
        Task.Run(async () =>
        {
            _savedTimeLines[id] = await DownloadRaidAsync(path);
            _downloading.Remove(id);
        });

        return [];
    }

    static async Task<TimelineItem[]> DownloadRaidAsync(string path)
    {
        var langs = await DownloadRaidLangsAsync(path);

        return await DownloadRaidTimeAsync(path, langs);
    }
    static async Task<TimelineItem[]> GetRaidAsync(ushort id)
    {
        if (_savedTimeLines.TryGetValue(id, out var value)) return value;
        if (PathForRaids.TryGetValue(id, out var path))
        {
            return _savedTimeLines[id] = await DownloadRaidAsync(path);
        }
        return [];
    }

    static async Task<RaidLangs> DownloadRaidLangsAsync(string path)
    {
        using var client = new HttpClient();
        var message = await client.GetAsync("https://raw.githubusercontent.com/xpdota/event-trigger/master/timelines/src/main/resources/timeline/translations/" + path + ".json");

        if (!message.IsSuccessStatusCode)
        {
            return new();
        }

        var str = await message.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<RaidLangs>(str) ?? new();
    }

    static async Task<TimelineItem[]> DownloadRaidTimeAsync(string path, RaidLangs lang)
    {
        using var client = new HttpClient();
        var message = await client.GetAsync("https://raw.githubusercontent.com/xpdota/event-trigger/master/timelines/src/main/resources/timeline/" + path);

        if (!message.IsSuccessStatusCode)
        {
            return [];
        }

        var str = await message.Content.ReadAsStringAsync();

        var result = new List<TimelineItem>();
        foreach (var timelineItem in TimeLineItem().Matches(str).Cast<Match>())
        {
            try
            {
                var timeline = timelineItem.Value;

                var header = TimeHeader().Match(timeline).Value;

                if (string.IsNullOrEmpty(header)) continue;

                var time = float.Parse(Time().Match(header).Value);
                var name = Name().Match(header).Value[1..^1];

                var timelineStr = ActionGetter().Match(timeline).Value;

                var item = string.IsNullOrEmpty(timelineStr) ? null : JObject.Parse(timelineStr);

                var rest = timeline[header.Length..];
                var type = Type().Match(rest)?.Value ?? string.Empty;
                if (type.Length > 3)
                {
                    type = type[1..^2].Split(' ').LastOrDefault() ?? string.Empty;
                }

                result.Add(new (time, name, type, item, lang));
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "sth wrong with matching!");
            }
        }

#if DEBUG
        foreach (var item in result)
        {
            Svc.Log.Debug(item.ToString());
        }
#endif
        return [..result.OrderBy(i => i.Time)];
    }

    [GeneratedRegex(" .*? {")]
    private static partial Regex Type();

    [GeneratedRegex("[\\d\\.]+.*")]
    private static partial Regex TimeLineItem();

    [GeneratedRegex("^[\\d\\.]+")]
    private static partial Regex Time();

    [GeneratedRegex("\".*?\"")]
    private static partial Regex Name();

    [GeneratedRegex("^[\\d\\.]+ \".*?\"")]
    private static partial Regex TimeHeader();

    [GeneratedRegex("{.*}")]
    private static partial Regex ActionGetter();
}