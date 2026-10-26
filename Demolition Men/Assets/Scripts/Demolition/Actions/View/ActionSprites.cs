using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>The actions' sprites in Resources/Actions, made by tools/artpipe/action_frames.py:
    /// each action's frames in Player 1's and Player 2's look, and the props (pallet, radio, note).</summary>
    public static class ActionSprites
    {
        private static readonly Dictionary<string, Sprite[]> _frames = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Sprite> _props = new Dictionary<string, Sprite>();

        /// <summary>The frames of <paramref name="info"/> in play order; an entry is null if its sprite is missing.</summary>
        public static Sprite[] Frames(ActionInfo info, bool player2)
        {
            string key = (player2 ? "P2/" : "P1/") + info.Id;
            if (_frames.TryGetValue(key, out Sprite[] frames))
                return frames;
            frames = new Sprite[info.Frames.Length];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Resources.Load<Sprite>($"Actions/{key}_{i}");
            return _frames[key] = frames;
        }

        public static Sprite Prop(string name)
        {
            if (!_props.TryGetValue(name, out Sprite sprite))
                _props[name] = sprite = Resources.Load<Sprite>("Actions/" + name);
            return sprite;
        }
    }
}
