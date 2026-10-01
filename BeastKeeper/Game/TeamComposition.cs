using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastKeeper.Game;

/// <summary>One row of the Team Composition list.</summary>
/// <param name="Row">List row, which is also the index the game's own click callback uses.</param>
/// <param name="Slot">Battlehorn slot 0-2, or <see cref="TeamComposition.NoSlot"/> when unassigned.</param>
public sealed record Familiar(int Row, int PetId, string Name, uint Hp, uint MaxHp, int Slot)
{
    public bool IsAssigned => Slot is >= 0 and < TeamComposition.SlotCount;

    public bool IsIncapacitated => Hp == 0;
}

/// <summary>
/// Snapshot of the Team Composition window. Value layout (patch 7.56):
/// [2] mode (0 = pre-run roster, 2 = battle prep), [5] familiar count, then one 77-value block per familiar
/// from [6]: +1 icon (242000 + pet id), +3 name, +5 HP, +6 max HP, +74 battlehorn slot (3 = none), +76 pet id.
/// </summary>
public sealed class TeamComposition
{
    public const int SlotCount = 3;
    public const int NoSlot = 3;
    private const uint RosterMode = 0;
    private const uint BattlePrepMode = 2;
    private const int HeaderSize = 6;
    private const int BlockSize = 77;
    private const int MaxFamiliars = 15;
    private const int IconOffset = 1;
    private const int NameOffset = 3;
    private const int HpOffset = 5;
    private const int MaxHpOffset = 6;
    private const int SlotOffset = 74;
    private const int PetIdOffset = 76;
    private const uint FamiliarIconBase = 242000;

    private TeamComposition(uint mode, IReadOnlyList<Familiar> familiars)
    {
        Mode = mode;
        Familiars = familiars;
    }

    public uint Mode { get; }

    public IReadOnlyList<Familiar> Familiars { get; }

    /// <summary>The per-battle prompt ("Select familiars to call upon during combat.").</summary>
    public bool IsBattlePrep => Mode == BattlePrepMode;

    /// <summary>The pre-run screen where the roster for the run is chosen.</summary>
    public bool IsRoster => Mode == RosterMode;

    /// <summary>Pet id per battlehorn slot, 0 where unassigned.</summary>
    public int[] AssignedPets()
    {
        var pets = new int[SlotCount];
        foreach (var familiar in Familiars)
        {
            if (familiar.IsAssigned)
                pets[familiar.Slot] = familiar.PetId;
        }
        return pets;
    }

    /// <summary>
    /// True when the window is the pre-run roster screen (choosing familiars before "Challenge This Board"),
    /// i.e. a new run is being set up. Reads only the header, so it works before the list is filled.
    /// </summary>
    public static unsafe bool IsRosterScreen(AtkUnitBase* addon) =>
        addon != null && addon->AtkValues != null && addon->AtkValuesCount > 2 &&
        TryUInt(addon->AtkValues + 2, out var mode) && mode == RosterMode;

    /// <summary>
    /// Parses the window's values. Returns null if the layout isn't what this version expects (e.g. after a game
    /// update), so the plugin never clicks rows it can't identify: every familiar's icon must be 242000 + its id.
    /// </summary>
    public static unsafe TeamComposition? Read(AtkUnitBase* addon)
    {
        if (addon == null || addon->AtkValues == null)
            return null;

        var values = addon->AtkValues;
        var count = addon->AtkValuesCount;
        if (count < HeaderSize || !TryUInt(values + 2, out var mode) || !TryUInt(values + 5, out var familiarCount) ||
            familiarCount > MaxFamiliars || HeaderSize + BlockSize * familiarCount > count)
            return null;

        var familiars = new List<Familiar>((int)familiarCount);
        for (var row = 0; row < familiarCount; row++)
        {
            var block = values + HeaderSize + BlockSize * row;
            if (!TryUInt(block + PetIdOffset, out var petId) || !TryUInt(block + IconOffset, out var icon) ||
                icon != FamiliarIconBase + petId || !TryUInt(block + SlotOffset, out var slot) ||
                !TryUInt(block + HpOffset, out var hp) || !TryUInt(block + MaxHpOffset, out var maxHp))
                return null;

            // The string pointer lives at +8; read it without trusting it.
            var name = block[NameOffset].Type is AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString
                ? SafeText.Read(*(nint*)((byte*)(block + NameOffset) + 8))
                : null;
            familiars.Add(new Familiar(row, (int)petId, SafeText.Clean(name ?? $"Familiar #{petId}"), hp, maxHp, (int)slot));
        }

        return new TeamComposition(mode, familiars);
    }

    private static unsafe bool TryUInt(AtkValue* value, out uint result)
    {
        switch (value->Type)
        {
            case AtkValueType.UInt:
                result = value->UInt;
                return true;
            case AtkValueType.Int:
                result = (uint)value->Int;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
