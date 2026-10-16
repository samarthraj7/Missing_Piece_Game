using System.Collections.Generic;
using UnityEngine;

public class EchoGhost
{
    public int Id;
    public int Owner;
    public Vector2Int Pos;
    public List<int> Seq;
    public int Step;

    public EchoGhost Clone()
    {
        return new EchoGhost
        {
            Id = Id,
            Owner = Owner,
            Pos = Pos,
            Seq = new List<int>(Seq),
            Step = Step
        };
    }
}

public class EchoState
{
    public const int WinScore = 5;
    public const int GhostEvery = 5;
    public const int MaxGhosts = 1;

    public bool Flipped;
    public Vector2Int[] Players = new Vector2Int[2];
    public List<EchoGhost> Ghosts = new List<EchoGhost>();
    public List<int>[] Hist = { new List<int>(), new List<int>() };
    public Vector2Int?[] HistStart = new Vector2Int?[2];
    public List<Vector2Int>[] Trail = { new List<Vector2Int>(), new List<Vector2Int>() };
    public int[] Score = new int[2];
    public Vector2Int? Gem;
    public int Turn;
    public int Winner = -1; // -1 none, 0 blue, 1 red, 2 draw
    public string Message = "";
    public int NextGhostId;

    public static EchoState Initial(string[] rows)
    {
        int w = rows[0].Length;
        int h = rows.Length;
        return new EchoState
        {
            Players = new[] { new Vector2Int(0, 0), new Vector2Int(w - 1, h - 1) },
            Gem = new Vector2Int(w / 2, h / 2)
        };
    }

    public EchoState Clone()
    {
        return new EchoState
        {
            Flipped = Flipped,
            Players = new[] { Players[0], Players[1] },
            Ghosts = Ghosts.ConvertAll(g => g.Clone()),
            Hist = new[] { new List<int>(Hist[0]), new List<int>(Hist[1]) },
            HistStart = new[] { HistStart[0], HistStart[1] },
            Trail = new[] { new List<Vector2Int>(Trail[0]), new List<Vector2Int>(Trail[1]) },
            Score = new[] { Score[0], Score[1] },
            Gem = Gem,
            Turn = Turn,
            Winner = Winner,
            Message = "",
            NextGhostId = NextGhostId
        };
    }
}

public class EchoMoveResult
{
    public EchoState State;
    public List<Vector2Int> PlayerPath = new List<Vector2Int>();
    public List<(int ghostId, List<Vector2Int> path)> GhostPaths = new List<(int, List<Vector2Int>)>();
}
