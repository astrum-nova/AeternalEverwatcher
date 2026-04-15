using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.AssetHelper.ManagedAssets;
using Silksong.FsmUtil;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace AeternalEverwatcher;

[BepInAutoPlugin(id: "io.github.astrum-nova.aeternaleverwatcher")]
[BepInDependency("org.silksong-modding.fsmutil")]
[BepInDependency("org.silksong-modding.assethelper")]
public partial class AeternalEverwatcherPlugin : BaseUnityPlugin
{
    private static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("[Aeternal Everwatcher]");
    public static void log(string msg) => logger.LogInfo(msg);
    public static AeternalEverwatcherPlugin Instance { get; set; } = null!;
    private static ManagedAsset<GameObject> skProjectile;

    private static bool PHASE_2 = true;
    private static bool PHASE_3 = true;
    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
        Harmony.CreateAndPatchAll(typeof(AeternalEverwatcherPlugin));
        SceneManager.sceneLoaded += SceneLoadSetup;
        skProjectile = ManagedAsset<GameObject>.FromNonSceneAsset("Assets/Prefabs/Hornet Enemies/Song Knight Projectile.prefab", "localpoolprefabs_assets_areahangareasong");
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
    private static bool didSlashCombo1;
    private static bool didJumpSlashLaunch;
    private static void SetupWatcher()
    {
        controlFsm.GetFirstActionOfType<Wait>("Idle")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Range Out Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Emerge Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Dig Out Antic")!.time = 0.3f;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Dig Out Uppercut")!.speed = 70;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Uppercut 1")!.speed = 70;
        controlFsm.GetState("Slash Combo Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = GetPosDiffSpeed() * -1);
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = GetPosDiffSpeed() * -1);
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = GetPosDiffSpeed());
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:true));
        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = GetPosDiffSpeed() * -0.75f);
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ => { if (Random.Range(0, 2) == 0) Instance.StartCoroutine(jumpSlashMixup()); });
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<FloatMultiply>("Jump Slash Launch")!.multiplyBy = 8);
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => jumpSlashAnticHappened = true);
        controlFsm.GetState("Jump Slash Air")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:jumpSlashAnticHappened));
        controlFsm.GetState("Jump Slash New")!.AddLambdaMethod(_ => jumpSlashAnticHappened = false);
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ => controlFsm.SetState("Uppercut Launch"));
        controlFsm.GetState("Dash To Jump")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Slash Combo 3")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("F Slash Recover")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(SpawnSandWave()); });
        controlFsm.GetState("Dig Out Uppercut")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(SpawnSandWave()); });
        controlFsm.GetState("Slash Combo 1")!.AddLambdaMethod(_ => { if (PHASE_2) didSlashCombo1 = true; });
        controlFsm.GetState("Jump Slash Launch")!.AddLambdaMethod(_ => { if (PHASE_2) { didJumpSlashLaunch = true; } });
        controlFsm.GetState("Dash To Antic")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Uppercut Antic")!.AddLambdaMethod(_ =>
        {
            if (didJumpSlashLaunch)
            {
                didJumpSlashLaunch = false;
                transform.FlipLocalScale(x:true);
            }
        });
        controlFsm.GetState("Uppercut Antic Q")!.AddLambdaMethod(_ =>
        {
            if (didJumpSlashLaunch)
            {
                didJumpSlashLaunch = false;
                transform.FlipLocalScale(x:true);
            }
        });
        controlFsm.GetState("Slash Combo 12")!.AddLambdaMethod(_ => { didSlashCombo1 = false; });
        controlFsm.GetState("Jump Slash New")!.AddLambdaMethod(_ => { if (PHASE_2 && (didSlashCombo1 || didJumpSlashLaunch)) { Instance.StartCoroutine(SpawnSandWave(transform.localScale.x == 1, transform.localScale.x == -1)); } });
    }
    private static IEnumerator jumpSlashMixup()
    {
        yield return new WaitForSeconds(0.2f);
        controlFsm.SetState("Jump Slash Antic");
    }
    private static IEnumerator spawnSkProjectile()
    {
        yield return skProjectile.Load();
        var instance = skProjectile.InstantiateAsset();
        instance.transform.position = transform.position;
        instance.GetComponent<Collider2D>().isTrigger = true;
        MakeProjectileIgnoreEnvironment(instance);
        RemoveProjectileWallEvents(instance);
        instance.AddComponent<ProjectileMover>();
        instance.transform.SetPositionAndRotation(new Vector3(
            HeroController.instance.transform.position.x + (ObjLeftOfHornet(instance) ? 15 : -15),
            transform.position.y - 1,
            transform.position.z
        ), !ObjLeftOfHornet(instance) ? Quaternion.Euler(0, 180, 0) : Quaternion.Euler(0, 0, 0));
        instance.transform.localScale = new Vector3(1.75f, Random.Range(1.8f, 2.2f), 1);
        instance.transform.SetLocalRotation2D(Random.Range(-10, 10));
        var left1 = Instantiate(sandburst);
        left1.transform.position = new Vector3(HeroController.instance.transform.position.x + (ObjLeftOfHornet(instance) ? -13 : 13), sandburst.transform.position.y, sandburst.transform.position.z);
        left1.SetActive(false);
        left1.SetActive(true);
        yield return new WaitForSeconds(1);
        Destroy(left1);
    }

    private static IEnumerator Teleport(float x, float y, string nextState)
    {
        //! i think it breaks because it has vertical speed as soon as it gets out
        controlFsm.SetState("Jump Away Launch");
        yield return new WaitForSeconds(0.1f);
        controlFsm.SetState("Dig In 1");
        controlFsm.GetFirstActionOfType<SetPosition>("Dig Pos")!.x = x;
        controlFsm.GetFirstActionOfType<SetPosition>("Dig Pos")!.y = y;
        yield return new WaitForSeconds(0.2f);
        controlFsm.SetState("Dig Out 1");
        yield return new WaitForSeconds(0.2f);
        controlFsm.SetState(nextState);
    }
    private static IEnumerator SpawnGroundWave()
    {
        //! find something to give this attack to
        controlFsm.SetState("Jump Away Antic");
        yield return new WaitForSeconds(0.3f);
        for (var i = 20; i > -20; i--)
        {
            yield return new WaitForSeconds(0.05f);
            CreateWave(new Vector3(HeroController.instance.transform.position.x + i * 4, sandburst.transform.position.y - 10, sandburst.transform.position.z));
        }
        controlFsm.SetState("Range Check");
    }

    private static void CreateWave(Vector3 position)
    {
        var wave = Instantiate(sandburst);
        wave.transform.position = position;
        wave.SetActive(false);
        wave.SetActive(true);
    }
    private static GameObject sandburst;
    private static IEnumerator SpawnSandWave(bool left = true, bool right = true)
    {
        yield return new WaitForSeconds(0.2f);
        if (sandburst == null) sandburst = transform.Find("sand_burst_effect_uppercut").gameObject;
        else if (left && right)
        {
            var left1 = Instantiate(sandburst);
            left1.transform.position = new Vector3(sandburst.transform.position.x - 5, sandburst.transform.position.y, sandburst.transform.position.z);
            left1.SetActive(false);
            left1.SetActive(true);
            var right1 = Instantiate(sandburst);
            right1.transform.position = new Vector3(sandburst.transform.position.x + 5, sandburst.transform.position.y, sandburst.transform.position.z);
            right1.SetActive(false);
            right1.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            var left2 = Instantiate(sandburst);
            left2.transform.position = new Vector3(sandburst.transform.position.x - 10, sandburst.transform.position.y, sandburst.transform.position.z);
            left2.SetActive(false);
            left2.SetActive(true);
            var right2 = Instantiate(sandburst);
            right2.transform.position = new Vector3(sandburst.transform.position.x + 10, sandburst.transform.position.y, sandburst.transform.position.z);
            right2.SetActive(false);
            right2.SetActive(true);
            yield return new WaitForSeconds(1);
            Destroy(left1);
            Destroy(left2);
            Destroy(right1);
            Destroy(right2);
        } else if (left)
        {
            var left1 = Instantiate(sandburst);
            left1.transform.position = new Vector3(transform.position.x - 5, sandburst.transform.position.y, sandburst.transform.position.z);
            left1.SetActive(false);
            left1.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            var left2 = Instantiate(sandburst);
            left2.transform.position = new Vector3(transform.position.x - 10, sandburst.transform.position.y, sandburst.transform.position.z);
            left2.SetActive(false);
            left2.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            var left3 = Instantiate(sandburst);
            left3.transform.position = new Vector3(transform.position.x - 15, sandburst.transform.position.y, sandburst.transform.position.z);
            left3.SetActive(false);
            left3.SetActive(true);
            yield return new WaitForSeconds(1);
            Destroy(left1);
            Destroy(left2);
            Destroy(left3);
        } else if (right)
        {
            var right1 = Instantiate(sandburst);
            right1.transform.position = new Vector3(transform.position.x + 5, sandburst.transform.position.y, sandburst.transform.position.z);
            right1.SetActive(false);
            right1.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            var right2 = Instantiate(sandburst);
            right2.transform.position = new Vector3(transform.position.x + 10, sandburst.transform.position.y, sandburst.transform.position.z);
            right2.SetActive(false);
            right2.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            var right3 = Instantiate(sandburst);
            right3.transform.position = new Vector3(transform.position.x + 15, sandburst.transform.position.y, sandburst.transform.position.z);
            right3.SetActive(false);
            right3.SetActive(true);
            yield return new WaitForSeconds(1);
            Destroy(right1);
            Destroy(right2);
            Destroy(right3);
        }
    }

    private static bool ObjLeftOfHornet(GameObject go) => go.transform.position.x < HeroController.instance.transform.position.x;
    private static void MakeProjectileIgnoreEnvironment(GameObject projectile)
    {
        var colliders = projectile.GetComponentsInChildren<Collider2D>(true);
        if (colliders == null || colliders.Length == 0) return;
        foreach (var col in colliders)
        {
            int environmentLayer = LayerMask.NameToLayer("Terrain");
            if (environmentLayer >= 0) Physics2D.IgnoreLayerCollision(projectile.layer, environmentLayer, true);
            col.isTrigger = true;
        }
    }
    private static void RemoveProjectileWallEvents(GameObject projectile)
    {
        var fsm = projectile.LocateMyFSM("Control");
        foreach (var state in fsm.FsmStates)
        {
            var newTransitions = state.Transitions
                .Where(t => !t.EventName.Equals("WALL", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (newTransitions.Length != state.Transitions.Length) state.Transitions = newTransitions;
        }
        foreach (var stateName in new[] { "Wall End", "Floor?" })
        {
            var s = fsm.GetState(stateName);
            if (s != null)
            {
                s.Transitions = Array.Empty<FsmTransition>();
                s.Actions = Array.Empty<FsmStateAction>();
            }
        }
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
        if (iframeStates.Contains(controlFsm.ActiveStateName)) HeroController.instance.StartInvulnerable(0.3f);
        if (!GameManager.instance.TimeSlowed) GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
        
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

    private static readonly HashSet<string> iframeStates =
    [
        "Uppercut 1",
        "Uppercut 2",
        "Uppercut 3",
        "Dig Out Uppercut",
        "Slash Combo 9",
        "Slash Combo 10",
        "Slash Combo 11",
        "Slash Combo 12",
    ];
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
        try
        {
            HeroController.instance.MaxHealth();
        } catch {/*ignored*/}
        //log(controlFsm.ActiveStateName);
    }
}