using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
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
    private const float SANDBURST_DEFAULT_Y = 6.552498f;
    private const float WATCHER_GROUND_Y = 8.3275f;

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
        HeroController.instance.OnTakenDamage += () =>
        {
            if (!undergroundStates.Contains(controlFsm.ActiveStateName)) controlFsm.SetState("Dig In 1");
            ResetFlags();
        };
        SetupWatcher();
    }

    private static readonly HashSet<string> undergroundStates =
    [
        "Dig In 1",
        "Dig In 2",
        "Away",
        "Hiding",
        "Emerge Pause",
        "Dig Pos",
        "Dig Out Antic",
        "Dig Out 1"
    ];

    private static void ResetFlags()
    {
        didSlashCombo1 = false;
        didJumpSlashLaunch = false;
        jumpSlashAnticHappened = false;
        fiveSLash = false;
        fiveSLashedOnce = false;
    }
    private static PlayMakerFSM controlFsm = null!;
    private static HealthManager healthManager = null!;
    private new static Transform transform = null!;
    private static bool foundWatcher;
    private static bool jumpSlashAnticHappened;
    private static bool didSlashCombo1;
    private static bool didJumpSlashLaunch;
    private static bool fiveSLash;
    private static bool fiveSLashedOnce;
    private static void SetupWatcher()
    {
        controlFsm.GetFirstActionOfType<Wait>("Dig Out Antic")!.time = 0.3f;
        controlFsm.GetFirstActionOfType<StartRoarEmitter>("Wake Roar 2")!.stunHero = false;
        controlFsm.GetFirstActionOfType<Wait>("Wake Roar 2")!.time = 0.5f;
        controlFsm.GetFirstActionOfType<Wait>("Idle")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Range Out Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<Wait>("Emerge Pause")!.time = 0;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Dig Out Uppercut")!.speed = 120;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Uppercut 1")!.speed = 70;
        controlFsm.GetState("Slash Combo Antic")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(FinishStateEarly("FINISHED", 0.55f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : GetPosDiffSpeed() * -1;
        });
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(FinishStateEarly("FINISHED", 0.45f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : GetPosDiffSpeed() * -1;
        });
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = GetPosDiffSpeed() * (fiveSLash ? -1 : 1));
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:!fiveSLash));
        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ =>
        {
            if (fiveSLash)
            {
                if (!fiveSLashedOnce)
                {
                    Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + 3.5f * transform.localScale.x * -1, transform.position.y, "Slash Combo Antic Q"));
                    fiveSLashedOnce = true;
                }
                else
                {
                    Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + 5f * transform.localScale.x, transform.position.y, "Slash Combo 8"));
                    fiveSLashedOnce = false;
                }
            }
        });
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = GetPosDiffSpeed() * -0.75f);
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ =>
        {
            var num = Random.Range(0, 3);
            if (num == 0) Instance.StartCoroutine(jumpSlashMixup()); 
            else if (num == 1) Instance.StartCoroutine(SpawnGroundWave());
        });
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<FloatMultiply>("Jump Slash Launch")!.multiplyBy = 8);
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => jumpSlashAnticHappened = true);
        controlFsm.GetState("Jump Slash Air")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:jumpSlashAnticHappened));
        controlFsm.GetState("Jump Slash New")!.AddLambdaMethod(_ => jumpSlashAnticHappened = false);
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ => controlFsm.SetState("Uppercut Launch"));
        controlFsm.GetState("Dash To Jump")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Very Far")!.AddLambdaMethod(_ => Instance.StartCoroutine(SpawnGroundWave()));
        controlFsm.GetState("Slash Combo 3")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("F Slash Recover")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(spawnSkProjectile()); });
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ =>
        {
            if (!sandburst)
            {
                var sandburstOriginal = GameObject.Find("sand_burst_effect_uppercut");
                SandColorSetup(sandburstOriginal);
                sandburst = Instantiate(sandburstOriginal);
                sandburst.SetActive(false);
                SandSpeedSetup(sandburst, 3);
                groundWave = Instantiate(sandburst);
                groundWave.SetActive(false);
                SandColorSetup(groundWave);
                SandSpeedSetup(groundWave);
                var damagerHitboxOriginal = groundWave.transform.FindRelativeTransformWithPath("damager", false).GetComponent<PolygonCollider2D>();
                Vector2[] points = [ new(-10, 3), new(10, 3), new(-10, 0), new(10, 0) ];
                damagerHitboxOriginal.SetPath(0, points);
                foreach (var pt in groundWave.GetComponentsInChildren<ParticleSystem>(true))
                {
                    // for some FUCKASS reason i need to get a ref to the shape before changing its scale wtff???
                    var shape = pt.shape;
                    shape.scale = new Vector3(6, 0.1f, 1);
                    var emission = pt.emission;
                    var bursts = new ParticleSystem.Burst[emission.burstCount];
                    emission.GetBursts(bursts);
                    for (var i = 0; i < bursts.Length; i++)
                    {
                        bursts[i].minCount *= 20;
                        bursts[i].maxCount *= 20;
                    }
                    emission.SetBursts(bursts);
                    var main = pt.main;
                    main.maxParticles = 1000;
                }
            }
            if (PHASE_2) Instance.StartCoroutine(SpawnSandWave());
        });
        controlFsm.GetState("Dig Out Uppercut")!.AddLambdaMethod(_ =>
        {
            if (sandburst == null) Instance.StartCoroutine(SpawnSandWave());
        });
        controlFsm.GetState("Slash Combo 1")!.AddLambdaMethod(_ => { if (PHASE_2 && !fiveSLash) didSlashCombo1 = true; });
        controlFsm.GetState("Jump Slash Launch")!.AddLambdaMethod(_ => { if (PHASE_2) { didJumpSlashLaunch = true; } });
        controlFsm.GetState("Dash To Antic")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Uppercut Antic")!.everyFrame = true;
        controlFsm.GetState("Slash Combo 11")!.AddLambdaMethod(_ =>
        {
            if (sandburstSmall == null)
            {
                sandburstSmall = transform.Find("Pt SwordSlam").gameObject;
                SandColorSetup(sandburstSmall);
            }
            if (PHASE_2 && (didSlashCombo1 || didJumpSlashLaunch))
            {
                Instance.StartCoroutine(SpawnSandWave(transform.localScale.x == 1, transform.localScale.x == -1));
            }
            ResetFlags();
        });
        controlFsm.GetState("Uppercut Antic")!.AddLambdaMethod(_ =>
        {
            if (Random.Range(0, 3) == 0)
            {
                fiveSLash = true;
                fiveSLashedOnce = false;
                controlFsm.SetState("Slash Combo Antic Q");
            }
        });
    }
    private static void SandColorSetup(GameObject wave)
    {
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (pt.name is "sand_blown" or "particles_small") continue;
            var main = pt.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            var renderer = pt.GetComponent<ParticleSystemRenderer>();
            if (renderer && renderer.material)
            {
                renderer.material.color = Color.white;
                renderer.material.SetColor(Color1, new Color(2.5f, 2.5f, 2.5f, 1f));
            }
        }
    }
    private static void SandSpeedSetup(GameObject wave, float velLimit = 2.0f)
    {
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            var limitVelocity = pt.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = velLimit;
            limitVelocity.dampen = 0.5f;
        }
    }
    private static IEnumerator FinishStateEarly(string eventName, float delay)
    {
        yield return new WaitForSeconds(delay);
        controlFsm.SendEvent(eventName);
    }
    private static IEnumerator jumpSlashMixup()
    {
        //TODO: experiment with shortening the delay, right now hes telegraphing an F slash and randomly switching to this
        // which is a bit confusing
        yield return new WaitForSeconds(0.2f);
        controlFsm.SetState("Jump Slash Antic");
    }
    private static GameObject skProjectileSetup = null!;
    private static IEnumerator spawnSkProjectile()
    {
        if (fiveSLash) yield break;
        if (!skProjectileSetup)
        {
            yield return skProjectile.Load();
            skProjectileSetup = skProjectile.InstantiateAsset();
            skProjectileSetup.GetComponent<Collider2D>().isTrigger = true;
            MakeProjectileIgnoreEnvironment(skProjectileSetup);
            RemoveProjectileWallEvents(skProjectileSetup);
            skProjectileSetup.AddComponent<ProjectileMover>();
            skProjectileSetup.SetActive(false);
            skProjectileSetup.transform.position = new Vector3(0, 500, 0);
        }
        var instance = Instantiate(skProjectileSetup);
        instance.SetActive(true);
        SetProjetilePosition(instance);
        yield return new WaitForSeconds(1);
        Destroy(instance);
    }
    private static void SetProjetilePosition(GameObject instance)
    {
        instance.transform.position = transform.position;
        instance.transform.SetPositionAndRotation(new Vector3(
            HeroController.instance.transform.position.x + (ObjLeftOfHornet(instance) ? 15 : -15),
            transform.position.y - 1,
            transform.position.z
        ), !ObjLeftOfHornet(instance) ? Quaternion.Euler(0, 180, 0) : Quaternion.Euler(0, 0, 0));
        instance.transform.localScale = new Vector3(1.75f, Random.Range(1.8f, 2.2f), 1);
        instance.transform.SetLocalRotation2D(Random.Range(-10, 10));
        /*Instance.StartCoroutine(CreateWave(new Vector3(
            HeroController.instance.transform.position.x + (ObjLeftOfHornet(instance) ? -13 : 13),
            sandburst.transform.position.y,
            sandburst.transform.position.z)));*/
    }
    private static IEnumerator Teleport(float x, float y, string? nextState = null, bool flipX = false)
    {
        var waveOld = CreateWave(sandburstSmall, new Vector3(transform.position.x, transform.position.y - 1.5f, sandburst.transform.position.z));
        yield return new WaitForSeconds(0.05f);
        transform.position = new Vector3(x, y, transform.position.z);
        var waveNew = CreateWave(sandburstSmall, new Vector3(transform.position.x, transform.position.y - 1.5f, sandburst.transform.position.z));
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = 0;
        if (nextState != null) controlFsm.SetState(nextState);
        if (flipX) transform.FlipLocalScale(x:true);
        yield return new WaitForSeconds(1f);
        Destroy(waveOld);
        Destroy(waveNew);
    }
    private static IEnumerator SpawnGroundWave()
    {
        ResetFlags();
        controlFsm.SetState("Wake Roar 2");
        yield return new WaitForSeconds(0.5f);
        controlFsm.SetState("Dig In 1");
        CreateWave(groundWave, new Vector3(HeroController.instance.transform.position.x, SANDBURST_DEFAULT_Y, sandburst.transform.position.z), delayToDestruction: 3, rotation:false);
    }
    private static GameObject CreateWave(GameObject go, Vector3 position, float delayToDestruction = 1, bool rotation = true)
    {
        var wave = Instantiate(go);
        wave.transform.position = position;
        if (rotation) wave.transform.SetRotation2D(Random.Range(-5, 5));
        wave.SetActive(false);
        wave.SetActive(true);
        Instance.StartCoroutine(DestroyLater(wave, delayToDestruction));
        return wave;
    }
    private static IEnumerator DestroyLater(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(go);
    }
    private static GameObject groundWave = null!;
    private static GameObject sandburst = null!;
    private static GameObject sandburstSmall = null!;
    private static IEnumerator SpawnSandWave(bool left = true, bool right = true)
    {
        var originObject = transform.FindRelativeTransformWithPath("sand_burst_effect_uppercut_origin", false)!;
        yield return new WaitForSeconds(0.2f);
        if (left && right)
        {
            CreateWave(sandburst, new Vector3(originObject.position.x + 6 * transform.localScale.x * -1, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(originObject.position.x + 10 * transform.localScale.x * -1, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(originObject.position.x + 14 * transform.localScale.x * -1, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
        } else if (left)
        {
            CreateWave(sandburst, new Vector3(transform.position.x - 5, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(transform.position.x - 10, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(transform.position.x - 15, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
        } else if (right)
        {
            CreateWave(sandburst, new Vector3(transform.position.x + 5, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(transform.position.x + 10, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(transform.position.x + 15, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
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
                s.Transitions = [];
                s.Actions = [];
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

    private static readonly int Color1 = Shader.PropertyToID("_Color");

    private void Update()
    {
        try
        {
            HeroController.instance.MaxHealth();
        } catch {/*ignored*/}
    }
}