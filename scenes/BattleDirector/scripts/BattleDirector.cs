using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FunkEngine;
using Godot;

/**
 * @class BattleDirector
 * @brief Higher priority director to manage battle effects. Can directly access managers, which should signal up to Director WIP
 */
public partial class BattleDirector : Node2D
{ //TODO: Maybe move some Director functionality to a sub node.
    #region Declarations

    public PlayerPuppet Player;
    public EnemyPuppet Enemy;

    [Export]
    private ChartManager CM;

    [Export]
    public NotePlacementBar NotePlacementBar;

    [Export]
    private Conductor CD;

    [Export]
    private AudioStreamPlayer Audio;

    [Export]
    private Button _focusedButton; //Initially start button

    private double _timingInterval = .1; //secs, maybe make somewhat note dependent
    private double _lastBeat;

    private SongData _curSong;

    #endregion

    #region Note Handling
    private bool PlayerAddNote(ArrowType type, int beat)
    {
        if (!NotePlacementBar.CanPlaceNote())
            return false;
        if (!CD.AddNoteToLane(type, beat % CM.BeatsPerLoop, NotePlacementBar.PlacedNote(), false))
            return false;
        NotePlaced?.Invoke(this);
        return true;
    }

    public PuppetTemplate GetTarget(Note note)
    {
        if (note.Owner == Player)
        {
            return Enemy;
        }

        return Player;
    }
    #endregion

    #region Initialization
    public override void _Ready()
    {
        //TODO: Should come from transition into battle
        _curSong = StageProducer.Config.CurSong.SongData;
        Audio.SetStream(GD.Load<AudioStream>(StageProducer.Config.CurSong.AudioLocation));
        if (_curSong.SongLength <= 0)
        {
            _curSong.SongLength = Audio.Stream.GetLength();
        }
        TimeKeeper.Bpm = _curSong.Bpm;

        Player = GD.Load<PackedScene>("res://scenes/Puppets/PlayerPuppet.tscn")
            .Instantiate<PlayerPuppet>();
        AddChild(Player);
        Player.Defeated += CheckBattleStatus;
        EventizeRelics();
        NotePlacementBar.Setup(StageProducer.PlayerStats);

        //TODO: Refine
        Enemy = GD.Load<PackedScene>(StageProducer.Config.EnemyScenePath)
            .Instantiate<EnemyPuppet>();
        AddChild(Enemy);
        Enemy.Defeated += CheckBattleStatus;
        AddEnemyEffects();

        CM.PrepChart(_curSong);
        CD.Prep();
        CD.TimedInput += OnTimedInput;

        CM.Connect(nameof(InputHandler.NotePressed), new Callable(this, nameof(OnNotePressed)));
        CM.Connect(nameof(InputHandler.NoteReleased), new Callable(this, nameof(OnNoteReleased)));

        _focusedButton.GrabFocus();
        _focusedButton.Pressed += () =>
        {
            var timer = GetTree().CreateTimer(AudioServer.GetTimeToNextMix());
            timer.Timeout += Begin;
            _focusedButton.QueueFree();
            _focusedButton = null;
        };
    }

    //TODO: This will all change
    private void Begin()
    {
        CM.BeginTweens();
        Audio.Play();
    }

    private void EndBattle()
    {
        StageProducer.ChangeCurRoom(StageProducer.Config.BattleRoom);
        GetNode<StageProducer>("/root/StageProducer").TransitionStage(Stages.Map);
    }

    public override void _Process(double delta)
    {
        _focusedButton?.GrabFocus();
        TimeKeeper.CurrentTime = Audio.GetPlaybackPosition();
        double realBeat = TimeKeeper.CurrentTime / (60 / (double)TimeKeeper.Bpm) % CM.BeatsPerLoop;
        CD.CheckMiss(realBeat);
        if (realBeat < _lastBeat)
            ChartLooped?.Invoke(this);
        _lastBeat = realBeat;
    }
    #endregion

    #region Input&Timing

    public override void _UnhandledInput(InputEvent @event)
    {
        //this one is for calling a debug key to insta-kill the enemy
        if (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.Echo)
        {
            if (eventKey.Keycode == Key.Key0)
            {
                DebugKillEnemy();
            }
        }
    }

    private void OnNotePressed(ArrowType type)
    {
        CD.CheckNoteTiming(type);
    }

    private void OnNoteReleased(ArrowType arrowType) { }

    private void OnTimedInput(Note note, ArrowType arrowType, int beat, double beatDif)
    {
        if (note == null)
        {
            if (PlayerAddNote(arrowType, beat))
                return; //Miss on empty note. This does not apply to inactive existing notes as a balance decision for now.
            NotePlacementBar.MissNote();
            CM.ComboText(Timing.Miss.ToString(), arrowType, NotePlacementBar.GetCurrentCombo());
            Player.TakeDamage(4);
            return;
        }

        Timing timed = CheckTiming(beatDif);

        note.OnHit(this, timed);
        if (timed == Timing.Miss)
        {
            NotePlacementBar.MissNote();
        }
        else
        {
            NotePlacementBar.HitNote();
        }
        CM.ComboText(timed.ToString(), arrowType, NotePlacementBar.GetCurrentCombo());
    }

    private Timing CheckTiming(double beatDif)
    {
        if (beatDif < _timingInterval * 1)
        {
            return Timing.Perfect;
        }

        if (beatDif < _timingInterval * 2)
        {
            return Timing.Good;
        }

        if (beatDif < _timingInterval * 3)
        {
            return Timing.Okay;
        }

        return Timing.Miss;
    }

    private void CheckBattleStatus(PuppetTemplate puppet)
    {
        if (puppet == Player)
        {
            BattleLost();
            return;
        }
        else if (puppet == Enemy)
            BattleWon(); //will have to adjust this to account for when we have multiple enemies at once
    }

    private void BattleWon()
    {
        Audio.StreamPaused = true;
        CleanUpRelics();
        ShowRewardSelection(3);
    }

    private void BattleLost()
    {
        Audio.StreamPaused = true;
        AddChild(GD.Load<PackedScene>("res://scenes/UI/EndScreen.tscn").Instantiate());
        GetTree().Paused = true;
    }

    private void ShowRewardSelection(int amount)
    {
        string type = "Note";
        if (StageProducer.Config.RoomType == Stages.Boss)
            type = "Relic";
        var rewardSelect = RewardSelect.CreateSelection(this, Player.Stats, amount, type);
        rewardSelect.GetNode<Label>("%TopLabel").Text = "You win!";
        rewardSelect.Selected += EndBattle;
    }

    #endregion

    #region BattleEffect Handling

    private delegate void NotePlacedHandler(BattleDirector BD);
    private event NotePlacedHandler NotePlaced;

    private delegate void ChartLoopHandler(BattleDirector BD);
    private event ChartLoopHandler ChartLooped;

    private void AddEvent(IBattleEvent bEvent)
    {
        switch (bEvent.GetTrigger()) //TODO: Look into a way to get eventhandler from string
        {
            case BattleEffectTrigger.NotePlaced:
                NotePlaced += bEvent.OnTrigger;
                break;
            case BattleEffectTrigger.OnLoop:
                ChartLooped += bEvent.OnTrigger;
                break;
        }
    }

    private void AddEnemyEffects()
    {
        foreach (var effect in Enemy.GetBattleEvents())
        {
            AddEvent(effect);
        }
    }

    private void EventizeRelics()
    {
        foreach (var relic in Player.Stats.CurRelics)
        {
            foreach (var effect in relic.Effects)
            {
                AddEvent(effect);
            }
        }
    }

    private void CleanUpRelics()
    {
        foreach (var relic in Player.Stats.CurRelics)
        {
            foreach (var effect in relic.Effects)
            {
                effect.OnBattleEnd();
            }
        }
    }
    #endregion

    private void DebugKillEnemy()
    {
        //Enemy.TakeDamage(1000);
    }
}
