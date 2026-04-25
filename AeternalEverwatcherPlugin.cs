using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
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
    //! DEBUG !\\
    //! private static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("[Aeternal Everwatcher]");
    //! public static void log(string msg) => logger.LogInfo(msg);
    //! DEBUG !\\
    public static AeternalEverwatcherPlugin Instance { get; private set; } = null!;
    public static bool PHASE_2;
    public static bool PHASE_3;
    public static int parryCounter;
    public static PlayMakerFSM controlFsm = null!;
    public static HealthManager healthManager = null!;
    public new static Transform transform = null!;
    public static bool foundWatcher;
    private static bool didSlashCombo1;
    public static bool fiveSLash;
    private static bool fiveSLashedOnce;
    public static bool pcrSlamming;
    public static bool eigongAirDashing;
    public static bool quadSlashing;
    public static bool tookDamage;
    private static bool sandburstOutSetup;
    public IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        Harmony.CreateAndPatchAll(typeof(BossTitlePatch));
    }
    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
        Harmony.CreateAndPatchAll(typeof(PatchesLikeFromEldenRing));
        Settings.SetupSettings(Config);
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (!scene.name.Equals("Coral_39")) return;
            if (Settings.DISABLE_WIND_EFFECTS) GameObject.Find("wind_effects").SetActive(false);
            PlayerData.instance.wokeGreyWarrior = false;
            PlayerData.instance.defeatedGreyWarrior = false;
            foundWatcher = false;
            foreach (var fsm in FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)!.Where(fsm => fsm.name.Equals("Coral Warrior Grey")))
                switch (fsm.FsmName)
                {
                    case "Stun Control": Destroy(fsm); break;
                    case "Control":
                        controlFsm = fsm;
                        healthManager = fsm.GetComponent<HealthManager>();
                        healthManager.TookDamage += () => healthManager.HealToMax();
                        transform = fsm.gameObject.transform;
                        transform.gameObject.GetComponentInChildren<tk2dSprite>().renderLayer = 200;
                        foreach (var componentsInChild in HeroController.instance.GetComponentsInChildren<tk2dSprite>()) componentsInChild.renderLayer = 500;
                        foundWatcher = true;
                        break;
                }
            HeroController.instance.OnTakenDamage += () =>
            {
                tookDamage = true;
                if (!Settings.ON_DAMAGE_FREEZE) return;
                if (Settings.PARRY_TIME_FREEZE >= 0 && Settings.PARRY_TIME_FREEZE <= 0.261f) Instance.StartCoroutine(GameManager.instance.FreezeMoment(0.01f, Settings.PARRY_TIME_FREEZE - 0.11f, 0.1f, 0f));
                else GameManager.instance.FreezeMoment(FreezeMomentTypes.NailClashEffect);
            };
            ResetFlags();
            SetupWatcher();
            PHASE_2 = false;
            PHASE_3 = false;
            parryCounter = 0;
            didSlashCombo1 = false;
            fiveSLash = false;
            fiveSLashedOnce = false;
            pcrSlamming = false;
            eigongAirDashing = false;
            quadSlashing = false;
            sandburstOutSetup = false;
            Pools.Clear();
        };
        CustomBehaviour.skProjectile = ManagedAsset<GameObject>.FromNonSceneAsset("Assets/Prefabs/Hornet Enemies/Song Knight Projectile.prefab", "localpoolprefabs_assets_areahangareasong");
        CustomBehaviour.khannUcSpear = ManagedAsset<GameObject>.FromSceneAsset("memory_coral_tower", "Boss Scene/Uppercut Spear");
    }
    public static void ResetFlags()
    {
        didSlashCombo1 = false;
        fiveSLash = false;
        fiveSLashedOnce = false;
        eigongAirDashing = false;
        tookDamage = false;
    }
    private static bool PhaseCheck()
    {
        if (fiveSLash || eigongAirDashing || pcrSlamming || quadSlashing) return false;
        if (parryCounter >= Settings.PHASE_2_QUOTA && !PHASE_2 && CustomBehaviour.groundWave && CustomBehaviour.pcrBurst && CustomBehaviour.sandburst && CustomBehaviour.sandburstSmall)
        {
            parryCounter = Settings.PHASE_2_QUOTA;
            ResetFlags();
            PHASE_2 = true;
            controlFsm.SetState("Stun Start");
            return true;
        }
        if (parryCounter < Settings.PHASE_3_QUOTA || PHASE_3 || !PHASE_2) return false;
        parryCounter = Settings.PHASE_3_QUOTA;
        ResetFlags();
        PHASE_3 = true;
        controlFsm.SetState("Stun Start");
        return true;
    }
    private static void SetupWatcher()
    {
        //* Initialization
        controlFsm.GetFirstActionOfType<CheckHeroPerformanceRegion>("Sleep")!.MinReactDelay = 1;
        controlFsm.GetFirstActionOfType<CheckHeroPerformanceRegion>("Sleep")!.MaxReactDelay = 1;
        controlFsm.GetState("Sleep")!.AddLambdaMethod(_ => transform.position = transform.position with { x = transform.position.x + 15 });
        controlFsm.GetState("Wake Roar 2")!.AddLambdaMethod(_ =>
        {
            if (sandburstOutSetup) return;
            controlFsm.RemoveActionsOfType<DisplayBossTitle>("Wake Roar 2");
            Helpers.SandColorSetup(controlFsm.GetFirstActionOfType<ActivateGameObject>("Wake Roar 2")!.gameObject.GameObject.Value, "sandburstOut");
            sandburstOutSetup = true;
        });
        controlFsm.GetState("Wake Antic")!.AddLambdaMethod(_ =>
        {
            Instance.StartCoroutine(CustomBehaviour.ArenaBorders(true));
            Helpers.ModifyTerrain();
        });
        controlFsm.GetState("Stun Recover")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()));
        controlFsm.GetState("Stunned")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.StunSpears()));
        controlFsm.GetState("Stunned")!.AddAction(new ObjectJitter
        {
            gameObject = new FsmOwnerDefault
            {
                ownerOption = OwnerDefaultOption.UseOwner,
                OwnerOption = OwnerDefaultOption.UseOwner,
                gameObject = transform.gameObject,
                GameObject = transform.gameObject
            },
            x = 0.2f,
            y = 0.2f,
            z = 0,
            allowMovement = false,
            limitFps = 30,
        });
        controlFsm.GetFirstActionOfType<StartRoarEmitter>("Wake Roar 2")!.stunHero = false;
        controlFsm.GetFirstActionOfType<Wait>("Wake Roar 2")!.time = 0.5f;
        controlFsm.GetFirstActionOfType<Wait>("Emerge Pause")!.time = 0;
        //* Neutral
        controlFsm.GetFirstActionOfType<Wait>("Init Idle")!.time = 0.3f;
        controlFsm.GetFirstActionOfType<Wait>("Idle")!.time = 0;
        controlFsm.GetState("Very Far")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()));
        controlFsm.GetFirstActionOfType<Wait>("Range Out Pause")!.time = 0;
        //* Slash Combo
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Slash Combo Antic Q")!.everyFrame = true;
        controlFsm.GetState("Slash Combo Antic Q")!.AddLambdaMethod(_ =>
        {
            if (PhaseCheck()) return;
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.45f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Slash Combo Antic")!.everyFrame = true;
        controlFsm.GetState("Slash Combo Antic")!.AddLambdaMethod(_ =>
        {
            if (!quadSlashing && PhaseCheck()) return;
            Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.55f));
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = fiveSLash ? 0 : Helpers.GetPosDiffSpeed() * -1;
        });
        controlFsm.GetState("Slash Combo 1")!.AddLambdaMethod(_ => { if (PHASE_2 && !fiveSLash && !quadSlashing) didSlashCombo1 = true; });
        controlFsm.GetState("Slash Combo 3")!.AddLambdaMethod(_ => { if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.SpawnSkProjectile()); });
        controlFsm.GetState("Slash Combo 4")!.AddLambdaMethod(_ => { controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 5")!.speed = Helpers.GetPosDiffSpeed() * (fiveSLash ? -1 : 1); });
        controlFsm.GetState("Slash Combo 5")!.AddLambdaMethod(_ => transform.FlipLocalScale(x:!fiveSLash && !quadSlashing));
        controlFsm.GetState("Slash Combo 7")!.AddLambdaMethod(_ =>
        {
            if (PHASE_3)
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
            }
            if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.SpawnSkProjectile());
        });
        controlFsm.GetState("Switchup 2")!.AddLambdaMethod(_ => controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = Helpers.GetPosDiffSpeed() * -0.5f);
        controlFsm.GetState("Slash Combo 11")!.AddLambdaMethod(_ =>
        {
            if (CustomBehaviour.sandburstSmall == null)
            {
                CustomBehaviour.sandburstSmall = transform.Find("Pt SwordSlam").gameObject;
                Helpers.SandColorSetup(CustomBehaviour.sandburstSmall, "sandburstSmall");
            }
            if (PHASE_2 && didSlashCombo1)
            {
                Instance.StartCoroutine(CustomBehaviour.SpawnSandWave(transform.localScale.x == 1, transform.localScale.x == -1));
            }
            ResetFlags();
        });
        controlFsm.GetState("Slash Combo 13")!.AddLambdaMethod(_ => controlFsm.SetState("Range Check"));
        //* F Slash Branch
        controlFsm.GetState("F Slash Antic")!.AddLambdaMethod(_ =>
        {
            if (!quadSlashing && PhaseCheck()) return;
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.ySpeed = eigongAirDashing ? -50 : 0;
            controlFsm.GetFirstActionOfType<SetVelocityByScale>("F Slash 2")!.speed = Helpers.GetPosDiffSpeed() * -0.60f * (eigongAirDashing ? 0.75f : 1) * (quadSlashing ? 0 : 1);
            if (eigongAirDashing || quadSlashing || !PHASE_2) return;
            var num = Random.Range(0, 3);
            if (num == 0) Instance.StartCoroutine(CustomBehaviour.QuadWindSlash()); 
            else if (num == 1) Instance.StartCoroutine(CustomBehaviour.SpawnGroundWave()); 
        });
        controlFsm.GetState("F Slash Recover")!.AddLambdaMethod(_ =>
        {
            if (PHASE_2 && !eigongAirDashing && !quadSlashing) Instance.StartCoroutine(CustomBehaviour.SpawnSkProjectile());
            else if (PHASE_3 && eigongAirDashing) controlFsm.SetState("F Slash Antic");
            if (quadSlashing) controlFsm.SetState("Init Idle");
        });
        //* Jump Slash / Dash
        controlFsm.GetState("Blocked Hit")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash New"));
        controlFsm.GetState("Dash To Jump")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Dash To Antic")!.AddLambdaMethod(_ => controlFsm.SetState("Jump Slash Antic"));
        controlFsm.GetState("Jump Slash Antic")!.AddLambdaMethod(_ =>
        {
            if (quadSlashing) controlFsm.Fsm.manualUpdate = true;
            else controlFsm.SetState("F Slash Antic");
        });
        controlFsm.GetFirstActionOfType<SetVelocity2d>("Jump Slash Launch")!.y = 3;
        //* Digging / Uppercut
        controlFsm.GetFirstActionOfType<FloatClamp>("Dig Pos")!.minValue = 0;
        controlFsm.GetFirstActionOfType<Wait>("Dig Out Antic")!.time = 0.3f;
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Dig Out Uppercut")!.speed = 120;
        controlFsm.GetFirstActionOfType<FaceObjectV2>("Uppercut Antic")!.everyFrame = true;
        controlFsm.GetState("Uppercut Antic")!.AddLambdaMethod(_ =>
        {
            if (!pcrSlamming && PhaseCheck()) return;
            switch (Random.Range(0, PHASE_3 ? 3 : 4))
            {
                case 0 when PHASE_3:
                    fiveSLash = true;
                    fiveSLashedOnce = false;
                    controlFsm.SetState("Slash Combo Antic Q");
                    break;
                case 1:
                    controlFsm.SetState("Evade Antic");
                    break;
            }
        });
        controlFsm.GetFirstActionOfType<SetVelocityByScale>("Uppercut 1")!.speed = 70;
        controlFsm.GetState("Uppercut 1")!.AddLambdaMethod(_ =>
        {
            if (!CustomBehaviour.sandburst) Helpers.InitSandEffects();
            if (PHASE_2) Instance.StartCoroutine(CustomBehaviour.SpawnSandWave());
            if (!pcrSlamming && PHASE_3) Instance.StartCoroutine(CustomBehaviour.PCRSlams());
        });
        controlFsm.GetState("Uppercut End")!.AddLambdaMethod(_ =>
        {
            controlFsm.GetFirstActionOfType<FloatClamp>("Uppercut Launch")!.minValue = 0;
            controlFsm.SetState("Uppercut Launch");
        });
        controlFsm.GetState("Die")!.AddLambdaMethod(_ => Instance.StartCoroutine(CustomBehaviour.DesperationSpears()));
    }
}