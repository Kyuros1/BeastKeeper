using BeastKeeper.Automation;
using BeastKeeper.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BeastKeeper;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/bk";

    private static readonly string[] CommandHelp =
    [
        "/bk — open settings",
        "/bk on | off — set battlehorns automatically",
        "/bk mode last | first — remember my last team / first three in roster",
        "/bk assign — set battlehorns now",
        "/bk forget — forget the remembered team",
        "/bk commence on | off — start battles automatically this run",
    ];

    private readonly WindowSystem windowSystem = new("BeastKeeper");
    private readonly SettingsWindow settingsWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Run = new RunTracker(Configuration);
        AutoAssigner = new AutoAssigner(Configuration, Run);

        settingsWindow = new SettingsWindow(this);
        windowSystem.AddWindow(settingsWindow);
        windowSystem.AddWindow(new RunPromptWindow(Configuration, Run));

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Beast Keeper settings. \"/bk help\" lists all commands.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += settingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += settingsWindow.Toggle;
    }

    public Configuration Configuration { get; }

    public RunTracker Run { get; }

    public AutoAssigner AutoAssigner { get; }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= settingsWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= settingsWindow.Toggle;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
        AutoAssigner.Dispose();
        Run.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.ToLowerInvariant().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        switch (parts)
        {
            case []:
                settingsWindow.Toggle();
                break;
            case ["on" or "off"]:
                Configuration.AutoAssign = parts[0] == "on";
                Configuration.Save();
                Print(Configuration.AutoAssign ? "Battlehorns will be set automatically." : "Automatic battlehorns turned off.");
                break;
            case ["mode", "last" or "first"]:
                Configuration.Mode = parts[1] == "last" ? AssignMode.RememberLast : AssignMode.FirstInRoster;
                Configuration.Save();
                Print(Configuration.Mode == AssignMode.RememberLast
                    ? "Your last team will be remembered."
                    : "The first three familiars in your roster will be used.");
                break;
            case ["assign"]:
                AutoAssigner.AssignNow();
                break;
            case ["forget"]:
                Configuration.LastTeam = [0, 0, 0];
                Configuration.Save();
                Print("Forgot the remembered team.");
                break;
            case ["commence", "on" or "off"]:
                Run.AutoCommence = parts[1] == "on";
                Print(Run.AutoCommence == true
                    ? "Battles will start automatically for the rest of this run."
                    : "You'll start battles yourself for the rest of this run.");
                break;
            default:
                foreach (var line in CommandHelp)
                    Print(line);
                break;
        }
    }

    private static void Print(string message) => ChatGui.Print(message, "Beast Keeper");
}
