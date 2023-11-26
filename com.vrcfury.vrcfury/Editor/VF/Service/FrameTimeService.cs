﻿using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * This service gives you the current frametime. Woo!
     */
    [VFService]
    public class FrameTimeService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;

        private VFAFloat cachedFrameTime;
        public VFAFloat GetFrameTime() {
            if (cachedFrameTime != null) return cachedFrameTime;

            var fx = manager.GetFx();
            var timeSinceLoad = GetTimeSinceLoad();
            var lastTimeSinceLoad = fx.NewFloat("lastTimeSinceLoad");
            directTree.Add(math.MakeCopier(timeSinceLoad, lastTimeSinceLoad));

            var diff = math.Subtract(timeSinceLoad, lastTimeSinceLoad, name: "frameTime");

            cachedFrameTime = diff;
            return diff;
        }

        private VFAFloat cachedLoadTime;
        public VFAFloat GetTimeSinceLoad() {
            if (cachedLoadTime != null) return cachedLoadTime;

            var fx = manager.GetFx();
            var timeSinceStart = fx.NewFloat("timeSinceLoad");
            var layer = fx.NewLayer("FrameTime Counter");
            var clip = fx.NewClip("FrameTime Counter");
            clip.SetCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), timeSinceStart.Name()),
                AnimationCurve.Linear(0, 0, 10_000_000, 10_000_000)
            );
            layer.NewState("Time").WithAnimation(clip);

            cachedLoadTime = timeSinceStart;
            return timeSinceStart;
        }
    }
}
