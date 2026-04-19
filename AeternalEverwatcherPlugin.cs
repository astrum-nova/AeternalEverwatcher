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
    public static bool PHASE_3 = true;
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
    public static bool eigongAirDashing;
    public static bool quadSlashing;

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
                    fsm.Preprocess();
                    controlFsm = fsm;
                    healthManager = fsm.GetComponent<HealthManager>();
                    transform = fsm.gameObject.transform;
                    foundWatcher = true;
                    break;
            }
            /*HeroController.instance.OnTakenDamage += () =>
            {
                if (!StateData.undergroundStates.Contains(controlFsm.ActiveStateName) && !pcrSlamming && !eigongAirDashing)
                {
                    controlFsm.SetState("Dig In 1");
                    ResetFlags();
                }
            };*/
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
        eigongAirDashing = false;
    }
    private static void SetupWatcher()
    {
        // --- Initialization & Intro (Top Center) ---
        controlFsm.GetFirstActionOfType<StartRoarEmitter>("Wake Roar 2")!.stunHero = false;
        controlFsm.GetFirstActionOfType<Wait>("Wake Roar 2")!.time = 0.5f;
        controlFsm.GetFirstActionOfType<Wait>("Emerge Pause")!.time = 0;

        // --- Neutral & Range Checking (Middle Section) ---
        controlFsm.GetFirstActionOfType<Wait>("Idle")!.time = 0;
        controlFsm.GetState("Very Far")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()));
        controlFsm.GetFirstActionOfType<Wait>("Range Out Pause")!.time = 0;

        // --- Slash Combo Branch (Left Side) ---
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Slash Combo Antic Q")!.everyFrame = true;
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.45f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });

        controlFsm.GetFirstActionOfType<FaceObjectV2>("Slash Combo Antic")!.everyFrame = true;
        controlFsm.GetState("Slash Combo Antic")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.55f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });

        controlFsm.GetState("Slash Combo 1")!.AddLambdaMethod(_ => { if (PHASE_2 && !fiveSLash && !quadSlashing) didSlashCombo1 = true; });
        controlFsm.GetState("Slash Combo 3")!.AddLambdaMethod(_ => { if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile()); });
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = Helpers.GetPosDiffSpeed() * (fiveSLash ? -1 : 1));
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:!fiveSLash && !quadSlashing));

        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ =>
        {
            if (fiveSLash && !pcrSlamming && !eigongAirDashing)
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
            if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile());
        });

        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = Helpers.GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("Slash Combo 11")!.AddLambdaMethod(_ =>
        {
            if (CustomBehaviour.sandburstSmall == null)
            {
                CustomBehaviour.sandburstSmall = transform.Find("Pt SwordSlam").gameObject;
                Helpers.SandColorSetup(CustomBehaviour.sandburstSmall);
            }
            if (PHASE_2 && didSlashCombo1)
            {
                Instance.StartCoroutine(CustomBehaviour.SpawnSandWave(transform.localScale.x == 1, transform.localScale.x == -1));
            }
            ResetFlags();
        });
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));

        // --- F Slash Branch (Between Left and Center) ---
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ =>
        {
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.ySpeed = eigongAirDashing ? -50 : 0;
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = Helpers.GetPosDiffSpeed() * -0.75f * (eigongAirDashing ? 0.75f : 1) * (quadSlashing ? 0 : 1);
            if (eigongAirDashing || quadSlashing) return;
            var num = 0; // Random.Range(0, 3);
            if (num == 0 && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.QuadWindSlash()); 
            else if (num == 1 && !eigongAirDashing) Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()); 
        });
        controlFsm.GetState("F Slash Recover")!.AddLambdaMethod(_ =>
        {
            if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.spawnSkProjectile());
            else if (PHASE_3 && eigongAirDashing) controlFsm.SetState("F Slash Antic");
        });

        // --- Jump Slash / Dash Branch (Center / Right Center) ---
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Dash To Jump")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Dash To Antic")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ =>
        {
            if (!quadSlashing) return;
            Instance.StartCoroutine(CustomBehaviour.QuadWindSlash());
        });

        // --- Digging & Uppercut Branch (Far Right) ---
        controlFsm.GetFirstActionOfType<Wait>("Dig Out Antic")!.time = 0.3f;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Dig Out Uppercut")!.speed = 120;

        controlFsm.GetFirstActionOfType<FaceObjectV2>("Uppercut Antic")!.everyFrame = true;
        controlFsm.GetState("Uppercut Antic")!.AddLambdaMethod(_ =>
        {
            var randy = Random.Range(0, 3);
            if (randy == 0)
            {
                fiveSLash = true;
                fiveSLashedOnce = false;
                controlFsm.SetState("Slash Combo Antic Q");
            } else if (randy == 1) controlFsm.SetState("Evade Antic");
        });

        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Uppercut 1")!.speed = 70;
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ =>
        {
            if (!CustomBehaviour.sandburst) Helpers.InitSandEffects();
            if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.SpawnSandWave());
            if (!pcrSlamming) Instance.StartCoroutine(CustomBehaviour.PCRSlams());
        });
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ => controlFsm.SetState("Uppercut Launch"));
    }
    private void Update()
    {
        try
        {
            HeroController.instance.MaxHealth();
        } catch {/*ignored*/}
    }
}