﻿using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

public class BuildDocumentation  {
    [MenuItem("MyTools/Windows Build With Postprocess")]
    public static void BuildGame()
    {
        buildSpecificPlayer(BuildTarget.StandaloneWindows);
    }

    [MenuItem("MyTools/Windows 64 Build With Postprocess")]
    public static void BuildGame64()
    {
        buildSpecificPlayer(BuildTarget.StandaloneWindows64);
    }

    static void buildSpecificPlayer(BuildTarget buildTarget)
    {
        // Get filename.
        string path = EditorUtility.SaveFolderPanel("Choose Location of Built Game", "", "");
        
        if (path != string.Empty)
        {
            List<string> levels = new List<string>(EditorBuildSettings.scenes.Length);

            for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i)
            {
                EditorBuildSettingsScene scene = EditorBuildSettings.scenes[i];
                if (scene.enabled)
                {
                    levels.Add(scene.path);
                    UnityEngine.Debug.Log(scene);
                }
            }

            // Build player.
            BuildPipeline.BuildPlayer(levels.ToArray(), path + "/Moonscraper Chart Editor.exe", buildTarget, BuildOptions.None);

            if (Directory.Exists("Assets/Custom Resources"))
            {
                // Copy a file from the project folder to the build folder, alongside the built game.
                clearAndDeleteDirectory(path + "/Custom Resources");
                FileUtil.CopyFileOrDirectory("Assets/Custom Resources", path + "/Custom Resources");
            }

            if (Directory.Exists("Assets/Documentation"))
            {
                // Copy a file from the project folder to the build folder, alongside the built game.
                clearAndDeleteDirectory(path + "/Documentation");
                FileUtil.CopyFileOrDirectory("Assets/Documentation", path + "/Documentation");
            }

            if (Directory.Exists("Assets\\ExtraBuildFiles"))
            {
                // Copy a file from the project folder to the build folder, alongside the built game.
                foreach (string filepath in Directory.GetFiles("Assets\\ExtraBuildFiles"))
                {
                    FileUtil.CopyFileOrDirectory(filepath, path + "/" + Path.GetFileName(filepath));
                }

            }

            foreach (string file in Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
        else
        {
            UnityEngine.Debug.Log("Build canceled");
        }
    }

    static void clearAndDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            DirectoryInfo di = new DirectoryInfo(path);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }

            Directory.Delete(path);
        }
    }
}
