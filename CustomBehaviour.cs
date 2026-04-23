using System.Collections;
using GlobalEnums;
using GlobalSettings;
using HutongGames.PlayMaker.Actions;
using Silksong.AssetHelper.ManagedAssets;
using Silksong.FsmUtil;
using UnityEngine;

namespace AeternalEverwatcher;

public static class CustomBehaviour
{
    public const float WATCHER_GROUND_Y = 8.3275f;
    private const float SANDBURST_DEFAULT_Y = 6.552498f;
    private const float WINDSLASH_DEFAULT_Y = 7.327499f;
    public static ManagedAsset<GameObject> skProjectile = null!;
    public static GameObject skProjectileSetup = null!;
    public static GameObject groundWave = null!;
    public static GameObject pcrBurst = null!;
    public static GameObject sandburst = null!;
    public static GameObject sandburstSmall = null!;

    public static IEnumerator spawnSkProjectile()
    {
        if (AeternalEverwatcherPlugin.fiveSLash || AeternalEverwatcherPlugin.eigongAirDashing) yield break;
        if (!skProjectileSetup)
        {
            yield return skProjectile.Load();
            skProjectileSetup = skProjectile.InstantiateAsset();
            skProjectileSetup.GetComponent<Collider2D>().isTrigger = true;
            Helpers.MakeProjectileIgnoreEnvironment(skProjectileSetup);
            Helpers.RemoveProjectileWallEvents(skProjectileSetup);
            skProjectileSetup.AddComponent<ProjectileMover>();
            skProjectileSetup.SetActive(false);
            skProjectileSetup.transform.position = new Vector3(0, 500, 0);
        }
        var instance = Object.Instantiate(skProjectileSetup);
        instance.transform.position = AeternalEverwatcherPlugin.transform.position;
        instance.transform.SetPosition2D(new Vector3(
            HeroController.instance.transform.position.x + (Helpers.ObjLeftOfHornet(instance) ? 15 : -15) * (AeternalEverwatcherPlugin.quadSlashing ? -1 : 1),
            WINDSLASH_DEFAULT_Y,
            AeternalEverwatcherPlugin.transform.position.z
        ));
        instance.SetActive(true);
        yield return new WaitForSeconds(1);
        Object.Destroy(instance);
    }

    public static IEnumerator Teleport(float x, float y, string? nextState = null, bool flipX = false)
    {
        Effects.EnemyCoalHurtSound.SpawnAndPlayOneShot(AeternalEverwatcherPlugin.transform.position);
        CreateWave(sandburstSmall, new Vector3(AeternalEverwatcherPlugin.transform.position.x, AeternalEverwatcherPlugin.transform.position.y - 1.5f, sandburst.transform.position.z));
        yield return new WaitForSeconds(0.05f);
        AeternalEverwatcherPlugin.transform.position = new Vector3(x, y, AeternalEverwatcherPlugin.transform.position.z);
        CreateWave(sandburstSmall, new Vector3(AeternalEverwatcherPlugin.transform.position.x, AeternalEverwatcherPlugin.transform.position.y - 1.5f, sandburst.transform.position.z));
        AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 9")!.speed = 0;
        if (nextState != null) AeternalEverwatcherPlugin.controlFsm.SetState(nextState);
        if (flipX) AeternalEverwatcherPlugin.transform.FlipLocalScale(x:true);
    }

    public static IEnumerator SpawnGroundWave()
    {
        AeternalEverwatcherPlugin.ResetFlags();
        AeternalEverwatcherPlugin.eigongAirDashing = AeternalEverwatcherPlugin.PHASE_3;
        AeternalEverwatcherPlugin.controlFsm.SetState("Wake Roar 2");
        Effects.EnemyCoalHurtSound.SpawnAndPlayOneShot(AeternalEverwatcherPlugin.transform.position);
        yield return new WaitForSeconds(0.5f);
        CreateWave(groundWave, new Vector3(HeroController.instance.transform.position.x, SANDBURST_DEFAULT_Y, sandburst.transform.position.z), delayToDestruction: 3, rotation:false);
        if (!AeternalEverwatcherPlugin.PHASE_3) AeternalEverwatcherPlugin.controlFsm.SetState("Dig In 1");
        else if (!AeternalEverwatcherPlugin.pcrSlamming)
        {
            if (Helpers.CheckDamage()) 
            {
                AeternalEverwatcherPlugin.controlFsm.SetState("Dig In 1");
                AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 0;
                yield break;
            }
            yield return new WaitForSeconds(0.2f);
            if (HeroController.instance.transform.position.y < 11.5) HeroController.instance.TakeDamage(null, CollisionSide.top, 2, HazardType.NON_HAZARD);
            var xOffset = Random.Range(0, 2) == 0 ? -9 : 9;
            AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 30;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + xOffset, HeroController.instance.transform.position.y, "F Slash Antic"));
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.3f));
            yield return new WaitForSeconds(0.5f);
            if (Helpers.CheckDamage()) 
            {
                AeternalEverwatcherPlugin.controlFsm.SetState("Dig In 1");
                AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 0;
                yield break;
            }
            AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 0;
            if (HeroController.instance.transform.position.y < 11.5) HeroController.instance.TakeDamage(null, CollisionSide.top, 2, HazardType.NON_HAZARD);
            CreateWave(groundWave, new Vector3(HeroController.instance.transform.position.x, SANDBURST_DEFAULT_Y, sandburst.transform.position.z), delayToDestruction: 3, rotation:false);
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x - xOffset, HeroController.instance.transform.position.y + 3, "F Slash Antic"));
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.1f));
            yield return new WaitForSeconds(0.3f);
            if (Helpers.CheckDamage()) 
            {
                AeternalEverwatcherPlugin.controlFsm.SetState("Dig In 1");
                AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 0;
                yield break;
            }
            if (HeroController.instance.transform.position.y < 11.5) HeroController.instance.TakeDamage(null, CollisionSide.top, 2, HazardType.NON_HAZARD);
            CreateWave(groundWave, new Vector3(HeroController.instance.transform.position.x, SANDBURST_DEFAULT_Y, sandburst.transform.position.z), delayToDestruction: 3, rotation:false);
            AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 30;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + xOffset, HeroController.instance.transform.position.y + 1, "F Slash Antic"));
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Helpers.FinishStateEarly("FINISHED", 0.3f));
            yield return new WaitForSeconds(0.5f);
            AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocity2d>("F Slash Antic")!.y = 0;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + (Random.Range(0, 2) == 0 ? -7 : 7), HeroController.instance.transform.position.y + 4, "Uppercut End"));
        }
        yield return new WaitForSeconds(0.2f);
        AeternalEverwatcherPlugin.eigongAirDashing = false;
    }
    private static GameObject CreateWave(GameObject go, Vector3 position, float delayToDestruction = 1, bool rotation = true)
    {
        var wave = Object.Instantiate(go);
        wave.transform.position = position;
        if (rotation) wave.transform.SetRotation2D(Random.Range(-5, 5));
        wave.SetActive(false);
        wave.SetActive(true);
        AeternalEverwatcherPlugin.Instance.StartCoroutine(Helpers.DestroyLater(wave, delayToDestruction));
        return wave;
    }
    public static IEnumerator SpawnSandWave(bool left = true, bool right = true)
    {
        //TODO: try to chache the origin instead of finding it every time
        var originObject = AeternalEverwatcherPlugin.transform.FindRelativeTransformWithPath("sand_burst_effect_uppercut_origin", false)!;
        yield return new WaitForSeconds(0.2f);
        float pos1 = 0;
        float pos2 = 0;
        float pos3 = 0;
        if (left && right)
        {
            var scaleOffset = AeternalEverwatcherPlugin.transform.localScale.x * -1;
            pos1 = originObject.position.x + 6 * scaleOffset;
            pos2 = originObject.position.x + 10 * scaleOffset;
            pos3 = originObject.position.x + 14 * scaleOffset;
        } 
        else if (left)
        {
            pos1 = AeternalEverwatcherPlugin.transform.position.x - 5;
            pos2 = AeternalEverwatcherPlugin.transform.position.x - 10;
            pos3 = AeternalEverwatcherPlugin.transform.position.x - 15;
        } 
        else if (right)
        {
            pos1 = AeternalEverwatcherPlugin.transform.position.x + 5;
            pos2 = AeternalEverwatcherPlugin.transform.position.x + 10;
            pos3 = AeternalEverwatcherPlugin.transform.position.x + 15;
        }

        if (!pcrSlamsEndWave)
        {
            CreateWave(sandburst, new Vector3(pos1, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(pos2, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
            yield return new WaitForSeconds(0.1f);
            CreateWave(sandburst, new Vector3(pos3, SANDBURST_DEFAULT_Y, sandburst.transform.position.z));
        }
        else
        {
            var burst = CreateWave(pcrBurst, new Vector3(HeroController.instance.transform.position.x, SANDBURST_DEFAULT_Y, sandburst.transform.position.z), delayToDestruction: 3, rotation: false);
            Helpers.SandSpeedSetup(burst, velLimit:10);
            pcrSlamsEndWave = false;
        }
    }

    private static bool pcrSlamsEndWave;
    public static IEnumerator PCRSlams()
    {
        AeternalEverwatcherPlugin.pcrSlamming = true;
        yield return new WaitForSeconds(0.8f);
        AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + (Random.Range(0, 2) == 0 ? -7 : 7), HeroController.instance.transform.position.y + 7, "Uppercut End"));
        AeternalEverwatcherPlugin.transform.localScale.Set(Helpers.ObjLeftOfHornet(AeternalEverwatcherPlugin.transform.gameObject) ? -1 : 1, 1, 1);
        yield return new WaitForSeconds(0.7f);/*
        AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + (Random.Range(0, 2) == 0 ? -7 : 7), HeroController.instance.transform.position.y + 7, "Uppercut End"));
        AeternalEverwatcherPlugin.transform.localScale.Set(Helpers.ObjLeftOfHornet(AeternalEverwatcherPlugin.transform.gameObject) ? -1 : 1, 1, 1);
        yield return new WaitForSeconds(0.7f);*/
        AeternalEverwatcherPlugin.Instance.StartCoroutine(Teleport(HeroController.instance.transform.position.x + (Random.Range(0, 2) == 0 ? -7 : 7), HeroController.instance.transform.position.y + 7, "Uppercut End"));
        AeternalEverwatcherPlugin.transform.localScale.Set(Helpers.ObjLeftOfHornet(AeternalEverwatcherPlugin.transform.gameObject) ? -1 : 1, 1, 1);
        yield return new WaitForSeconds(0.7f);
        pcrSlamsEndWave = true;
        AeternalEverwatcherPlugin.controlFsm.SetState("Uppercut Antic Q");
        yield return new WaitForSeconds(0.65f);
        AeternalEverwatcherPlugin.controlFsm.SetState("Jump Away Air");
        AeternalEverwatcherPlugin.pcrSlamming = false;
    }
    
    public static IEnumerator QuadWindSlash()
    {
        if (!AeternalEverwatcherPlugin.quadSlashing)
        {
            AeternalEverwatcherPlugin.quadSlashing = true;
            AeternalEverwatcherPlugin.controlFsm.SetState("F Slash Antic");
            AeternalEverwatcherPlugin.controlFsm.Fsm.ManualUpdate = true;
            yield return new WaitForSeconds(0.375f);
            AeternalEverwatcherPlugin.Instance.StartCoroutine(spawnSkProjectile());
            yield return new WaitForSeconds(0.425f);
            AeternalEverwatcherPlugin.controlFsm.GetFirstActionOfType<SetVelocityByScale>("Slash Combo 1")!.speed = 0;
            AeternalEverwatcherPlugin.controlFsm.SetState("Slash Combo 1");
            AeternalEverwatcherPlugin.controlFsm.Fsm.ManualUpdate = true;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(spawnSkProjectile());
            yield return new WaitForSeconds(0.25f);
            AeternalEverwatcherPlugin.controlFsm.SetState("F Slash 2");
            AeternalEverwatcherPlugin.controlFsm.Fsm.ManualUpdate = true;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(spawnSkProjectile());
            yield return new WaitForSeconds(0.4f);
            AeternalEverwatcherPlugin.controlFsm.SetState("Jump Slash New");
            AeternalEverwatcherPlugin.controlFsm.Fsm.ManualUpdate = true;
            AeternalEverwatcherPlugin.Instance.StartCoroutine(spawnSkProjectile());
            yield return new WaitForSeconds(0.2f);
            
            AeternalEverwatcherPlugin.quadSlashing = false;
            AeternalEverwatcherPlugin.controlFsm.Fsm.ManualUpdate = false;
            AeternalEverwatcherPlugin.controlFsm.SetState("Range Check");
        }
    }
}