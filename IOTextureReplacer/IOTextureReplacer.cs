using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;


namespace IOTextureReplacer
{
    [BepInPlugin(GUID: "IOTextureReplacer", Name: "RipeDurian.InsultOrder.IOTextureReplacer", Version: Version)]
    public class IOTextureReplacer : BaseUnityPlugin
    {
        public const string Version = "0.1.1";

        public string imagesPath { get; set; } = Path.Combine(Paths.PluginPath, "IOTextureReplacer/images");
        public string imagesDumpPath { get; set; } = Path.Combine(Paths.PluginPath, "IOTextureReplacer/imageDump");

        private Dictionary<string, string> images;
        private ConfigEntry<KeyboardShortcut> _dumpHotkey;

        public void Start()
        {
            _dumpHotkey = Config.Bind("Hotkeys", "Dump images", new KeyboardShortcut(KeyCode.F11, KeyCode.RightAlt), "Save all images present in the current scene inside " + imagesDumpPath);

            SceneManager.sceneLoaded += OnLevelFinishedLoading;

            getImages();
            replaceImages();
        }

        public void LateUpdate()
        {
            if (_dumpHotkey.Value.IsDown())
            {
                Logger.Log(LogLevel.Debug, "Dumping Images..." + Environment.NewLine);
                try
                {
                    DumpImagesFromScene();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, ex.ToString());
                }
            }
        }

        public void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            Logger.Log(LogLevel.Info, "New Scene: " + scene.name);
            getImages();
            replaceImages();
        }

        private void getImages()
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Fetching Images...");
                images = new Dictionary<string, string>();

                foreach (string file in Directory.GetFiles(imagesPath))
                {
                    if (Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        images.Add(Path.GetFileNameWithoutExtension(file), file);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.ToString());
            }
        }

        private void replaceImages()
        {
            UnityEngine.Object[] textures = Resources.FindObjectsOfTypeAll(typeof(Texture2D));

            foreach (UnityEngine.Object t in textures)
            {
                try
                {
                    Texture2D tex = (Texture2D)t;
                    if (images.ContainsKey(t.name))
                    {
                        Logger.Log(LogLevel.Debug, "Replacing Image: " + tex.name);
                        byte[] fileData = File.ReadAllBytes(images[t.name]);
                        tex.LoadImage(fileData);

                    }
                }
                catch (Exception ex)
                {

                    Logger.Log(LogLevel.Error, ex.ToString());
                }
            }

            Resources.UnloadUnusedAssets();
        }

        private void DumpImagesFromScene()
        {
            string dumppath = Path.Combine(imagesDumpPath, SceneManager.GetActiveScene().name);

            if (!Directory.Exists(dumppath))
            {
                Directory.CreateDirectory(dumppath);
            }

            UnityEngine.Object[] textures = Resources.FindObjectsOfTypeAll(typeof(Texture2D));

            foreach (UnityEngine.Object t in textures)
            {
                try
                {
                    Texture2D tex = (Texture2D)t;
                    Texture2D tex_readable = getReadableTexture2D(tex);
                    byte[] bytes = tex_readable.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(dumppath, t.name + ".png"), bytes);
                    Logger.Log(LogLevel.Debug, "Dumping " + t.name);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, ex.ToString());
                }
            }

            Logger.Log(LogLevel.Debug, "Dumppath: " + dumppath);
        }

        public static Texture2D getReadableTexture2D(Texture2D texture)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(
                                texture.width,
                                texture.height,
                                0,
                                RenderTextureFormat.Default,
                                RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            RenderTexture.active = previous;

            RenderTexture.ReleaseTemporary(tmp);

            return myTexture2D;
        }
    }
}
