using System;
using System.Collections.Generic;
using System.Linq;
using BeastKeeper.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastKeeper.Automation;

/// <summary>
/// When the Team Composition window opens before a battle, clicks familiars exactly like the player would
/// (callback [1, row]) and checks that each battlehorn took before moving on. Presses Commence Battle (Board Layout
/// callback [8]) only if the player opted in for this run and the team is their usual one. A window the player has
/// already started on is left alone.
/// </summary>
public sealed unsafe class AutoAssigner : IDisposable
{
    private const int SelectFamiliarCallback = 1;
    private const int CommenceBattleCallback = 8;
    private const string Idle = "Waiting for your next Crucible battle.";

    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(2);

    private enum Phase
    {
        Idle,
        Waiting,
        Clicking,
        Verifying,
        Commencing,
    }

    private readonly Configuration config;
    private readonly RunTracker run;
    private readonly Queue<Familiar> pending = new();
    private TeamPlan? plan;
    private Familiar? current;
    private Phase phase = Phase.Idle;
    private DateTime phaseStarted;
    private DateTime nextActionAt;

    public AutoAssigner(Configuration config, RunTracker run)
    {
        this.config = config;
        this.run = run;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, GameUi.TeamCompositionAddon, OnSetup);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreClose, GameUi.TeamCompositionAddon, OnClose);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, GameUi.TeamCompositionAddon, OnFinalize);
        Plugin.Framework.Update += OnUpdate;
    }

    public string Status { get; private set; } = Idle;

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.AddonLifecycle.UnregisterListener(OnSetup, OnClose, OnFinalize);
    }

    /// <summary>Sets the battlehorns on the open Team Composition window, even if automatic mode is off.</summary>
    public void AssignNow()
    {
        if (GameUi.GetAddon(GameUi.TeamCompositionAddon) == null)
        {
            Report("The Team Composition window isn't open. It appears before each Crucible battle.", chat: true);
            return;
        }
        Start();
    }

    private void OnSetup(AddonEvent type, AddonArgs args)
    {
        plan = null;
        if (config.AutoAssign)
            Start();
    }

    private void OnClose(AddonEvent type, AddonArgs args)
    {
        RememberTeam();
        Reset();
    }

    private void OnFinalize(AddonEvent type, AddonArgs args) => Reset();

    private void Start()
    {
        Reset();
        SetPhase(Phase.Waiting);
    }

    private void Reset()
    {
        phase = Phase.Idle;
        pending.Clear();
        current = null;
    }

    private void SetPhase(Phase next)
    {
        phase = next;
        phaseStarted = DateTime.UtcNow;
    }

    private void OnUpdate(IFramework framework)
    {
        if (phase == Phase.Idle)
            return;

        try
        {
            Step();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Setting battlehorns failed");
            Stop("Something went wrong while setting your battlehorns. Please set them manually.", chat: true);
        }
    }

    private void Step()
    {
        var now = DateTime.UtcNow;
        var addon = GameUi.GetAddon(GameUi.TeamCompositionAddon);
        if (!GameUi.IsReady(addon))
        {
            if (now - phaseStarted > ReadTimeout)
                Stop(Idle);
            return;
        }

        switch (phase)
        {
            case Phase.Waiting:
                if (now - phaseStarted >= SettleDelay)
                    Plan(addon, now);
                break;

            case Phase.Clicking:
                if (now < nextActionAt)
                    return;
                if (!pending.TryDequeue(out current))
                {
                    Finish(now);
                    return;
                }
                GameUi.FireCallback(addon, SelectFamiliarCallback, current.Row);
                SetPhase(Phase.Verifying);
                break;

            case Phase.Verifying when current is { } clicked:
                var row = TeamComposition.Read(addon)?.Familiars.ElementAtOrDefault(clicked.Row);
                if (row is { IsAssigned: true } && row.PetId == clicked.PetId)
                {
                    phase = Phase.Clicking;
                    nextActionAt = now + StepDelay;
                }
                else if (now - phaseStarted > VerifyTimeout)
                {
                    Stop($"The game didn't accept {clicked.Name}. Please set your battlehorns manually.", chat: true);
                }
                break;

            case Phase.Commencing:
                if (now >= nextActionAt)
                    Commence(addon);
                break;
        }
    }

    private void Plan(AtkUnitBase* addon, DateTime now)
    {
        var team = TeamComposition.Read(addon);
        var timedOut = now - phaseStarted > ReadTimeout;
        if (team == null)
        {
            if (timedOut)
                Stop("Couldn't read the Team Composition window, so your battlehorns weren't set. A game update may have changed it.", chat: true);
            return;
        }

        if (team.IsRoster)
        {
            // The pre-run screen chooses the roster for the run; there are no battlehorns to set there.
            Stop(Idle);
            return;
        }

        if (!team.IsBattlePrep || team.Familiars.Count == 0)
        {
            // The list can fill in a moment after the window opens.
            if (timedOut)
                Stop(Idle);
            return;
        }

        LearnNames(team);
        if (team.Familiars.Any(f => f.IsAssigned))
        {
            Stop("Your battlehorns were already set, so they were left as they are.");
            return;
        }

        plan = TeamPlanner.Plan(team.Familiars, config.Mode, config.LastTeam, config.ReplaceKnockedOut);
        if (plan.Picks.Count == 0)
        {
            Stop("All your familiars are knocked out, so no battlehorns were set.", chat: true);
            return;
        }

        foreach (var familiar in plan.Picks)
            pending.Enqueue(familiar);
        nextActionAt = now;
        SetPhase(Phase.Clicking);
    }

    private void Finish(DateTime now)
    {
        var assigned = plan!;
        var summary = $"Battlehorns set: {string.Join(", ", assigned.Picks.Select(f => f.Name))}.";
        if (run.AutoCommence != true)
        {
            Stop(summary, chat: true);
            return;
        }

        if (assigned.IsUsualTeam)
        {
            Report($"{summary} Commencing battle…", chat: true);
            nextActionAt = now + TimeSpan.FromMilliseconds(config.AutoCommenceDelayMs);
            SetPhase(Phase.Commencing);
            return;
        }

        // Auto-commence is on, but this isn't the team the player settled on: let them check it first.
        var reason = !assigned.IsFull ? "Fewer than three familiars are available"
            : assigned.Substituted ? "A knocked-out familiar was replaced"
            : "This is the first battle of the run";
        Stop($"{summary} {reason}, so press Commence Battle when you're ready.", chat: true);
    }

    private void Commence(AtkUnitBase* teamAddon)
    {
        // Only commence with the exact team that was set; if the player changed anything, leave it to them.
        var team = TeamComposition.Read(teamAddon);
        if (team == null || !team.AssignedPets().SequenceEqual(plan!.Picks.Select(f => f.PetId)))
        {
            Stop("You changed the team, so Commence Battle was left to you.");
            return;
        }

        var boardLayout = GameUi.GetAddon(GameUi.BoardLayoutAddon);
        if (!GameUi.IsReady(boardLayout))
        {
            Stop("Couldn't press Commence Battle. Please press it yourself.", chat: true);
            return;
        }

        GameUi.FireCallback(boardLayout, CommenceBattleCallback);
        Stop("Battle commenced.");
    }

    /// <summary>
    /// Remembers whatever is assigned when the window closes, including manual changes, except a replacement for a
    /// knocked-out familiar: that familiar should come back once it has recovered.
    /// </summary>
    private void RememberTeam()
    {
        var team = TeamComposition.Read(GameUi.GetAddon(GameUi.TeamCompositionAddon));
        if (team == null || !team.IsBattlePrep)
            return;

        LearnNames(team);
        var pets = team.AssignedPets();
        if (pets.All(petId => petId == 0) || pets.SequenceEqual(config.LastTeam))
            return;
        if (plan is { Substituted: true } && pets.Where(petId => petId != 0).SequenceEqual(plan.Picks.Select(f => f.PetId)))
            return;

        config.LastTeam = pets;
        config.Save();
    }

    private void LearnNames(TeamComposition team)
    {
        var changed = false;
        foreach (var familiar in team.Familiars)
        {
            if (config.FamiliarNames.TryGetValue(familiar.PetId, out var known) && known == familiar.Name)
                continue;
            config.FamiliarNames[familiar.PetId] = familiar.Name;
            changed = true;
        }
        if (changed)
            config.Save();
    }

    private void Stop(string message, bool chat = false)
    {
        Reset();
        Report(message, chat);
    }

    private void Report(string message, bool chat)
    {
        Status = message;
        Plugin.Log.Debug(message);
        if (chat && config.ChatMessages)
            Plugin.ChatGui.Print(message, "Beast Keeper");
    }
}
