using System;
using System.IO;
using UnityEngine;

namespace BaiguVN
{
    public class SaveService
    {
        private const string SaveMagic = "BAIGU_V2_SAVE";

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
            return File.Exists(GetSlotPath(slot));
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

            string path = GetSlotPath(slot);
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

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

            VNSnapshot verify =
                JsonUtility.FromJson<VNSnapshot>(
                    verifyJson
                );

            if (verify == null ||
                verify.magic != SaveMagic ||
                verify.schemaVersion != 2)
            {
                throw new Exception(
                    "存档临时文件校验失败。"
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

            if (snapshot.magic != SaveMagic)
            {
                Debug.LogWarning(
                    "存档 magic 不正确。"
                );

                return null;
            }

            if (snapshot.schemaVersion != 2)
            {
                Debug.LogWarning(
                    $"不支持的存档版本：{snapshot.schemaVersion}"
                );

                return null;
            }

            if (snapshot.contentVersion != contentVersion)
            {
                Debug.LogWarning(
                    $"存档内容版本不兼容。存档={snapshot.contentVersion}，当前={contentVersion}"
                );

                return null;
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

                return JsonUtility
                    .FromJson<VNSnapshot>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"读取存档失败：{path}\n{ex}"
                );

                return null;
            }
        }
    }
}