﻿using Lumina.Excel.GeneratedSheets;
using RotationSolver.GameData.Getters.Actions;

namespace RotationSolver.GameData.Getters;

/// <summary>
/// Provides methods to generate rotation code for a specific job.
/// </summary>
internal class RotationGetter
{
    private readonly Lumina.GameData gameData;
    private readonly ClassJob job;

    /// <summary>
    /// Initializes a new instance of the <see cref="RotationGetter"/> class.
    /// </summary>
    /// <param name="gameData">The game data.</param>
    /// <param name="job">The job.</param>
    public RotationGetter(Lumina.GameData gameData, ClassJob job)
    {
        this.gameData = gameData;
        this.job = job;
    }

    /// <summary>
    /// Gets the name of the rotation.
    /// </summary>
    /// <returns>The name of the rotation.</returns>
    public string GetName() => $"{job.Name} Rotation".ToPascalCase();

    /// <summary>
    /// Generates the rotation code.
    /// </summary>
    /// <returns>The generated rotation code.</returns>
    public string GetCode()
    {
        var jobName = job.Name.ToString();
        var jobs = GetJobs();
        var jobGauge = GetJobGauge();
        var rotationsGetter = new ActionSingleRotationGetter(gameData, job);
        var traitsGetter = new TraitRotationGetter(gameData, job);

        var rotationsCode = rotationsGetter.GetCode();
        var traitsCode = traitsGetter.GetCode();

        return $$"""
         /// <summary>
         /// <see href="https://na.finalfantasyxiv.com/jobguide/{{jobName?.Replace(" ", "").ToLower()}}"><strong>{{jobName}}</strong></see>
         /// <br>Number of Actions: {{rotationsGetter.Count}}</br>
         /// <br>Number of Traits: {{traitsGetter.Count}}</br>
         /// </summary>
         [Jobs({{jobs}})]
         public abstract partial class {{GetName()}} : CustomRotation
         {
             {{jobGauge}}

         #region Actions
         {{rotationsCode.Table()}}

         {{Util.ArrayNames("AllBaseActions", "IBaseAction",
         "public override", [.. rotationsGetter.AddedNames]).Table()}}

         {{GetLBInRotation(job.LimitBreak1.Value, 1)}}
         {{GetLBInRotation(job.LimitBreak2.Value, 2)}}
         {{GetLBInRotation(job.LimitBreak3.Value, 3)}}
         {{GetLBInRotationPvP(gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.FirstOrDefault(i => i.ActionCategory.Row is 15
            && ((bool?)i.ClassJobCategory.Value?.GetType().GetRuntimeProperty(job.Abbreviation)?.GetValue(i.ClassJobCategory.Value) ?? false)))}}

         #endregion

         #region Traits
         
         {{traitsCode.Table()}}

         {{Util.ArrayNames("AllTraits", "IBaseTrait",
         "public override", [.. traitsGetter.AddedNames]).Table()}}
         #endregion
         }
         """;
    }

    /// <summary>
    /// Gets the jobs associated with the current job.
    /// </summary>
    /// <returns>A string representing the jobs.</returns>
    private string GetJobs()
    {
        var jobs = $"Job.{job.Abbreviation}";
        if (job.RowId != 28 && job.RowId != job.ClassJobParent.Row)
        {
            jobs += $", Job.{job.ClassJobParent.Value?.Abbreviation ?? "ADV"}";
        }
        return jobs;
    }

    /// <summary>
    /// Gets the job gauge code.
    /// </summary>
    /// <returns>The job gauge code.</returns>
    private string GetJobGauge() => job.Abbreviation == "BLU" ? string.Empty : $"static {job.Abbreviation}Gauge JobGauge => Svc.Gauges.Get<{job.Abbreviation}Gauge>();";

    /// <summary>
    /// Gets the limit break code for a specific action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="index">The index of the limit break.</param>
    /// <returns>The limit break code.</returns>
    private string GetLBInRotation(Lumina.Excel.GeneratedSheets.Action? action, int index)
    {
        if (action == null || action.RowId == 0) return string.Empty;

        var code = GetLBPvE(action, out var name);

        return code + "\n" + $"""
            /// <summary>
            /// {action.GetDescName()}
            /// {GetDesc(action)}
            /// </summary>
            private sealed protected override IBaseAction LimitBreak{index} => {name};
            """;
    }

    /// <summary>
    /// Gets the PvE limit break code for a specific action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="name">The name of the limit break.</param>
    /// <returns>The PvE limit break code.</returns>
    private string GetLBPvE(Lumina.Excel.GeneratedSheets.Action action, out string name)
    {
        name = $"{action.Name.RawString.ToPascalCase()}PvE";
        var descName = action.GetDescName();

        return action.ToCode(name, descName, GetDesc(action), false);
    }

    /// <summary>
    /// Gets the PvP limit break code for a specific action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The PvP limit break code.</returns>
    private string GetLBInRotationPvP(Lumina.Excel.GeneratedSheets.Action? action)
    {
        if (action == null || action.RowId == 0) return string.Empty;

        var code = GetLBPvP(action, out var name);

        return code + "\n" + $"""
            /// <summary>
            /// {action.GetDescName()}
            /// {GetDesc(action)}
            /// </summary>
            private sealed protected override IBaseAction LimitBreakPvP => {name};
            """;
    }

    /// <summary>
    /// Gets the PvP limit break code for a specific action.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="name">The name of the limit break.</param>
    /// <returns>The PvP limit break code.</returns>
    private string GetLBPvP(Lumina.Excel.GeneratedSheets.Action action, out string name)
    {
        name = $"{action.Name.RawString.ToPascalCase()}PvP";
        var descName = action.GetDescName();

        return action.ToCode(name, descName, GetDesc(action), false);
    }

    /// <summary>
    /// Gets the description of a specific action.
    /// </summary>
    /// <param name="item">The action.</param>
    /// <returns>The description of the action.</returns>
    private string GetDesc(Lumina.Excel.GeneratedSheets.Action item)
    {
        var desc = gameData.GetExcelSheet<ActionTransient>()?.GetRow(item.RowId)?.Description.RawString ?? string.Empty;
        return $"<para>{desc.Replace("\n", "</para>\n/// <para>")}</para>";
    }
}