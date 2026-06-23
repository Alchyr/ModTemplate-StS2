using Godot;
using ModTemplate.ModTemplateCode.Patches;

namespace ModTemplate.ModTemplateCode.Nodes;

// Persistent node added to the scene root. Polls F5 (save) and F9 (load latest).
// Requires a brief hold to avoid accidental triggers.
public partial class SnapshotInputNode : Node
{
    private const double HoldSeconds = 0.4;

    private double _saveHeld;
    private double _loadHeld;

    public override void _Process(double delta)
    {
        Track(ref _saveHeld, delta, Key.F5, SnapshotPatch.SaveCurrent);
        Track(ref _loadHeld, delta, Key.F9, SnapshotPatch.RestoreLatest);
    }

    private static void Track(ref double held, double delta, Key key, Action action)
    {
        if (Input.IsKeyPressed(key))
        {
            if (held >= 0)
            {
                held += delta;
                if (held >= HoldSeconds)
                {
                    held = double.MinValue; // suppress until key is released
                    action();
                }
            }
        }
        else
        {
            held = 0;
        }
    }
}
