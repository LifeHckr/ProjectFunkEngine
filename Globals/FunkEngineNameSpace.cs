﻿using Godot;

namespace FunkEngine;

public enum ArrowType
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
}

public struct SongData
{
    public int Bpm;
    public double SongLength;
    public int NumLoops;
}

public struct ArrowData
{
    public Color Color;
    public string Key;
    public NoteChecker Node;
    public ArrowType Type;
}

public interface IBattleEvent
{
    void OnTrigger(BattleDirector BD);
    string GetTrigger();
}
