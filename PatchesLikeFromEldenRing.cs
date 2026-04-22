using System;
using System.Collections;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace AeternalEverwatcher;

[HarmonyPatch]
public class PatchesLikeFromEldenRing
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
    private static void HealthManager_Hit(HealthManager __instance)
    {
        if (!AeternalEverwatcherPlugin.foundWatcher) return;
        __instance.invincible = true;
    }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Invincible))]
    private static void HealthManager_Invincible(HealthManager __instance, HitInstance hitInstance)
    {
        if (!AeternalEverwatcherPlugin.foundWatcher) return;
        if (!StateData.parryableStates.Contains(AeternalEverwatcherPlugin.controlFsm.ActiveStateName)) return;
        if (!__instance.gameObject.transform.root.Find("Body Damager").TryGetComponent<DamageHero>(out var damager)) return;
        AeternalEverwatcherPlugin.Instance.StartCoroutine(damager.NailClash(0, "Nail Attack", AeternalEverwatcherPlugin.transform.position));
        GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
    }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.FreezeMoment), typeof(FreezeMomentTypes), typeof(Action))]
    private static bool GameManager_FreezeMoment(GameManager __instance, FreezeMomentTypes type)
    {
        if (!AeternalEverwatcherPlugin.foundWatcher || type != FreezeMomentTypes.NailClashEffect) return true;
        if (Settings.PARRY_TIME_FREEZE >= 0 && Settings.PARRY_TIME_FREEZE <= 0.261f)
        {
            AeternalEverwatcherPlugin.Instance.StartCoroutine(GameManager.instance.FreezeMoment(0.01f, Settings.PARRY_TIME_FREEZE - 0.11f, 0.1f, 0f));
            return false;
        }
        return true;
        //todo: remove the thunk freeze
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageHero), nameof(DamageHero.NailClash))]
    private static void DamageHero_NailClash(DamageHero __instance)
    {
        if (!AeternalEverwatcherPlugin.foundWatcher) return;
        AeternalEverwatcherPlugin.parryCounter++;
        if (StateData.iframeStates.Contains(AeternalEverwatcherPlugin.controlFsm.ActiveStateName)) HeroController.instance.StartInvulnerable(0.3f);
        if (!GameManager.instance.TimeSlowed) GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
        AeternalEverwatcherPlugin.healthManager.SpriteFlash.flashArmoured();
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