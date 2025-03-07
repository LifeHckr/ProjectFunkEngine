using System;
using System.Linq;
using FunkEngine;
using Godot;

public partial class Cartographer : Node2D
{
    [Export]
    public Sprite2D PlayerSprite;

    private Button[] _validButtons = Array.Empty<Button>();

    [Export]
    private Button _focusedButton = null;

    private BgAudioPlayer _bgPlayer;

    public override void _Ready()
    {
        DrawMap();
        GetViewport().GuiFocusChanged += UpdateFocus;
        if (StageProducer.CurRoom.Type == Stages.Boss && StageProducer.CurRoom.Children.Length == 0)
        {
            WinStage();
        }
    }

    public override void _EnterTree()
    {
        _bgPlayer = GetNode<BgAudioPlayer>("/root/BgAudioPlayer");
        _bgPlayer.PlayLevelMusic();
    }

    public override void _Process(double delta)
    {
        if (!GetTree().Paused && !_validButtons.Contains(GetViewport().GuiGetFocusOwner()))
        {
            _focusedButton?.GrabFocus();
        }
    }

    private void UpdateFocus(Control focusOwner)
    {
        if (_validButtons.Contains(focusOwner))
            _focusedButton = focusOwner as Button;
    }

    private Vector2 GetPosition(int x, int y)
    {
        return new Vector2((float)x * 640 / StageProducer.MapSize.X - 1 + 64, y * 48 + 16);
    }

    private void DrawMap()
    {
        var rooms = StageProducer.Map.GetRooms();
        foreach (MapGrid.Room room in rooms)
        {
            DrawMapSprite(room);
            foreach (int roomIdx in room.Children)
            {
                Line2D newLine = new Line2D();
                newLine.AddPoint(GetPosition(room.X, room.Y));
                newLine.AddPoint(GetPosition(rooms[roomIdx].X, rooms[roomIdx].Y));
                AddChild(newLine);
            }
        }

        _validButtons = _validButtons.OrderBy(x => x.Position.X).ToArray();
        AddFocusNeighbors();
    }

    private void DrawMapSprite(MapGrid.Room room)
    {
        var newButton = new Button();
        AddChild(newButton);
        //button is disabled if it is not a child of current room.
        if (!StageProducer.CurRoom.Children.Contains(room.Idx))
        {
            newButton.Disabled = true;
            newButton.FocusMode = Control.FocusModeEnum.None;
        }
        else
        {
            newButton.GrabFocus();
            _focusedButton = newButton;
            newButton.Pressed += () =>
            {
                EnterStage(room.Idx, newButton);
            };
            _validButtons = _validButtons.Append(newButton).ToArray();
        }

        switch (room.Type)
        {
            case Stages.Battle:
                newButton.Icon = (Texture2D)GD.Load("res://scenes/Maps/assets/BattleIcon.png");
                break;
            case Stages.Boss:
                newButton.Icon = (Texture2D)GD.Load("res://scenes/Maps/assets/BossIcon.png");
                break;
            case Stages.Chest:
                newButton.Icon = (Texture2D)GD.Load("res://scenes/Maps/assets/ChestIcon.png");
                break;
        }
        newButton.ZIndex = 1;
        newButton.Position = GetPosition(room.X, room.Y) - newButton.Size * 2;
        if (room == StageProducer.CurRoom)
            PlayerSprite.Position = newButton.Position + newButton.Size * .5f;
    }

    private void AddFocusNeighbors()
    {
        for (int i = 0; i < _validButtons.Length; i++)
        {
            _validButtons[i].FocusNeighborRight = _validButtons[(i + 1) % (_validButtons.Length)]
                .GetPath();
            _validButtons[(i + 1) % (_validButtons.Length)].FocusNeighborLeft = _validButtons[i]
                .GetPath();
        }
    }

    private void EnterStage(int roomIdx, Button button)
    {
        foreach (Button btn in _validButtons)
        {
            btn.Disabled = true;
            if (btn == button)
                continue;
            btn.FocusMode = Control.FocusModeEnum.None;
        }

        var tween = CreateTween()
            .TweenProperty(PlayerSprite, "position", button.Position + button.Size * .5f, 1f);
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.InOut);
        tween.Finished += () =>
        {
            _bgPlayer.StopMusic();
            GetNode<StageProducer>("/root/StageProducer").TransitionFromRoom(roomIdx);
        };
    }

    private void WinStage()
    {
        GD.Print("Player is Dead");
        EndScreen es = GD.Load<PackedScene>("res://scenes/UI/EndScreen.tscn")
            .Instantiate<EndScreen>();
        AddChild(es);
        es.TopLabel.Text = "You Win!";
        GetTree().Paused = true;
    }
}
