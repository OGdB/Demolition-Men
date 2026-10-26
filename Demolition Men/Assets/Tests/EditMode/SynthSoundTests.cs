using NUnit.Framework;

namespace Demolition.Tests
{
    public class SynthSoundTests
    {
        [Test]
        public void Sounds_HaveTheRightLength_AndStayInRange()
        {
            float[][] sounds =
            {
                SynthSound.Crunch(0.2f, 0.3f, 1),
                SynthSound.Rumble(1.5f, 2),
                SynthSound.Clang(0.4f, 300f, 3),
                SynthSound.Chime(0.8f, 1320f),
                SynthSound.Burp(0.9f, 4),
                SynthSound.Whistle(0.6f),
            };
            float[] lengths = { 0.2f, 1.5f, 0.4f, 0.8f, 0.9f, 0.6f };
            for (int s = 0; s < sounds.Length; s++)
            {
                Assert.AreEqual((int)(lengths[s] * SynthSound.SampleRate), sounds[s].Length);
                float peak = 0f;
                foreach (float v in sounds[s])
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                    peak = System.Math.Max(peak, System.Math.Abs(v));
                }
                Assert.That(peak, Is.InRange(0.5f, 1f), $"sound {s} peak");
            }
        }

        [Test]
        public void SameSeed_SameSound() =>
            Assert.AreEqual(SynthSound.Crunch(0.1f, 0.5f, 7), SynthSound.Crunch(0.1f, 0.5f, 7));
    }
}
