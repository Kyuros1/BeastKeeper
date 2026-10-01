using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace BeastKeeper.Windows;

public sealed class SettingsWindow : Window
{
    private static readonly string[] ModeLabels = ["Remember my last team", "First three in roster"];

    private readonly Plugin plugin;

    public SettingsWindow(Plugin plugin) : base("Beast Keeper###BeastKeeperSettings", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        var autoAssign = config.AutoAssign;
        if (ImGui.Checkbox("Set battlehorns automatically", ref autoAssign))
        {
            config.AutoAssign = autoAssign;
            config.Save();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("When the Team Composition window opens before a Crucible battle, your familiars are assigned to your battlehorns for you.");

        using (ImRaii.Disabled(!config.AutoAssign))
        {
            var mode = (int)config.Mode;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("Team", ref mode, ModeLabels, ModeLabels.Length))
            {
                config.Mode = (AssignMode)mode;
                config.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Remember my last team: the familiars on your battlehorns in the previous battle are set again, in the same order. Change them by hand and the new team is remembered.\n\nFirst three in roster: always the first three familiars in your Team Composition list.");

            if (config.Mode == AssignMode.RememberLast)
            {
                var fresh = config.FreshTeamEachRun;
                if (ImGui.Checkbox("Start each run with a fresh team", ref fresh))
                {
                    config.FreshTeamEachRun = fresh;
                    config.Save();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Forgets the remembered team when you start a new run. The first battle then uses the first three familiars in your roster, and the team you commence with is remembered for the rest of the run.");

                var replace = config.ReplaceKnockedOut;
                if (ImGui.Checkbox("Replace knocked-out familiars", ref replace))
                {
                    config.ReplaceKnockedOut = replace;
                    config.Save();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("If a remembered familiar is knocked out, the next available familiar in your roster takes its battlehorn for that battle. The remembered familiar returns once it has recovered.");
            }

            var offer = config.OfferAutoCommence;
            if (ImGui.Checkbox("Offer to press Commence Battle", ref offer))
            {
                config.OfferAutoCommence = offer;
                config.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("When you enter a run, you'll be asked whether battles should start automatically after your battlehorns are set. It never does on the first battle of a run, when a knocked-out familiar was replaced, or with fewer than three familiars.");

            if (config.OfferAutoCommence)
            {
                using var indent = ImRaii.PushIndent();
                var delay = config.AutoCommenceDelayMs / 1000f;
                ImGui.SetNextItemWidth(160);
                if (ImGui.SliderFloat("Delay before commencing", ref delay, 0f, 5f, "%.1f s"))
                {
                    config.AutoCommenceDelayMs = (int)(delay * 1000);
                    config.Save();
                }

                if (plugin.Run.InDuty)
                {
                    var thisRun = plugin.Run.AutoCommence == true;
                    if (ImGui.Checkbox("Commence automatically this run", ref thisRun))
                        plugin.Run.AutoCommence = thisRun;
                }
            }
        }

        var chat = config.ChatMessages;
        if (ImGui.Checkbox("Show messages in chat", ref chat))
        {
            config.ChatMessages = chat;
            config.Save();
        }

        if (config.Mode == AssignMode.RememberLast)
        {
            ImGui.Separator();
            var team = config.LastTeam;
            if (team.All(petId => petId == 0))
            {
                ImGui.TextUnformatted("Remembered team: none yet");
            }
            else
            {
                var names = team.Select((petId, i) => $"{i + 1}. {(petId == 0 ? "(empty)" : config.FamiliarName(petId))}");
                ImGui.TextUnformatted($"Remembered team: {string.Join("   ", names)}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Forget"))
                {
                    config.LastTeam = [0, 0, 0];
                    config.Save();
                }
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled(plugin.AutoAssigner.Status);
    }
}
