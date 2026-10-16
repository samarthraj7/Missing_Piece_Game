using System.Collections.Generic;
using UnityEngine;

public static class EchoRules
{
    public static bool Occupied(EchoState s, Vector2Int p)
    {
        return s.Players[0] == p || s.Players[1] == p || s.Ghosts.Exists(g => g.Pos == p);
    }

    public static bool Blocked(string[] rows, EchoState s, Vector2Int p)
    {
        return BoardRules.IsSolid(rows, s.Flipped, p) || Occupied(s, p);
    }

    public static List<Vector2Int> Slide(string[] rows, EchoState s, ref Vector2Int pos, int dir, int owner)
    {
        var path = new List<Vector2Int>();
        Vector2Int step = BoardRules.Dirs[dir];
        for (int i = 0; i < 400; i++)
        {
            Vector2Int next = pos + step;
            if (Blocked(rows, s, next)) break;
            pos = next;
            if (rows[pos.y][pos.x] == 'o') s.Flipped = !s.Flipped;
            if (s.Gem.HasValue && s.Gem.Value == pos)
            {
                s.Score[owner]++;
                s.Gem = null;
            }
            path.Add(pos);
        }
        return path;
    }

    public static EchoMoveResult Apply(string[] rows, EchoState s, int pi, int dir)
    {
        if (s.Winner >= 0) return null;
        var n = s.Clone();
        Vector2Int start = n.Players[pi];
        var path = Slide(rows, n, ref n.Players[pi], dir, pi);
        if (path.Count == 0) return null;

        var result = new EchoMoveResult { State = n, PlayerPath = path };
        string name = pi == 0 ? "Blue" : "Red";
        int before = s.Score[pi];
        n.Message = $"{name} slid {BoardRules.DirNames[dir]}";
        if (n.Score[pi] > before) n.Message += " and grabbed a gem!";

        if (n.Hist[pi].Count == 0) n.HistStart[pi] = start;
        n.Hist[pi].Add(dir);
        n.Trail[pi].Add(n.Players[pi]);

        foreach (var ghost in n.Ghosts)
        {
            if (ghost.Owner != pi) continue;
            int ghostDir = ghost.Seq[ghost.Step % ghost.Seq.Count];
            ghost.Step++;
            int scoreBefore = n.Score[pi];
            var ghostPath = Slide(rows, n, ref ghost.Pos, ghostDir, pi);
            result.GhostPaths.Add((ghost.Id, ghostPath));
            if (n.Score[pi] > scoreBefore) n.Message += $" {name}'s ghost grabbed a gem!";
        }

        if (n.Hist[pi].Count >= EchoState.GhostEvery)
        {
            Vector2Int? spawn = FreeNear(rows, n, n.HistStart[pi] ?? start);
            if (spawn.HasValue)
            {
                n.Ghosts.Add(new EchoGhost
                {
                    Id = n.NextGhostId++,
                    Owner = pi,
                    Pos = spawn.Value,
                    Seq = new List<int>(n.Hist[pi]),
                    Step = 0
                });
                EchoGhost oldest = null;
                int mine = 0;
                foreach (var ghost in n.Ghosts)
                {
                    if (ghost.Owner != pi) continue;
                    mine++;
                    if (oldest == null) oldest = ghost;
                }
                if (mine > EchoState.MaxGhosts) n.Ghosts.Remove(oldest);
                n.Message += $" A {name} ghost appeared!";
            }
            n.Hist[pi].Clear();
            n.Trail[pi].Clear();
            n.HistStart[pi] = null;
        }

        if (n.Score[0] >= EchoState.WinScore) n.Winner = 0;
        else if (n.Score[1] >= EchoState.WinScore) n.Winner = 1;
        n.Turn = 1 - pi;
        return result;
    }

    public static void AfterTurn(string[] rows, EchoState s)
    {
        if (!s.Gem.HasValue && s.Winner < 0) SpawnGem(rows, s);
        ApplyStuck(rows, s);
    }

    public static void ApplyStuck(string[] rows, EchoState s)
    {
        if (s.Winner >= 0) return;
        bool selfStuck = !HasMove(rows, s, s.Turn);
        bool otherStuck = !HasMove(rows, s, 1 - s.Turn);
        if (selfStuck && otherStuck)
        {
            if (s.Score[0] > s.Score[1]) s.Winner = 0;
            else if (s.Score[1] > s.Score[0]) s.Winner = 1;
            else s.Winner = 2;
            s.Message = s.Winner == 2
                ? "Both players are boxed in: it's a draw."
                : $"Both players are boxed in: {(s.Winner == 0 ? "Blue" : "Red")} wins with more gems.";
            return;
        }
        if (selfStuck) s.Message = $"{(s.Turn == 0 ? "Blue" : "Red")} is boxed in.";
    }

    public static bool IsStuck(string[] rows, EchoState s)
    {
        return s != null && s.Winner < 0 && !HasMove(rows, s, s.Turn);
    }

    public static Vector2Int Home(string[] rows, int pi)
    {
        return pi == 0 ? Vector2Int.zero : new Vector2Int(rows[0].Length - 1, rows.Length - 1);
    }

    public static bool TryUnstick(string[] rows, EchoState s)
    {
        int pi = s.Turn;
        if (s.Score[pi] <= 0) return false;
        s.Score[pi]--;
        Vector2Int saved = s.Players[pi];
        s.Players[pi] = new Vector2Int(-99, -99);
        Vector2Int? dest = FreeNear(rows, s, Home(rows, pi));
        s.Players[pi] = dest ?? saved;
        s.Hist[pi].Clear();
        s.Trail[pi].Clear();
        s.HistStart[pi] = null;
        s.Message = $"{(pi == 0 ? "Blue" : "Red")} spent a gem and respawned.";
        return dest.HasValue;
    }

    public static void SkipStuckTurn(EchoState s)
    {
        string name = s.Turn == 0 ? "Blue" : "Red";
        s.Message = $"{name} skipped a boxed-in turn.";
        s.Turn = 1 - s.Turn;
    }

    public static bool HasMove(string[] rows, EchoState s, int pi)
    {
        for (int d = 0; d < 4; d++)
            if (Apply(rows, s, pi, d) != null) return true;
        return false;
    }

    public static void SpawnGem(string[] rows, EchoState s)
    {
        var near = new List<Vector2Int>();
        var any = new List<Vector2Int>();
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] != '.') continue;
                var p = new Vector2Int(x, y);
                if (Occupied(s, p)) continue;
                any.Add(p);
                int d0 = Mathf.Abs(p.x - s.Players[0].x) + Mathf.Abs(p.y - s.Players[0].y);
                int d1 = Mathf.Abs(p.x - s.Players[1].x) + Mathf.Abs(p.y - s.Players[1].y);
                if (Mathf.Min(d0, d1) >= 3) near.Add(p);
            }
        }
        var pool = near.Count > 0 ? near : any;
        s.Gem = pool.Count == 0 ? (Vector2Int?)null : pool[Random.Range(0, pool.Count)];
    }

    static Vector2Int? FreeNear(string[] rows, EchoState s, Vector2Int origin)
    {
        Vector2Int? best = null;
        int bestDist = int.MaxValue;
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                var p = new Vector2Int(x, y);
                if (Blocked(rows, s, p)) continue;
                int d = Mathf.Abs(p.x - origin.x) + Mathf.Abs(p.y - origin.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
        }
        return best;
    }
}
