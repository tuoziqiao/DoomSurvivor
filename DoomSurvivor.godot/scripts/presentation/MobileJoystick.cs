using System;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class MobileJoystick : Control
{
    private const float DeadZone = 0.12f;
    private int activeTouch = -1;
    private Vector2 value;

    public Vector2 Value => value;

    public MobileJoystick()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(220, 220);
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                activeTouch = touch.Index;
                UpdateValue(touch.Position);
                AcceptEvent();
                break;
            case InputEventScreenTouch touch when !touch.Pressed && (activeTouch < 0 || touch.Index == activeTouch):
                activeTouch = -1;
                value = Vector2.Zero;
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventScreenDrag drag when activeTouch < 0 || drag.Index == activeTouch:
                UpdateValue(drag.Position);
                AcceptEvent();
                break;
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed) UpdateValue(mouseButton.Position);
                else value = Vector2.Zero;
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseMotion mouseMotion when Input.IsMouseButtonPressed(MouseButton.Left):
                UpdateValue(mouseMotion.Position);
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var radius = Math.Min(Size.X, Size.Y) * 0.43f;
        DrawCircle(center, radius, new Color(0.05f, 0.1f, 0.12f, 0.68f));
        DrawArc(center, radius, 0f, Mathf.Tau, 64, new Color("#8BB5A3"), 3f, true);
        DrawCircle(center + value * radius * 0.68f, radius * 0.36f, new Color(0.78f, 0.9f, 0.78f, 0.78f));
    }

    private void UpdateValue(Vector2 localPosition)
    {
        var center = Size * 0.5f;
        var radius = Math.Min(Size.X, Size.Y) * 0.43f;
        var offset = localPosition - center;
        if (offset.Length() > radius) offset = offset.Normalized() * radius;
        value = offset / radius;
        if (value.Length() < DeadZone) value = Vector2.Zero;
        QueueRedraw();
    }
}
