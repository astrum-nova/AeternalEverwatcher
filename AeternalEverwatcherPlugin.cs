using System.Linq;
using BepInEx;
using BepInEx.Logging;
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

    private static bool PHASE_2 = true;
    private static bool PHASE_3 = true;
    public static PlayMakerFSM controlFsm = null!;
    public static HealthManager healthManager = null!;
    public new static Transform transform = null!;
    public static bool foundWatcher;
    private static bool jumpSlashAnticHappened;
    private static bool didSlashCombo1;
    private static bool didJumpSlashLaunch;
    public static bool fiveSLash;
    private static bool fiveSLashedOnce;
    public static bool pcrSlamming;

    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
        Harmony.CreateAndPatchAll(typeof(PatchesLikeFromEldenRing));
        SceneManager.sceneLoaded += (scene, _) =>
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
                if (!StateData.undergroundStates.Contains(controlFsm.ActiveStateName) && !pcrSlamming) controlFsm.SetState("Dig In 1");
                ResetFlags();
            };
            ResetFlags();
            SetupWatcher();
        };
        CustomBehaviour.skProjectile = ManagedAsset<GameObject>.FromNonSceneAsset("Assets/Prefabs/Hornet Enemies/Song Knight Projectile.prefab", "localpoolprefabs_assets_areahangareasong");
    }
    public static void ResetFlags()
    {
        didSlashCombo1 = false;
        didJumpSlashLaunch = false;
        jumpSlashAnticHappened = false;
        fiveSLash = false;
        fiveSLashedOnce = false;
    }
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
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.55f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.45f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = Helpers.GetPosDiffSpeed() * (fiveSLash ? -1 : 1));
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:!fiveSLash));
        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = Helpers.GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ =>
        {
            if (fiveSLash)
            {
                if (!fiveSLashedOnce)
                {
                    Instance.StartCoroutine(CustomBehaviour.Teleport(HeroController.instance.transform.position.x + 3.5f * transform.localScale.x * -1, transform.position.y, "Slash Combo Antic Q"));
                    fiveSLashedOnce = true;
                }
                else
                {
                    Instance.StartCoroutine(CustomBehaviour.Teleport(HeroController.instance.transform.position.x + 5f * transform.localScale.x, transform.position.y, "Slash Combo 8"));
                    fiveSLashedOnce = false;
                }
            }
        });
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = Helpers.GetPosDiffSpeed() * -0.75f);
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ =>
        {
            var num = Random.Range(0, 3);
            if (num == 0) Instance.StartCoroutine(CustomBehaviour.jumpSlashMixup()); 
            else if (num == 1) Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave());
        });
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<FloatMultiply>("Jump Slash Launch")!.multiplyBy = 8);
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ => jumpSlashAnticHappened = true);
        controlFsm.GetState("Jump Slash Air")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:jumpSlashAnticHappened));
        controlFsm.GetState("Jump Slash New")!.AddLambdaMethod(_ => jumpSlashAnticHappened = false);
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ => controlFsm.SetState("Uppercut Launch"));
        controlFsm.GetState("Dash To Jump")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Very Far")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()));
        controlFsm.GetState("Slash Combo 3")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile()); });
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile()); });
        controlFsm.GetState("F Slash Recover")!.AddLambdaMethod(_ => { if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile()); });
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ =>
        {
            if (!CustomBehaviour.sandburst)
            {
                var sandburstOriginal = GameObject.Find("sand_burst_effect_uppercut");
                Helpers.SandColorSetup(sandburstOriginal);
                CustomBehaviour.sandburst = Instantiate(sandburstOriginal);
                CustomBehaviour.sandburst.SetActive(false);
                Helpers.SandSpeedSetup(CustomBehaviour.sandburst, 3);
                CustomBehaviour.groundWave = Instantiate(CustomBehaviour.sandburst);
                CustomBehaviour.groundWave.SetActive(false);
                Helpers.SandColorSetup(CustomBehaviour.groundWave);
                var damagerHitboxOriginal = CustomBehaviour.groundWave.transform.FindRelativeTransformWithPath("damager", false).GetComponent<PolygonCollider2D>();
                Vector2[] points = [ new(-10, 3), new(10, 3), new(-10, 0), new(10, 0) ];
                damagerHitboxOriginal.SetPath(0, points);
                foreach (var pt in CustomBehaviour.groundWave.GetComponentsInChildren<ParticleSystem>(true))
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
                CustomBehaviour.pcrBurst = Instantiate(CustomBehaviour.groundWave);
                CustomBehaviour.pcrBurst.SetActive(false);
                Helpers.SandSpeedSetup(CustomBehaviour.groundWave);
            }
            if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.SpawnSandWave());
        });
        controlFsm.GetState("Dig Out Uppercut")!.AddLambdaMethod(_ =>
        {
            if (CustomBehaviour.sandburst == null) Instance.StartCoroutine(CustomBehaviour.SpawnSandWave());
        });
        controlFsm.GetState("Slash Combo 1")!.AddLambdaMethod(_ => { if (PHASE_2 && !fiveSLash) didSlashCombo1 = true; });
        controlFsm.GetState("Jump Slash Launch")!.AddLambdaMethod(_ => { if (PHASE_2) { didJumpSlashLaunch = true; } });
        controlFsm.GetState("Dash To Antic")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Uppercut Antic")!.everyFrame = true;
        controlFsm.GetState("Slash Combo 11")!.AddLambdaMethod(_ =>
        {
            if (CustomBehaviour.sandburstSmall == null)
            {
                CustomBehaviour.sandburstSmall = transform.Find("Pt SwordSlam").gameObject;
                Helpers.SandColorSetup(CustomBehaviour.sandburstSmall);
            }
            if (PHASE_2 && (didSlashCombo1 || didJumpSlashLaunch))
            {
                Instance.StartCoroutine(CustomBehaviour.SpawnSandWave(transform.localScale.x == 1, transform.localScale.x == -1));
            }
            ResetFlags();
        });
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ => { if (!pcrSlamming) Instance.StartCoroutine(CustomBehaviour.PCRSlams()); });
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
    private void Update()
    {
        try
        {
            HeroController.instance.MaxHealth();
        } catch {/*ignored*/}
    }
}