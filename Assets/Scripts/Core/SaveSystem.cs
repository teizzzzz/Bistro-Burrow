using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 存档系统：PlayerPrefs + JSON，支持 3 个独立档位。
    /// 选用 PlayerPrefs 的原因（WebGL 兼容性）：WebGL 平台 PlayerPrefs 落在浏览器
    /// IndexedDB 中，而 File IO 在 WebGL 沙盒里不可靠；
    /// 浏览器随时可能被关闭，所以每次写档后必须显式 PlayerPrefs.Save()。
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        const string LegacyKey = "bistro_burrow_save_v1";        // 多档位之前的单档键
        const string SlotKeyPrefix = "bistro_burrow_save_v1_slot"; // + 0/1/2

        static string KeyOf(int slot) => SlotKeyPrefix + Mathf.Clamp(slot, 0, SlotCount - 1);

        /// <summary>旧单档存档迁移到 1 号档位（仅当 1 号位为空时），启动时调用一次。</summary>
        public static void MigrateLegacySave()
        {
            if (!PlayerPrefs.HasKey(LegacyKey)) return;
            if (!PlayerPrefs.HasKey(KeyOf(0)))
            {
                PlayerPrefs.SetString(KeyOf(0), PlayerPrefs.GetString(LegacyKey));
                Debug.Log("[SaveSystem] 旧存档已迁移至档位 1。");
            }
            PlayerPrefs.DeleteKey(LegacyKey);
            PlayerPrefs.Save();
        }

        public static void Save(PlayerState state, int slot)
        {
            if (state == null || state.Data == null)
            {
                Debug.LogError("[SaveSystem] 尝试保存空状态，已跳过。");
                return;
            }
            try
            {
                string json = JsonUtility.ToJson(state.Data);
                PlayerPrefs.SetString(KeyOf(slot), json);
                PlayerPrefs.Save(); // WebGL：强制刷入 IndexedDB
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存档位{slot + 1}失败: {e.Message}");
            }
        }

        /// <summary>读档；无档或损坏时返回 null（由调用方决定开新档）。</summary>
        public static PlayerState LoadOrNull(int slot)
        {
            SaveData data = Peek(slot);
            return data != null ? new PlayerState(data) : null;
        }

        public static void Wipe(int slot)
        {
            PlayerPrefs.DeleteKey(KeyOf(slot));
            PlayerPrefs.Save();
        }

        /// <summary>该档位是否存在可读存档。</summary>
        public static bool HasSave(int slot) => Peek(slot) != null;

        /// <summary>只读窥探档位内容（主菜单摘要展示），不构建运行时状态。</summary>
        public static SaveData Peek(int slot)
        {
            string key = KeyOf(slot);
            if (!PlayerPrefs.HasKey(key)) return null;
            try
            {
                string json = PlayerPrefs.GetString(key, null);
                if (string.IsNullOrEmpty(json)) return null;
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return (data != null && data.version >= 1) ? data : null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 档位{slot + 1}读档损坏: {e.Message}");
                return null;
            }
        }
    }
}
