using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Code-configured particle bursts for hit/destruction/impact feedback, so the game
    /// needs no imported particle assets. Every burst is emitted from one shared particle
    /// system per scene (no GameObject per burst), which keeps big collapses cheap on phones.
    /// </summary>
    public static class Particles
    {
        private const int MaxParticles = 3000;

        private static Material _material;
        private static ParticleSystem _system;

        private static Material SharedMaterial()
        {
            if (_material != null)
                return _material;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            _material = new Material(shader) { mainTexture = PrimitiveSprite.Unit().texture };
            return _material;
        }

        // Recreated after a scene change destroyed the previous one.
        private static ParticleSystem System()
        {
            if (_system != null)
                return _system;

            var go = new GameObject("Particles");
            _system = go.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _system.main;
            main.loop = true; // stays playing; emission is off, bursts come from Emit
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.1f;
            main.maxParticles = MaxParticles;
            // Its empty bounds sit wherever it was made: never cull it when the camera moves on.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0f;

            var emission = _system.emission;
            emission.enabled = false;
            var shape = _system.shape;
            shape.enabled = false;

            var fade = _system.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = SharedMaterial();
            renderer.sortingOrder = 100;

            _system.Play();
            return _system;
        }

        public static void Burst(Vector3 position, Color color, int count = 12,
                                 float speed = 4f, float size = 0.14f, float life = 0.7f)
        {
            ParticleSystem system = System();
            var p = new ParticleSystem.EmitParams
            {
                startColor = color,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false,
            };
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = position + (Vector3)(Random.insideUnitCircle * 0.12f);
                p.velocity = dir * speed;
                system.Emit(p, 1);
            }
        }
    }
}
