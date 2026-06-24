using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace ModTemplate.ModTemplateCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Checkpoint";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);
        try { harmony.PatchAll(); }
        catch (Exception ex) { Logger.Info($"[Checkpoint] PatchAll error: {ex.Message}"); }
    }
}
