using System.Collections.Generic;
using System.Linq;
using BeastKeeper.Game;

namespace BeastKeeper.Automation;

/// <summary>Familiars to click, in battlehorn order (the game fills battlehorns in the order familiars are clicked).</summary>
/// <param name="Substituted">A familiar the player would normally use was knocked out or missing and got replaced or skipped.</param>
/// <param name="IsUsualTeam">
/// Exactly the player's established team: the remembered three, or the first three in the roster in that mode.
/// Only then is pressing Commence Battle automatically appropriate.
/// </param>
public sealed record TeamPlan(List<Familiar> Picks, bool Substituted, bool IsUsualTeam)
{
    public bool IsFull => Picks.Count == TeamComposition.SlotCount;
}

public static class TeamPlanner
{
    /// <param name="lastTeam">Familiar id per battlehorn remembered from the last battle, 0 = empty.</param>
    public static TeamPlan Plan(IReadOnlyList<Familiar> roster, AssignMode mode, int[] lastTeam, bool replaceKnockedOut)
    {
        var available = roster.Where(f => !f.IsIncapacitated).ToList();
        if (mode == AssignMode.FirstInRoster)
        {
            var picks = available.Take(TeamComposition.SlotCount).ToList();
            var knockedOut = roster.Take(TeamComposition.SlotCount).Any(f => f.IsIncapacitated);
            return new TeamPlan(picks, knockedOut, !knockedOut && picks.Count == TeamComposition.SlotCount);
        }

        // Nothing remembered yet: start from the top of the roster; the player confirms this first team.
        if (lastTeam.All(petId => petId == 0))
            return new TeamPlan(available.Take(TeamComposition.SlotCount).ToList(), false, false);

        var result = new List<Familiar>();
        var substituted = false;
        for (var slot = 0; slot < TeamComposition.SlotCount; slot++)
        {
            var petId = slot < lastTeam.Length ? lastTeam[slot] : 0;
            var remembered = petId == 0 ? null : available.FirstOrDefault(f => f.PetId == petId && !result.Contains(f));
            if (remembered != null)
            {
                result.Add(remembered);
                continue;
            }

            if (petId != 0)
                substituted = true;
            if (!replaceKnockedOut)
                continue;

            // Prefer a familiar that isn't remembered for a later battlehorn, so that one keeps its own slot.
            var laterPets = lastTeam.Skip(slot + 1).Where(id => id != 0).ToHashSet();
            var replacement = available.FirstOrDefault(f => !result.Contains(f) && !laterPets.Contains(f.PetId))
                              ?? available.FirstOrDefault(f => !result.Contains(f));
            if (replacement != null)
                result.Add(replacement);
        }

        var isUsual = !substituted && result.Count == TeamComposition.SlotCount &&
                      lastTeam.Take(TeamComposition.SlotCount).All(petId => petId != 0);
        return new TeamPlan(result, substituted, isUsual);
    }
}
