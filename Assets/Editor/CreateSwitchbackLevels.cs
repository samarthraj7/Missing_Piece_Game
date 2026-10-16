using UnityEditor;
using UnityEngine;

public static class CreateEchoArena
{
    [MenuItem("Echo/Create Arena Asset")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Levels")) AssetDatabase.CreateFolder("Assets", "Levels");

        var level = ScriptableObject.CreateInstance<LevelData>();
        level.levelName = "Echo Arena";
        level.moveLimit = 0;
        level.intro = "First to 5 gems. Ghosts replay your last 5 slides.";
        level.rows = new[]
        {
            "....#....",
            ".o..2..1.",
            "..#...#..",
            ".2..1..#.",
            "#..2.2..#",
            ".#..1..2.",
            "..#...#..",
            ".1..2..o.",
            "....#....",
        };
        AssetDatabase.CreateAsset(level, "Assets/Levels/EchoArena.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Created Echo arena in Assets/Levels/EchoArena.asset");
    }
}
