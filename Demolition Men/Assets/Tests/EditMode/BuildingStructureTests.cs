using System.Collections.Generic;
using Demolition.Core;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    /// <summary>
    /// Headless unit tests for the Option C support-graph. These run in the Unity Test
    /// Runner (EditMode) with no scene, physics, or play loop — they exercise the pure
    /// connectivity logic that decides what collapses.
    /// </summary>
    public class BuildingStructureTests
    {
        private static BuildingStructure Column(int height, int x = 0)
        {
            // Anchor at the bottom, then a vertical stack above it.
            var s = new BuildingStructure();
            s.AddCell(new Vector2Int(x, 0), isAnchor: true);
            for (int y = 1; y < height; y++)
                s.AddCell(new Vector2Int(x, y));
            return s;
        }

        [Test]
        public void RemoveMidColumn_DetachesEverythingAbove()
        {
            var s = Column(4); // (0,0)a (0,1) (0,2) (0,3)

            List<Vector2Int> detached = s.RemoveCell(new Vector2Int(0, 1));

            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 2), new Vector2Int(0, 3) }, detached);
            // Detached cells leave the structure; only the anchor remains.
            Assert.IsTrue(s.HasCell(new Vector2Int(0, 0)));
            Assert.IsFalse(s.HasCell(new Vector2Int(0, 2)));
            Assert.IsFalse(s.HasCell(new Vector2Int(0, 3)));
            Assert.AreEqual(1, s.CellCount);
        }

        [Test]
        public void RemoveTopBlock_DetachesNothing()
        {
            var s = Column(4);

            List<Vector2Int> detached = s.RemoveCell(new Vector2Int(0, 3));

            Assert.AreEqual(0, detached.Count);
            Assert.AreEqual(3, s.CellCount); // (0,0),(0,1),(0,2) still standing
        }

        [Test]
        public void RemoveAnchor_DetachesWholeColumn()
        {
            var s = Column(3); // (0,0)a (0,1) (0,2)

            List<Vector2Int> detached = s.RemoveCell(new Vector2Int(0, 0));

            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 1), new Vector2Int(0, 2) }, detached);
            Assert.AreEqual(0, s.CellCount);
        }

        [Test]
        public void SecondPathToGround_HoldsUpTheSpan()
        {
            // Two legs joined by a top span => a bridge/portal frame.
            //  y2: (0,2)(1,2)(2,2)
            //  y1: (0,1)      (2,1)
            //  y0: (0,0)a     (2,0)a
            var s = new BuildingStructure();
            s.AddCell(new Vector2Int(0, 0), true);
            s.AddCell(new Vector2Int(0, 1));
            s.AddCell(new Vector2Int(0, 2));
            s.AddCell(new Vector2Int(1, 2));
            s.AddCell(new Vector2Int(2, 2));
            s.AddCell(new Vector2Int(2, 1));
            s.AddCell(new Vector2Int(2, 0), true);

            // Knock out the left foundation: the left leg is still held via the span
            // and the right leg, so nothing should fall.
            List<Vector2Int> detached = s.RemoveCell(new Vector2Int(0, 0));
            Assert.AreEqual(0, detached.Count, "Redundant load path should keep the span up.");
            Assert.AreEqual(6, s.CellCount);

            // Now knock out the right foundation too: no anchors remain, everything drops.
            detached = s.RemoveCell(new Vector2Int(2, 0));
            Assert.AreEqual(5, detached.Count);
            Assert.AreEqual(0, s.CellCount);
        }

        [Test]
        public void RemoveNonexistentCell_IsNoOp()
        {
            var s = Column(2);
            List<Vector2Int> detached = s.RemoveCell(new Vector2Int(9, 9));
            Assert.AreEqual(0, detached.Count);
            Assert.AreEqual(2, s.CellCount);
        }

        [Test]
        public void ComputeSupported_ReachesEveryConnectedCellFromAnyAnchor()
        {
            // 3-wide, 3-tall solid block, anchored only at one bottom corner.
            var s = new BuildingStructure();
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    s.AddCell(new Vector2Int(x, y), isAnchor: x == 0 && y == 0);

            HashSet<Vector2Int> supported = s.ComputeSupported();
            Assert.AreEqual(9, supported.Count, "A solid connected slab is fully supported by one anchor.");
        }

        // ---------- Stress heuristic (pre-collapse lean visuals) ----------

        [Test]
        public void ComputeStress_PillaredColumn_IsZero()
        {
            var s = Column(4);
            foreach (var kv in s.ComputeStress())
                Assert.AreEqual(0f, kv.Value.Stress, 1e-5f,
                    $"Pillared cell {kv.Key} should carry no stress.");
        }

        [Test]
        public void ComputeStress_CantileverArm_GrowsWithDistance_AndLeansAwayFromSupport()
        {
            // Pillar x=0 (anchor at base) with a horizontal arm off the top: (1,3)(2,3)(3,3).
            var s = Column(4);
            s.AddCell(new Vector2Int(1, 3));
            s.AddCell(new Vector2Int(2, 3));
            s.AddCell(new Vector2Int(3, 3));

            var stress = s.ComputeStress(4);

            Assert.AreEqual(0.25f, stress[new Vector2Int(1, 3)].Stress, 1e-4f);
            Assert.AreEqual(0.50f, stress[new Vector2Int(2, 3)].Stress, 1e-4f);
            Assert.AreEqual(0.75f, stress[new Vector2Int(3, 3)].Stress, 1e-4f);
            for (int x = 1; x <= 3; x++)
                Assert.AreEqual(-1f, stress[new Vector2Int(x, 3)].Lean, 1e-4f,
                    "Supported from the left → the overhang tips clockwise (negative lean).");

            // The mast now carries the arm: load 3 shares / cap 4, moment -(1+2+3) / cap 8.
            var mastBase = stress[new Vector2Int(0, 0)];
            Assert.AreEqual(0.75f, mastBase.Stress, 1e-4f, "Loaded mast is stressed.");
            Assert.AreEqual(-0.75f, mastBase.Lean, 1e-4f, "Mast leans clockwise, toward its arm.");
            Assert.AreEqual(0.75f, stress[new Vector2Int(0, 3)].Drift, 1e-4f,
                "Mast top bows right (+x), toward the arm it carries.");
        }

        [Test]
        public void ComputeStress_SimplySupportedSpan_PeaksAtMidspan_WithMirroredLean()
        {
            // Two pillars (x=0 and x=6, anchored) joined by a beam row at y=2, x=1..5.
            var s = new BuildingStructure();
            for (int y = 0; y < 3; y++)
            {
                s.AddCell(new Vector2Int(0, y), isAnchor: y == 0);
                s.AddCell(new Vector2Int(6, y), isAnchor: y == 0);
            }
            for (int x = 1; x <= 5; x++)
                s.AddCell(new Vector2Int(x, 2));

            var stress = s.ComputeStress(4);
            var mid = stress[new Vector2Int(3, 2)];

            Assert.AreEqual(0.525f, mid.Stress, 1e-4f, "Mid-span carries the most stress.");
            Assert.AreEqual(0f, mid.Lean, 1e-4f, "Mid-span sags level.");
            Assert.Greater(mid.Stress, stress[new Vector2Int(2, 2)].Stress);
            Assert.Greater(mid.Stress, stress[new Vector2Int(4, 2)].Stress);
            Assert.AreEqual(-1f / 3f, stress[new Vector2Int(2, 2)].Lean, 1e-4f,
                "Left of mid-span tilts clockwise, toward the sag.");
            Assert.AreEqual(1f / 3f, stress[new Vector2Int(4, 2)].Lean, 1e-4f,
                "Right of mid-span mirrors it.");

            // Both pillars lean inward, toward the load between them.
            Assert.Less(stress[new Vector2Int(0, 0)].Lean, 0f, "Left pillar leans right (CW).");
            Assert.Greater(stress[new Vector2Int(6, 0)].Lean, 0f, "Right pillar leans left (CCW).");
        }

        [Test]
        public void ComputeStress_LoadedColumn_LeansTowardItsLoad()
        {
            // The "last wall standing" case: lone wall x=6 (anchored) carrying a roof row
            // that extends to its left. In real physics all the stress concentrates in
            // that wall — it must lean/bow toward the roof, not stand rigid.
            var s = new BuildingStructure();
            for (int y = 0; y < 4; y++)
                s.AddCell(new Vector2Int(6, y), isAnchor: y == 0);
            for (int x = 0; x < 6; x++)
                s.AddCell(new Vector2Int(x, 3)); // roof row hanging off the wall top

            var stress = s.ComputeStress(4);

            var wallBase = stress[new Vector2Int(6, 0)];
            Assert.AreEqual(1f, wallBase.Stress, 1e-4f,
                "Wall stress saturates under the roof load (4 shares / cap 4) → it trembles.");
            Assert.AreEqual(1f, wallBase.Lean, 1e-4f,
                "Net moment is left of the wall → leans counter-clockwise toward the roof.");

            Assert.AreEqual(0f, stress[new Vector2Int(6, 0)].Drift, 1e-4f, "No bend at the base.");
            Assert.AreEqual(-(1f / 3f) * (1f / 3f), stress[new Vector2Int(6, 1)].Drift, 1e-4f);
            Assert.AreEqual(-(2f / 3f) * (2f / 3f), stress[new Vector2Int(6, 2)].Drift, 1e-4f);
            Assert.AreEqual(-1f, stress[new Vector2Int(6, 3)].Drift, 1e-4f,
                "Bend displacement grows with height² — the wall bows left over the roof.");
        }

        [Test]
        public void ComputeStress_BalancedLoad_HeavyButLevel()
        {
            // Pillar with equal arms both sides: overloaded (trembles) but moments cancel,
            // so it stays upright with no lean and no bend.
            var s = new BuildingStructure();
            for (int y = 0; y < 3; y++)
                s.AddCell(new Vector2Int(2, y), isAnchor: y == 0);
            s.AddCell(new Vector2Int(0, 2));
            s.AddCell(new Vector2Int(1, 2));
            s.AddCell(new Vector2Int(3, 2));
            s.AddCell(new Vector2Int(4, 2));

            var pillar = s.ComputeStress(4)[new Vector2Int(2, 0)];
            Assert.AreEqual(1f, pillar.Stress, 1e-4f, "Four shares saturate the default capacity.");
            Assert.AreEqual(0f, pillar.Lean, 1e-4f, "Balanced moments — no lean.");
            Assert.AreEqual(0f, s.ComputeStress(4)[new Vector2Int(2, 2)].Drift, 1e-4f);
        }

        [Test]
        public void ComputeStress_HangingCell_GetsMaxStress()
        {
            // Pillar x=0 with a roof arm (1,2)(2,2); cell (2,1) hangs below the arm tip
            // with no contiguous row path to any pillar.
            var s = new BuildingStructure();
            s.AddCell(new Vector2Int(0, 0), isAnchor: true);
            s.AddCell(new Vector2Int(0, 1));
            s.AddCell(new Vector2Int(0, 2));
            s.AddCell(new Vector2Int(1, 2));
            s.AddCell(new Vector2Int(2, 2));
            s.AddCell(new Vector2Int(2, 1));

            var hanging = s.ComputeStress(4)[new Vector2Int(2, 1)];
            Assert.AreEqual(1f, hanging.Stress, 1e-5f);
            Assert.AreEqual(0f, hanging.Lean, 1e-5f, "No sideways support → straight-down sag.");
        }

        [Test]
        public void Cascade_RemovingSupportBaseDropsFloorsAbove()
        {
            // A wider structure: 4 cells wide, 4 tall, all connected, anchored across the
            // whole bottom row. Removing an entire interior column's base still leaves the
            // upper floors tied to neighbouring columns (horizontal floors) => no collapse,
            // which verifies floors provide lateral support. Then sever the top floor links
            // to isolate a column and confirm it drops.
            var s = new BuildingStructure();
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    s.AddCell(new Vector2Int(x, y), isAnchor: y == 0);

            // Remove base of column x=1. Column above stays up via horizontal floor links.
            var d1 = s.RemoveCell(new Vector2Int(1, 0));
            Assert.AreEqual(0, d1.Count);

            // Isolate cell (1,3): remove its horizontal neighbours (0,3) and (2,3) and the
            // cell below it (1,2). Now (1,3) has no intact neighbour => it detaches.
            s.RemoveCell(new Vector2Int(0, 3));
            s.RemoveCell(new Vector2Int(2, 3));
            var d2 = s.RemoveCell(new Vector2Int(1, 2));

            Assert.Contains(new Vector2Int(1, 3), d2, "Fully isolated cell must fall.");
        }
    }
}
