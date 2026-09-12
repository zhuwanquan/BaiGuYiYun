using System;
using System.IO;
using UnityEngine;

namespace BaiguVN
{
    public class SettingsService
    {
        private const string FileName = "settings.json";

        public VNSettings settings = new VNSettings();

        public string FilePath
        {
            get
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    FileName
                );
            }
        }

        public void Load()
        {
            // 每次先恢复一份默认设置
            settings = new VNSettings();

            if (!File.Exists(FilePath))
            {
                Debug.Log(
                    "尚无 settings.json，使用默认设置。"
                );

                return;
            }

            try
            {
                string json =
                    File.ReadAllText(FilePath);

                VNSettings loaded =
                    JsonUtility.FromJson<VNSettings>(json);

                if (loaded != null)
                {
                    settings = loaded;
                }

                Debug.Log(
                    "设置读取成功：" + FilePath
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "设置读取失败，恢复默认值：\n" + ex
                );

                settings = new VNSettings();
            }
        }

        public void Save()
        {
            try
            {
                string json =
                    JsonUtility.ToJson(settings, true);

                File.WriteAllText(
                    FilePath,
                    json
                );

                Debug.Log(
                    "设置保存成功：" + FilePath
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "设置保存失败：\n" + ex
                );
            }
        }
    }
}