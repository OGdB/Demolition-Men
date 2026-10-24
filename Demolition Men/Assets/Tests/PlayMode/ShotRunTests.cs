#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demolition.Clips;
using Demolition.Levels;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demolition.Tests
{
    /// <summary>
    /// Every clip shot in content-work/shots plays to its end without recording, and no step
    /// runs out of time waiting for its collapse, blast or walk (a mistuned shot shows up here
    /// before an evening of recording). Bot footage (broll-*) is left out: whole levels take
    /// minutes. Window ▸ General ▸ Test Runner ▸ PlayMode.
    /// </summary>
    public class ShotRunTests
    {
        private static IEnumerable Shots() =>
            Directory.GetFiles("../content-work/shots", "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => !id.StartsWith("broll-"))
                .OrderBy(id => id);

        // Destroy is deferred to the end of the frame: wait for it, so the time scale and the
        // profile are back before the next shot's director starts.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ClipDirector.Current != null)
                Object.Destroy(ClipDirector.Current.gameObject);
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ShotPlaysToTheEnd([ValueSource(nameof(Shots))] string id)
        {
            ShotSpec shot = ShotSpec.FromJson(File.ReadAllText($"../content-work/shots/{id}.json"));
            Assert.AreEqual(0, shot.seed, "only shots on the level's own seed are played here");
            CampaignLevel level = GameConfig.Instance.Campaign.FirstOrDefault(l => l.Spec.id == shot.level);
            Assert.IsNotNull(level, shot.level);

            ClipDirector director = ClipDirector.Play(shot, 1, Path.Combine(Application.temporaryCachePath, "shot-" + id), false);
            LevelSelection.Select(level.Index);
            SceneManager.LoadScene(GameConfig.Instance.levelScene);

            float limit = Time.realtimeSinceStartup + 150f;
            while (director.State != ClipDirector.Stage.Done && Time.realtimeSinceStartup < limit)
                yield return null;

            Assert.AreEqual(ClipDirector.Stage.Done, director.State, $"{id} stalled in {director.State}");
            CollectionAssert.IsEmpty(director.Log.timeouts, $"{id}: steps that never saw their event");
            Assert.IsFalse(RunController.Current != null && RunController.Current.State.Phase == RunPhase.Failed && !id.StartsWith("keep-up"),
                $"{id}: the run failed during the shot");
        }
    }
}
#endif
