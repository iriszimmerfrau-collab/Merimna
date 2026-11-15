using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click WebGL build: Tools > Merimna > Build WebGL  (or run with -executeMethod WebBuild.Build).
/// Creates a small scene that already contains the World Generator (with sprites assigned),
/// because the editor-only auto-create step doesn't exist in built players.
/// Output: Builds/Web/index.html
/// </summary>
public static class WebBuild
{
    const string ScenePath = "Assets/Scenes/WebBuild.unity";
    const string OutputDir = "Builds/Web";

    static void IncludeShader(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null) return;

        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(gs);
        SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Merimna/Build WebGL")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.36f, 0.55f);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        var gen = new GameObject("World Generator").AddComponent<WorldGenerator>();
        gen.AutoAssignSprites();   // serialized references => sprites are included in the build

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        IncludeShader("Sprites/Default");   // runtime Shader.Find needs it kept in the build

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // works on any static host
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.productName = "Merimna";
        PlayerSettings.WebGL.template = "PROJECT:Merimna";   // Assets/WebGLTemplates/Merimna: fullscreen canvas, no Unity branding
        try { PlayerSettings.SplashScreen.show = false; } catch { }   // (Unity Personal always keeps its own splash)

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("WebBuild: " + report.summary.result + " -> " + Path.GetFullPath(OutputDir));
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
