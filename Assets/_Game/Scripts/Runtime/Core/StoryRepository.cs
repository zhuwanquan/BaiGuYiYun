using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaiguVN
{
    public class StoryRepository
    {
        private VNStory story;
        private Dictionary<string, VNNode> index;

        public string ContentVersion
        {
            get
            {
                return story != null ? story.contentVersion : null;
            }
        }

        public string StartNode
        {
            get
            {
                return story != null ? story.startNode : null;
            }
        }

        public void Load(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(
                    nameof(textAsset),
                    "剧情 JSON 的 TextAsset 不能为空。"
                );
            }

            story = JsonUtility.FromJson<VNStory>(textAsset.text);

            if (story == null)
            {
                throw new Exception("剧情 JSON 解析失败。");
            }

            Validate();

            index = new Dictionary<string, VNNode>();

            foreach (VNNode node in story.nodes)
            {
                index.Add(node.id, node);
            }
        }

        public VNNode Get(string id)
        {
            if (index == null)
            {
                throw new InvalidOperationException(
                    "StoryRepository 尚未加载剧情。"
                );
            }

            if (!index.TryGetValue(id, out VNNode node))
            {
                throw new KeyNotFoundException(
                    $"找不到剧情节点：{id}"
                );
            }

            return node;
        }

        public bool HasNode(string id)
        {
            return index != null
                && !string.IsNullOrEmpty(id)
                && index.ContainsKey(id);
        }

        private void Validate()
        {
            if (story.schemaVersion != 2)
            {
                throw new Exception(
                    $"不支持的 schemaVersion：{story.schemaVersion}，需要版本 2。"
                );
            }

            if (string.IsNullOrWhiteSpace(story.contentVersion))
            {
                throw new Exception("contentVersion 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(story.startNode))
            {
                throw new Exception("startNode 不能为空。");
            }

            if (story.nodes == null || story.nodes.Length == 0)
            {
                throw new Exception("剧情 nodes 不能为空。");
            }

            HashSet<string> ids = new HashSet<string>();

            // 第一遍：检查 ID，并收集全部节点 ID
            foreach (VNNode node in story.nodes)
            {
                if (node == null)
                {
                    throw new Exception("剧情中存在空节点。");
                }

                if (string.IsNullOrWhiteSpace(node.id))
                {
                    throw new Exception("发现 id 为空的剧情节点。");
                }

                if (!ids.Add(node.id))
                {
                    throw new Exception(
                        $"发现重复的节点 ID：{node.id}"
                    );
                }

                if (node.type != "line"
                    && node.type != "observe"
                    && node.type != "pause"
                    && node.type != "end"
                    && node.type != "choice")
                {
                    throw new Exception(
                        $"节点 {node.id} 的 type 无效：{node.type}"
                    );
                }
            }

            // 起始节点必须存在
            if (!ids.Contains(story.startNode))
            {
                throw new Exception(
                    $"startNode 指向不存在的节点：{story.startNode}"
                );
            }

            // 第二遍：检查节点之间的连接
            foreach (VNNode node in story.nodes)
            {
                switch (node.type)
                {
                    case "line":
                    case "pause":
                        ValidateTarget(
                            ids,
                            node.id,
                            "next",
                            node.next
                        );
                        break;

                    case "observe":
                        ValidateObserveNode(ids, node);
                        break;

                    case "choice":
                        ValidateChoiceNode(ids, node);
                        break;

                    case "end":
                        if (!string.IsNullOrEmpty(node.next))
                        {
                            throw new Exception(
                                $"end 节点 {node.id} 不应设置 next。"
                            );
                        }
                        break;
                }
            }
        }

        private void ValidateChoiceNode(
            HashSet<string> ids,
            VNNode node)
        {
            if (node.choices == null ||
                node.choices.Length == 0)
            {
                throw new Exception(
                    $"choice 节点 {node.id} 至少需要一个选项。"
                );
            }

            HashSet<string> choiceIds =
                new HashSet<string>();

            foreach (VNChoice choice in node.choices)
            {
                if (choice == null)
                {
                    throw new Exception(
                        $"choice 节点 {node.id} 存在空选项。"
                    );
                }

                if (string.IsNullOrWhiteSpace(choice.id))
                {
                    throw new Exception(
                        $"choice 节点 {node.id} 存在 id 为空的选项。"
                    );
                }

                if (!choiceIds.Add(choice.id))
                {
                    throw new Exception(
                        $"choice 节点 {node.id} 存在重复选项 ID：{choice.id}"
                    );
                }

                if (string.IsNullOrWhiteSpace(choice.label))
                {
                    throw new Exception(
                        $"choice 节点 {node.id} 的选项 {choice.id} 没有 label。"
                    );
                }

                if (!string.IsNullOrWhiteSpace(choice.kind)
                    && choice.kind != "main"
                    && choice.kind != "perfect"
                    && choice.kind != "fun")
                {
                    throw new Exception(
                        $"choice 节点 {node.id} 的选项 {choice.id} kind 无效：{choice.kind}"
                    );
                }

                ValidateTarget(
                    ids,
                    node.id,
                    $"choices[{choice.id}].next",
                    choice.next
                );
            }
        }

        private void ValidateObserveNode(
            HashSet<string> ids,
            VNNode node)
        {
            ValidateTarget(
                ids,
                node.id,
                "resume",
                node.resume
            );

            if (node.objects == null || node.objects.Length == 0)
            {
                throw new Exception(
                    $"observe 节点 {node.id} 至少需要一个观察对象。"
                );
            }

            HashSet<string> objectIds = new HashSet<string>();

            foreach (VNObserveObject obj in node.objects)
            {
                if (obj == null)
                {
                    throw new Exception(
                        $"observe 节点 {node.id} 存在空观察对象。"
                    );
                }

                if (string.IsNullOrWhiteSpace(obj.id))
                {
                    throw new Exception(
                        $"observe 节点 {node.id} 存在 id 为空的观察对象。"
                    );
                }

                if (!objectIds.Add(obj.id))
                {
                    throw new Exception(
                        $"observe 节点 {node.id} 存在重复观察对象 ID：{obj.id}"
                    );
                }

                if (string.IsNullOrWhiteSpace(obj.label))
                {
                    throw new Exception(
                        $"observe 节点 {node.id} 的观察对象 {obj.id} 没有 label。"
                    );
                }

                ValidateTarget(
                    ids,
                    node.id,
                    $"objects[{obj.id}].next",
                    obj.next
                );
            }
        }

        private void ValidateTarget(
            HashSet<string> ids,
            string nodeId,
            string fieldName,
            string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new Exception(
                    $"节点 {nodeId} 的 {fieldName} 不能为空。"
                );
            }

            if (!ids.Contains(targetId))
            {
                throw new Exception(
                    $"节点 {nodeId} 的 {fieldName} 指向不存在的节点：{targetId}"
                );
            }
        }
    }
}