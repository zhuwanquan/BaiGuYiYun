using System.Collections.Generic;

namespace BaiguVN
{
    public static class StoryAssetReferences
    {
        public static bool IsReference(string id) => !string.IsNullOrWhiteSpace(id) && id != "-" && id != "@clear";
        public static IEnumerable<KeyValuePair<string, string>> ForNode(VNNode node)
        {
            var refs = new List<KeyValuePair<string, string>>();
            Add(refs, node.backgroundId, "background"); Add(refs, node.portraitId, "portrait"); Add(refs, node.cgId, "cg");
            Add(refs, node.bgmId, "bgm"); Add(refs, node.seId, "se"); Add(refs, node.ambienceId, "ambience");
            if (node.actions != null) foreach (var action in node.actions)
            {
                if (action == null) continue;
                if (action.type == "transform") Add(refs, action.assetId, "portrait");
                if (action.type == "prop") Add(refs, action.assetId, "prop");
                Add(refs, action.effectId, "effect");
            }
            return refs;
        }
        public static IEnumerable<KeyValuePair<string, string>> ForVisuals(VisualSnapshot visuals)
        {
            var refs = new List<KeyValuePair<string, string>>(); if (visuals == null) return refs;
            Add(refs, visuals.backgroundId, "background"); Add(refs, visuals.cgId, "cg");
            Add(refs, visuals.leftPortraitId, "portrait"); Add(refs, visuals.centerPortraitId, "portrait"); Add(refs, visuals.rightPortraitId, "portrait");
            Add(refs, visuals.bgmId, "bgm"); Add(refs, visuals.ambienceId, "ambience");
            if (visuals.props != null) foreach (var prop in visuals.props)
                if (prop != null) Add(refs, prop.assetId, "prop");
            return refs;
        }
        private static void Add(List<KeyValuePair<string, string>> refs, string id, string kind)
        { if (IsReference(id) || (id == "@clear" && kind != "portrait")) refs.Add(new KeyValuePair<string, string>(id, kind)); }
    }
}
