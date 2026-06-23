using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Players;
using ModTemplate.ModTemplateCode.Snapshots;

namespace ModTemplate.ModTemplateCode.Patches;

internal static class SnapshotPatch
{
    // Cached at NRun._Ready so we can read state on F5 without scene traversal.
    private static RunState? _runState;

    // ── Cache RunState when a run scene loads ──────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Ready")]
    static class NRunReadyPatch
    {
        [HarmonyPostfix]
        static void Postfix(NRun __instance)
        {
            try
            {
                _runState = null;
                // RunState is stored in a private field on NRun.
                // We iterate all fields to find whichever one holds a RunState —
                // this is resilient to field renames across game updates.
                var t = Traverse.Create(__instance);
                foreach (var fieldName in t.Fields())
                {
                    if (t.Field(fieldName).GetValue() is RunState rs)
                    {
                        _runState = rs;
                        MainFile.Logger.Info("[Snapshot] RunState cached from NRun.");
                        break;
                    }
                }
                if (_runState is null)
                    MainFile.Logger.Info("[Snapshot] Warning: RunState not found in NRun fields.");
            }
            catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] NRunReady error: {ex.Message}"); }
        }
    }

    // ── Public entry points called from SnapshotInputNode ─────────────────────

    public static void SaveCurrent()
    {
        var snapshot = Capture();
        if (snapshot is null)
        {
            MainFile.Logger.Info("[Snapshot] Cannot save: run is not active or RunState is not cached.");
            return;
        }
        SnapshotManager.Save(snapshot);
    }

    public static void RestoreLatest()
    {
        var snapshot = SnapshotManager.LoadLatest();
        if (snapshot is null)
        {
            MainFile.Logger.Info("[Snapshot] No snapshot to restore.");
            return;
        }
        Restore(snapshot);
    }

    // ── Capture ────────────────────────────────────────────────────────────────

    private static RunSnapshot? Capture()
    {
        if (_runState is null) return null;
        var player = GetSoloPlayer();
        if (player is null) return null;

        var pt = Traverse.Create(player);
        var snapshot = new RunSnapshot
        {
            Floor     = _runState.TotalFloor,
            // TODO: update private field names below after decompiling sts2.dll.
            // Common patterns tried first; adjust if Traverse returns defaults (0).
            CurrentHp = pt.Field("_currentHp").GetValue<decimal>(),
            MaxHp     = pt.Field("_maxHp").GetValue<decimal>(),
            Gold      = pt.Field("_gold").GetValue<decimal>(),
        };

        // Deck — cards are stored in a private CardPile field on the player.
        // TODO: update "_masterDeck" and "_group" to match actual field names.
        var deckItems = pt.Field("_masterDeck").Field("_group").GetValue<System.Collections.IList>()
                     ?? pt.Field("masterDeck").Field("group").GetValue<System.Collections.IList>();
        if (deckItems is not null)
        {
            foreach (var card in deckItems)
            {
                var ct = Traverse.Create(card);
                snapshot.Deck.Add(new CardData
                {
                    ModelId      = ct.Property("ModelId").GetValue<string>() ?? "",
                    UpgradeCount = ct.Field("_timesUpgraded").GetValue<int>(),
                });
            }
        }

        // Relics — stored in a private list on the player.
        // TODO: update "_relics" to match the actual field name.
        var relicList = pt.Field("_relics").GetValue<System.Collections.IList>()
                     ?? pt.Field("relics").GetValue<System.Collections.IList>();
        if (relicList is not null)
        {
            foreach (var relic in relicList)
            {
                var id = Traverse.Create(relic).Property("ModelId").GetValue<string>() ?? "";
                if (id.Length > 0) snapshot.RelicIds.Add(id);
            }
        }

        return snapshot;
    }

    // ── Restore ────────────────────────────────────────────────────────────────

    private static void Restore(RunSnapshot snapshot)
    {
        if (_runState is null)
        {
            MainFile.Logger.Info("[Snapshot] Cannot restore: run is not active.");
            return;
        }
        var player = GetSoloPlayer();
        if (player is null) return;

        // Player does not publicly expose Creature inheritance without publicization,
        // so we invoke the game's command methods via reflection.
        AccessTools.Method("MegaCrit.Sts2.Core.Commands.CreatureCmd:SetMaxAndCurrentHp")
            ?.Invoke(null, [player, snapshot.CurrentHp]);
        AccessTools.Method("MegaCrit.Sts2.Core.Commands.PlayerCmd:SetGold")
            ?.Invoke(null, [snapshot.Gold, player]);

        // TODO: restore deck and relics.
        // Cards: iterate deck, call RunState.RemoveCard() on each, then RunState.AddCard()
        //        using ModelDb to look up CardModel by snapshot.Deck[i].ModelId.
        // Relics: similar pattern via RunState or PlayerCmd.

        MainFile.Logger.Info(
            $"[Snapshot] Restored HP {snapshot.CurrentHp}/{snapshot.MaxHp}, " +
            $"Gold {snapshot.Gold} (Floor {snapshot.Floor}). " +
            "Deck/relic restore pending — see SnapshotPatch.Restore().");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Player? GetSoloPlayer()
        => _runState?.Players.FirstOrDefault();
}
