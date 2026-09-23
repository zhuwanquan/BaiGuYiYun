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

        public string choiceGroupId;
        public VNChoice[] choices;
        public VNVisualAction[] actions;
        public string resultId;

        public string backgroundId;
        public string cgId;

        public string portraitId;
        public int portraitSlot = 1;

        // -1 = 不突出任何角色
        // 0 = 左
        // 1 = 中
        // 2 = 右
        public int focusSlot = -1;

        public string bgmId;
        public string seId;
        public string ambienceId;

        public string noteId;
        public string completeChapter;

        // 跨轮永久纪念解锁。
        // 空字符串 = 本节点不解锁纪念。
        public string memorialId;

        // 跨轮永久收藏解锁。
        // 空字符串 = 本节点不解锁收藏。
        public string collectionId;

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

        // 当前显示的 CG。
        // 空 / null = 当前没有 CG。
        public string cgId;

        // 三个独立立绘槽
        public string leftPortraitId;
        public string centerPortraitId;
        public string rightPortraitId;

        public int focusSlot = -1;

        public string bgmId;
        public string ambienceId;
        public List<VNPropState> props = new List<VNPropState>();
    }

    [Serializable]
    public class VNSnapshot : VNRunSnapshot
    {
        public string magic = "BAIGU_V2_SAVE";
        public int schemaVersion = 3;
        public string contentVersion;
        public VNRunSnapshot routeCheckpoint;

        [NonSerialized] public bool migratedFromV2;
        [NonSerialized] public int sourceSchemaVersion;
    }

    [Serializable]
    public class VNProfile
    {
        public string magic =
        "BAIGU_V2_PROFILE";

        public int schemaVersion = 1;

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
