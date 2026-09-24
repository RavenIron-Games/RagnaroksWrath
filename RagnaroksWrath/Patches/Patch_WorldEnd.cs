using System;
using HarmonyLib;
using RavenIron.RagnaroksWrath.Core;

namespace RavenIron.RagnaroksWrath.Patches
{
    /// <summary>
    /// The end of a world: flush and forget it (WorldTick.EndWorld).
    ///
    /// ZNet lives in the game scene and is destroyed on every logout to the main menu and on
    /// shutdown; our WorldTick is not (DontDestroyOnLoad), so this is where a world actually ends
    /// for us. A prefix, so it runs while ZNet.instance still names this ZNet — and `ZNet.m_world`
    /// and `m_isServer` are static and still describe the closing world (decompiled 1.0.15:
    /// OnDestroy clears only m_instance), so the final save lands in the right files.
    ///
    /// OnDestroy is private in the real assembly, hence the name string. Observing only: void
    /// prefix, vanilla always runs. WorldTick.Update backs this up if the patch ever fails to bind.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    public static class Patch_WorldEnd
    {
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(ZNet __instance)
        {
            try
            {
                if (!ReferenceEquals(__instance, ZNet.instance)) return;
                WorldTick.EndWorld("ZNet.OnDestroy");
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError($"World-end hook failed: {ex}");
            }
        }
    }
}
