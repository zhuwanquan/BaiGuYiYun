using System;
using System.IO;
using UnityEngine;

namespace BaiguVN
{
    public class SaveService
    {
        private const string SaveMagic = "BAIGU_V2_SAVE";
        public const int CurrentSchemaVersion = 3;

        // Invalid defaults: omitted header fields must never look like a valid save.
        [Serializable]
        private class SaveHeader
        {
            public string magic = null;
            public int schemaVersion = 0;
        }

        private string GetSlotPath(int slot)
        {
            if (slot == 0)
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    "save_auto.json"
                );
            }

            return Path.Combine(
                Application.persistentDataPath,
                $"save_{slot}.json"
            );
        }

        public bool HasSlot(int slot)
        {
            string path = GetSlotPath(slot);
            return File.Exists(path) || File.Exists(path + ".bak");
        }

        public void SaveSlot(
            int slot,
            VNSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot)
                );
            }

            if (snapshot.magic != SaveMagic ||
                snapshot.schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentException("只能写入有效的版本 3 存档。", nameof(snapshot));
            }
            if (!ValidatePayload(snapshot, out string validationError))
            {
                throw new ArgumentException(validationError, nameof(snapshot));
            }

            string path = GetSlotPath(slot);
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            Directory.CreateDirectory(Application.persistentDataPath);

            string json =
                JsonUtility.ToJson(snapshot, true);

            // 1. 写临时文件
            File.WriteAllText(
                tempPath,
                json
            );

            // 2. 回读并验证临时文件
            string verifyJson =
                File.ReadAllText(tempPath);

            if (!TryDeserialize(verifyJson, out VNSnapshot verify, out string verifyError) ||
                verify.sourceSchemaVersion != CurrentSchemaVersion)
            {
                throw new Exception(
                    $"存档临时文件校验失败：{verifyError}"
                );
            }

            // 3. 旧主档存在时先备份
            if (File.Exists(path))
            {
                File.Copy(
                    path,
                    backupPath,
                    true
                );
            }

            // 4. 临时文件替换主档
            File.Copy(
                tempPath,
                path,
                true
            );

            File.Delete(tempPath);

            Debug.Log(
                $"存档成功：Slot {slot}\n{path}"
            );
        }

        public VNSnapshot ReadSlot(
            int slot,
            string contentVersion)
        {
            string path = GetSlotPath(slot);
            string backupPath = path + ".bak";

            VNSnapshot snapshot =
                TryRead(path);

            // 主档坏了就尝试备份
            if (snapshot == null)
            {
                snapshot = TryRead(
                    backupPath
                );
            }

            if (snapshot == null)
            {
                return null;
            }

            if (snapshot.contentVersion != contentVersion)
            {
                Debug.Log(
                    $"存档内容版本已变化，将由剧情加载器检查节点和路线兼容性。" +
                    $"存档={snapshot.contentVersion}，当前={contentVersion}"
                );
            }

            return snapshot;
        }

        private VNSnapshot TryRead(
            string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json =
                    File.ReadAllText(path);

                if (TryDeserialize(json, out VNSnapshot snapshot, out string error))
                {
                    return snapshot;
                }

                Debug.LogWarning($"读取存档失败：{path}\n{error}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"读取存档失败：{path}\n{ex}"
                );

                return null;
            }
        }

        // Parsing/migration is side-effect free. The original slot and backup
        // are never rewritten while loading, including legacy version 2 slots.
        public static bool TryDeserialize(
            string json, out VNSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "存档内容为空。";
                    return false;
                }

                SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);
                if (header == null || header.magic != SaveMagic)
                {
                    error = "存档 magic 缺失或不正确。";
                    return false;
                }
                if (header.schemaVersion != 2 && header.schemaVersion != CurrentSchemaVersion)
                {
                    error = $"不支持的存档版本：{header.schemaVersion}。";
                    return false;
                }

                VNSnapshot parsed = JsonUtility.FromJson<VNSnapshot>(json);
                if (parsed == null || !ValidatePayload(parsed, out error)) return false;

                if (header.schemaVersion == 2)
                {
                    // Version 2 had no route/checkpoint data. The runner decides
                    // whether its surviving node belongs to the canonical route.
                    parsed.routeId = null;
                    parsed.routeKind = null;
                    parsed.routeRevision = 0;
                    parsed.resultId = null;
                    parsed.runCompleted = parsed.mainCompleted;
                    parsed.routeCheckpoint = null;
                }

                snapshot = GameState.FromSnapshot(parsed).ToSnapshot(parsed.contentVersion);
                snapshot.sourceSchemaVersion = header.schemaVersion;
                snapshot.migratedFromV2 = header.schemaVersion == 2;
                return true;
            }
            catch (Exception ex)
            {
                error = $"存档 JSON 损坏或格式不正确：{ex.Message}";
                snapshot = null;
                return false;
            }
        }

        private static bool ValidatePayload(VNSnapshot snapshot, out string error)
        {
            if (string.IsNullOrWhiteSpace(snapshot.contentVersion))
            {
                error = "存档缺少内容版本。";
                return false;
            }
            if (!ValidateRun(snapshot, out error)) return false;
            if (snapshot.routeCheckpoint != null &&
                !ValidateRun(snapshot.routeCheckpoint, out error))
            {
                error = $"路线检查点损坏：{error}";
                return false;
            }
            return true;
        }

        private static bool ValidateRun(VNRunSnapshot snapshot, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(snapshot.currentNodeId))
            {
                error = "存档缺少当前剧情节点。";
                return false;
            }
            if (snapshot.pageIndex < 0 || snapshot.routeRevision < 0)
            {
                error = "存档的页码或路线版本不能为负数。";
                return false;
            }
            if (snapshot.history != null)
            {
                foreach (HistoryEntry entry in snapshot.history)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.nodeId))
                    {
                        error = "存档的阅读历史包含无效记录。";
                        return false;
                    }
                }
            }
            if (snapshot.visuals != null && snapshot.visuals.props != null)
            {
                foreach (VNPropState prop in snapshot.visuals.props)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.instanceId) ||
                        string.IsNullOrWhiteSpace(prop.assetId) ||
                        !IsFinite(prop.x) || !IsFinite(prop.y) || !IsFinite(prop.scale))
                    {
                        error = "存档的画面道具状态损坏。";
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
