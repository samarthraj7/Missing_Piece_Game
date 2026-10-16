using System.Collections.Generic;
using UnityEngine;

public static class EchoBot
{
    public static int Choose(string[] rows, EchoState s, int pi, int level)
    {
        var acts = new List<int>();
        for (int d = 0; d < 4; d++)
            if (EchoRules.Apply(rows, s, pi, d) != null) acts.Add(d);
        if (acts.Count == 0) return -1;
        Shuffle(acts);
        if (level == 0 && Random.value < 0.35f) return acts[0];

        int depth = level == 0 ? 1 : level == 1 ? 2 : 4;
        float best = float.NegativeInfinity;
        int choice = acts[0];
        foreach (int dir in acts)
        {
            EchoState next = EchoRules.Apply(rows, s, pi, dir).State;
            float v = -Negamax(rows, next, depth - 1, float.NegativeInfinity, -best, 1 - pi);
            if (v > best)
            {
                best = v;
                choice = dir;
            }
        }
        return choice;
    }

    static float Negamax(string[] rows, EchoState s, int depth, float alpha, float beta, int pi)
    {
        if (s.Winner >= 0) return s.Winner == pi ? 1e6f + depth : -1e6f - depth;
        if (depth == 0 || !s.Gem.HasValue) return Eval(rows, s, pi);

        float best = float.NegativeInfinity;
        bool any = false;
        for (int dir = 0; dir < 4; dir++)
        {
            var move = EchoRules.Apply(rows, s, pi, dir);
            if (move == null) continue;
            any = true;
            float v = -Negamax(rows, move.State, depth - 1, -beta, -alpha, 1 - pi);
            if (v > best) best = v;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return any ? best : Eval(rows, s, pi);
    }

    static float Eval(string[] rows, EchoState s, int pi)
    {
        return 100 * (s.Score[pi] - s.Score[1 - pi]) + 8 * (GemDist(rows, s, 1 - pi) - GemDist(rows, s, pi));
    }

    static int GemDist(string[] rows, EchoState s, int pi)
    {
        if (!s.Gem.HasValue) return 0;
        Vector2Int gem = s.Gem.Value;
        var seen = new HashSet<(int, int, bool)> { (s.Players[pi].x, s.Players[pi].y, s.Flipped) };
        var frontier = new List<(int x, int y, bool flip)> { (s.Players[pi].x, s.Players[pi].y, s.Flipped) };
        for (int depth = 1; depth <= 8; depth++)
        {
            var next = new List<(int x, int y, bool flip)>();
            foreach (var (x0, y0, flip0) in frontier)
            {
                for (int dir = 0; dir < 4; dir++)
                {
                    Vector2Int step = BoardRules.Dirs[dir];
                    int x = x0, y = y0;
                    bool flip = flip0;
                    bool hit = false;
                    for (int i = 0; i < 400; i++)
                    {
                        var p = new Vector2Int(x + step.x, y + step.y);
                        if (BlockedForSearch(rows, s, pi, p, flip)) break;
                        x = p.x;
                        y = p.y;
                        if (rows[y][x] == 'o') flip = !flip;
                        if (x == gem.x && y == gem.y) hit = true;
                    }
                    if (hit) return depth;
                    if (seen.Add((x, y, flip))) next.Add((x, y, flip));
                }
            }
            frontier = next;
        }
        return 9;
    }

    static bool BlockedForSearch(string[] rows, EchoState s, int pi, Vector2Int p, bool flip)
    {
        if (BoardRules.IsSolid(rows, flip, p)) return true;
        if (s.Players[1 - pi] == p) return true;
        return s.Ghosts.Exists(g => g.Pos == p);
    }

    static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
