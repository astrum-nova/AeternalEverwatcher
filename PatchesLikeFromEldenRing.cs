using GlobalEnums;
using HarmonyLib;

namespace AeternalEverwatcher;

[HarmonyPatch]
public class PatchesLikeFromEldenRing
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
    private static void HealthManager_Hit(HealthManager __instance) => __instance.invincible = AeternalEverwatcherPlugin.foundWatcher;
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Invincible))]
    private static void HealthManager_Invincible(HealthManager __instance, HitInstance hitInstance)
    {
        if (!StateData.parryableStates.Contains(AeternalEverwatcherPlugin.controlFsm.ActiveStateName)) return;
        if (!__instance.gameObject.transform.root.Find("Body Damager").TryGetComponent<DamageHero>(out var damager)) return;
        AeternalEverwatcherPlugin.Instance.StartCoroutine(damager.NailClash(0, "Nail Attack", AeternalEverwatcherPlugin.transform.position));
        GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageHero), nameof(DamageHero.NailClash))]
    private static void DamageHero_NailClash(DamageHero __instance)
    {
        if (StateData.iframeStates.Contains(AeternalEverwatcherPlugin.controlFsm.ActiveStateName)) HeroController.instance.StartInvulnerable(0.3f);
        if (!GameManager.instance.TimeSlowed) GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
        return;
        if (__instance == null) return;
        AeternalEverwatcherPlugin.healthManager.TakeDamage(new HitInstance
        {
            Source = HeroController.instance.gameObject,
            AttackType = AttackTypes.Nail,
            DamageDealt = PlayerData.instance.nailDamage,
            Direction = 270,
            Multiplier = 1f,
            MagnitudeMultiplier = 1f,
            IgnoreInvulnerable = true,
            IsNailTag = true
        });
    }

}