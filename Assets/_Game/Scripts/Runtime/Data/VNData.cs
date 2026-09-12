using System;
using System.Collections.Generic;

namespace BaiguVN
{
    [Serializable]
    public class VNStory
    {
        public int schemaVersion = 2;
        public string contentVersion;
        public string startNode;
        public VNNode[] nodes;
    }

    [Serializable]
    public class VNNode
    {
        public string id;
        public string type;

        public string speaker;
        public string text;

        public string next;
        public string resume;

        public string backgroundId;
        public string portraitId;
        public string bgmId;
        public string noteId;

        public VNObserveObject[] objects;
    }

    [Serializable]
    public class VNObserveObject
    {
        public string id;
        public string label;
        public string next;
    }

    [Serializable]
    public class HistoryEntry
    {
        public string nodeId;
        public string speaker;
        public string text;
    }

    [Serializable]
    public class VisualSnapshot
    {
        public string backgroundId;
        public string portraitId;
        public string bgmId;
    }

    [Serializable]
    public class VNSnapshot
    {
        public string magic = "BAIGU_V2_SAVE";
        public int schemaVersion = 2;
        public string contentVersion;

        public string currentNodeId;
        public int pageIndex;

        public List<string> observed = new List<string>();
        public List<HistoryEntry> history = new List<HistoryEntry>();

        public VisualSnapshot visuals = new VisualSnapshot();

        public List<string> completedChapters = new List<string>();

        public bool mainCompleted;
    }

    [Serializable]
    public class VNProfile
    {
        public List<string> readNodeIds = new List<string>();
        public List<string> completedChapters = new List<string>();
        public List<string> memorials = new List<string>();
        public List<string> collections = new List<string>();
    }

    [Serializable]
    public class VNSettings
    {
        public float textSpeed = 30f;
        public int fontSize = 38;

        public float bgmVolume = 1f;
        public float seVolume = 1f;
    }
}