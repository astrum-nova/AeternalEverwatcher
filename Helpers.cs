using System;
using System.Collections;
using System.Linq;
using Silksong.FsmUtil;
using UnityEngine;
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
    public static IEnumerator DestroyLater(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        Object.Destroy(go);
    }
    public static void SandColorSetup(GameObject wave)
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
        var sandburstOriginal = GameObject.Find("sand_burst_effect_uppercut");
        SandColorSetup(sandburstOriginal);
        CustomBehaviour.sandburst = Object.Instantiate(sandburstOriginal);
        CustomBehaviour.sandburst.SetActive(false);
        SandSpeedSetup(CustomBehaviour.sandburst, 3);
        CustomBehaviour.groundWave = Object.Instantiate(CustomBehaviour.sandburst);
        CustomBehaviour.groundWave.SetActive(false);
        SandColorSetup(CustomBehaviour.groundWave);
        GroundWaveSetup(CustomBehaviour.groundWave);
        CustomBehaviour.pcrBurst = Object.Instantiate(CustomBehaviour.groundWave);
        CustomBehaviour.pcrBurst.SetActive(false);
        SandSpeedSetup(CustomBehaviour.groundWave);
    }
    public static IEnumerator FinishStateEarly(string eventName, float delay)
    {
        yield return new WaitForSeconds(delay);
        AeternalEverwatcherPlugin.controlFsm.SendEvent(eventName);
    }
    private static readonly int Color1 = Shader.PropertyToID("_Color");
}