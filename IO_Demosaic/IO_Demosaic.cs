using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace IO_Demosaic
{
    [BepInProcess("IO")]
    [BepInPlugin("spockbauru.insultorder.io_demosaic", "IO_Demosaic", Version)]
    public class IO_Demosaic : BaseUnityPlugin
    {
        // Set version in BepInEx and in AssemblyInfo
        public const string Version = "1.2";
        public IO_Demosaic()
        {
            // Patch Everything
            Harmony.CreateAndPatchAll(typeof(IO_Demosaic));
        }

        //===============================================Hooks===============================================
        //Disabling flag for anal mosaic
        [HarmonyPrefix, HarmonyPatch(typeof(MozaicSetUp), "Start")]
        private static void AnalDemosaic()
        {
            ConfigClass.AnalMoza = false;
        }

        // Disabling Mosaic Object in Characters
        [HarmonyPostfix, HarmonyPatch(typeof(MozaicSetUp), "Start")]
        private static void CharacterDemosaic(Renderer ___MozaObj, MozaicSetUp __instance)
        {
            ___MozaObj.enabled = false;
            __instance.enabled = false;
        }

        // Disabling Mosaic Object in X-ray window
        [HarmonyPostfix, HarmonyPatch(typeof(DanmenPixel), "Start")]
        private static void XrayDemosaic(Renderer ___PC00_ute05_moza_ANA, Renderer ___PC00_ute05_moza)
        {
            ___PC00_ute05_moza_ANA.enabled=false;
            ___PC00_ute05_moza.enabled = false;
        }
    }
}
