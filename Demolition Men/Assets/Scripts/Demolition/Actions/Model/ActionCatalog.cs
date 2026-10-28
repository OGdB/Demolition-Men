using System.Collections.Generic;
using Demolition.Meta;

namespace Demolition.Actions
{
    /// <summary>The crew's special moves. A player carries one into a level (the loadout) and
    /// fires it once its meter is full.</summary>
    public enum ActionKind { Burp, ButtSlam, CraneDrop, Implosion, CoffeeBreak, Intern, HiVis, ShovelLean, SiteRadio }

    /// <summary>One action: what it's called, what fills its meter, and how its frames play.</summary>
    public sealed class ActionInfo
    {
        /// <summary>Hold the frame until the player lands (the butt slam's airborne pose).</summary>
        public const float UntilLanded = -1f;

        public ActionKind Kind;
        /// <summary>Save id, and the name of its frames in Resources/Actions/P1|P2.</summary>
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Meter points a full meter takes (<see cref="ActionMeter.PointsFor"/>).</summary>
        public float Charge;
        /// <summary>Seconds per frame, in play order; <see cref="UntilLanded"/> holds one until the player lands.</summary>
        public float[] Frames;
        /// <summary>The frame whose start fires the action into the world.</summary>
        public int StrikeFrame;
        /// <summary>Plays the frames over and over for this many seconds (a held pose); 0 plays them once.</summary>
        public float Hold;
        /// <summary>Seconds the action's effect lasts (a buff, the radio, the intern); 0 for a one-off.</summary>
        public float Duration;
        /// <summary>Cash it costs in the shop; 0 for the one every player starts with.</summary>
        public int Price;
    }

    /// <summary>Every action, in the order the loadout screen lists them, and the loadout itself.</summary>
    public static class ActionCatalog
    {
        public const ActionKind Default = ActionKind.Burp;

        public static readonly IReadOnlyList<ActionInfo> All = new[]
        {
            new ActionInfo
            {
                Kind = ActionKind.Burp, Id = "boer", Name = "THE BURP",
                Description = "A can of fizz, then a belch that shakes loose whatever is in front of you.",
                Charge = 90f, Frames = new[] { 0.55f, 0.12f, 0.35f, 0.08f, 0.5f }, StrikeFrame = 4,
            },
            new ActionInfo
            {
                Kind = ActionKind.ButtSlam, Id = "kontplof", Name = "BUTT SLAM",
                Description = "Up, trousers slipping, and down through every floor under you.",
                Charge = 90f, Frames = new[] { ActionInfo.UntilLanded, 0.08f, 0.45f }, StrikeFrame = 1, Price = 2500,
            },
            new ActionInfo
            {
                Kind = ActionKind.CraneDrop, Id = "kees", Name = "OI, CRANE!",
                Description = "Whistle up the crane: a pallet of bricks drops where you point.",
                Charge = 100f, Frames = new[] { 0.5f, 0.1f, 0.5f }, StrikeFrame = 2, Price = 4000,
            },
            new ActionInfo
            {
                Kind = ActionKind.Implosion, Id = "sloop", Name = "CONTROLLED DEMOLITION",
                Description = "Charges on the next building's footing. 3, 2, 1... it folds in on itself.",
                Charge = 140f, Frames = new[] { 1.4f, 0.1f, 0.6f }, StrikeFrame = 0, Price = 6000, // charges set, 3-2-1, plunger down: boom
            },
            new ActionInfo
            {
                Kind = ActionKind.CoffeeBreak, Id = "bakkie", Name = "COFFEE BREAK",
                Description = "A cup from the flask: faster feet and harder hits for a while.",
                Charge = 60f, Frames = new[] { 0.6f, 0.1f, 0.35f }, StrikeFrame = 2, Duration = 8f, Price = 1500,
            },
            new ActionInfo
            {
                Kind = ActionKind.Intern, Id = "stagiair", Name = "THE INTERN",
                Description = "Point at the job and the intern runs ahead doing it for you.",
                Charge = 90f, Frames = new[] { 0.15f, 0.15f, 0.3f, 0.15f }, StrikeFrame = 0, Hold = 1f, Duration = 10f, Price = 3500,
            },
            new ActionInfo
            {
                Kind = ActionKind.HiVis, Id = "hesje", Name = "HI-VIS VEST",
                Description = "Vest on, clipboard out: nothing can touch you. Falling rubble bounces off.",
                Charge = 60f, Frames = new[] { 0.15f, 0.15f, 0.3f, 0.15f }, StrikeFrame = 0, Hold = 0.8f, Duration = 8f, Price = 2000,
            },
            new ActionInfo
            {
                Kind = ActionKind.ShovelLean, Id = "schep", Name = "SHOVEL LEAN",
                Description = "Lean on your shovel and watch: you heal, your partner hits twice as hard.",
                Charge = 50f, Frames = new[] { 0.3f, 0.2f, 0.9f, 0.2f }, StrikeFrame = 0, Hold = 5f, Duration = 8f, Price = 1500,
            },
            new ActionInfo
            {
                Kind = ActionKind.SiteRadio, Id = "radio", Name = "SITE RADIO",
                Description = "Down goes the radio, up goes the volume: glass bursts, walls rattle loose.",
                Charge = 80f, Frames = new[] { 0.4f, 0.12f, 0.5f }, StrikeFrame = 2, Duration = 8f, Price = 4500,
            },
        };

        public static ActionInfo Get(ActionKind kind)
        {
            foreach (ActionInfo info in All)
                if (info.Kind == kind)
                    return info;
            return All[0];
        }

        /// <summary>The action <paramref name="id"/> names; <see cref="Default"/> for an unknown one.</summary>
        public static ActionKind FromId(string id)
        {
            foreach (ActionInfo info in All)
                if (info.Id == id)
                    return info.Kind;
            return Default;
        }

        /// <summary>What player <paramref name="player"/> (0 = P1) carries into a level: the pick, if the profile owns it.</summary>
        public static ActionKind Equipped(SaveData save, int player)
        {
            ActionKind kind = Picked(save, player);
            return Owns(save, kind) ? kind : Default;
        }

        private static ActionKind Picked(SaveData save, int player) =>
            save.actions != null && player >= 0 && player < save.actions.Count ? FromId(save.actions[player]) : Default;

        /// <summary>What player <paramref name="player"/> carries into a ranked run: any action, owned or
        /// not (docs/ranked.md K-D2).</summary>
        public static ActionKind RankedPick(SaveData save, int player) =>
            save.rankedActions != null && player >= 0 && player < save.rankedActions.Count ? FromId(save.rankedActions[player]) : Default;

        /// <summary>Pick <paramref name="kind"/> for player <paramref name="player"/>'s ranked runs.</summary>
        public static void PickRanked(SaveData save, int player, ActionKind kind)
        {
            save.rankedActions ??= new List<string>(); // a profile saved before ranked
            while (save.rankedActions.Count <= player)
                save.rankedActions.Add(Get(Default).Id);
            save.rankedActions[player] = Get(kind).Id;
        }

        public static bool Owns(SaveData save, ActionKind kind)
        {
            ActionInfo info = Get(kind);
            return info.Price <= 0 || (save.ownedActions != null && save.ownedActions.Contains(info.Id));
        }

        /// <summary>Buy an action with cash. False when owned already or too expensive.</summary>
        public static bool TryBuy(SaveData save, ActionKind kind)
        {
            ActionInfo info = Get(kind);
            if (Owns(save, kind) || save.cash < info.Price)
                return false;
            save.cash -= info.Price;
            save.ownedActions ??= new List<string>(); // a profile saved before the shop sold actions
            save.ownedActions.Add(info.Id);
            return true;
        }

        public static void Equip(SaveData save, int player, ActionKind kind)
        {
            if (player < 0)
                return;
            save.actions ??= new List<string>(); // a profile saved before actions
            while (save.actions.Count <= player)
                save.actions.Add(Get(Default).Id);
            save.actions[player] = Get(kind).Id;
        }
    }
}
