using FunkEngine;
using Godot;

/**
 * @class NoteArrow
 * @brief This class represents a visual note that scrolls across the screen to be played by the player. WIP
 */
public partial class NoteArrow : Sprite2D
{ //TextRect caused issues later :)
    public ArrowType Type;
    public int Beat;
    public float Bounds;
    public bool IsActive = true;
    public Note NoteRef;

    [Export]
    public Sprite2D OutlineSprite;

    [Export]
    public Sprite2D IconSprite;

    public void Init(ArrowData parentArrowData, int beat, Note note)
    {
        ZIndex = 1;

        Type = parentArrowData.Type;
        Beat = beat;

        Position += Vector2.Down * (parentArrowData.Node.GlobalPosition.Y);
        RotationDegrees = parentArrowData.Node.RotationDegrees;
        IconSprite.Texture = note.Texture;
        IconSprite.Rotation = -Rotation;
    }

    public override void _Process(double delta)
    {
        Vector2 newPos = Position;
        newPos.X =
            (float)(
                (-TimeKeeper.CurrentTime / TimeKeeper.LoopLength * TimeKeeper.ChartLength)
                % TimeKeeper.ChartLength
                / 2
            ) + Bounds;
        if (newPos.X > Position.X)
        {
            OnLoop();
        }
        Position = newPos;
    }

    private void OnLoop()
    {
        if (!IsActive)
        {
            Modulate /= .7f;
        }
        IsActive = true;
    }

    public void NoteHit()
    {
        Modulate *= .7f;
        IsActive = false;
    }
}
