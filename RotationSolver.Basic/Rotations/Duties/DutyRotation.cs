﻿namespace RotationSolver.Basic.Rotations.Duties;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Represents the base class for duty rotations.
/// </summary>
public partial class DutyRotation : IDisposable
{
    #region GCD
    public virtual bool EmergencyGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool MyInterruptGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool GeneralGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool RaiseGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool DispelGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool MoveForwardGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool HealSingleGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool HealAreaGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool DefenseSingleGCD(out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool DefenseAreaGCD(out IAction? act)
    {
        act = null; return false;
    }
    #endregion

    #region Ability

    public virtual bool InterruptAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool AntiKnockbackAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool ProvokeAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool EmergencyAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool MoveForwardAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool MoveBackAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool HealSingleAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool HealAreaAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool DefenseSingleAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool DefenseAreaAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool SpeedAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool GeneralAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    public virtual bool AttackAbility(IAction nextGCD, out IAction? act)
    {
        act = null; return false;
    }

    #endregion

    /// <summary>
    /// Releases all resources used by the <see cref="DutyRotation"/> class.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets the hostile target.
    /// </summary>
    public IBattleChara? HostileTarget => DataCenter.HostileTarget;

    /// <summary>
    /// Gets all actions available in the duty rotation.
    /// </summary>
    internal IAction[] AllActions
    {
        get
        {
            IEnumerable<PropertyInfo> properties = GetType().GetRuntimeProperties()
                .Where(p => DataCenter.DutyActions.Contains(p.GetCustomAttribute<IDAttribute>()?.ID ?? uint.MaxValue));

            return properties.Select(p => (IAction)p.GetValue(this)!).ToArray();
        }
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member