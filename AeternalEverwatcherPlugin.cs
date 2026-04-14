using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using Silksong.FsmUtil;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeternalEverwatcher;

[BepInAutoPlugin(id: "io.github.astrum-nova.aeternaleverwatcher")]
[BepInDependency("org.silksong-modding.fsmutil")]
public partial class AeternalEverwatcherPlugin : BaseUnityPlugin
{
    private static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("[Aeternal Everwatcher]");
    public static void log(string msg) => logger.LogInfo(msg);
    public static AeternalEverwatcherPlugin Instance { get; set; } = null!;
    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
        Harmony.CreateAndPatchAll(typeof(AeternalEverwatcherPlugin));
        SceneManager.sceneLoaded += SceneLoadSetup;
    }
    private static void SceneLoadSetup(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Equals("Coral_39")) return;
        foreach (var fsm in FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)!.Where(fsm => fsm.name.Equals("Coral Warrior Grey"))) switch (fsm.FsmName)
        {
            case "Stun Control": Destroy(fsm); break;
            case "Control":
                controlFsm = fsm;
                healthManager = fsm.GetComponent<HealthManager>();
                transform = fsm.gameObject.transform;
                foundWatcher = true;
                break;
        }

        SetupWatcher();
    }
    private static PlayMakerFSM controlFsm = null!;
    private static HealthManager healthManager = null!;
    private static Transform transform = null!;
    private static bool foundWatcher;
    private static bool jumpSlashAnticHappened;
    private static void SetupWatcher()
    {
        controlFsm.GetFirstActionOfType<Wait>("Idle")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Range Out Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Emerge Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Dig Out Antic")!.time = 0.3f;
        controlFsm.GetState("Slash Combo Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = GetPosDiffSpeed() * -1);
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = GetPosDiffSpeed() * -1);
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = GetPosDiffSpeed());
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:true));
        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = GetPosDiffSpeed() * -0.75f);
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<FloatMultiply>("Jump Slash Launch")!.multiplyBy = 10);
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => jumpSlashAnticHappened = true);
        controlFsm.GetState("Jump Slash Air")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:jumpSlashAnticHappened));
        controlFsm.GetState("Jump Slash New")!.AddLambdaMethod(_ => jumpSlashAnticHappened = false);
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ => controlFsm.SetState("Uppercut Launch"));
    }

    private static float GetPosDiffSpeed() => Mathf.Clamp(Math.Abs(HeroController.instance.transform.position.x - transform.position.x) * 30, 230, 270);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
    private static void HealthManager_Hit(HealthManager __instance) => __instance.invincible = foundWatcher;
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Invincible))]
    private static void HealthManager_Invincible(HealthManager __instance, HitInstance hitInstance)
    {
        if (!parryableStates.Contains(controlFsm.ActiveStateName)) return;
        if (__instance.gameObject.transform.root.Find("Body Damager").TryGetComponent<DamageHero>(out var damager))
        {
            Instance.StartCoroutine(damager.NailClash(0, "Nail Attack", transform.position));
            GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
        }
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageHero), nameof(DamageHero.NailClash))]
    private static void DamageHero_NailClash(DamageHero __instance)
    {
        log(controlFsm.ActiveStateName);
        if (controlFsm.ActiveStateName is "Uppercut 1" or "Uppercut 2" or "Uppercut 3" or "Dig Out Uppercut") HeroController.instance.StartInvulnerable(0.3f);
        return;
        if (__instance == null) return;
        healthManager.TakeDamage(new HitInstance
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
    private static readonly HashSet<string> parryableStates =
    [
        "Slash Combo 1",
        "Slash Combo 2",
        "Slash Combo 3",
        "Slash Combo 5",
        "Slash Combo 6",
        "Slash Combo 7",
        "Slash Combo 9",
        "Slash Combo 10",
        "F Slash 2",
        "F Slash 3",
        "F Slash 4",
        "Uppercut 1",
        "Uppercut 2",
        "Uppercut 3",
        "Dig Out Uppercut"
    ];

    private void Update()
    {
        //HeroController.instance.MaxHealth();
        //log(controlFsm.ActiveStateName);
    }
}