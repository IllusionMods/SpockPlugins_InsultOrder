using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MeshPrefabData;
using Renderer = UnityEngine.Renderer;

namespace SiH_Uncensor
{
    [BepInPlugin(GUID, DisplayName, Version)]
    public class SummerHeatUncensorPlugin : BaseUnityPlugin
    {
        public const string GUID = "sih_uncensor";
        public const string DisplayName = "SummerInHeat Uncensor";
        public const string Version = "1.0.0";

        private const int NoMosaicId = 4;
        private const string NoMosaicStr = "非表示";

        private static string _imagesPath;
        private static Shader _replacementShader;

        internal static new ManualLogSource Logger;
        private static Dictionary<string, string> _pathLookup;
        private static Dictionary<string, Texture2D> _texLookup;

        public void Awake()
        {
            Logger = base.Logger;

            ReloadReplacementImages();

            Harmony.CreateAndPatchAll(typeof(Hooks));

            SceneManager.sceneLoaded += (scene, mode) => Logger.Log(LogLevel.Debug, $"SceneManager.sceneLoaded - Name=[{scene.name}] Mode=[{mode}]");
        }

        private void ReloadReplacementImages()
        {
            _imagesPath = Path.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(_imagesPath))
                _imagesPath = Paths.PluginPath;
            _imagesPath = Path.Combine(_imagesPath, "replacements");

            var files = Directory.GetFiles(_imagesPath, "*.png", SearchOption.TopDirectoryOnly);
            _pathLookup = files.ToDictionary(Path.GetFileNameWithoutExtension, x => x);
            _texLookup = new Dictionary<string, Texture2D>();

            Logger.Log(LogLevel.Debug, $"Found {files.Length} replacement images:\n{string.Join("\n", files)}");
        }

        private static void ReplaceMaterialsAndTextures(Renderer[] mats)
        {
            if (!_replacementShader)
            {
                _replacementShader = Shader.Find("Miconisomi/ASE_Miconisomi_VerTex");
                if (_replacementShader == null)
                {
                    Logger.Log(LogLevel.Error, "Failed to find replacement shader");
                    return;
                }
            }

            int hitsShd = 0, hitsTex = 0;
            foreach (var r in mats)
            {
                if (!r) continue;

                var material = r.sharedMaterial ?? r.material;
                if (!material) continue;
                var shaderName = material.shader.name;
                if (shaderName == "Miconisomi/ASE_Miconisomi_VerTex_Moza")
                {
                    material.shader = _replacementShader;
                    hitsShd++;
                }

                var validTarget = shaderName == "Miconisomi/Danmen" || // xray window
                                  shaderName.StartsWith("Miconisomi/ASE_Miconisomi_VerTex"); // characters
                if (!validTarget) continue;

                var mainTexture = material.mainTexture;
                if (!mainTexture) continue;
                var mainTextureName = mainTexture.name;
                if (_pathLookup.TryGetValue(mainTextureName, out var bytes))
                {
                    _texLookup.TryGetValue(mainTextureName, out var replacement);
                    if (!replacement)
                    {
                        replacement = new Texture2D(2, 2);
                        replacement.LoadImage(File.ReadAllBytes(bytes));
                        _texLookup[mainTextureName] = replacement;
                    }

                    if (material.mainTexture != replacement)
                    {
                        material.mainTexture = replacement;
                        hitsTex++;
                    }
                }
            }

            Logger.Log(LogLevel.Debug, $"Replaced {hitsShd} shaders and {hitsTex} textures in {mats.Length} renderers");
        }

        private static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MaterialChange_Moza), nameof(MaterialChange_Moza.MatChange))]
            private static void MaterialChange_Moza_MatChange_Postfix(MaterialChange_Moza __instance, bool ___RendeCheck, int ID, string[] List, bool b)
            {
                var run = List.Contains(__instance.name);
                if (b) run = !run;
                if (!run) return;

                var isDecensor = ID == NoMosaicId;

                if (___RendeCheck)
                    __instance.GetComponent<SkinnedMeshRenderer>().enabled = !isDecensor;
                else
                    __instance.GetComponent<MeshRenderer>().enabled = !isDecensor;

                var overrideEye = __instance.GetComponent<OverrideEye>();
                if (overrideEye.enabled)
                    overrideEye.enabled = !isDecensor;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ConfigSetting), "Awake")]
            private static void ConfigSetting_Awake_Postfix(ConfigSetting __instance, UIPopupList ___ConfBt_MosaicType_List, UIPopupList ___HS_QS09)
            {
                ___ConfBt_MosaicType_List.AddItem(NoMosaicStr);
                ___HS_QS09.AddItem(NoMosaicStr);
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting_Conf))]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting_Mini))]
            private static IEnumerable<CodeInstruction> MosaicSettingTpl(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions).MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting))))
                                                    .ThrowIfInvalid("No MosaicSetting?")
                                                    .Insert(new CodeInstruction(OpCodes.Ldloc_0),
                                                            CodeInstruction.Call(typeof(Hooks), nameof(Hooks.MosaicSettingHelper)))
                                                    .Instructions();
            }
            private static void MosaicSettingHelper(string text)
            {
                if (text == NoMosaicStr)
                    ConfigClass.MosaicSetting = 4;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting))]
            private static void MosaicSetting_Prefix(ConfigSetting __instance, ref bool ___MosaicSettingTimer_, UIPopupList ___ConfBt_MosaicType_List, UIPopupList ___HS_QS09, Camera ___DanmenCamera)
            {
                if (___MosaicSettingTimer_) return;

                if (ConfigClass.MosaicSetting == NoMosaicId)
                {
                    ___ConfBt_MosaicType_List.value = NoMosaicStr;
                    ___HS_QS09.value = NoMosaicStr;
                }

                var mosaicEnabled = ConfigClass.MosaicSetting != NoMosaicId;
                var xrayWindow = ___DanmenCamera.transform.parent;
                foreach (Transform child in xrayWindow)
                {
                    if (child.name.Contains("moza"))
                        child.gameObject.SetActive(mosaicEnabled);

                }

                ReplaceMaterialsAndTextures(xrayWindow.GetComponentsInChildren<Renderer>(true));
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomDataInputCustomManager), nameof(CustomDataInputCustomManager.UpdateMaterialID))]
            private static bool UpdateMaterialID_Override(CustomDataInputCustomManager __instance, string[] MeshNames, int ID, AssetBundleSystem ___AssetBundleSystem)
            {
                ReplaceMaterialsAndTextures(__instance.RootBone.GetComponentsInChildren<Renderer>(true));

                foreach (Transform child in __instance.RootBone)
                {
                    if (!MeshNames.Contains(child.name)) continue;

                    var mpd = child.GetComponent<MeshPrefabData>();
                    if (mpd == null) continue;

                    var renderer = child.GetComponent<SkinnedMeshRenderer>();

                    if (child.name.Contains("moza"))
                        renderer.enabled = ID != NoMosaicId;

                    if (mpd.MatsNameList.Count > ID)
                        renderer.sharedMaterials = ___AssetBundleSystem.GetMaterial(mpd.MatsNameList[ID].Mats);
                }

                return false;
            }
        }
    }
}
