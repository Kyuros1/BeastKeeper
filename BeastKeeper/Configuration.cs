using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace BeastKeeper;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Set battlehorns when the Team Composition window opens before a battle.</summary>
    public bool AutoAssign { get; set; } = true;

    public AssignMode Mode { get; set; } = AssignMode.RememberLast;

    /// <summary>Clear the remembered team when a new run starts.</summary>
    public bool FreshTeamEachRun { get; set; } = true;

    /// <summary>Give a knocked-out (or missing) remembered familiar's battlehorn to another familiar from the roster.</summary>
    public bool ReplaceKnockedOut { get; set; } = true;

    /// <summary>Ask on entering a run whether to press Commence Battle automatically for that run.</summary>
    public bool OfferAutoCommence { get; set; } = false;

    public int AutoCommenceDelayMs { get; set; } = 800;

    public bool ChatMessages { get; set; } = true;

    /// <summary>Familiar id per battlehorn (1-3) remembered from the last battle; 0 = empty.</summary>
    public int[] LastTeam { get; set; } = [0, 0, 0];

    /// <summary>Familiar names as shown in the Team Composition window, for the settings window.</summary>
    public Dictionary<int, string> FamiliarNames { get; set; } = [];

    public string FamiliarName(int petId) =>
        FamiliarNames.TryGetValue(petId, out var name) ? name : $"Familiar #{petId}";

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
