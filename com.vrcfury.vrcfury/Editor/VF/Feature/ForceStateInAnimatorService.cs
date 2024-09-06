using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {
    /**
     * When you first load into a world, contact receivers already touching a sender register as 0 proximity
     * until they are removed and then reintroduced to each other.
     */
    [VFService]
    internal class ForceStateInAnimatorService : FeatureBuilder {
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private readonly List<VFGameObject> _forceEnable = new List<VFGameObject>();
        public void ForceEnable(VFGameObject t) {
            _forceEnable.Add(t);
        }

        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Apply() {
            if (_forceEnable.Count == 0) return;

            var clip = clipFactory.NewClip("Force On");
            directTree.Add(clip);
            foreach (var obj in _forceEnable) {
                clip.SetEnabled(obj, true);
            }
        }
    }
}
