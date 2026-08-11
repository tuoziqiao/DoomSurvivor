using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomSurvivor.Gameplay.Effects
{
    /// <summary>
    /// 沿法线抖动的闪电路径。端点固定，中段抖动在靠近端点时自然收束。
    /// </summary>
    public static class LightningPath
    {
        public static List<Vector2> Build(
            float x1,
            float y1,
            float x2,
            float y2,
            int segmentCount,
            float jitter,
            Func<float> random = null)
        {
            random ??= () => UnityEngine.Random.value;
            var count = Mathf.Max(2, segmentCount);
            var dx = x2 - x1;
            var dy = y2 - y1;
            var length = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
            var nx = -dy / length;
            var ny = dx / length;
            var points = new List<Vector2>(count + 1) { new Vector2(x1, y1) };

            for (var i = 1; i < count; i++)
            {
                var t = i / (float)count;
                var envelope = Mathf.Sin(Mathf.PI * t);
                var normalOffset = (random() - 0.5f) * 2f * jitter * envelope;
                var tangentOffset = (random() - 0.5f) * jitter * 0.22f * envelope;
                points.Add(new Vector2(
                    x1 + dx * t + nx * normalOffset + dx / length * tangentOffset,
                    y1 + dy * t + ny * normalOffset + dy / length * tangentOffset));
            }

            points.Add(new Vector2(x2, y2));
            return points;
        }
    }
}
