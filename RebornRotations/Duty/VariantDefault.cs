﻿using RotationSolver.Basic.Rotations.Duties;

namespace RebornRotations.Duty;

[Rotation("Variant Default", CombatType.PvE)]

internal class VariantDefault : VariantRotation
{
    public override bool ProvokeAbility(IAction nextGCD, out IAction? act)
    {
        if (VariantUltimatumPvE.CanUse(out act, skipStatusProvideCheck: true))
        {
            return true;
        }

        return base.ProvokeAbility(nextGCD, out act);
    }

    public override bool AttackAbility(IAction nextGCD, out IAction? act)
    {
        if (VariantSpiritDartPvE_33863.CanUse(out act, skipAoeCheck: true))
        {
            return true;
        }

        if (VariantSpiritDartPvE.CanUse(out act, skipAoeCheck: true))
        {
            return true;
        }

        return base.AttackAbility(nextGCD, out act);
    }

    public override bool DefenseSingleAbility(IAction nextGCD, out IAction? act)
    {
        if (VariantRampartPvE_33864.CanUse(out act, skipStatusProvideCheck: true))
        {
            return true;
        }

        if (VariantRampartPvE.CanUse(out act, skipStatusProvideCheck: true))
        {
            return true;
        }

        return base.DefenseSingleAbility(nextGCD, out act);
    }

    public override bool GeneralAbility(IAction nextGCD, out IAction? act)
    {
        if (VariantRampartPvE_33864.CanUse(out act))
        {
            return true;
        }

        if (VariantRampartPvE.CanUse(out act))
        {
            return true;
        }

        if (VariantUltimatumPvE.CanUse(out act))
        {
            return true;
        }

        return base.GeneralAbility(nextGCD, out act);
    }

    public override bool HealSingleGCD(out IAction? act)
    {
        if (VariantCurePvE_33862.CanUse(out act, skipStatusProvideCheck: true))
        {
            return true;
        }

        if (VariantCurePvE.CanUse(out act, skipStatusProvideCheck: true))
        {
            return true;
        }

        return base.HealSingleGCD(out act);
    }

    public override bool RaiseGCD(out IAction? act)
    {
        if (VariantRaisePvE.CanUse(out act))
        {
            return true;
        }

        if (VariantRaiseIiPvE.CanUse(out act))
        {
            return true;
        }

        return base.RaiseGCD(out act);
    }
}
