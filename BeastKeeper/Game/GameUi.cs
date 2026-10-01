using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastKeeper.Game;

/// <summary>The Crucible of the Unbroken windows (the game's internal "XBM" addons) and helpers to drive them.</summary>
public static unsafe class GameUi
{
    /// <summary>Team Composition: the familiar list shown before a run and before each battle.</summary>
    public const string TeamCompositionAddon = "XBMPetParty";

    /// <summary>Board Layout, which holds the Commence Battle button.</summary>
    public const string BoardLayoutAddon = "XBMStageDetailList";

    /// <summary>The Crucible HUD: set up once when entering a run and torn down when leaving it.</summary>
    public const string CrucibleHudAddon = "XBMContentsMainHUD";

    public static AtkUnitBase* GetAddon(string name) => (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name).Address;

    public static bool IsReady(AtkUnitBase* addon) => addon != null && addon->IsVisible && addon->IsReady;

    /// <summary>Sends the same callback the window sends when the player clicks it.</summary>
    public static void FireCallback(AtkUnitBase* addon, params int[] values)
    {
        using var args = new AtkValueArray(values);
        addon->FireCallback((uint)args.Length, args.Pointer, true);
    }
}
