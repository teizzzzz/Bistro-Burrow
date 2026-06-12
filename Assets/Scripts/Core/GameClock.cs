using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 游戏内时钟（GDD §5.1：1 现实秒 = 10 游戏分钟）。
    /// 白天 08:00→18:00 = 现实 60 秒；夜晚 19:00→次日 08:00 = 现实 78 秒。
    /// 由 GameManager 在 Day/Night 阶段驱动 Tick，其余阶段冻结。
    /// </summary>
    public class GameClock
    {
        /// <summary>当天 00:00 起累计的游戏分钟数（跨日时允许 >1440，由阶段切换时重置）。</summary>
        public float GameMinutes { get; private set; }

        readonly float _minutesPerRealSecond;

        public GameClock(float minutesPerRealSecond)
        {
            // 防御：配置为 0 会导致时间停滞死循环，强制最小值
            _minutesPerRealSecond = Mathf.Max(0.01f, minutesPerRealSecond);
        }

        public void SetHour(int hour) => GameMinutes = Mathf.Max(0, hour) * 60f;

        public void Tick(float realDeltaSeconds)
        {
            if (realDeltaSeconds <= 0f) return;
            GameMinutes += realDeltaSeconds * _minutesPerRealSecond;
        }

        /// <summary>当前小时（可超过 24，用于夜间跨日判断：32 = 次日 08:00）。</summary>
        public float HourFloat => GameMinutes / 60f;

        /// <summary>HH:MM 文本（自动对 24 取模显示）。</summary>
        public string TimeText
        {
            get
            {
                int total = Mathf.FloorToInt(GameMinutes);
                int h = (total / 60) % 24;
                int m = total % 60;
                return $"{h:00}:{m:00}";
            }
        }
    }
}
