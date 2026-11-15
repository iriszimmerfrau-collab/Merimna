using UnityEditor;
using UnityEngine;

public static class WorldGeneratorMenu
{
    [MenuItem("Tools/Merimna/Create World Generator")]
    static void Create()
    {
        var go = new GameObject("World Generator");
        var gen = go.AddComponent<WorldGenerator>();
        gen.AutoAssignSprites();
        Undo.RegisterCreatedObjectUndo(go, "Create World Generator");
        Selection.activeObject = go;
    }
}
