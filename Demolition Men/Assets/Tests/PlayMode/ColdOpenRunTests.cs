using System.Collections;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demolition.Tests
{
    /// <summary>
    /// The app's start (docs/hit-plan.md F4): a brand-new profile opens cold in the Tutorial and,
    /// with nobody pressing anything, still drops the painted block and hands over the controls;
    /// everyone else, and everyone with the switch off, gets the title and the lobby. Needs a
    /// keyboard or a gamepad on the machine (the opening's default player). About ten seconds.
    /// </summary>
    public class ColdOpenRunTests
    {
        private bool _switch;

        [SetUp]
        public void SetUp()
        {
            Profile.UseTemporary();
            LevelSelection.ReturnToLevelSelect = false;
            _switch = GameConfig.Instance.coldOpen;
        }

        [TearDown]
        public void TearDown()
        {
            GameConfig.Instance.coldOpen = _switch;
            Profile.UseDisk();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NewProfile_OpensInTheTutorial_AndNeedsNoPress()
        {
            GameConfig config = GameConfig.Instance;
            config.coldOpen = true;
            yield return LoadMenu(config, config.tutorialScene);
            Assert.AreEqual(config.tutorialScene, SceneManager.GetActiveScene().name, "no title, no lobby");

            RunController run = RunController.Current;
            Assert.IsNotNull(run);
            Assert.IsTrue(run.Held, "the opening plays in the countdown's place");
            Assert.AreEqual(1, run.Players.Count, "one player, on the default device");
            RunController.Player player = run.Players[0];
            WeakPointMark mark = Object.FindAnyObjectByType<WeakPointMark>();
            Assert.IsNotNull(mark, "the Tutorial has a painted block");
            float markX = mark.transform.position.x;

            for (float t = 0f; t < 20f && run.State.Phase == RunPhase.Countdown; t += Time.unscaledDeltaTime)
                yield return null;

            Assert.AreEqual(RunPhase.Playing, run.State.Phase, "it never waits for a press");
            Assert.IsFalse(run.Held);
            Assert.IsTrue(mark == null, "the painted block dropped");
            Assert.Greater(run.DestroyedFraction, 0f);
            Assert.IsTrue(player.Motor.enabled && player.Combat.enabled, "the controls are the player's");
            Assert.IsFalse(player.Input.Source is ScriptedInputSource);
            Assert.That(player.Transform.position.x, Is.InRange(markX - 3f, markX), "beside the block, short of the pit");
            Assert.AreEqual(0, Profile.Data.runsPlayed, "nothing is banked: quit now and the next start opens cold again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FinishedTutorial_StartsAtTheTitle()
        {
            GameConfig config = GameConfig.Instance;
            config.coldOpen = true;
            Profile.Data.RecordRun(config.tutorialScene, true, Grade.C, 0, 60f, tutorial: true);
            yield return LoadMenu(config, config.tutorialScene);
            Assert.AreEqual(config.menuScene, SceneManager.GetActiveScene().name);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SwitchOff_StartsAtTheTitle()
        {
            GameConfig config = GameConfig.Instance;
            config.coldOpen = false;
            yield return LoadMenu(config, config.tutorialScene);
            Assert.AreEqual(config.menuScene, SceneManager.GetActiveScene().name);
        }

        // Load the menu and give its Start a second to send the game on to `until`.
        private static IEnumerator LoadMenu(GameConfig config, string until)
        {
            SceneManager.LoadScene(config.menuScene);
            yield return null; // the menu is up and its Start has decided
            for (float t = 0f; t < 1f && SceneManager.GetActiveScene().name != until; t += Time.unscaledDeltaTime)
                yield return null;
            yield return null; // the scene's Starts have run
        }
    }
}
