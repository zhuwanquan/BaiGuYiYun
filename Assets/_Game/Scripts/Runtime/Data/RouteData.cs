using System;
using System.Collections.Generic;

namespace BaiguVN
{
    public static class RouteKinds
    {
        public const string Canonical = "canonical";
        public const string Perfect = "perfect";
        public const string Entertainment = "entertainment";

        public static bool IsValid(string kind) =>
            kind == Canonical || kind == Perfect || kind == Entertainment;

        public static bool CanRecordRewards(string kind) =>
            kind == Canonical || kind == Perfect;
    }

    [Serializable]
    public class VNChoice
    {
        public string id;
        public string label;
        public string next;
    }

    [Serializable]
    public class VNAssetRequirement
    {
        public string id;
        public string kind;
    }

    [Serializable]
    public class VNRoute
    {
        public int schemaVersion = 1;
        public string routeId;
        public string kind = RouteKinds.Entertainment;
        public string choiceGroupId = "first_encounter";
        public string title;
        public string choiceText;
        public int order;
        public string status = "draft";
        public string entryNode;
        public int contentRevision = 1;
        public bool artReviewed;
        // Assigned by the catalog builder; source art stays outside Resources.
        public string resourceCatalogPath;
        public VNAssetRequirement[] requiredAssets = Array.Empty<VNAssetRequirement>();
        public VNNode[] nodes = Array.Empty<VNNode>();
    }

    [Serializable]
    public class VNRouteIndex
    {
        public int schemaVersion = 1;
        public VNRoute[] routes = Array.Empty<VNRoute>();
    }

    [Serializable]
    public class VNVisualAction
    {
        // transform, flash, shake, prop, clearProp, wait
        public string type;
        public string assetId;
        public string effectId;
        public int slot = 1;
        public float duration = 0.3f;
        public string instanceId = "prop";
        public float x;
        public float y;
        public float scale = 1f;
    }

    [Serializable]
    public class VNPropState
    {
        public string instanceId;
        public string assetId;
        public float x;
        public float y;
        public float scale = 1f;
    }

    // A checkpoint contains run state only: it cannot contain another checkpoint.
    [Serializable]
    public class VNRunSnapshot
    {
        public string currentNodeId;
        public int pageIndex;
        public List<string> observed = new List<string>();
        public List<HistoryEntry> history = new List<HistoryEntry>();
        public VisualSnapshot visuals = new VisualSnapshot();
        public List<string> completedChapters = new List<string>();
        public bool mainCompleted;
        public string routeId;
        public string routeKind;
        public int routeRevision;
        public bool runCompleted;
        public string resultId;
    }
}
