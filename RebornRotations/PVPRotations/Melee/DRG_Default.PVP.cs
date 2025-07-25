﻿namespace RebornRotations.PVPRotations.Melee;

[Rotation("Default PVP", CombatType.PvP, GameVersion = "7.25")]
[SourceCode(Path = "main/RebornRotations/PVPRotations/Melee/DRG_Default.PvP.cs")]
[Api(5)]
public sealed class DRG_DefaultPvP : DragoonRotation
{
    #region Configurations

    [RotationConfig(CombatType.PvP, Name = "Use Purify")]
    public bool UsePurifyPvP { get; set; } = true;

    [RotationConfig(CombatType.PvP, Name = "Stop attacking while in Guard.")]
    public bool RespectGuard { get; set; } = true;

    [Range(0, 1, ConfigUnitType.Percent)]
    [RotationConfig(CombatType.PvP, Name = "Player health threshold needed for Bloodbath use")]
    public float BloodBathPvPPercent { get; set; } = 0.75f;

    [Range(0, 1, ConfigUnitType.Percent)]
    [RotationConfig(CombatType.PvP, Name = "Enemy health threshold needed for Smite use")]
    public float SmitePvPPercent { get; set; } = 0.25f;

    [RotationConfig(CombatType.PvP, Name = "Allow the use of high jump if there are enemies in melee range.")]
    public bool JumpYeet { get; set; } = true;
    #endregion

    #region Standard PVP Utilities
    private bool DoPurify(out IAction? action)
    {
        action = null;
        if (!UsePurifyPvP)
        {
            return false;
        }

        List<int> purifiableStatusesIDs = new()
        {
            // Stun, DeepFreeze, HalfAsleep, Sleep, Bind, Heavy, Silence
            1343, 3219, 3022, 1348, 1345, 1344, 1347
        };

        return purifiableStatusesIDs.Any(id => Player.HasStatus(false, (StatusID)id)) && PurifyPvP.CanUse(out action);
    }
    #endregion

    #region oGCDs
    protected override bool EmergencyAbility(IAction nextGCD, out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        if (DoPurify(out action))
        {
            return true;
        }

        if (BloodbathPvP.CanUse(out action) && Player.GetHealthRatio() < BloodBathPvPPercent)
        {
            return true;
        }

        if (SwiftPvP.CanUse(out action))
        {
            return true;
        }

        if (SmitePvP.CanUse(out action) && CurrentTarget?.GetHealthRatio() <= SmitePvPPercent)
        {
            return false;
        }

        return base.EmergencyAbility(nextGCD, out action);
    }

    protected override bool DefenseSingleAbility(IAction nextGCD, out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        return base.DefenseSingleAbility(nextGCD, out action);
    }

    protected override bool AttackAbility(IAction nextGCD, out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        if (HorridRoarPvP.CanUse(out action))
        {
            return true;
        }

        if (GeirskogulPvP.CanUse(out action))
        {
            return true;
        }

        if (NastrondPvP.CanUse(out action))
        {
            return true;
        }

        if (HighJumpPvP.CanUse(out action) && HasHostilesInRange && JumpYeet)
        {
            return true;
        }

        return base.AttackAbility(nextGCD, out action);
    }

    protected override bool MoveForwardAbility(IAction nextGCD, out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        if (HighJumpPvP.CanUse(out action))
        {
            return true;
        }

        return base.MoveForwardAbility(nextGCD, out action);
    }

    protected override bool MoveBackAbility(IAction nextGCD, out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        if (ElusiveJumpPvP.CanUse(out action))
        {
            return true;
        }

        return base.MoveBackAbility(nextGCD, out action);
    }
    #endregion

    #region GCDs
    protected override bool GeneralGCD(out IAction? action)
    {
        action = null;
        if (RespectGuard && Player.HasStatus(true, StatusID.Guard))
        {
            return false;
        }

        if (WyrmwindThrustPvP.CanUse(out action))
        {
            return true;
        }

        if (HeavensThrustPvP.CanUse(out action))
        {
            return true;
        }

        if (StarcrossPvP.CanUse(out action))
        {
            return true;
        }

        if (ChaoticSpringPvP.CanUse(out action))
        {
            return true;
        }

        if (DrakesbanePvP.CanUse(out action))
        {
            return true;
        }

        if (WheelingThrustPvP.CanUse(out action))
        {
            return true;
        }

        if (FangAndClawPvP.CanUse(out action))
        {
            return true;
        }

        if (RaidenThrustPvP.CanUse(out action))
        {
            return true;
        }

        return base.GeneralGCD(out action);
    }
    #endregion
}