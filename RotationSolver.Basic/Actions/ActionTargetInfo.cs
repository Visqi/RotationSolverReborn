﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using RotationSolver.Basic.Configuration;
using static RotationSolver.Basic.Configuration.ConfigTypes;
using AttackType = RotationSolver.Basic.Data.AttackType;
using CombatRole = RotationSolver.Basic.Data.CombatRole;

namespace RotationSolver.Basic.Actions;

/// <summary>
/// The target info
/// </summary>
/// <param name="action">the input action.</param>
public struct ActionTargetInfo(IBaseAction action)
{
    /// <summary>
    /// The range of this action.
    /// </summary>
    public readonly float Range => ActionManager.GetActionRange(action.Info.ID);

    /// <summary>
    /// The effect range of this action.
    /// </summary>
    public readonly float EffectRange => (ActionID)action.Info.ID == ActionID.LiturgyOfTheBellPvE ? 20 : action.Action.EffectRange;

    /// <summary>
    /// Is this action single target.
    /// </summary>
    public readonly bool IsSingleTarget => action.Action.CastType == 1;
    /// <summary>
    /// Is this action target area.
    /// </summary>
    public readonly bool IsTargetArea => action.Action.TargetArea;

    /// <summary>
    /// Is this action friendly.
    /// </summary>
    public readonly bool IsTargetFriendly => action.Setting.IsFriendly;

    #region Targetting Behaviour

    /// <summary>
    /// Retrieves a collection of valid battle characters that can be targeted based on the specified criteria.
    /// </summary>
    /// <param name="skipStatusProvideCheck">If set to <c>true</c>, skips the status provide check.</param>
    /// <param name="skipTargetStatusNeedCheck">If set to <c>true</c>, skips the target status need check.</param>
    /// <param name="type">The type of target to filter (e.g., Heal).</param>
    /// <returns>
    /// An <see cref="IEnumerable{IBattleChara}"/> containing the valid targets.
    /// </returns>
    private readonly IEnumerable<IBattleChara> GetCanTargets(bool skipStatusProvideCheck, bool skipTargetStatusNeedCheck, TargetType type)
    {
        if (DataCenter.AllTargets == null)
        {
            return Enumerable.Empty<IBattleChara>();
        }

        List<IBattleChara> validTargets = new(TargetFilter.GetObjectInRadius(DataCenter.AllTargets, Range).Count());

        foreach (IBattleChara target in TargetFilter.GetObjectInRadius(DataCenter.AllTargets, Range))
        {
            if (type == TargetType.Heal && target.GetHealthRatio() == 1)
            {
                continue;
            }

            if (!GeneralCheck(target, skipStatusProvideCheck, skipTargetStatusNeedCheck))
            {
                continue;
            }

            validTargets.Add(target);
        }

        List<IBattleChara> result = [];
        foreach (IBattleChara b in validTargets)
        {
            if (!DataCenter.IsManual || IsTargetFriendly || b.GameObjectId == Svc.Targets.Target?.GameObjectId || b.GameObjectId == Player.Object?.GameObjectId)
            {
                if (InViewTarget(b) && CanUseTo(b) && action.Setting.CanTarget(b))
                {
                    result.Add(b);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Retrieves a list of battle characters that can be affected based on the specified criteria.
    /// </summary>
    /// <param name="skipStatusProvideCheck">If set to <c>true</c>, skips the status provide check.</param>
    /// <param name="skipTargetStatusNeedCheck">If set to <c>true</c>, skips the target status need check.</param>
    /// <param name="type">The type of target to filter (e.g., Heal).</param>
    /// <returns>
    /// A <see cref="List{IBattleChara}"/> containing the valid targets.
    /// </returns>
    private readonly List<IBattleChara> GetCanAffects(bool skipStatusProvideCheck, bool skipTargetStatusNeedCheck, TargetType type)
    {
        if (EffectRange == 0)
        {
            return [];
        }

        if ((action.Setting.IsFriendly
            ? DataCenter.PartyMembers
            : DataCenter.AllHostileTargets) == null)
        {
            return [];
        }

        IEnumerable<IBattleChara> items = TargetFilter.GetObjectInRadius(action.Setting.IsFriendly
            ? DataCenter.PartyMembers
            : DataCenter.AllHostileTargets, Range + EffectRange);

        if (type == TargetType.Heal)
        {
            List<IBattleChara> filteredItems = [];
            foreach (IBattleChara i in items)
            {
                if (i.GetHealthRatio() < 1)
                {
                    filteredItems.Add(i);
                }
            }
            items = filteredItems;
        }

        List<IBattleChara> validTargets = new(items.Count());

        foreach (IBattleChara obj in items)
        {
            if (!GeneralCheck(obj, skipStatusProvideCheck, skipTargetStatusNeedCheck))
            {
                continue;
            }

            validTargets.Add(obj);
        }

        return validTargets;
    }

    /// <summary>
    /// Determines whether the specified battle character is within the player's view and vision cone based on the configuration settings.
    /// </summary>
    /// <param name="gameObject">The battle character to check.</param>
    /// <returns>
    /// <c>true</c> if the battle character is within the player's view and vision cone; otherwise, <c>false</c>.
    /// </returns>
    private static bool InViewTarget(IBattleChara gameObject)
    {
        if (Service.Config.OnlyAttackInView)
        {
            if (!Svc.GameGui.WorldToScreen(gameObject.Position, out _))
            {
                return false;
            }
        }

        if (Service.Config.OnlyAttackInVisionCone && Player.Object != null)
        {
            Vector3 dir = gameObject.Position - Player.Object.Position;
            Vector3 faceVec = Player.Object.GetFaceVector();
            dir = Vector3.Normalize(dir);
            faceVec = Vector3.Normalize(faceVec);

            // Calculate the angle between the direction vector and the facing vector
            double dotProduct = Vector3.Dot(faceVec, dir);
            double angle = Math.Acos(dotProduct);

            if (angle > Math.PI * Service.Config.AngleOfVisionCone / 360)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified game object can be targeted and used for an action.
    /// </summary>
    /// <param name="tar">The game object to check.</param>
    /// <returns>
    /// <c>true</c> if the game object can be targeted and used for an action; otherwise, <c>false</c>.
    /// </returns>
    private readonly unsafe bool CanUseTo(IGameObject tar)
    {
        if (tar == null)
        {
            return false;
        }

        if (!Player.AvailableThreadSafe)
        {
            return false;
        }

        return tar.GameObjectId != 0 && tar.Struct() != null && (IsSpecialAbility(action.Info.ID) || ActionManager.CanUseActionOnTarget(action.Info.AdjustedID, tar.Struct())) && tar.CanSee();
    }

    private readonly List<ActionID> _specialActions =
    [
        ActionID.AethericMimicryPvE,
        ActionID.EruptionPvE,
        ActionID.BishopAutoturretPvP,
        ActionID.CometPvP,
        ActionID.DotonPvE,
        ActionID.DotonPvE_18880,
        ActionID.FeatherRainPvE,
        ActionID.SaltAndDarknessPvP,
    ];

    private readonly bool IsSpecialAbility(uint iD)
    {
        return _specialActions.Contains((ActionID)iD);
    }

    /// <summary>
    /// Performs a general check on the specified battle character to determine if it meets the criteria for targeting.
    /// </summary>
    /// <param name="gameObject">The battle character to check.</param>
    /// <param name="skipStatusProvideCheck">If set to <c>true</c>, skips the status provide check.</param>
    /// <param name="skipTargetStatusNeedCheck">If set to <c>true</c>, skips the target status need check.</param>
    /// <returns>
    /// <c>true</c> if the battle character meets the criteria for targeting; otherwise, <c>false</c>.
    /// </returns>
    public readonly bool GeneralCheck(IBattleChara gameObject, bool skipStatusProvideCheck, bool skipTargetStatusNeedCheck)
    {
        if (gameObject == null)
        {
            return false;
        }

        // Defensive: check that the underlying struct is valid before accessing StatusList
        unsafe
        {
            if (gameObject.Struct() == null)
            {
                return false;
            }
        }

        // Optionally, check for IsDead or other validity flags if available
        if (!gameObject.IsTargetable)
        {
            return false;
        }

        try
        {
            if (gameObject.StatusList == null)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Exception accessing StatusList for {gameObject?.NameId}: {ex.Message}");
            return false;
        }

        if (Service.Config.RaiseType == RaiseType.PartyOnly && gameObject.IsAllianceMember() && !gameObject.IsParty())
        {
            return false;
        }

        bool isBlacklisted = false;
        foreach (uint id in DataCenter.BlacklistedNameIds)
        {
            if (id == gameObject.NameId)
            {
                isBlacklisted = true;
                break;
            }
        }
        if (isBlacklisted)
        {
            return false;
        }

        if (gameObject.IsEnemy() && !gameObject.IsAttackable())
        {
            return false;
        }

        bool isStopTarget = false;
        long[] stopTargets = MarkingHelper.GetStopTargets();
        foreach (long stopId in stopTargets)
        {
            if (stopId == (long)gameObject.GameObjectId)
            {
                isStopTarget = true;
                break;
            }
        }
        return (!isStopTarget || !Service.Config.FilterStopMark) && CheckStatus(gameObject, skipStatusProvideCheck, skipTargetStatusNeedCheck)
            && CheckTimeToKill(gameObject)
            && CheckResistance(gameObject);
    }

    /// <summary>
    /// Checks the status of the specified game object to determine if it meets the criteria for the action.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <param name="skipStatusProvideCheck">If set to <c>true</c>, skips the status provide check.</param>
    /// <param name="skipTargetStatusNeedCheck">If set to <c>true</c>, skips the target status need check.</param>
    /// <returns>
    /// <c>true</c> if the game object meets the status criteria for the action; otherwise, <c>false</c>.
    /// </returns>
    private readonly bool CheckStatus(IGameObject gameObject, bool skipStatusProvideCheck, bool skipTargetStatusNeedCheck)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (!action.Config.ShouldCheckTargetStatus && !action.Config.ShouldCheckStatus)
        {
            return true;
        }

        if (action.Setting.TargetStatusNeed != null && !skipTargetStatusNeedCheck)
        {
            if (gameObject.WillStatusEndGCD(0, 0, action.Setting.StatusFromSelf, action.Setting.TargetStatusNeed))
            {
                return false;
            }
        }

        if (action.Setting.TargetStatusProvide != null && !skipStatusProvideCheck)
        {
            if (!gameObject.WillStatusEndGCD(action.Config.StatusGcdCount, 0, action.Setting.StatusFromSelf, action.Setting.TargetStatusProvide))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks the resistance status of the specified game object to determine if it meets the criteria for the action.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <returns>
    /// <c>true</c> if the game object meets the resistance criteria for the action; otherwise, <c>false</c>.
    /// </returns>
    private readonly bool CheckResistance(IGameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        try
        {
            if (action.Info.AttackType == AttackType.Magic)
            {
                if (gameObject.HasStatus(false, StatusHelper.MagicResistance))
                {
                    return false;
                }
            }
            else if (action.Info.Aspect != Aspect.Piercing) // Physical
            {
                if (gameObject.HasStatus(false, StatusHelper.PhysicalResistance))
                {
                    return false;
                }
            }
            if (Range >= 20) // Range
            {
                if (gameObject.HasStatus(false, StatusID.RangedResistance, StatusID.EnergyField))
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging purposes
            PluginLog.Error($"Error checking resistance: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks the time to kill of the specified game object to determine if it meets the criteria for the action.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <returns>
    /// <c>true</c> if the game object meets the time to kill criteria for the action; otherwise, <c>false</c>.
    /// </returns>
    private readonly bool CheckTimeToKill(IGameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (gameObject is not IBattleChara b)
        {
            return false;
        }

        float time = b.GetTTK();
        return float.IsNaN(time) || time >= action.Config.TimeToKill;
    }

    #endregion

    #region Target Result

    /// <summary>
    /// Finds the target based on the specified criteria.
    /// </summary>
    /// <param name="skipAoeCheck">If set to <c>true</c>, skips the AoE check.</param>
    /// <param name="skipStatusProvideCheck">If set to <c>true</c>, skips the status provide check.</param>
    /// <param name="skipTargetStatusNeedCheck">If set to <c>true</c>, skips the target status need check.</param>
    /// <returns>
    /// A <see cref="TargetResult"/> containing the target and affected characters, or <c>null</c> if no target is found.
    /// </returns>
    internal readonly TargetResult? FindTarget (bool skipAoeCheck, bool skipStatusProvideCheck, bool skipTargetStatusNeedCheck)
    {
        float range = Range;

        if (action == null || action.Setting == null || action.Config == null)
        {
            return null;
        }

        if (range == 0 && EffectRange == 0)
        {
            return new TargetResult(Player.Object, Array.Empty<IBattleChara>(), Player.Object.Position);
        }

        TargetType type = action.Setting.TargetType;

        IEnumerable<IBattleChara> canTargets = GetCanTargets(skipStatusProvideCheck, skipTargetStatusNeedCheck, type);
        List<IBattleChara> canAffects = GetCanAffects(skipStatusProvideCheck, skipTargetStatusNeedCheck, type);

        if (canTargets == null || canAffects == null)
        {
            return null;
        }

        if (IsTargetArea)
        {
            return FindTargetArea(canTargets, canAffects, range, Player.Object);
        }

        List<IBattleChara> targetsList = [.. GetMostCanTargetObjects(canTargets, canAffects, skipAoeCheck ? 0 : action.Config.AoeCount)];

        IBattleChara? target;
        if (targetsList.Count > 0)
        {
            // FindTargetByType is static and expects IEnumerable, so pass the list
            target = FindTargetByType(targetsList, type, action.Config.AutoHealRatio, action.Setting.SpecialType);
        }
        else
        {
            target = FindTargetByType(Array.Empty<IBattleChara>(), type, action.Config.AutoHealRatio, action.Setting.SpecialType);
        }

        IBattleChara[] affectedTargets;
        if (target != null)
        {
            var affectsEnum = GetAffectsTarget(target, canAffects);
            List<IBattleChara> affectsList = [];
            if (affectsEnum != null)
            {
                foreach (var a in affectsEnum)
                {
                    affectsList.Add(a);
                }
            }
            affectedTargets = [.. affectsList];
        }
        else
        {
            affectedTargets = [];
        }

        return target == null ? null : new TargetResult(target, affectedTargets, target.Position);
    }

    /// <summary>
    /// Finds the target area based on the specified criteria.
    /// </summary>
    /// <param name="canTargets">The potential targets that can be affected.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <param name="range">The range within which to find the target area.</param>
    /// <param name="player">The player character.</param>
    /// <returns>
    /// A <see cref="TargetResult"/> containing the target area and affected characters, or <c>null</c> if no target area is found.
    /// </returns>
    private readonly TargetResult? FindTargetArea (IEnumerable<IBattleChara> canTargets, IEnumerable<IBattleChara> canAffects,
        float range, IPlayerCharacter player)
    {
        if (player == null)
        {
            return null;
        }

        if (canTargets == null)
        {
            return null;
        }

        if (canAffects == null)
        {
            return null;
        }

        if (action.Setting.TargetType == TargetType.Move)
        {
            return FindTargetAreaMove(range);
        }
        else
        {
            return action.Setting.IsFriendly
                ? !Service.Config.UseGroundBeneficialAbility
                            ? null
                            : !Service.Config.UseGroundBeneficialAbilityWhenMoving && DataCenter.IsMoving
                            ? null
                            : FindTargetAreaFriend(range, canAffects, player)
                : FindTargetAreaHostile(canTargets, canAffects, action.Config.AoeCount);
        }
    }

    /// <summary>
    /// Finds the hostile target area based on the specified criteria.
    /// </summary>
    /// <param name="canTargets">The potential targets that can be affected.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <param name="aoeCount">The number of targets to consider for AoE.</param>
    /// <returns>
    /// A <see cref="TargetResult"/> containing the target and affected characters, or <c>null</c> if no target is found.
    /// </returns>
    private readonly TargetResult? FindTargetAreaHostile(IEnumerable<IBattleChara> canTargets, IEnumerable<IBattleChara> canAffects, int aoeCount)
    {
        if (canAffects == null)
        {
            return null;
        }

        if (canTargets == null)
        {
            return null;
        }

        IBattleChara? target = null;
        IEnumerable<IBattleChara> mostCanTargetObjects = GetMostCanTargetObjects(canTargets, canAffects, aoeCount);
        IEnumerator<IBattleChara> enumerator = mostCanTargetObjects.GetEnumerator();

        while (enumerator.MoveNext())
        {
            IBattleChara t = enumerator.Current;
            if (target == null || ObjectHelper.GetHealthRatio(t) > ObjectHelper.GetHealthRatio(target))
            {
                target = t;
            }
        }

        if (target == null)
        {
            return null;
        }

        List<IBattleChara> affectedTargets = [];
        IEnumerator<IBattleChara> affectsEnumerator = canAffects.GetEnumerator();
        while (affectsEnumerator.MoveNext())
        {
            IBattleChara t = affectsEnumerator.Current;
            if (Vector3.Distance(target.Position, t.Position) - t.HitboxRadius <= EffectRange)
            {
                affectedTargets.Add(t);
            }
        }

        return new TargetResult(target, affectedTargets.ToArray(), target.Position);
    }

    /// <summary>
    /// Finds the target area for movement based on the specified range.
    /// </summary>
    /// <param name="range">The range within which to find the target area.</param>
    /// <returns>
    /// A <see cref="TargetResult"/> containing the target area and affected characters, or <c>null</c> if no target area is found.
    /// </returns>
    private readonly TargetResult? FindTargetAreaMove(float range)
    {
        if (Service.Config.MoveAreaActionFarthest)
        {
            Vector3 pPosition = Player.Object.Position;
            if (Service.Config.MoveTowardsScreenCenter)
            {
                unsafe
                {
                    CameraManager* cameraManager = CameraManager.Instance();
                    if (cameraManager == null)
                    {
                        return null;
                    }

                    FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera = cameraManager->CurrentCamera;
                    FFXIVClientStructs.FFXIV.Common.Math.Vector3 tar = camera->LookAtVector - camera->Object.Position;
                    tar.Y = 0;
                    float length = ((Vector3)tar).Length();
                    if (length == 0)
                    {
                        return null;
                    }

                    tar = tar / length * range;
                    return new TargetResult(Player.Object, Array.Empty<IBattleChara>(), new Vector3(pPosition.X + tar.X, pPosition.Y, pPosition.Z + tar.Z));
                }
            }
            else
            {
                float rotation = Player.Object.Rotation;
                return new TargetResult(Player.Object, Array.Empty<IBattleChara>(), new Vector3(pPosition.X + ((float)Math.Sin(rotation) * range), pPosition.Y, pPosition.Z + ((float)Math.Cos(rotation) * range)));
            }
        }
        else
        {
            List<IBattleChara> availableCharas = [];
            foreach (IBattleChara availableTarget in DataCenter.AllTargets)
            {
                if (availableTarget.GameObjectId != Player.Object.GameObjectId)
                {
                    availableCharas.Add(availableTarget);
                }
            }

            IEnumerable<IBattleChara> targetList = TargetFilter.GetObjectInRadius(availableCharas, range);
            IBattleChara? target = FindTargetByType(targetList, TargetType.Move, action.Config.AutoHealRatio, action.Setting.SpecialType);
            return target == null ? null : new TargetResult(target, Array.Empty<IBattleChara>(), target.Position);
        }
    }

    /// <summary>
    /// Finds the target area for friendly actions based on the specified range and strategy.
    /// </summary>
    /// <param name="range">The range within which to find the target area.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <param name="player">The player character.</param>
    /// <returns>
    /// A <see cref="TargetResult"/> containing the target area and affected characters, or <c>null</c> if no target area is found.
    /// </returns>
    private readonly TargetResult? FindTargetAreaFriend(float range, IEnumerable<IBattleChara> canAffects, IPlayerCharacter player)
    {
        if (canAffects == null)
        {
            return null;
        }

        // If range is zero, always target self
        if (range == 0)
        {
            return new TargetResult(player, [.. GetAffectsVector(player.Position, canAffects)], player.Position);
        }

        // --- Try OnLocations logic first ---
        _ = OtherConfiguration.BeneficialPositions.TryGetValue(Svc.ClientState.TerritoryType, out Vector3[]? pts);
        pts ??= [];

        // Use fallback points if no beneficial positions are found
        if (pts.Length == 0)
        {
            if (DataCenter.Territory?.ContentType == TerritoryContentType.Trials ||
                (DataCenter.Territory?.ContentType == TerritoryContentType.Raids &&
                 DataCenter.PartyMembers.Count(p => p is IPlayerCharacter) >= 8))
            {
                Vector3[] fallbackPoints = new[] { Vector3.Zero, new Vector3(100, 0, 100) };
                Vector3 closestFallback = fallbackPoints[0];
                float minDistance = Vector3.Distance(player.Position, fallbackPoints[0]);

                for (int i = 1; i < fallbackPoints.Length; i++)
                {
                    float distance = Vector3.Distance(player.Position, fallbackPoints[i]);
                    if (distance < minDistance)
                    {
                        closestFallback = fallbackPoints[i];
                        minDistance = distance;
                    }
                }

                pts = [closestFallback];
            }
        }

        // Find the closest point and apply a random offset
        if (pts.Length > 0)
        {
            Vector3 closest = pts[0];
            float minDistance = Vector3.Distance(player.Position, pts[0]);

            for (int i = 1; i < pts.Length; i++)
            {
                float distance = Vector3.Distance(player.Position, pts[i]);
                if (distance < minDistance)
                {
                    closest = pts[i];
                    minDistance = distance;
                }
            }

            Random random = new();
            double rotation = random.NextDouble() * Math.Tau;
            double radius = random.NextDouble();
            closest.X += (float)(Math.Sin(rotation) * radius);
            closest.Z += (float)(Math.Cos(rotation) * radius);

            // Check if the closest point is within the effect range
            if (Vector3.Distance(player.Position, closest) < player.HitboxRadius + EffectRange)
            {
                return new TargetResult(player, GetAffectsVector(closest, canAffects).ToArray(), closest);
            }
        }

        // --- OnCalculated logic (fallback) ---
        if (Svc.Targets.Target is IBattleChara b && b.DistanceToPlayer() < range &&
            b.IsBossFromIcon() && b.HasPositional() && b.HitboxRadius <= 8)
        {
            // Ensure the player's position is within the range of the ability
            if (Vector3.Distance(player.Position, b.Position) <= range)
            {
                return new TargetResult(b, GetAffectsVector(b.Position, canAffects).ToArray(), b.Position);
            }
            else
            {
                // Adjust the position to be within the range
                Vector3 directionToTarget = b.Position - player.Position;
                Vector3 adjustedPosition = player.Position + (directionToTarget / directionToTarget.Length() * range);
                return new TargetResult(b, GetAffectsVector(adjustedPosition, canAffects).ToArray(), adjustedPosition);
            }
        }
        else
        {
            float effectRange = EffectRange;
            IBattleChara? attackT = FindTargetByType(DataCenter.PartyMembers.GetObjectInRadius(range + effectRange),
                TargetType.BeAttacked, action.Config.AutoHealRatio, action.Setting.SpecialType);

            if (attackT == null)
            {
                return new TargetResult(player, GetAffectsVector(player.Position, canAffects).ToArray(), player.Position);
            }
            else
            {
                float disToTankRound = Vector3.Distance(player.Position, attackT.Position) + attackT.HitboxRadius;

                if (disToTankRound < effectRange
                    || disToTankRound > (2 * effectRange) - player.HitboxRadius)
                {
                    return new TargetResult(player, GetAffectsVector(player.Position, canAffects).ToArray(), player.Position);
                }
                else
                {
                    Vector3 directionToTank = attackT.Position - player.Position;
                    Vector3 moveDirection = directionToTank / directionToTank.Length() * Math.Max(0, disToTankRound - effectRange);
                    return new TargetResult(player, GetAffectsVector(player.Position, canAffects).ToArray(), player.Position + moveDirection);
                }
            }
        }
    }


    /// <summary>
    /// Gets the characters that are affected within the specified range from a given point.
    /// </summary>
    /// <param name="point">The point from which to measure the effect range.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <returns>
    /// An <see cref="IEnumerable{IBattleChara}"/> containing the characters that are within the effect range.
    /// </returns>
    private readonly IEnumerable<IBattleChara> GetAffectsVector(Vector3? point, IEnumerable<IBattleChara> canAffects)
    {
        if (canAffects == null)
        {
            yield break;
        }

        if (point == null)
        {
            yield break;
        }

        foreach (IBattleChara t in canAffects)
        {
            if (Vector3.Distance(point.Value, t.Position) - t.HitboxRadius <= EffectRange)
            {
                yield return t;
            }
        }
    }

    /// <summary>
    /// Gets the characters that are affected by the specified target.
    /// </summary>
    /// <param name="tar">The target character.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <returns>
    /// An <see cref="IEnumerable{IBattleChara}"/> containing the characters that are affected by the target.
    /// </returns>
    private readonly IEnumerable<IBattleChara> GetAffectsTarget(IBattleChara tar, IEnumerable<IBattleChara> canAffects)
    {
        if (tar == null)
        {
            yield break;
        }

        if (canAffects == null)
        {
            yield break;
        }

        foreach (IBattleChara t in canAffects)
        {
            if (CanGetTarget(tar, t))
            {
                yield return t;
            }
        }
    }

    #endregion

    #region Get Most Target

    /// <summary>
    /// Gets the most targetable objects based on the specified criteria.
    /// </summary>
    /// <param name="canTargets">The potential targets that can be affected.</param>
    /// <param name="canAffects">The potential characters that can be affected.</param>
    /// <param name="aoeCount">The number of targets to consider for AoE.</param>
    /// <returns>
    /// An <see cref="IEnumerable{IBattleChara}"/> containing the most targetable objects based on the specified criteria.
    /// </returns>
    private readonly IEnumerable<IBattleChara> GetMostCanTargetObjects(IEnumerable<IBattleChara> canTargets, IEnumerable<IBattleChara> canAffects, int aoeCount)
    {
        if (canTargets == null || canAffects == null)
        {
            yield break;
        }

        if (IsSingleTarget || EffectRange <= 0)
        {
            foreach (IBattleChara target in canTargets)
            {
                yield return target;
            }
            yield break;
        }
        if (!action.Setting.IsFriendly && Service.Config.AoEType == AoEType.Off)
        {
            yield break;
        }

        if (aoeCount > 1 && Service.Config.AoEType == AoEType.Cleave)
        {
            yield break;
        }

        int canTargetsCount = 0;
        foreach (var _ in canTargets)
        {
            canTargetsCount++;
        }
        List<IBattleChara> objectMax = new(canTargetsCount);

        foreach (IBattleChara t in canTargets)
        {
            int count = CanGetTargetCount(t, canAffects);

            if (count == aoeCount)
            {
                objectMax.Add(t);
            }
            else if (count > aoeCount)
            {
                aoeCount = count;
                objectMax.Clear();
                objectMax.Add(t);
            }
        }

        foreach (IBattleChara obj in objectMax)
        {
            yield return obj;
        }
    }

    /// <summary>
    /// Counts the number of objects that can be targeted based on the specified criteria.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="canAffects">The potential objects that can be affected.</param>
    /// <returns>The count of objects that can be targeted.</returns>
    private readonly int CanGetTargetCount(IGameObject target, IEnumerable<IGameObject> canAffects)
    {
        if (target == null || canAffects == null)
        {
            return 0;
        }

        int count = 0;
        foreach (IGameObject t in canAffects)
        {
            if (target != t && !CanGetTarget(target, t))
            {
                continue;
            }

            if (Service.Config.NoNewHostiles && t.TargetObject == null)
            {
                return 0;
            }
            count++;
        }

        return count;
    }

    private const double _alpha = Math.PI / 3;
    /// <summary>
    /// Determines if the sub-target can be targeted based on the specified criteria.
    /// </summary>
    /// <param name="target">The main target object.</param>
    /// <param name="subTarget">The sub-target object.</param>
    /// <returns>True if the sub-target can be targeted; otherwise, false.</returns>
    private readonly bool CanGetTarget(IGameObject target, IGameObject subTarget)
    {
        if (target == null || subTarget == null)
        {
            return false;
        }

        Vector3 pPos = Player.Object.Position;
        Vector3 dir = target.Position - pPos;
        Vector3 tdir = subTarget.Position - pPos;

        switch (action.Action.CastType)
        {
            case 2: // Circle
                return Vector3.Distance(target.Position, subTarget.Position) - subTarget.HitboxRadius <= EffectRange;

            case 3: // Sector
                if (subTarget.DistanceToPlayer() > EffectRange)
                {
                    return false;
                }

                tdir += dir / dir.Length() * target.HitboxRadius / (float)Math.Sin(_alpha);
                return Vector3.Dot(dir, tdir) / (dir.Length() * tdir.Length()) >= Math.Cos(_alpha);

            case 4: // Line
                if (subTarget.DistanceToPlayer() > EffectRange)
                {
                    return false;
                }

                return Vector3.Cross(dir, tdir).Length() / dir.Length() <= 2 + target.HitboxRadius
                    && Vector3.Dot(dir, tdir) >= 0;

            case 10: // Donut
                float dis = Vector3.Distance(target.Position, subTarget.Position) - subTarget.HitboxRadius;
                return dis <= EffectRange && dis >= 8;

            default:
                PluginLog.Debug($"{action.Action.Name.ExtractText()}'s CastType is not valid! The value is {action.Action.CastType}");
                return false;
        }
    }
    #endregion

    #region TargetFind

    /// <summary>
    /// Finds the target based on the specified type and criteria.
    /// </summary>
    /// <param name="IGameObjects"></param>
    /// <param name="type"></param>
    /// <param name="healRatio"></param>
    /// <param name="actionType"></param>
    /// <returns></returns>
    public static IBattleChara? FindTargetByType(IEnumerable<IBattleChara> IGameObjects, TargetType type, float healRatio, SpecialActionType actionType)
    {
        if (IGameObjects == null)
        {
            return null;
        }

        if (type == TargetType.Self)
        {
            return Player.Object;
        }

        switch (actionType)
        {
            case SpecialActionType.MeleeRange:
                IGameObjects = IGameObjects.Where(t => t.DistanceToPlayer() >= 3 + (Service.Config?.MeleeRangeOffset ?? 0));
                break;

            case SpecialActionType.MovingForward:
                if (Service.Config != null)
                {
                    if (DataCenter.MergedStatus.HasFlag(AutoStatus.MoveForward) || Service.CountDownTime > 0)
                    {
                        type = TargetType.Move;
                    }
                    else
                    {
                        IGameObjects = IGameObjects.Where(t => t.DistanceToPlayer() < Service.Config.DistanceForMoving);
                    }
                }
                break;
        }

        switch (type) // Filter the objects.
        {
            case TargetType.Death:
                IGameObjects = IGameObjects.Where(ObjectHelper.IsDeathToRaise);
                break;

            case TargetType.Move:
                // No filtering needed for Move type
                break;

            default:
                IGameObjects = IGameObjects.Where(ObjectHelper.IsAlive);
                break;
        }

        return type switch // Find the object.
        {
            TargetType.BeAttacked => FindBeAttackedTarget(),
            TargetType.Provoke => FindProvokeTarget(),
            TargetType.Dispel => FindDispelTarget(),
            TargetType.Death => FindDeathPeople(),
            TargetType.Move => FindTargetForMoving(),
            TargetType.Heal => FindHealTarget(healRatio),
            TargetType.Interrupt => FindInterruptTarget(),
            TargetType.Tank => FindTankTarget(),
            TargetType.Melee => IGameObjects != null ? RandomMeleeTarget(IGameObjects) : null,
            TargetType.Range => IGameObjects != null ? RandomRangeTarget(IGameObjects) : null,
            TargetType.Magical => IGameObjects != null ? RandomMagicalTarget(IGameObjects) : null,
            TargetType.Physical => IGameObjects != null ? RandomPhysicalTarget(IGameObjects) : null,
            TargetType.DancePartner => FindDancePartner(),
            TargetType.MimicryTarget => FindMimicryTarget(),
            TargetType.TheSpear => FindTheSpear(),
            TargetType.TheBalance => FindTheBalance(),
            TargetType.Kardia => FindKardia(),
            TargetType.Deployment => FindDeploymentTacticsTarget(),
            _ => FindHostile(),
        };

        IBattleChara? FindDancePartner()
        {
            List<Job> dancePartnerPriority = OtherConfiguration.DancePartnerPriority;

            if (!Player.Object.IsJobs(Job.DNC))
            {
                return null;
            }

            if (DataCenter.PartyMembers == null)
            {
                return null;
            }

            foreach (Job job in dancePartnerPriority)
            {
                foreach (IBattleChara member in DataCenter.PartyMembers)
                {
                    if (member.IsJobs(job) && !member.IsDead && !member.HasStatus(false, StatusID.DamageDown_2911, StatusID.DamageDown, StatusID.Weakness, StatusID.BrinkOfDeath) && member != Player.Object)
                    {
                        PluginLog.Debug($"FindDancePartner: {member.Name} selected target.");
                        return member;
                    }
                }
            }

            PluginLog.Debug($"FindDancePartner: No target found, using fallback.");
            return RandomMeleeTarget(IGameObjects)
                ?? RandomRangeTarget(IGameObjects)
                ?? RandomMagicalTarget(IGameObjects)
                ?? RandomPhysicalTarget(IGameObjects)
                ?? null;
        }

        IBattleChara? FindTheSpear()
        {
            // The Spear priority based on the info from The Balance Discord for Level 100 Dance Partner
            List<Job> TheSpearPriority = OtherConfiguration.TheSpearPriority;

            if (!Player.Object.IsJobs(Job.AST))
            {
                return null;
            }

            if (DataCenter.PartyMembers == null)
            {
                return null;
            }

            foreach (Job job in TheSpearPriority)
            {
                foreach (IBattleChara member in DataCenter.PartyMembers)
                {
                    if (member.IsJobs(job) && !member.IsDead)
                    {
                        PluginLog.Debug($"FindTheSpear: {member.Name} selected target.");
                        return member;
                    }
                }
            }

            return RandomRangeTarget(IGameObjects)
                ?? RandomMeleeTarget(IGameObjects)
                ?? RandomMagicalTarget(IGameObjects)
                ?? RandomPhysicalTarget(IGameObjects)
                ?? null;
        }

        IBattleChara? FindTheBalance()
        {
            // The Balance priority based on the info from The Balance Discord for Level 100 Dance Partner
            List<Job> TheBalancePriority = OtherConfiguration.TheBalancePriority;

            if (!Player.Object.IsJobs(Job.AST))
            {
                return null;
            }

            if (DataCenter.PartyMembers == null)
            {
                return null;
            }

            foreach (Job job in TheBalancePriority)
            {
                foreach (IBattleChara member in DataCenter.PartyMembers)
                {
                    if (member.IsJobs(job) && !member.IsDead)
                    {
                        PluginLog.Debug($"FindTheBalance: {member.Name} selected target.");
                        return member;
                    }
                }
            }

            return RandomMeleeTarget(IGameObjects)
                ?? RandomRangeTarget(IGameObjects)
                ?? RandomMagicalTarget(IGameObjects)
                ?? RandomPhysicalTarget(IGameObjects)
                ?? null;
        }

        IBattleChara? FindKardia()
        {
            List<Job> KardiaTankPriority = OtherConfiguration.KardiaTankPriority;

            if (!Player.Object.IsJobs(Job.SGE))
            {
                return null;
            }

            if (DataCenter.PartyMembers == null)
            {
                return null;
            }

            foreach (Job job in KardiaTankPriority)
            {
                foreach (IBattleChara m in DataCenter.PartyMembers)
                {
                    if (m.IsJobCategory(JobRole.Tank) && m.IsJobs(job) && !m.IsDead)
                    {
                        // 1. Tanks with tank stance and without Kardion
                        if (m.HasStatus(false, StatusHelper.TankStanceStatus) && !m.HasStatus(false, StatusID.Kardion))
                        {
                            PluginLog.Debug($"FindKardia 1: {m.Name} is a tank with TankStanceStatus and without Kardion.");
                            return m;
                        }
                    }
                }
            }

            foreach (Job job in KardiaTankPriority)
            {
                foreach (IBattleChara m in DataCenter.PartyMembers)
                {
                    if (m.IsJobCategory(JobRole.Tank) && m.IsJobs(job) && !m.IsDead)
                    {
                        // 2. Tanks with tank stance (regardless of Kardion)
                        if (m.HasStatus(false, StatusHelper.TankStanceStatus))
                        {
                            PluginLog.Debug($"FindKardia 2: {m.Name} is a tank with TankStanceStatus.");
                            return m;
                        }
                    }
                }
            }

            foreach (Job job in KardiaTankPriority)
            {
                foreach (IBattleChara m in DataCenter.PartyMembers)
                {
                    if (m.IsJobCategory(JobRole.Tank) && m.IsJobs(job) && !m.IsDead)
                    {
                        // 3. Any alive tank in priority order
                        PluginLog.Debug($"FindKardia 3: {m.Name} is a tank fallback.");
                        return m;
                    }
                }
            }

            PluginLog.Debug($"FindKardia: No target found, using fallback.");
            return FindTankTarget()
                ?? RandomMeleeTarget(DataCenter.PartyMembers)
                ?? RandomPhysicalTarget(DataCenter.PartyMembers)
                ?? RandomRangeTarget(DataCenter.PartyMembers)
                ?? RandomMagicalTarget(DataCenter.PartyMembers)
                ?? null;
        }

        IBattleChara? FindDeploymentTacticsTarget()
        {
            if (!Player.Object.IsJobs(Job.SCH))
            {
                return null;
            }

            if (DataCenter.PartyMembers == null)
            {
                return null;
            }

            IBattleChara? bestCatalyze = null;
            uint bestCatalyzeShield = 0;

            IBattleChara? bestGalvanize = null;
            uint bestGalvanizeShield = 0;

            foreach (IBattleChara obj in DataCenter.PartyMembers)
            {
                if (obj == null || obj.IsDead)
                {
                    continue;
                }

                uint shield = ObjectHelper.GetObjectShield(obj);

                if (!obj.WillStatusEnd(20, true, StatusID.Catalyze))
                {
                    if (bestCatalyze == null || shield > bestCatalyzeShield)
                    {
                        bestCatalyze = obj;
                        bestCatalyzeShield = shield;
                    }
                }
                else if (!obj.WillStatusEnd(20, true, StatusID.Galvanize))
                {
                    if (bestGalvanize == null || shield > bestGalvanizeShield)
                    {
                        bestGalvanize = obj;
                        bestGalvanizeShield = shield;
                    }
                }
            }

            if (bestCatalyze != null)
            {
                PluginLog.Debug($"FindDeploymentTacticsTarget: {bestCatalyze.Name} is a valid target with Catalyze and largest shield.");
                return bestCatalyze;
            }

            if (bestGalvanize != null)
            {
                PluginLog.Debug($"FindDeploymentTacticsTarget: {bestGalvanize.Name} is a valid target with Galvanize and largest shield.");
                return bestGalvanize;
            }

            return null;
        }

        IBattleChara? FindProvokeTarget()
        {
            return IGameObjects == null || DataCenter.ProvokeTarget == null
                ? null
                : IGameObjects.Any(o => o.GameObjectId == DataCenter.ProvokeTarget.GameObjectId) ? DataCenter.ProvokeTarget : null;
        }

        IBattleChara? FindDeathPeople()
        {
            return IGameObjects == null || DataCenter.DeathTarget == null
                ? null
                : IGameObjects.Any(o => o.GameObjectId == DataCenter.DeathTarget.GameObjectId) ? DataCenter.DeathTarget : null;
        }

        IBattleChara? FindTargetForMoving()
        {
            return Service.Config == null || IGameObjects == null
                ? null
                : Service.Config.MoveTowardsScreenCenter ? FindMoveTargetScreenCenter() : FindMoveTargetFaceDirection();
            IBattleChara? FindMoveTargetScreenCenter()
            {
                Vector3 pPosition = Player.Object.Position;
                if (!Svc.GameGui.WorldToScreen(pPosition, out Vector2 playerScrPos))
                {
                    return null;
                }

                IOrderedEnumerable<IBattleChara> tars = IGameObjects.Where(t =>
                {
                    if (t.DistanceToPlayer() > Service.Config.DistanceForMoving)
                    {
                        return false;
                    }

                    if (!Svc.GameGui.WorldToScreen(t.Position, out Vector2 scrPos))
                    {
                        return false;
                    }

                    Vector2 dir = scrPos - playerScrPos;

                    return dir.Y <= 0 && Math.Abs(dir.X / dir.Y) <= Math.Tan(Math.PI * Service.Config.MoveTargetAngle / 360);
                }).OrderByDescending(ObjectHelper.DistanceToPlayer);

                return tars.FirstOrDefault();
            }

            IBattleChara? FindMoveTargetFaceDirection()
            {
                Vector3 pPosition = Player.Object.Position;
                Vector3 faceVec = Player.Object.GetFaceVector();

                IOrderedEnumerable<IBattleChara> tars = IGameObjects.Where(t =>
                {
                    if (t.DistanceToPlayer() > Service.Config.DistanceForMoving)
                    {
                        return false;
                    }

                    Vector3 dir = t.Position - pPosition;
                    float angle = Vector3.Dot(faceVec, Vector3.Normalize(dir));
                    return angle >= Math.Cos(Math.PI * Service.Config.MoveTargetAngle / 360);
                }).OrderByDescending(ObjectHelper.DistanceToPlayer);

                return tars.FirstOrDefault();
            }
        }

        IBattleChara? FindHealTarget(float healRatio)
        {
            if (IGameObjects == null)
            {
                return null;
            }

            // Manual Any() check
            bool hasAny = false;
            foreach (var _ in IGameObjects)
            {
                hasAny = true;
                break;
            }
            if (!hasAny || Service.Config == null)
            {
                return null;
            }

            // FilteredGameObjects = IGameObjects
            List<IBattleChara> filteredGameObjects = new();
            foreach (var o in IGameObjects)
            {
                if (!IBaseAction.AutoHealCheck || o.GetHealthRatio() < healRatio)
                {
                    filteredGameObjects.Add(o);
                }
            }

            // partyMembers = filteredGameObjects.Where(ObjectHelper.IsParty)
            List<IBattleChara> partyMembers = new();
            foreach (var o in filteredGameObjects)
            {
                if (ObjectHelper.IsParty(o))
                {
                    partyMembers.Add(o);
                }
            }

            IBattleChara? result = GeneralHealTarget(partyMembers);
            if (result != null) return result;

            result = GeneralHealTarget(filteredGameObjects);
            if (result != null) return result;

            // partyMembers.FirstOrDefault(t => t.HasStatus(false, StatusHelper.TankStanceStatus))
            foreach (var t in partyMembers)
            {
                if (t.HasStatus(false, StatusHelper.TankStanceStatus))
                {
                    return t;
                }
            }

            // partyMembers.FirstOrDefault()
            if (partyMembers.Count > 0)
            {
                return partyMembers[0];
            }

            // filteredGameObjects.FirstOrDefault(t => t.HasStatus(false, StatusHelper.TankStanceStatus))
            foreach (var t in filteredGameObjects)
            {
                if (t.HasStatus(false, StatusHelper.TankStanceStatus))
                {
                    return t;
                }
            }

            // filteredGameObjects.FirstOrDefault()
            if (filteredGameObjects.Count > 0)
            {
                return filteredGameObjects[0];
            }

            return null;

            static IBattleChara? GeneralHealTarget(List<IBattleChara> objs)
            {
                // healingNeededObjs = objs.Where(StatusHelper.NoNeedHealingInvuln).OrderBy(ObjectHelper.GetHealthRatio)
                List<IBattleChara> healingNeededObjs = [];
                foreach (var o in objs)
                {
                    if (StatusHelper.NoNeedHealingInvuln(o))
                    {
                        healingNeededObjs.Add(o);
                    }
                }
                // Sort by ObjectHelper.GetHealthRatio
                healingNeededObjs.Sort((a, b) => ObjectHelper.GetHealthRatio(a).CompareTo(ObjectHelper.GetHealthRatio(b)));

                // healerTars = healingNeededObjs.GetJobCategory(JobRole.Healer)
                List<IBattleChara> healerTars = [];
                foreach (var o in healingNeededObjs)
                {
                    if (TargetFilter.GetJobCategory([o], JobRole.Healer).Any())
                    {
                        healerTars.Add(o);
                    }
                }

                // tankTars = healingNeededObjs.GetJobCategory(JobRole.Tank)
                List<IBattleChara> tankTars = [];
                foreach (var o in healingNeededObjs)
                {
                    if (TargetFilter.GetJobCategory([o], JobRole.Tank).Any())
                    {
                        tankTars.Add(o);
                    }
                }

                // healerTar = healerTars.FirstOrDefault()
                IBattleChara? healerTar = healerTars.Count > 0 ? healerTars[0] : null;
                if (healerTar != null && healerTar.GetHealthRatio() < Service.Config.HealthHealerRatio)
                {
                    return healerTar;
                }

                // tankTar = tankTars.FirstOrDefault()
                IBattleChara? tankTar = tankTars.Count > 0 ? tankTars[0] : null;
                if (tankTar != null && tankTar.GetHealthRatio() < Service.Config.HealthTankRatio)
                {
                    return tankTar;
                }

                // tar = healingNeededObjs.FirstOrDefault()
                IBattleChara? tar = healingNeededObjs.Count > 0 ? healingNeededObjs[0] : null;
                return tar != null && tar.GetHealthRatio() < 1 ? tar : null;
            }
        }

        IBattleChara? FindInterruptTarget()
        {
            if (IGameObjects == null || DataCenter.InterruptTarget == null)
            {
                return null;
            }

            foreach (var o in IGameObjects)
            {
                if (o.GameObjectId == DataCenter.InterruptTarget.GameObjectId)
                {
                    return DataCenter.InterruptTarget;
                }
            }
            return null;
        }

        IBattleChara? FindHostile()
        {
            if (IGameObjects == null)
            {
                return null;
            }

            // Manual Any() check
            bool hasAny = false;
            foreach (var _ in IGameObjects)
            {
                hasAny = true;
                break;
            }
            if (!hasAny)
            {
                return null;
            }

            // Filter out characters marked with stop markers
            if (Service.Config.FilterStopMark)
            {
                IEnumerable<IBattleChara> filteredCharacters = MarkingHelper.FilterStopCharacters(IGameObjects);
                // Manual Any() check
                bool filteredHasAny = false;
                if (filteredCharacters != null)
                {
                    foreach (var _ in filteredCharacters)
                    {
                        filteredHasAny = true;
                        break;
                    }
                }
                if (filteredCharacters != null && filteredHasAny)
                {
                    IGameObjects = filteredCharacters;
                }
            }

            // Handle treasure characters
            if (DataCenter.TreasureCharas != null && DataCenter.TreasureCharas.Length > 0)
            {
                IBattleChara? treasureChara = null;
                foreach (var b in IGameObjects)
                {
                    if (b.GameObjectId == DataCenter.TreasureCharas[0])
                    {
                        treasureChara = b;
                        break;
                    }
                }
                if (treasureChara != null)
                {
                    return treasureChara;
                }

                var tempList = new List<IBattleChara>();
                foreach (var b in IGameObjects)
                {
                    bool found = false;
                    foreach (var id in DataCenter.TreasureCharas)
                    {
                        if (b.GameObjectId == id)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        tempList.Add(b);
                    }
                }
                IGameObjects = tempList;
            }

            // Filter high priority hostiles
            var highPriorityHostiles = new List<IBattleChara>();
            foreach (var b in IGameObjects)
            {
                if (ObjectHelper.IsTopPriorityHostile(b))
                {
                    highPriorityHostiles.Add(b);
                }
            }
            if (highPriorityHostiles.Count > 0)
            {
                IGameObjects = highPriorityHostiles;
            }

            return FindHostileRaw();
        }

        IBattleChara? FindHostileRaw()
        {
            if (IGameObjects == null)
            {
                return null;
            }

            // Prepare a list to sort manually
            List<IGameObject> objects = [.. IGameObjects];

            List<IGameObject> filtered;
            switch (DataCenter.TargetingType)
            {
                case TargetingType.Small:
                    if (Service.Config.SmallHp)
                    {
                        // Order by HitboxRadius ascending, then by CurrentHp ascending
                        filtered = [.. objects];
                        filtered.Sort((a, b) =>
                        {
                            int cmp = a.HitboxRadius.CompareTo(b.HitboxRadius);
                            if (cmp != 0) return cmp;
                            float aHp = a is IBattleChara ba ? ba.CurrentHp : float.MaxValue;
                            float bHp = b is IBattleChara bb ? bb.CurrentHp : float.MaxValue;
                            return aHp.CompareTo(bHp);
                        });
                    }
                    else
                    {
                        // Order by HitboxRadius ascending, then by CurrentHp descending
                        filtered = [.. objects];
                        filtered.Sort((a, b) =>
                        {
                            int cmp = a.HitboxRadius.CompareTo(b.HitboxRadius);
                            if (cmp != 0) return cmp;
                            float aHp = a is IBattleChara ba ? ba.CurrentHp : 0;
                            float bHp = b is IBattleChara bb ? bb.CurrentHp : 0;
                            return bHp.CompareTo(aHp);
                        });
                    }
                    break;
                case TargetingType.HighHP:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        uint aHp = a is IBattleChara ba ? ba.CurrentHp : 0;
                        uint bHp = b is IBattleChara bb ? bb.CurrentHp : 0;
                        return bHp.CompareTo(aHp);
                    });
                    break;
                case TargetingType.LowHP:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        uint aHp = a is IBattleChara ba ? ba.CurrentHp : 0;
                        uint bHp = b is IBattleChara bb ? bb.CurrentHp : 0;
                        return aHp.CompareTo(bHp);
                    });
                    break;
                case TargetingType.HighHPPercent:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        float aPct = a is IBattleChara ba && ba.MaxHp != 0 ? (float)ba.CurrentHp / ba.MaxHp : 0;
                        float bPct = b is IBattleChara bb && bb.MaxHp != 0 ? (float)bb.CurrentHp / bb.MaxHp : 0;
                        return bPct.CompareTo(aPct);
                    });
                    break;
                case TargetingType.LowHPPercent:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        float aPct = a is IBattleChara ba && ba.MaxHp != 0 ? (float)ba.CurrentHp / ba.MaxHp : 0;
                        float bPct = b is IBattleChara bb && bb.MaxHp != 0 ? (float)bb.CurrentHp / bb.MaxHp : 0;
                        return aPct.CompareTo(bPct);
                    });
                    break;
                case TargetingType.HighMaxHP:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        uint aHp = a is IBattleChara ba ? ba.MaxHp : 0;
                        uint bHp = b is IBattleChara bb ? bb.MaxHp : 0;
                        return bHp.CompareTo(aHp);
                    });
                    break;
                case TargetingType.LowMaxHP:
                    filtered = [.. objects];
                    filtered.Sort((a, b) =>
                    {
                        uint aHp = a is IBattleChara ba ? ba.MaxHp : 0;
                        uint bHp = b is IBattleChara bb ? bb.MaxHp : 0;
                        return aHp.CompareTo(bHp);
                    });
                    break;
                case TargetingType.Nearest:
                    filtered = [.. objects];
                    filtered.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                    break;
                case TargetingType.Farthest:
                    filtered = [.. objects];
                    filtered.Sort((a, b) => b.DistanceToPlayer().CompareTo(a.DistanceToPlayer()));
                    break;
                case TargetingType.PvPHealers:
                    {
                        // Filter for healers
                        List<IGameObject> healers = new();
                        foreach (var p in objects)
                        {
                            if (p.IsJobs(JobRole.Healer.ToJobs()))
                                healers.Add(p);
                        }
                        if (healers.Count > 0)
                        {
                            healers.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                            filtered = healers;
                        }
                        else
                        {
                            filtered = [.. objects];
                            filtered.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                        }
                        break;
                    }
                case TargetingType.PvPTanks:
                    {
                        List<IGameObject> tanks = new();
                        foreach (var p in objects)
                        {
                            if (p.IsJobs(JobRole.Tank.ToJobs()))
                                tanks.Add(p);
                        }
                        if (tanks.Count > 0)
                        {
                            tanks.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                            filtered = tanks;
                        }
                        else
                        {
                            filtered = [.. objects];
                            filtered.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                        }
                        break;
                    }
                case TargetingType.PvPDPS:
                    {
                        List<IGameObject> dps = new();
                        foreach (var p in objects)
                        {
                            if (p.IsJobs(JobRole.AllDPS.ToJobs()))
                                dps.Add(p);
                        }
                        if (dps.Count > 0)
                        {
                            dps.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                            filtered = dps;
                        }
                        else
                        {
                            filtered = [.. objects];
                            filtered.Sort((a, b) => a.DistanceToPlayer().CompareTo(b.DistanceToPlayer()));
                        }
                        break;
                    }
                default:
                    if (Service.Config.SmallHp)
                    {
                        filtered = [.. objects];
                        filtered.Sort((a, b) =>
                        {
                            int cmp = b.HitboxRadius.CompareTo(a.HitboxRadius);
                            if (cmp != 0) return cmp;
                            float aHp = a is IBattleChara ba ? ba.CurrentHp : float.MaxValue;
                            float bHp = b is IBattleChara bb ? bb.CurrentHp : float.MaxValue;
                            return aHp.CompareTo(bHp);
                        });
                    }
                    else
                    {
                        filtered = [.. objects];
                        filtered.Sort((a, b) =>
                        {
                            int cmp = b.HitboxRadius.CompareTo(a.HitboxRadius);
                            if (cmp != 0) return cmp;
                            float aHp = a is IBattleChara ba ? ba.CurrentHp : 0;
                            float bHp = b is IBattleChara bb ? bb.CurrentHp : 0;
                            return bHp.CompareTo(aHp);
                        });
                    }
                    break;
            }

            foreach (var obj in filtered)
            {
                if (obj is IBattleChara bc)
                    return bc;
            }
            return null;
        }

        IBattleChara? FindBeAttackedTarget()
        {
            if (IGameObjects == null)
            {
                return null;
            }

            // Manual Any() check
            bool hasAny = false;
            foreach (var _ in IGameObjects)
            {
                hasAny = true;
                break;
            }
            if (!hasAny)
            {
                return null;
            }

            // attachedT = IGameObjects.Where(ObjectHelper.IsTargetOnSelf)
            List<IBattleChara> attachedT = new();
            foreach (var t in IGameObjects)
            {
                if (ObjectHelper.IsTargetOnSelf(t))
                {
                    attachedT.Add(t);
                }
            }

            if (!DataCenter.AutoStatus.HasFlag(AutoStatus.DefenseSingle))
            {
                // If attachedT is empty, try tanks with TankStanceStatus
                if (attachedT.Count == 0)
                {
                    foreach (var tank in IGameObjects)
                    {
                        if (tank.HasStatus(false, StatusHelper.TankStanceStatus))
                        {
                            attachedT.Add(tank);
                        }
                    }
                }

                // If still empty, try tanks by job category
                if (attachedT.Count == 0)
                {
                    foreach (var tank in IGameObjects.GetJobCategory(JobRole.Tank))
                    {
                        attachedT.Add(tank);
                    }
                }

                // If still empty, fallback to all
                if (attachedT.Count == 0)
                {
                    foreach (var t in IGameObjects)
                    {
                        attachedT.Add(t);
                    }
                }
            }

            // Select target based on Priolowtank config
            if (attachedT.Count == 0)
            {
                return null;
            }

            if (Service.Config.Priolowtank)
            {
                // OrderByDescending(ObjectHelper.GetHealthRatio).LastOrDefault()
                IBattleChara? lowest = null;
                float minHealth = float.MaxValue;
                foreach (var t in attachedT)
                {
                    float health = ObjectHelper.GetHealthRatio(t);
                    if (health < minHealth)
                    {
                        minHealth = health;
                        lowest = t;
                    }
                }
                return lowest;
            }
            else
            {
                // OrderBy(ObjectHelper.GetHealthRatio).FirstOrDefault()
                IBattleChara? lowest = null;
                float minHealth = float.MaxValue;
                foreach (var t in attachedT)
                {
                    float health = ObjectHelper.GetHealthRatio(t);
                    if (health < minHealth)
                    {
                        minHealth = health;
                        lowest = t;
                    }
                }
                return lowest;
            }
        }

        IBattleChara? FindDispelTarget()
        {
            if (IGameObjects == null || DataCenter.DispelTarget == null)
            {
                return null;
            }

            // Manual Any() check for GameObjectId match
            bool found = false;
            foreach (var o in IGameObjects)
            {
                if (o.GameObjectId == DataCenter.DispelTarget.GameObjectId)
                {
                    found = true;
                    break;
                }
            }
            if (found)
            {
                return DataCenter.DispelTarget;
            }

            // Manual FirstOrDefault for a battle chara with a dispellable status
            foreach (var o in IGameObjects)
            {
                if (o is IBattleChara b)
                {
                    foreach (var status in b.StatusList)
                    {
                        if (StatusHelper.CanDispel(status))
                        {
                            return b;
                        }
                    }
                }
            }

            return null;
        }

        IBattleChara? FindTankTarget()
        {
            return IGameObjects == null ? null : RandomPickByJobs(IGameObjects, JobRole.Tank);
        }
    }

    private static IBattleChara? FindMimicryTarget()
    {
        if (DataCenter.AllTargets == null)
        {
            return null;
        }

        IOrderedEnumerable<IBattleChara> targetCandidates = DataCenter.AllTargets
            .Where(target => target != null && IsNeededRole(target))
            .OrderBy(target => Player.Object != null ? Player.DistanceTo(target.Position) : float.MaxValue);

        return targetCandidates.FirstOrDefault();
    }

    private static bool IsNeededRole(IBattleChara character)
    {
        if (character.GameObjectId == Player.Object.GameObjectId)
        {
            return false;
        }

        if (character is not IPlayerCharacter player || player.ClassJob.Value.IsLimitedJob)
        {
            return false;
        }

        CombatRole? neededRole = DataCenter.BluRole;
        return neededRole != null && neededRole != CombatRole.None && (int)neededRole == (int)player.GetRole();
    }

    internal static IBattleChara? RandomPhysicalTarget(IEnumerable<IBattleChara> tars)
    {
        return RandomPickByJobs(tars, Job.VPR, Job.WAR, Job.GNB, Job.MNK, Job.SAM, Job.DRG, Job.MCH, Job.DNC)
            ?? RandomPickByJobs(tars, Job.PLD, Job.DRK, Job.NIN, Job.BRD, Job.RDM)
            ?? RandomObject(tars);
    }

    internal static IBattleChara? RandomMagicalTarget(IEnumerable<IBattleChara> tars)
    {
        return RandomPickByJobs(tars, Job.PCT, Job.SCH, Job.AST, Job.SGE, Job.BLM, Job.SMN)
            ?? RandomPickByJobs(tars, Job.PLD, Job.DRK, Job.NIN, Job.BRD, Job.RDM)
            ?? RandomObject(tars);
    }

    internal static IBattleChara? RandomRangeTarget(IEnumerable<IBattleChara> tars)
    {
        return RandomPickByJobs(tars, JobRole.RangedMagical, JobRole.RangedPhysical, JobRole.Melee)
            ?? RandomPickByJobs(tars, JobRole.Tank, JobRole.Healer)
            ?? RandomObject(tars);
    }

    internal static IBattleChara? RandomMeleeTarget(IEnumerable<IBattleChara> tars)
    {
        return RandomPickByJobs(tars, JobRole.Melee, JobRole.RangedMagical, JobRole.RangedPhysical)
            ?? RandomPickByJobs(tars, JobRole.Tank, JobRole.Healer)
            ?? RandomObject(tars);
    }

    private static IBattleChara? RandomPickByJobs(IEnumerable<IBattleChara> tars, params JobRole[] roles)
    {
        foreach (JobRole role in roles)
        {
            IBattleChara? tar = RandomPickByJobs(tars, role.ToJobs());
            if (tar != null)
            {
                return tar;
            }
        }
        return null;
    }

    private static IBattleChara? RandomPickByJobs(IEnumerable<IBattleChara> tars, params Job[] jobs)
    {
        IEnumerable<IBattleChara> targets = tars.Where(t => t.IsJobs(jobs));
        return targets.Any() ? RandomObject(targets) : null;
    }

    private static IBattleChara? RandomObject(IEnumerable<IBattleChara> objs)
    {
        return objs.FirstOrDefault();
        //Random ran = new(DateTime.Now.Millisecond);
        //var count = objs.Count();
        //if (count == 0) return null;
        //return objs.ElementAt(ran.Next(count));
    }

    #endregion
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public enum TargetType : byte
{
    Big,
    Small,
    HighHP,
    LowHP,
    HighMaxHP,
    LowMaxHP,
    Interrupt,
    Provoke,
    Death,
    Dispel,
    Move,
    BeAttacked,
    Heal,
    Tank,
    Melee,
    Range,
    Physical,
    Magical,
    Self,
    DancePartner,
    MimicryTarget,
    TheBalance,
    TheSpear,
    Kardia,
    Deployment,
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// The target result
/// </summary>
/// <param name="Target">the target.</param>
/// <param name="AffectedTargets">the targets that be affected by this action.</param>
/// <param name="Position">the position to use this action.</param>
public readonly record struct TargetResult(IBattleChara Target, IBattleChara[] AffectedTargets, Vector3? Position);
