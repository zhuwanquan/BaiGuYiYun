using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BaiguVN
{
    public class ProfileService
    {
        private const string ProfileMagic =
            "BAIGU_V2_PROFILE";

        private const int ProfileSchemaVersion = 1;

        private string GetProfilePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "profile.json"
            );
        }

        public bool HasProfile()
        {
            string path = GetProfilePath();

            return
                File.Exists(path) ||
                File.Exists(path + ".bak");
        }

        public VNProfile LoadOrCreate()
        {
            string path = GetProfilePath();
            string backupPath = path + ".bak";

            VNProfile profile =
                TryReadValid(path);

            // 主档异常时尝试备份。
            if (profile == null)
            {
                profile =
                    TryReadValid(backupPath);
            }

            // 第一次启动，没有任何档案。
            if (profile == null)
            {
                profile =
                    new VNProfile();
            }

            Normalize(profile);

            return profile;
        }

        public void SaveProfile(
            VNProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(
                    nameof(profile)
                );
            }

            Normalize(profile);

            profile.magic =
                ProfileMagic;

            profile.schemaVersion =
                ProfileSchemaVersion;

            string path =
                GetProfilePath();

            string tempPath =
                path + ".tmp";

            string backupPath =
                path + ".bak";

            string json =
                JsonUtility.ToJson(
                    profile,
                    true
                );

            // 1. 写入临时文件。
            File.WriteAllText(
                tempPath,
                json
            );

            // 2. 回读并验证临时文件。
            VNProfile verify =
                TryReadValid(
                    tempPath
                );

            if (verify == null)
            {
                throw new Exception(
                    "Profile 临时文件校验失败。"
                );
            }

            // 3. 已有主档时先备份。
            if (File.Exists(path))
            {
                File.Copy(
                    path,
                    backupPath,
                    true
                );
            }

            // 4. 临时文件替换主档。
            File.Copy(
                tempPath,
                path,
                true
            );

            File.Delete(tempPath);

            Debug.Log(
                $"Profile 保存成功：\n{path}"
            );
        }

        private VNProfile TryReadValid(
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

                VNProfile profile =
                    JsonUtility
                        .FromJson<VNProfile>(
                            json
                        );

                if (profile == null)
                {
                    return null;
                }

                if (profile.magic !=
                    ProfileMagic)
                {
                    Debug.LogWarning(
                        $"Profile magic 不正确：{path}"
                    );

                    return null;
                }

                if (profile.schemaVersion !=
                    ProfileSchemaVersion)
                {
                    Debug.LogWarning(
                        $"不支持的 Profile 版本：" +
                        $"{profile.schemaVersion}"
                    );

                    return null;
                }

                Normalize(profile);

                return profile;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"读取 Profile 失败：" +
                    $"{path}\n{ex}"
                );

                return null;
            }
        }

        private void Normalize(
            VNProfile profile)
        {
            if (profile.readNodeIds == null)
            {
                profile.readNodeIds =
                    new List<string>();
            }

            if (profile.completedChapters == null)
            {
                profile.completedChapters =
                    new List<string>();
            }

            if (profile.memorials == null)
            {
                profile.memorials =
                    new List<string>();
            }

            if (profile.collections == null)
            {
                profile.collections =
                    new List<string>();
            }
        }
    }
}