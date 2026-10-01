using System.Numerics;
using BeastKeeper.Automation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BeastKeeper.Windows;

/// <summary>Asks, when entering a Crucible run, whether battles should start automatically for this run.</summary>
public sealed class RunPromptWindow : Window
{
    private readonly Configuration config;
    private readonly RunTracker run;

    public RunPromptWindow(Configuration config, RunTracker run)
        : base("Beast Keeper###BeastKeeperRunPrompt",
               ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.config = config;
        this.run = run;
        PositionCondition = ImGuiCond.Appearing;
        RespectCloseHotkey = false;
        run.AskAutoCommence += Ask;
        run.Left += () => IsOpen = false;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        Position = new Vector2(viewport.Pos.X + viewport.Size.X / 2 - 190, viewport.Pos.Y + viewport.Size.Y * 0.18f);
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Start battles automatically this run?");
        ImGui.Spacing();
        ImGui.TextDisabled(config.Mode == AssignMode.RememberLast
            ? "From your second battle on, your team is set and the battle starts on its own."
            : "Your first three familiars are set and each battle starts on its own.");
        ImGui.TextDisabled("If a familiar is knocked out, you'll start that battle yourself.");
        ImGui.Spacing();

        if (ImGui.Button("Yes", new Vector2(130, 0)))
            Answer(true);
        ImGui.SameLine();
        if (ImGui.Button("No", new Vector2(130, 0)))
            Answer(false);
    }

    /// <summary>Closing the prompt without answering means no.</summary>
    public override void OnClose() => run.AutoCommence ??= false;

    private void Ask()
    {
        run.AutoCommence = null;
        IsOpen = true;
    }

    private void Answer(bool autoCommence)
    {
        run.AutoCommence = autoCommence;
        IsOpen = false;
    }
}
