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
        public void ActionPress_IsReadOnce_FromBotsAndTouch_AndMerges()
        {
            var bot = new ScriptedInputSource();
            bot.Next.ActionPressed = true;
            Assert.IsTrue(bot.Read().ActionPressed);
            Assert.IsFalse(bot.Read().ActionPressed);

            var touch = new TouchInputSource();
            touch.PressAction();
            PlayerInputFrame tapped = touch.Read();
            Assert.IsTrue(tapped.ActionPressed && tapped.IsActive);
            Assert.IsFalse(touch.Read().ActionPressed);

            Assert.IsTrue(PlayerInputFrame.Merge(default, new PlayerInputFrame { ActionPressed = true }).ActionPressed);
            Assert.AreEqual("[C] / (Y)", InputGlyphs.Format("{ACTION}", new InputStyle[0]));
        }

        [Test]
        public void Zoom_IsReadOnce_AndBothSourcesAddUp()
        {
            var bot = new ScriptedInputSource();
            bot.Next.Zoom = 1f;
            Assert.AreEqual(1f, bot.Read().Zoom);
            Assert.AreEqual(0f, bot.Read().Zoom, "a wheel notch is one step, not one per frame");
            var merged = PlayerInputFrame.Merge(new PlayerInputFrame { Zoom = 1f }, new PlayerInputFrame { Zoom = -0.25f });
            Assert.AreEqual(0.75f, merged.Zoom, 1e-5f);
        }

        [Test]
        public void TouchPinch_AddsUpUntilRead()
        {
            var touch = new TouchInputSource();
            touch.AddZoom(-0.5f);
            touch.AddZoom(-0.25f);
            Assert.AreEqual(-0.75f, touch.Read().Zoom, 1e-5f);
            Assert.AreEqual(0f, touch.Read().Zoom);
            touch.AddZoom(1f);
            touch.Release();
            Assert.AreEqual(0f, touch.Read().Zoom, "letting go drops a half-read pinch");
        }

        [Test]
        public void Glyphs_FollowTheDevicesInUse()
        {
            const string message = "{JUMP} to jump, hold {HIT} to smash";
            Assert.AreEqual("[SPACE] to jump, hold [Z] to smash", InputGlyphs.Format(message, new[] { InputStyle.Keyboard }));
            Assert.AreEqual("JUMP to jump, hold HIT to smash", InputGlyphs.Format(message, new[] { InputStyle.Touch }));
            Assert.AreEqual("[SPACE] / (A) to jump, hold [Z] / (X) to smash",
                InputGlyphs.Format(message, new[] { InputStyle.Gamepad, InputStyle.Keyboard, InputStyle.Gamepad }),
                "keyboard first, each device once");
            Assert.AreEqual("[SPACE] / (A) to jump, hold [Z] / (X) to smash", InputGlyphs.Format(message, new InputStyle[0]));
            Assert.AreEqual("no buttons here", InputGlyphs.Format("no buttons here", new[] { InputStyle.Touch }));
        }

        [Test]
        public void Combined_FollowsTheSourceUsedLast()
        {
            var bot = new ScriptedInputSource { Style = InputStyle.Gamepad };
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

        // The named keys went down this frame, or (up) were let go.
        private sealed class Keys : IKeyState
        {
            private readonly string[] _keys;
            private readonly bool _up;

            public Keys(bool up, params string[] keys)
            {
                _up = up;
                _keys = keys;
            }

            public bool Held(string key) => !_up && System.Array.IndexOf(_keys, key) >= 0;
            public bool Pressed(string key) => Held(key);
            public bool Released(string key) => _up && System.Array.IndexOf(_keys, key) >= 0;
        }

        private static PlayerInputFrame Press(KeyHalf half, params string[] keys) => KeyLayout.Read(half, new Keys(false, keys));

        [Test]
        public void OneKeyboardPlayer_HasEveryKeyItAlwaysHad()
        {
            foreach (string key in new[] { "A", "LeftArrow" })
                Assert.AreEqual(new Vector2(-1f, 0f), Press(KeyHalf.Whole, key).Move, key);
            foreach (string key in new[] { "D", "RightArrow" })
                Assert.AreEqual(new Vector2(1f, 0f), Press(KeyHalf.Whole, key).Move, key);
            foreach (string key in new[] { "S", "DownArrow" })
                Assert.AreEqual(new Vector2(0f, -1f), Press(KeyHalf.Whole, key).Move, key);
            foreach (string key in new[] { "Space", "W", "UpArrow" })
            {
                PlayerInputFrame down = Press(KeyHalf.Whole, key);
                Assert.IsTrue(down.JumpPressed && down.JumpHeld && !down.JumpReleased, key);
                Assert.AreEqual(key == "Space" ? 0f : 1f, down.Move.y, key);
                PlayerInputFrame up = KeyLayout.Read(KeyHalf.Whole, new Keys(true, key));
                Assert.IsTrue(up.JumpReleased && !up.JumpHeld && !up.JumpPressed, key);
            }
            foreach (string key in new[] { "Z", "F" })
                Assert.IsTrue(Press(KeyHalf.Whole, key).AttackHeld, key);
            foreach (string key in new[] { "X", "R" })
                Assert.IsTrue(Press(KeyHalf.Whole, key).DynamitePressed, key);
            Assert.IsTrue(Press(KeyHalf.Whole, "C").ActionPressed);
            Assert.AreEqual(-1, Press(KeyHalf.Whole, "Q").WeaponCycle);
            Assert.AreEqual(1, Press(KeyHalf.Whole, "E").WeaponCycle);
            Assert.AreEqual(new Vector2(1f, 1f), Press(KeyHalf.Whole, "D", "W").Move, "the source clamps, after the stick");
            foreach (string key in new[] { "G", "K", "L", "O", "U", "I" })
                Assert.IsFalse(Press(KeyHalf.Whole, key).IsActive, key + " is a key of the halves only");
            Assert.AreEqual(InputStyle.Keyboard, KeyLayout.Style(KeyHalf.Whole));
        }

        [TestCase(KeyHalf.Left, KeyHalf.Right, InputStyle.KeysLeft, "A", "D", "Space", "F", "R", "G", "Q", "E")]
        [TestCase(KeyHalf.Right, KeyHalf.Left, InputStyle.KeysRight, "LeftArrow", "RightArrow", "UpArrow", "K", "O", "L", "U", "I")]
        public void TwoOnOneKeyboard_EachHalfDrivesItsOwnPlayer_WithTheKeysItsGlyphsShow(KeyHalf mine, KeyHalf theirs, InputStyle style,
            string left, string right, string jump, string hit, string tnt, string action, string previous, string next)
        {
            Assert.AreEqual(style, KeyLayout.Style(mine));
            Assert.AreEqual(-1f, Press(mine, left).Move.x);
            Assert.AreEqual(1f, Press(mine, right).Move.x);
            PlayerInputFrame jumped = Press(mine, jump);
            Assert.IsTrue(jumped.JumpPressed && jumped.JumpHeld);
            Assert.IsTrue(KeyLayout.Read(mine, new Keys(true, jump)).JumpReleased);
            Assert.IsTrue(Press(mine, hit).AttackHeld);
            Assert.IsTrue(Press(mine, tnt).DynamitePressed);
            Assert.IsTrue(Press(mine, action).ActionPressed);
            Assert.AreEqual(-1, Press(mine, previous).WeaponCycle);
            Assert.AreEqual(1, Press(mine, next).WeaponCycle);

            PlayerInputFrame other = Press(theirs, left, right, jump, hit, tnt, action, previous, next);
            Assert.IsFalse(other.IsActive || other.JumpPressed, "the other half's player stands still");

            string Label(string key) => "[" + key.Replace("Arrow", string.Empty).ToUpperInvariant() + "]";
            Assert.AreEqual($"{Label(left)} {Label(right)} {Label(jump)} {Label(hit)} {Label(tnt)} {Label(action)} {Label(previous)} {Label(next)}",
                InputGlyphs.Format("{MOVE} {JUMP} {HIT} {TNT} {ACTION} {SWAP}", new[] { style }));
        }

        [Test]
        public void TwoOnOneKeyboard_NoKeyIsInBothHalves()
        {
            var keys = new System.Collections.Generic.List<string> { "Space", "LeftArrow", "RightArrow", "UpArrow", "DownArrow" };
            for (char c = 'A'; c <= 'Z'; c++)
                keys.Add(c.ToString());
            foreach (string key in keys)
                Assert.IsFalse(Press(KeyHalf.Left, key).IsActive && Press(KeyHalf.Right, key).IsActive, key);
        }

        [Test]
        public void Glyphs_OfBothHalves_NameEachPlayersKeys_AndSharedOnesOnce()
        {
            var halves = new[] { InputStyle.KeysRight, InputStyle.KeysLeft };
            Assert.AreEqual("[SPACE] / [UP] to jump, hold [F] / [K] to smash",
                InputGlyphs.Format("{JUMP} to jump, hold {HIT} to smash", halves), "the WASD half first");
            Assert.AreEqual("[ESC] pauses, MOUSE WHEEL zooms", InputGlyphs.Format("{PAUSE} pauses, {ZOOM} zooms", halves));
            Assert.AreEqual("[A] [D] / [LEFT] [RIGHT]", InputGlyphs.Format("{MOVE}", halves));
            Assert.AreEqual("[R]", InputGlyphs.Buttons(InputGlyphs.Tnt, InputStyle.KeysLeft));
            Assert.AreEqual("[L]", InputGlyphs.Buttons(InputGlyphs.ActionButton, InputStyle.KeysRight));
            Assert.AreEqual("[Q] [E]", InputGlyphs.Buttons(InputGlyphs.Swap, InputStyle.Keyboard));
            Assert.AreEqual(System.Enum.GetValues(typeof(InputStyle)).Length, InputGlyphs.Styles, "a column and a name per style");
            foreach (InputStyle style in System.Enum.GetValues(typeof(InputStyle)))
                Assert.IsNotEmpty(InputGlyphs.Name(style));
        }
    }
}
