using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker.Actions;
using Silksong.FsmUtil;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AeternalEverwatcher;

public static class Helpers
{
    public static float GetPosDiffSpeed() => Mathf.Clamp(Math.Abs(HeroController.instance.transform.position.x - AeternalEverwatcherPlugin.transform.position.x) * 30, 230, 270);
    public static bool ObjLeftOfHornet(GameObject go) => go.transform.position.x < HeroController.instance.transform.position.x;
    public static void MakeProjectileIgnoreEnvironment(GameObject projectile)
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
    public static void RemoveProjectileWallEvents(GameObject projectile)
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
    public static void removeEventFromState(string stateName, string eventName)
    {
        var state = AeternalEverwatcherPlugin.controlFsm.FsmStates.FirstOrDefault(state => state.Name == stateName);
        state.Transitions = state.Transitions
            .Where(t => t.EventName != eventName)
            .ToArray();
    }
    public static IEnumerator DestroyLater(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        go.SetActive(false);
    }
    public static void SandColorSetup(GameObject wave, string name)
    {
        wave.name = name;
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (pt.name is "sand_blown" or "particles_small") continue;
            var main = pt.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            var renderer = pt.GetComponent<ParticleSystemRenderer>();
            if (renderer && renderer.material)
            {
                renderer.material.color = Color.white;
                switch (pt.name)
                {
                    case "sand_burst  back":
                        renderer.material.SetColor(Color1, new Color(Settings.SAND_EFFECTS_BRIGHTNESS * 2, Settings.SAND_EFFECTS_BRIGHTNESS * 2, Settings.SAND_EFFECTS_BRIGHTNESS * 2, 1f));
                        break;
                    case "sand_burst front":
                        renderer.material.SetColor(Color1, new Color(Settings.SAND_EFFECTS_BRIGHTNESS * 2.2f, Settings.SAND_EFFECTS_BRIGHTNESS * 2.2f, Settings.SAND_EFFECTS_BRIGHTNESS * 2.2f, 0.3f));
                        if (!Settings.BOSS_AND_PLAYER_ABOVE_SAND) renderer.sortingOrder = 2000;
                        break;
                    default:
                        renderer.material.SetColor(Color1, new Color(Settings.SAND_EFFECTS_BRIGHTNESS * 2f, Settings.SAND_EFFECTS_BRIGHTNESS * 2f, Settings.SAND_EFFECTS_BRIGHTNESS * 2f, 1f));
                        break;
                }
            }
            var noise = pt.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 1.0f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }
    }
    public static void SandSpeedSetup(GameObject wave, float velLimit = 2.0f)
    {
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            var limitVelocity = pt.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = velLimit;
            limitVelocity.dampen = 0.5f;
        }
    }
    public static void GroundWaveSetup(GameObject wave)
    {
        var damagerHitboxOriginal = wave.transform.FindRelativeTransformWithPath("damager", false).GetComponent<PolygonCollider2D>();
        Vector2[] points = [ new(-10, 3), new(10, 3), new(-10, 0), new(10, 0) ];
        damagerHitboxOriginal.SetPath(0, points);
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            //if (pt.name == "sand_burst front") Object.Instantiate(pt, pt.gameObject.transform.parent);
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
    public static void InitSandEffects()
    {
        var sandburstOriginal = AeternalEverwatcherPlugin.controlFsm.GetLastActionOfType<ActivateGameObject>("Uppercut 1")!.gameObject.GameObject.Value;
        SandColorSetup(sandburstOriginal, "sandburst");
        CustomBehaviour.sandburst = Object.Instantiate(sandburstOriginal);
        CustomBehaviour.sandburst.SetActive(false);
        SandSpeedSetup(CustomBehaviour.sandburst, 3);
        CustomBehaviour.groundWave = Object.Instantiate(CustomBehaviour.sandburst);
        CustomBehaviour.groundWave.SetActive(false);
        SandColorSetup(CustomBehaviour.groundWave, "groundWave");
        GroundWaveSetup(CustomBehaviour.groundWave);
        CustomBehaviour.pcrBurst = Object.Instantiate(CustomBehaviour.groundWave);
        CustomBehaviour.pcrBurst.name = "pcrBurst";
        CustomBehaviour.pcrBurst.SetActive(false);
        SandSpeedSetup(CustomBehaviour.groundWave);
        CustomBehaviour.sandTelegraph = Object.Instantiate(CustomBehaviour.groundWave);
        Object.Destroy(CustomBehaviour.sandTelegraph.transform.Find("damager").gameObject);
        Object.Destroy(CustomBehaviour.sandTelegraph.transform.Find("sand_blown").gameObject);
        Object.Destroy(CustomBehaviour.sandTelegraph.transform.Find("particles_small").gameObject);
        Object.Destroy(CustomBehaviour.sandTelegraph.transform.Find("sand_burst").gameObject);
        Object.Destroy(CustomBehaviour.sandTelegraph.transform.Find("sand_burst  back").gameObject);
        CustomBehaviour.sandTelegraph.name = "sandTelegraph";
        CustomBehaviour.sandTelegraph.SetActive(false);
    }
    public static IEnumerator FinishStateEarly(string eventName, float delay)
    {
        yield return new WaitForSeconds(delay);
        AeternalEverwatcherPlugin.controlFsm.SendEvent(eventName);
    }
    public static bool CheckDamage()
    {
        if (!AeternalEverwatcherPlugin.tookDamage) return false;
        AeternalEverwatcherPlugin.tookDamage = false;
        return true;
    }

    public static void ModifyTerrain()
    {
        //? Boss aggro range, extended to the far left edges of the scene
        var battleRange = GameObject.Find("Battle Range");
        battleRange.transform.position = battleRange.transform.position with { x = 75.2f };
        battleRange.transform.localScale = battleRange.transform.localScale with { x = 1.372f };
        //? Room transition trigger, extended higher up
        var roomTrans = GameObject.Find("right1");
        roomTrans.transform.localScale = roomTrans.transform.localScale with { y = 10 };
        //? Camera triggers modified to fit the extended arena, otherwise they will point down at the sand sea
        var camlock4 = GameObject.Find("CameraLockArea (4)");
        camlock4.transform.position = camlock4.transform.position with { x = 81 };
        camlock4.transform.localScale = camlock4.transform.localScale with { x = 7 };
        GameObject.Find("CameraLockArea (5)").SetActive(false);
        //? Disable centipedes on the left edge since theyre useless with the arena extension
        GameObject.Find("sand_centipede_scuffle").SetActive(false);
        GameObject.Find("Sand Centipede Hero Damager").SetActive(false);
        GameObject.Find("Sand Centipede Group").SetActive(false);
        GameObject.Find("Sand_Centipede_Ambient_Audio Variant").SetActive(false);
        //? Terrain colliders that must be disabled
        GameObject.Find("terrain collider non slider").SetActive(false);
        GameObject.Find("terrain collider non slider (1)").SetActive(false);
        GameObject.Find("terrain collider non slider (2)").SetActive(false);
        GameObject.Find("terrain collider non slider (3)").SetActive(false);
        GameObject.Find("terrain collider (13)").SetActive(false);
        GameObject.Find("terrain collider (12)").SetActive(false);
        GameObject.Find("terrain collider (11)").SetActive(false);
        GameObject.Find("terrain collider (10)").SetActive(false);
        GameObject.Find("Roof Collider_Basic").SetActive(false);
        GameObject.Find("Roof Collider_Basic (2)").SetActive(false);
        //? Theres a fuckass hole in the roof for some reason this oughta fix it 
        var roofFiller = Object.Instantiate(GameObject.Find("Giant_Conch_bg_horn (23)"));
        roofFiller.transform.localScale = new Vector3(4.3223f, 5.3747f, 1);
        roofFiller.transform.position = new Vector3(159.4092f, 45.6818f, -5.1127f);
        roofFiller.transform.SetRotation2D(338.352f);
        //? Disable the colliders from the tilemap but keep the black rectangles to fill
        var tilemapRenderData = GameObject.Find("TileMap Render Data").transform.GetChild(0)!;
        foreach (var edgeCollider2D in tilemapRenderData.Find("Chunk 1 5").gameObject.GetComponents<EdgeCollider2D>()) edgeCollider2D.enabled = false;
        foreach (var edgeCollider2D in tilemapRenderData.Find("Chunk 1 4").gameObject.GetComponents<EdgeCollider2D>()) edgeCollider2D.enabled = false;
        foreach (var edgeCollider2D in tilemapRenderData.Find("Chunk 1 3").gameObject.GetComponents<EdgeCollider2D>()) edgeCollider2D.enabled = false;
        foreach (var edgeCollider2D in tilemapRenderData.Find("Chunk 0 5").gameObject.GetComponents<EdgeCollider2D>()) edgeCollider2D.enabled = false;
        var terrain = tilemapRenderData.Find("Chunk 0 4")!;
        terrain.transform.localScale = terrain.localScale with { x = 300 };
        terrain.transform.position = terrain.position with { x = 0 };
        var borderLeft = Object.Instantiate(terrain, terrain.parent);
        borderLeft.transform.position = new Vector3(-2.6f, 5, 0);
        borderLeft.transform.localScale = new Vector3(1, 65, 1);
        borderLeft.gameObject.AddComponent<NonSlider>();
        borderLeft.gameObject.GetComponent<MeshRenderer>().enabled = false;
        borderLeft.gameObject.name = "BorderLeftHitbox";
        var borderRight = Object.Instantiate(terrain, terrain.parent);
        borderRight.transform.position = new Vector3(173, -2, 0);
        borderRight.transform.localScale = new Vector3(1, 65, 1);
        borderRight.gameObject.AddComponent<NonSlider>();
        borderRight.gameObject.GetComponent<MeshRenderer>().enabled = false;
        borderRight.gameObject.name = "BorderRightHitbox";
        var stepFix = Object.Instantiate(terrain, terrain.parent);
        stepFix.transform.position = new Vector3(173.9688f, 3.0291f, terrain.transform.position.z);
        //? Copy over art assets to cover the extended ground
        var terrainExtension1 = new GameObject("Terrain Extension 1");
        var terrainExtension2 = new GameObject("Terrain Extension 2");
        HashSet<string> whiteList =
        [
            "kingdom_gate_0000_sand_dune_ground",
            "bone_deep",
            "Bone_floor_02"
        ];
        HashSet<string> assetBlacklist =
        [
            "kingdom_gate_0000_sand_dune_ground (28)",
            "kingdom_gate_0000_sand_dune_ground (19)",
            "bone_deep_0170_t (14)",
            "bone_deep_0170_t (12)",
            "bone_deep_0170_t (16)",
            "bone_deep_0170_t (15)",
            "bone_deep_0170_t (13)",
        ];
        foreach (var rootGameObject in SceneManager.GetActiveScene().GetRootGameObjects()) if (whiteList.Any(assetName => rootGameObject.name.StartsWith(assetName)) && !assetBlacklist.Contains(rootGameObject.name))
        {
            var newAsset = Object.Instantiate(rootGameObject, terrainExtension1.transform);
            var objX = rootGameObject.transform.position.x;
            //? 108 is the pivot, -10 is the average length of the asset im copying, should prolly make a switch for each type of thing for modularity but eh it works
            var offsetFromCenter = objX - 108 - 10;
            newAsset.transform.position = rootGameObject.transform.position with { x = objX - offsetFromCenter * 2};
            var newAssetFar = Object.Instantiate(rootGameObject, terrainExtension2.transform);
            //? -54 offset for further left shift
            newAssetFar.transform.position = rootGameObject.transform.position with { x = objX - offsetFromCenter * 2 - 54};
        }
    }

    public static void SetSpearX(GameObject spear, float x) => spear.transform.position = spear.transform.position with { x = x };
    public static readonly int Color1 = Shader.PropertyToID("_Color");
}