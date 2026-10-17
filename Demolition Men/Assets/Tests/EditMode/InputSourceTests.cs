using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    public class InputSourceTests
    {
        [Test]
        public void TouchTap_ShorterThanAFrame_StillJumps()
        {
            var touch = new TouchInputSource();
            touch.SetJump(true);
            touch.SetJump(false);
            PlayerInputFrame frame = touch.Read();
            Assert.IsTrue(frame.JumpPressed && frame.JumpReleased);
            Assert.IsFalse(touch.Read().JumpPressed, "a press is read once");
        }

        [Test]
        public void TouchHold_StaysHeld_UntilReleased()
        {
            var touch = new TouchInputSource();
            touch.SetAttack(true);
            touch.Move = new Vector2(0.5f, 0f);
            Assert.IsTrue(touch.Read().AttackHeld);
            Assert.IsTrue(touch.Read().AttackHeld);
            touch.Release();
            PlayerInputFrame frame = touch.Read();
            Assert.IsFalse(frame.AttackHeld);
            Assert.AreEqual(Vector2.zero, frame.Move);
        }

        [Test]
        public void Scripted_OneFramePresses_AreReadOnce()
        {
            var bot = new ScriptedInputSource();
            bot.Next.DynamitePressed = true;
            bot.Next.WeaponCycle = 1;
            bot.Next.AttackHeld = true;
            PlayerInputFrame first = bot.Read(), second = bot.Read();
            Assert.IsTrue(first.DynamitePressed);
            Assert.AreEqual(1, first.WeaponCycle);
            Assert.IsFalse(second.DynamitePressed);
            Assert.AreEqual(0, second.WeaponCycle);
            Assert.IsTrue(second.AttackHeld, "holds persist");
        }

        [Test]
        public void Combined_FollowsTheSourceUsedLast()
        {
            var bot = new ScriptedInputSource();
            var touch = new TouchInputSource();
            var both = new CombinedInputSource(bot, touch);
            touch.SetAttack(true);
            both.Read();
            Assert.AreEqual(InputStyle.Touch, both.Style);
            touch.SetAttack(false);
            both.Read();
            bot.Next.Move = new Vector2(1f, 0f);
            both.Read();
            Assert.AreEqual(InputStyle.Gamepad, both.Style);
        }
    }
}
