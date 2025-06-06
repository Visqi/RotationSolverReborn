﻿using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using RotationSolver.Basic.Configuration;
using RotationSolver.Commands;
using RotationSolver.Helpers;

namespace RotationSolver.Updaters;

internal static class MiscUpdater
{
    internal static void UpdateMisc()
    {
        UpdateEntry();
        UpdateCancelCast();
    }

    private static IDtrBarEntry? _dtrEntry;

    internal static void UpdateEntry()
    {
        string showStr = RSCommands.EntryString;
        BitmapFontIcon icon = GetJobIcon(Player.Job);

        if (Service.Config.ShowInfoOnDtr && !string.IsNullOrEmpty(showStr))
        {
            try
            {
                _dtrEntry ??= Svc.DtrBar.Get("Rotation Solver Reborn");
            }
            catch
            {
                WarningHelper.AddSystemWarning("Unable to add server bar entry");
                return;
            }

            if (_dtrEntry != null && !_dtrEntry.Shown)
            {
                _dtrEntry.Shown = true;
            }

            if (_dtrEntry != null)
            {
                _dtrEntry.Text = new SeString(
                    new IconPayload(icon),
                    new TextPayload(showStr)
                );
                _dtrEntry.OnClick = RSCommands.IncrementState;
            }
        }
        else if (_dtrEntry != null && _dtrEntry.Shown)
        {
            _dtrEntry.Shown = false;
        }
    }

    private static BitmapFontIcon GetJobIcon(Job job)
    {
        return job switch
        {
            Job.WAR => BitmapFontIcon.Warrior,
            Job.PLD => BitmapFontIcon.Paladin,
            Job.DRK => BitmapFontIcon.DarkKnight,
            Job.GNB => BitmapFontIcon.Gunbreaker,
            Job.AST => BitmapFontIcon.Astrologian,
            Job.WHM => BitmapFontIcon.WhiteMage,
            Job.SGE => BitmapFontIcon.Sage,
            Job.SCH => BitmapFontIcon.Scholar,
            Job.BLM => BitmapFontIcon.BlackMage,
            Job.SMN => BitmapFontIcon.Summoner,
            Job.RDM => BitmapFontIcon.RedMage,
            Job.PCT => BitmapFontIcon.Pictomancer,
            Job.BLU => BitmapFontIcon.BlueMage,
            Job.MNK => BitmapFontIcon.Monk,
            Job.SAM => BitmapFontIcon.Samurai,
            Job.DRG => BitmapFontIcon.Dragoon,
            Job.RPR => BitmapFontIcon.Reaper,
            Job.NIN => BitmapFontIcon.Ninja,
            Job.VPR => BitmapFontIcon.Viper,
            Job.BRD => BitmapFontIcon.Bard,
            Job.MCH => BitmapFontIcon.Machinist,
            Job.DNC => BitmapFontIcon.Dancer,
            Job.BSM => BitmapFontIcon.Blacksmith,
            Job.ARM => BitmapFontIcon.Armorer,
            Job.WVR => BitmapFontIcon.Weaver,
            Job.ALC => BitmapFontIcon.Alchemist,
            Job.CRP => BitmapFontIcon.Carpenter,
            Job.LTW => BitmapFontIcon.Leatherworker,
            Job.CUL => BitmapFontIcon.Culinarian,
            Job.GSM => BitmapFontIcon.Goldsmith,
            Job.FSH => BitmapFontIcon.Fisher,
            Job.MIN => BitmapFontIcon.Miner,
            Job.BTN => BitmapFontIcon.Botanist,
            Job.GLA => BitmapFontIcon.Gladiator,
            Job.CNJ => BitmapFontIcon.Conjurer,
            Job.MRD => BitmapFontIcon.Marauder,
            Job.PGL => BitmapFontIcon.Pugilist,
            Job.LNC => BitmapFontIcon.Lancer,
            Job.ROG => BitmapFontIcon.Rogue,
            Job.ARC => BitmapFontIcon.Archer,
            Job.THM => BitmapFontIcon.Thaumaturge,
            Job.ACN => BitmapFontIcon.Arcanist,
            _ => BitmapFontIcon.ExclamationRectangle,
        };
    }

    private static RandomDelay _tarStopCastDelay = new(() => Service.Config.StopCastingDelay);

    private static unsafe void UpdateCancelCast()
    {
        if (!Player.Object.IsCasting)
        {
            return;
        }

        bool tarDead = Service.Config.UseStopCasting
            && Svc.Objects.SearchById(Player.Object.CastTargetObjectId) is IBattleChara b
            && b.IsEnemy() && b.CurrentHp == 0;

        // Cancel raise cast if target already has Raise status
        bool tarHasRaise = false;
        if (Svc.Objects.SearchById(Player.Object.CastTargetObjectId) is IBattleChara battleChara)
        {
            tarHasRaise = battleChara.HasStatus(false, StatusID.Raise);
        }

        float[] statusTimes = GetStatusTimes();

        float minStatusTime = float.MaxValue;
        for (int i = 0; i < statusTimes.Length; i++)
        {
            if (statusTimes[i] < minStatusTime)
            {
                minStatusTime = statusTimes[i];
            }
        }

        bool stopDueStatus = statusTimes.Length > 0 && minStatusTime > Player.Object.TotalCastTime - Player.Object.CurrentCastTime && minStatusTime < 5;

        if (_tarStopCastDelay.Delay(tarDead) || stopDueStatus || tarHasRaise)
        {
            UIState* uiState = UIState.Instance();
            if (uiState != null)
            {
                uiState->Hotbar.CancelCast();
            }
        }
    }

    private static float[] GetStatusTimes()
    {
        List<float> statusTimes = [];
        if (Player.Object?.StatusList != null)
        {
            foreach (Dalamud.Game.ClientState.Statuses.Status status in Player.Object.StatusList)
            {
                // No LINQ used here, Contains is a method on the collection
                if (OtherConfiguration.NoCastingStatus.Contains(status.StatusId))
                {
                    statusTimes.Add(status.RemainingTime);
                }
            }
        }
        return [.. statusTimes];
    }

    internal static unsafe void PulseActionBar(uint actionID)
    {
        LoopAllSlotBar((bar, hot, index) =>
        {
            return IsActionSlotRight(bar, hot, actionID);
        });
    }

    private static unsafe bool IsActionSlotRight(ActionBarSlot slot, RaptureHotbarModule.HotbarSlot? hot, uint actionID)
    {
        if (hot.HasValue)
        {
            if (hot.Value.OriginalApparentSlotType is not RaptureHotbarModule.HotbarSlotType.CraftAction and not RaptureHotbarModule.HotbarSlotType.Action)
            {
                return false;
            }

            if (hot.Value.ApparentSlotType is not RaptureHotbarModule.HotbarSlotType.CraftAction and not RaptureHotbarModule.HotbarSlotType.Action)
            {
                return false;
            }
        }

        return Service.GetAdjustedActionId((uint)slot.ActionId) == actionID;
    }

    private unsafe delegate bool ActionBarAction(ActionBarSlot bar, RaptureHotbarModule.HotbarSlot? hot, uint highLightID);
    private unsafe delegate bool ActionBarPredicate(ActionBarSlot bar, RaptureHotbarModule.HotbarSlot* hot);
    private static unsafe void LoopAllSlotBar(ActionBarAction doingSomething)
    {
        int index = 0;
        int hotBarIndex = 0;

        // Replace .Union() LINQ with manual enumeration
        List<nint> addonPtrs =
        [
            .. Service.GetAddons<AddonActionBar>(),
            .. Service.GetAddons<AddonActionBarX>(),
            .. Service.GetAddons<AddonActionCross>(),
            .. Service.GetAddons<AddonActionDoubleCrossBase>(),
        ];

        foreach (nint intPtr in addonPtrs)
        {
            if (intPtr == IntPtr.Zero)
            {
                continue;
            }

            AddonActionBarBase* actionBar = (AddonActionBarBase*)intPtr;
            RaptureHotbarModule.Hotbar hotBar = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars[hotBarIndex];

            int slotIndex = 0;

            foreach (ActionBarSlot slot in actionBar->ActionBarSlotVector.AsSpan())
            {
                int highLightId = 0x53550000 + index;

                if (doingSomething(slot, hotBarIndex > 9 ? null : hotBar.Slots[slotIndex], (uint)highLightId))
                {
                    FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNode* iconAddon = slot.Icon;
                    if ((IntPtr)iconAddon == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (!iconAddon->AtkResNode.IsVisible())
                    {
                        continue;
                    }

                    actionBar->PulseActionBarSlot(slotIndex);
                    UIGlobals.PlaySoundEffect(12, 0, 0, 0);
                }
                slotIndex++;
                index++;
            }
            hotBarIndex++;
        }
    }

    public static unsafe void Dispose()
    {

    }
}