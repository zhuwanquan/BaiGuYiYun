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

        // choice 节点的选项列表。
        // 只在 type == "choice" 时使用。
        public VNChoice[] choices;
    }

    [Serializable]
    public class VNObserveObject
    {
        public string id;
        public string label;
        public string next;
    }

    [Serializable]
    public class VNChoice
    {
        public string id;
        public string label;
        public string next;

        // main = 原著线
        // perfect = 完美线
        // fun = 娱乐线（不参与结局/成就记录）
        // 空 = 继承当前分支类别
        public string kind;

        // 可选显示标签，如「娱乐」「原著」。
        public string tag;
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

        // 当前分支类别：main / perfect / fun / ""。
        // fun 分支不参与结局与成就记录。
        public string branchKind = "";
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