using Godot;
using ModTemplate.ModTemplateCode.Checkpoints;

namespace ModTemplate.ModTemplateCode.Nodes;

// Static UI manager — deliberately NOT a Node subclass.
// Custom partial Node classes crash in mod context because Harmony's MonoMod
// JIT hook fires when Godot tries to JIT-compile InvokeGodotClassMethod and
// throws ArgumentException. Using a static class with built-in Godot nodes
// (CanvasLayer, Label, etc.) avoids this entirely.
// Input polling and HUD updates are driven by a Harmony patch on NRun._Process.
internal static class CheckpointUi
{
    private static Label?         _hudLabel;
    private static Control?       _panel;
    private static VBoxContainer? _list;

    private static double _lHeld;
    private const  double ToggleHold = 0.3;

    // Called from NRunReadyPatch — builds the UI once per run scene.
    public static void Initialize(Node sceneRoot)
    {
        if (sceneRoot.HasNode("CheckpointUiLayer")) return;

        var layer = new CanvasLayer { Layer = 128, Name = "CheckpointUiLayer" };
        sceneRoot.AddChild(layer);
        BuildLayout(layer);
        MainFile.Logger.Info("[Checkpoint] CheckpointUi initialized.");
    }

    // Called from RunEndPatch — clears stale references when the run ends.
    public static void Teardown()
    {
        _hudLabel = null;
        _panel    = null;
        _list     = null;
        _lHeld    = 0;
    }

    // Called every frame from NRunProcessPatch.
    public static void Update(double delta)
    {
        if (_hudLabel == null) return;

        _hudLabel.Text = CheckpointManager.CheckpointCount > 0
            ? $"[Checkpoints: {CheckpointManager.CheckpointCount}]  hold L"
            : "[Checkpoint]  hold L";

        if (Input.IsKeyPressed(Key.L))
        {
            if (_lHeld >= 0)
            {
                _lHeld += delta;
                if (_lHeld >= ToggleHold)
                {
                    _lHeld = double.MinValue;
                    TogglePanel();
                }
            }
        }
        else
        {
            _lHeld = 0;
        }

        if (_panel?.Visible == true && Input.IsKeyPressed(Key.Escape))
            _panel.Visible = false;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private static void BuildLayout(CanvasLayer layer)
    {
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(root);

        _hudLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        _hudLabel.AnchorLeft   = 1f;
        _hudLabel.AnchorRight  = 1f;
        _hudLabel.AnchorTop    = 0f;
        _hudLabel.AnchorBottom = 0f;
        _hudLabel.OffsetLeft   = -260f;
        _hudLabel.OffsetRight  = -8f;
        _hudLabel.OffsetTop    = 8f;
        _hudLabel.OffsetBottom = 32f;
        root.AddChild(_hudLabel);

        _panel = new Control();
        _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        _panel.Visible     = false;
        root.AddChild(_panel);

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.7f) };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) _panel.Visible = false;
        };
        _panel.AddChild(overlay);

        var bg = new Panel();
        bg.AnchorLeft   = 0.10f;
        bg.AnchorTop    = 0.05f;
        bg.AnchorRight  = 0.90f;
        bg.AnchorBottom = 0.95f;
        _panel.AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        bg.AddChild(margin);

        var outer = new VBoxContainer();
        margin.AddChild(outer);

        var header = new HBoxContainer();
        outer.AddChild(header);
        header.AddChild(new Label
        {
            Text                = "Checkpoint History  (hold L or Esc to close)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
        var closeBtn = new Button { Text = "X" };
        closeBtn.Pressed += () => _panel.Visible = false;
        header.AddChild(closeBtn);

        outer.AddChild(new HSeparator());

        var cols = new HBoxContainer();
        outer.AddChild(cols);
        cols.AddChild(ColLabel("Floor", 80));
        cols.AddChild(ColLabel("Saved at", 180));
        cols.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        outer.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddChild(scroll);

        _list = new VBoxContainer();
        _list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_list);
    }

    private static Label ColLabel(string text, int minWidth) => new()
    {
        Text              = text,
        CustomMinimumSize = new Vector2(minWidth, 0),
    };

    // ── Panel ─────────────────────────────────────────────────────────────────

    private static void TogglePanel()
    {
        if (_panel == null) return;
        if (_panel.Visible) { _panel.Visible = false; return; }
        Refresh();
        _panel.Visible = true;
    }

    private static void Refresh()
    {
        if (_list == null) return;
        foreach (var child in _list.GetChildren()) child.QueueFree();

        var checkpoints = CheckpointManager.LoadAll();

        if (checkpoints.Count == 0)
        {
            _list.AddChild(new Label { Text = "No checkpoints yet." });
            return;
        }

        foreach (var cp in checkpoints)
        {
            var row = new HBoxContainer();
            _list.AddChild(row);
            row.AddChild(new Label { Text = $"Floor {cp.Floor,2}",                         CustomMinimumSize = new Vector2(80,  0) });
            row.AddChild(new Label { Text = cp.SavedAt.ToLocalTime().ToString("HH:mm:ss"), CustomMinimumSize = new Vector2(180, 0) });
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var btn = new Button { Text = "Load Checkpoint" };
            var captured = cp;
            btn.Pressed += () =>
            {
                CheckpointManager.LoadCheckpoint(captured);
                if (_panel != null) _panel.Visible = false;
            };
            row.AddChild(btn);
        }
    }
}
