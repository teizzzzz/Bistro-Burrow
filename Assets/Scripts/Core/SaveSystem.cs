using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 存档系统：PlayerPrefs + JSON。
    /// 选用 PlayerPrefs 的原因（WebGL 兼容性）：WebGL 平台 PlayerPrefs 落在浏览器
    /// IndexedDB 中，而 File IO 在 WebGL 沙盒里不可靠；
    /// 浏览器随时可能被关闭，所以每次写档后必须显式 PlayerPrefs.Save()。
    /// </summary>
    public static class SaveSystem
    {
        const string Key = "bistro_burrow_save_v1";

        public static void Save(PlayerState state)
        {
            if (state == null || state.Data == null)
            {
                Debug.LogError("[SaveSystem] 尝试保存空状态，已跳过。");
                return;
            }
            try
            {
                string json = JsonUtility.ToJson(state.Data);
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save(); // WebGL：强制刷入 IndexedDB
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存失败: {e.Message}");
            }
        }

        /// <summary>读档；无档或损坏时返回 null（由调用方决定开新档）。</summary>
        public static PlayerState LoadOrNull()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                string json = PlayerPrefs.GetString(Key, null);
                if (string.IsNullOrEmpty(json)) return null;
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.version < 1) return null;
                return new PlayerState(data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 读档损坏，将开新档: {e.Message}");
                return null;
            }
        }

        public static void Wipe()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
