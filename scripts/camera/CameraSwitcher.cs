using Godot;

namespace FloatingObjects.Camera;

/// <summary>
/// Camera switcher. Cycles through an array of Camera3D nodes.
/// Press 1-9 to switch directly, Tab to cycle.
/// </summary>
public partial class CameraSwitcher : Node
{
    [Export]
    public Godot.Collections.Array<Camera3D> Cameras { get; set; } = new();

    [Export]
    public bool EnableTabCycle { get; set; } = true;

    private int _currentIndex = 0;

    public override void _Ready()
    {
        if (Cameras.Count > 0)
            SwitchTo(0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key9)
            {
                int idx = (int)(keyEvent.Keycode - Key.Key1);
                if (idx < Cameras.Count)
                    SwitchTo(idx);
            }
            else if (EnableTabCycle && keyEvent.Keycode == Key.Tab)
            {
                SwitchTo((_currentIndex + 1) % Cameras.Count);
            }
        }
    }

    private void SwitchTo(int index)
    {
        if (index < 0 || index >= Cameras.Count) return;
        var cam = Cameras[index];
        if (cam == null) return;
        cam.MakeCurrent();
        _currentIndex = index;
        GD.Print($"切换到相机 {index + 1}: {cam.Name}");
    }
}
