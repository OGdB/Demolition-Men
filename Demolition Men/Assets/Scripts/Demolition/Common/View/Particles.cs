using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Code-configured particle bursts for hit/destruction/impact feedback, so the demo
    /// needs no imported particle assets. One short-lived ParticleSystem per burst that
    /// self-destroys when it finishes.
    /// </summary>
    public static class Particles
    {
        private static Material _material;

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

        public static void Burst(Vector3 position, Color color, int count = 12,
                                 float speed = 4f, float size = 0.14f, float life = 0.7f)
        {
            var go = new GameObject("Particles");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = life;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 1.1f;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = SharedMaterial();
            renderer.sortingOrder = 100;

            ps.Play();
        }
    }
}
