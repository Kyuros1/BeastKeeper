using System;
using System.Linq;
using BeastKeeper.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastKeeper.Automation;

/// <summary>
/// Tracks Crucible runs. A new run is the pre-run roster screen followed by entering the duty; the Crucible HUD is
/// set up once on entry and torn down on exit. Entering a new run can clear the remembered team, and every entry can
/// ask the player whether to auto-commence for that run.
/// </summary>
public sealed unsafe class RunTracker : IDisposable
{
    private readonly Configuration config;
    private bool newRunPending;

    public RunTracker(Configuration config)
    {
        this.config = config;
        InDuty = GameUi.GetAddon(GameUi.CrucibleHudAddon) != null;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, GameUi.TeamCompositionAddon, OnTeamCompositionRefresh);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, GameUi.CrucibleHudAddon, OnEntered);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, GameUi.CrucibleHudAddon, OnLeft);
    }

    /// <summary>Raised on entering a run when the player should be asked about auto-commence.</summary>
    public event Action? AskAutoCommence;

    /// <summary>Raised on leaving the duty.</summary>
    public event Action? Left;

    public bool InDuty { get; private set; }

    /// <summary>This run's answer: null until the player answers (treated as no).</summary>
    public bool? AutoCommence { get; set; }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnTeamCompositionRefresh, OnEntered, OnLeft);
    }

    private void OnTeamCompositionRefresh(AddonEvent type, AddonArgs args)
    {
        if (!newRunPending && TeamComposition.IsRosterScreen((AtkUnitBase*)args.Addon.Address))
            newRunPending = true;
    }

    private void OnEntered(AddonEvent type, AddonArgs args)
    {
        InDuty = true;
        AutoCommence = null;

        if (newRunPending)
        {
            newRunPending = false;
            if (config.FreshTeamEachRun && config.LastTeam.Any(petId => petId != 0))
            {
                config.LastTeam = [0, 0, 0];
                config.Save();
            }
        }

        if (config.AutoAssign && config.OfferAutoCommence)
            AskAutoCommence?.Invoke();
    }

    private void OnLeft(AddonEvent type, AddonArgs args)
    {
        InDuty = false;
        AutoCommence = null;
        Left?.Invoke();
    }
}
