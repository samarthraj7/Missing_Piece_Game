using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EchoGame : MonoBehaviour
{
    static readonly string[] ModeNames =
    {
        "vs Bot – Easy", "vs Bot – Medium", "vs Bot – Hard", "2 Players", "2 Players"
    };
    static readonly Color Blue = Hex("#4aa3ff"), Red = Hex("#ff5d6c");
    static readonly Color FloorA = Hex("#1a2131"), FloorB = Hex("#1c2436"), WallColor = Hex("#44516f");
    static readonly Color SwitchColor = Hex("#ffd66b"), WallFill = Hex("#6f4fc2"), WallOutline = Hex("#c9b0ff");
    static readonly Color GemColor = Hex("#4dff9a"), TrailColor = new Color(1f, 1f, 1f, 0.28f);

    const float SwitchWallSize = 0.84f, OutlineWidth = 0.08f;
    const int DashesPerSide = 4;
    const string HowTo = "First to 5 gems. Boxed in? Spend 1 gem to respawn. If both are stuck, more gems win.";

    [Header("Arena")]
    public LevelData[] levels;

    [Header("Sprites (Create > 2D > Sprites > Square / Circle)")]
    public Sprite square;
    public Sprite circle;

    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text introText;
    public TMP_Text movesText;
    public TMP_Text messageText;
    public Button[] levelButtons;
    public GameObject winPanel;
    public TMP_Text winText;
    public GameObject nextButton;

    public float cellsPerSecond = 16f;
    public float botThinkSeconds = 0.45f;

    LevelData arena;
    EchoState state;
    readonly Stack<EchoState> history = new Stack<EchoState>();
    readonly List<(GameObject solid, GameObject dotted, bool startsRaised)> switchWalls = new();
    readonly Dictionary<int, Transform> ghostViews = new();
    readonly List<GameObject> trailMarks = new();
    Transform board, blueBall, redBall, gemView;
    int mode; // 0 easy, 1 medium, 2 hard, 3+ pvp
    bool busy;
    bool awaitingUnstick;
    Vector2 swipeStart;

    bool VsBot => mode <= 2;
    bool BotTurn => VsBot && state != null && state.Turn == 1 && state.Winner < 0;
    string[] Rows => arena.rows;

    void Start()
    {
        arena = levels != null && levels.Length > 0 ? levels[0] : null;
        if (titleText)
        {
            titleText.text = "Echo";
            titleText.textWrappingMode = TextWrappingModes.Normal;
        }
        if (introText) introText.text = HowTo;
        if (nextButton)
        {
            var label = nextButton.GetComponentInChildren<TMP_Text>();
            if (label) label.text = "New game";
        }
        if (levelButtons != null)
        {
            string[] labels = { "Easy bot", "Medium bot", "Hard bot", "2 Players" };
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (i >= labels.Length)
                {
                    levelButtons[i].gameObject.SetActive(false);
                    continue;
                }
                levelButtons[i].gameObject.SetActive(true);
                var text = levelButtons[i].GetComponentInChildren<TMP_Text>();
                if (text) text.text = labels[i];
            }
        }
        FitHudText(introText, 70f, 16f);
        FitHudText(movesText, 170f, 16f);
        BuildBoard();
        FitCamera();
        NewGame();
    }

    public void Load(int index)
    {
        mode = Mathf.Clamp(index, 0, 3);
        NewGame();
    }

    public void Hint()
    {
        if (busy) return;
        mode = (mode + 1) % 4;
        NewGame();
    }

    public void Restart() => NewGame();

    public void NextLevel()
    {
        if (awaitingUnstick)
        {
            if (state != null && state.Score[state.Turn] > 0) SpendGemToRespawn();
            else SkipStuckTurn();
        }
        else NewGame();
    }

    public void SpendGemToRespawn()
    {
        if (!awaitingUnstick || busy || state == null || state.Winner >= 0) return;
        if (state.Score[state.Turn] <= 0)
        {
            if (messageText) messageText.text = "No gem to spend. Press N to skip this turn.";
            return;
        }
        history.Push(state);
        var next = state.Clone();
        EchoRules.TryUnstick(Rows, next);
        EchoRules.ApplyStuck(Rows, next);
        state = next;
        awaitingUnstick = false;
        HideStuckPrompt();
        SyncTokens(true);
        DrawTrails();
        if (messageText) messageText.text = state.Message;
        UpdateHud();
        if (state.Winner >= 0) { Finish(); return; }
        if (EchoRules.IsStuck(Rows, state)) BeginStuckChoice();
        else if (BotTurn) StartCoroutine(BotAfterDelay());
    }

    public void SkipStuckTurn()
    {
        if (!awaitingUnstick || busy || state == null || state.Winner >= 0) return;
        history.Push(state);
        var next = state.Clone();
        EchoRules.SkipStuckTurn(next);
        EchoRules.ApplyStuck(Rows, next);
        state = next;
        awaitingUnstick = false;
        HideStuckPrompt();
        if (messageText) messageText.text = state.Message;
        UpdateHud();
        if (state.Winner >= 0) { Finish(); return; }
        if (EchoRules.IsStuck(Rows, state)) BeginStuckChoice();
        else if (BotTurn) StartCoroutine(BotAfterDelay());
    }

    public void NewGame()
    {
        StopAllCoroutines();
        busy = false;
        awaitingUnstick = false;
        history.Clear();
        state = EchoState.Initial(Rows);
        SetNextButtonLabel("New game");
        SyncTokens(true);
        ApplyWalls(false);
        ClearTrails();
        DrawTrails();
        if (winPanel) winPanel.SetActive(false);
        if (messageText) messageText.text = $"Mode: {ModeNames[mode]}. Blue starts.";
        UpdateHud();
    }

    public void Undo()
    {
        if (busy || history.Count == 0) return;
        StopAllCoroutines();
        busy = false;
        awaitingUnstick = false;
        state = history.Pop();
        if (VsBot)
        {
            while (state.Turn == 1 && history.Count > 0) state = history.Pop();
        }
        SyncTokens(true);
        ApplyWalls(state.Flipped);
        DrawTrails();
        HideStuckPrompt();
        if (messageText) messageText.text = "";
        UpdateHud();
        if (EchoRules.IsStuck(Rows, state)) BeginStuckChoice();
    }

    public void Tilt(int dir)
    {
        if (busy || awaitingUnstick || state == null || state.Winner >= 0 || BotTurn) return;
        PlayMove(dir, true);
    }

    void PlayMove(int dir, bool record)
    {
        var move = EchoRules.Apply(Rows, state, state.Turn, dir);
        if (move == null)
        {
            if (messageText && !BotTurn) messageText.text = "You can't slide that way.";
            return;
        }
        if (record) history.Push(state);
        EchoRules.AfterTurn(Rows, move.State);
        if (messageText) messageText.text = move.State.Message;
        StartCoroutine(Animate(move));
    }

    IEnumerator Animate(EchoMoveResult move)
    {
        busy = true;
        int actor = state.Turn;
        yield return SlideToken(actor == 0 ? blueBall : redBall, move.PlayerPath, move.State);
        foreach (var (id, path) in move.GhostPaths)
        {
            if (!ghostViews.TryGetValue(id, out var view)) continue;
            yield return SlideToken(view, path, move.State);
        }
        state = move.State;
        SyncTokens(false);
        ApplyWalls(state.Flipped);
        DrawTrails();
        UpdateHud();
        if (state.Winner >= 0)
        {
            Finish();
            busy = false;
            yield break;
        }
        busy = false;
        if (EchoRules.IsStuck(Rows, state))
        {
            BeginStuckChoice();
            yield break;
        }
        if (BotTurn) yield return BotAfterDelay();
    }

    IEnumerator BotAfterDelay()
    {
        yield return new WaitForSeconds(botThinkSeconds);
        if (busy || state == null || state.Winner >= 0) yield break;
        if (EchoRules.IsStuck(Rows, state))
        {
            BeginStuckChoice();
            yield break;
        }
        if (!BotTurn) yield break;
        int dir = EchoBot.Choose(Rows, state, 1, mode);
        if (dir >= 0) PlayMove(dir, true);
    }

    IEnumerator SlideToken(Transform token, List<Vector2Int> path, EchoState end)
    {
        if (token == null) yield break;
        bool flip = state.Flipped;
        foreach (var cell in path)
        {
            Vector3 target = ToWorld(cell);
            while ((token.position - target).sqrMagnitude > 0.0001f)
            {
                token.position = Vector3.MoveTowards(token.position, target, cellsPerSecond * Time.deltaTime);
                yield return null;
            }
            if (Rows[cell.y][cell.x] == 'o') flip = !flip;
            ApplyWalls(flip);
            if (end.Gem == null && gemView) gemView.gameObject.SetActive(false);
        }
        ApplyWalls(end.Flipped);
    }

    void Finish()
    {
        awaitingUnstick = false;
        if (winText)
        {
            winText.text = state.Winner == 2 ? "It's a draw!"
                : state.Winner == 0 ? "Blue wins!" : "Red wins!";
        }
        SetNextButtonLabel("New game");
        if (nextButton) nextButton.SetActive(true);
        if (winPanel) winPanel.SetActive(true);
    }

    void BeginStuckChoice()
    {
        if (state.Winner >= 0) return;
        if (BotTurn)
        {
            awaitingUnstick = true;
            if (state.Score[1] > 0) SpendGemToRespawn();
            else SkipStuckTurn();
            return;
        }
        awaitingUnstick = true;
        bool canSpend = state.Score[state.Turn] > 0;
        string who = state.Turn == 0 ? "Blue" : "Red";
        if (winText)
        {
            winText.text = canSpend
                ? $"{who} is boxed in.\nSpend 1 gem to respawn (G), or skip this turn (N)."
                : $"{who} is boxed in and has no gems.\nPress N to skip this turn.";
        }
        SetNextButtonLabel(canSpend ? "Spend 1 gem" : "Skip turn");
        if (nextButton) nextButton.SetActive(true);
        if (winPanel) winPanel.SetActive(true);
        if (messageText) messageText.text = state.Message;
    }

    void HideStuckPrompt()
    {
        if (state != null && state.Winner >= 0) return;
        SetNextButtonLabel("New game");
        if (winPanel) winPanel.SetActive(false);
    }

    void SetNextButtonLabel(string label)
    {
        if (!nextButton) return;
        var text = nextButton.GetComponentInChildren<TMP_Text>();
        if (text) text.text = label;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Tilt(0);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Tilt(1);
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Tilt(2);
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Tilt(3);
            if (k.zKey.wasPressedThisFrame) Undo();
            if (k.rKey.wasPressedThisFrame) Restart();
            if (k.hKey.wasPressedThisFrame) Hint();
            if (k.digit1Key.wasPressedThisFrame) Load(0);
            if (k.digit2Key.wasPressedThisFrame) Load(1);
            if (k.digit3Key.wasPressedThisFrame) Load(2);
            if (k.digit4Key.wasPressedThisFrame) Load(3);
            if (k.gKey.wasPressedThisFrame) SpendGemToRespawn();
            if (k.nKey.wasPressedThisFrame) SkipStuckTurn();
            if (k.enterKey.wasPressedThisFrame)
            {
                if (awaitingUnstick)
                {
                    if (state.Score[state.Turn] > 0) SpendGemToRespawn();
                    else SkipStuckTurn();
                }
                else if (state != null && state.Winner >= 0) NewGame();
            }
        }

        var p = Pointer.current;
        if (p == null) return;
        if (p.press.wasPressedThisFrame) swipeStart = p.position.ReadValue();
        if (p.press.wasReleasedThisFrame)
        {
            Vector2 delta = p.position.ReadValue() - swipeStart;
            if (delta.magnitude < 50f) return;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Tilt(delta.x > 0 ? 3 : 2);
            else Tilt(delta.y > 0 ? 0 : 1);
        }
    }

    void BuildBoard()
    {
        if (board != null) Destroy(board.gameObject);
        board = new GameObject("Board").transform;
        switchWalls.Clear();
        ghostViews.Clear();
        trailMarks.Clear();

        for (int y = 0; y < arena.rows.Length; y++)
        {
            for (int x = 0; x < arena.rows[y].Length; x++)
            {
                var p = new Vector2Int(x, y);
                char c = arena.rows[y][x];
                Make(square, p, (x + y) % 2 == 0 ? FloorB : FloorA, 1f, 0);
                switch (c)
                {
                    case '#': Make(square, p, WallColor, 0.92f, 1); break;
                    case 'o': Make(circle, p, SwitchColor, 0.55f, 1); break;
                    case '1':
                    case '2':
                        MakeSwitchWall(p, c == '1');
                        break;
                }
            }
        }

        blueBall = Make(circle, Vector2Int.zero, Blue, 0.62f, 6).transform;
        redBall = Make(circle, Vector2Int.zero, Red, 0.62f, 6).transform;
        gemView = Make(square, Vector2Int.zero, GemColor, 0.42f, 4).transform;
        gemView.rotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void SyncTokens(bool snap)
    {
        if (snap)
        {
            blueBall.position = ToWorld(state.Players[0]);
            redBall.position = ToWorld(state.Players[1]);
        }
        if (gemView)
        {
            bool show = state.Gem.HasValue;
            gemView.gameObject.SetActive(show);
            if (show) gemView.position = ToWorld(state.Gem.Value);
        }

        var live = new HashSet<int>();
        foreach (var ghost in state.Ghosts)
        {
            live.Add(ghost.Id);
            if (!ghostViews.TryGetValue(ghost.Id, out var view))
            {
                Color c = (ghost.Owner == 0 ? Blue : Red);
                c.a = 0.45f;
                view = Make(circle, ghost.Pos, c, 0.56f, 5).transform;
                ghostViews[ghost.Id] = view;
            }
            if (snap) view.position = ToWorld(ghost.Pos);
        }
        var stale = new List<int>();
        foreach (var id in ghostViews.Keys)
            if (!live.Contains(id)) stale.Add(id);
        foreach (int id in stale)
        {
            if (ghostViews[id]) Destroy(ghostViews[id].gameObject);
            ghostViews.Remove(id);
        }
    }

    void DrawTrails()
    {
        ClearTrails();
        for (int i = 0; i < 2; i++)
        {
            if (!state.HistStart[i].HasValue) continue;
            Color c = (i == 0 ? Blue : Red);
            c.a = 0.35f;
            MakeTrail(state.HistStart[i].Value, c);
            foreach (var p in state.Trail[i]) MakeTrail(p, c);
        }
    }

    void MakeTrail(Vector2Int p, Color color)
    {
        var sr = Make(circle, p, color, 0.18f, 3);
        trailMarks.Add(sr.gameObject);
    }

    void ClearTrails()
    {
        foreach (var go in trailMarks)
            if (go) Destroy(go);
        trailMarks.Clear();
    }

    void ApplyWalls(bool flipped)
    {
        foreach (var (solid, dotted, startsRaised) in switchWalls)
        {
            bool raised = startsRaised != flipped;
            solid.SetActive(raised);
            dotted.SetActive(!raised);
        }
    }

    void MakeSwitchWall(Vector2Int p, bool startsRaised)
    {
        var root = new GameObject("SwitchWall").transform;
        root.SetParent(board);
        root.position = ToWorld(p);

        float s = SwitchWallSize, w = OutlineWidth, edge = (s - w) / 2f;
        var solid = new GameObject("Raised").transform;
        solid.SetParent(root, false);
        MakeRect(solid, Vector2.zero, new Vector2(s, s), WallFill, 1);
        MakeRect(solid, new Vector2(0f, edge), new Vector2(s, w), WallOutline, 2);
        MakeRect(solid, new Vector2(0f, -edge), new Vector2(s, w), WallOutline, 2);
        MakeRect(solid, new Vector2(-edge, 0f), new Vector2(w, s), WallOutline, 2);
        MakeRect(solid, new Vector2(edge, 0f), new Vector2(w, s), WallOutline, 2);

        var dotted = new GameObject("Lowered").transform;
        dotted.SetParent(root, false);
        float step = s / DashesPerSide, dash = step / 2f;
        for (int i = 0; i < DashesPerSide; i++)
        {
            float t = -s / 2f + step * (i + 0.5f);
            MakeRect(dotted, new Vector2(t, edge), new Vector2(dash, w), WallOutline, 2);
            MakeRect(dotted, new Vector2(t, -edge), new Vector2(dash, w), WallOutline, 2);
            MakeRect(dotted, new Vector2(-edge, t), new Vector2(w, dash), WallOutline, 2);
            MakeRect(dotted, new Vector2(edge, t), new Vector2(w, dash), WallOutline, 2);
        }
        switchWalls.Add((solid.gameObject, dotted.gameObject, startsRaised));
    }

    void MakeRect(Transform parent, Vector2 localPos, Vector2 size, Color color, int order)
    {
        var go = new GameObject("Rect");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = order;
    }

    SpriteRenderer Make(Sprite sprite, Vector2Int p, Color color, float scale, int order)
    {
        var go = new GameObject(sprite.name);
        go.transform.SetParent(board);
        go.transform.position = ToWorld(p);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    void FitCamera()
    {
        var cam = Camera.main;
        float w = arena.rows[0].Length, h = arena.rows.Length;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(h / 2f, w / 2f / cam.aspect) + 1.5f;
        cam.transform.position = new Vector3((w - 1) / 2f, -(h - 1) / 2f, -10f);
    }

    void UpdateHud()
    {
        int blueGhosts = 0, redGhosts = 0;
        foreach (var g in state.Ghosts)
        {
            if (g.Owner == 0) blueGhosts++;
            else redGhosts++;
        }
        string turn = state.Winner >= 0
            ? (state.Winner == 0 ? "Blue wins" : "Red wins")
            : (state.Turn == 0 ? "Blue's turn" : BotTurn ? "Red's turn – bot thinking" : "Red's turn");
        if (movesText)
        {
            movesText.text =
                $"<color=#4aa3ff>Blue {state.Score[0]}</color>  –  <color=#ff5d6c>{state.Score[1]} Red</color>\n" +
                $"{turn}\n" +
                $"Blue ghost in {EchoState.GhostEvery - state.Hist[0].Count} ({blueGhosts}/{EchoState.MaxGhosts})\n" +
                $"Red ghost in {EchoState.GhostEvery - state.Hist[1].Count} ({redGhosts}/{EchoState.MaxGhosts})\n" +
                $"Mode: {ModeNames[mode]}";
        }
        if (levelButtons != null)
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (!levelButtons[i] || !levelButtons[i].gameObject.activeSelf) continue;
                levelButtons[i].interactable = true;
                var colors = levelButtons[i].colors;
                colors.normalColor = i == mode ? new Color(0.55f, 0.7f, 1f) : Color.white;
                levelButtons[i].colors = colors;
            }
        }
    }

    static void FitHudText(TMP_Text text, float height, float fontSize)
    {
        if (!text) return;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        var layout = text.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layout)
        {
            layout.preferredHeight = height;
            layout.minHeight = height;
        }
    }

    static Vector3 ToWorld(Vector2Int p) => new Vector3(p.x, -p.y, 0f);

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
