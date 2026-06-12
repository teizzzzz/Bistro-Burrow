using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 员工花名册（共享 UI 构件）：招聘 / 今晚派遣 / 疲劳状态 / 在岗说明。
    /// 同一份逻辑供两处使用——白天的员工管理面板（StaffPanel）与
    /// 黄昏结算的人事页签（SettlementPanel），保证行为一致不漂移。
    /// </summary>
    public static class StaffRosterUi
    {
        static readonly Color RowBg = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color TextMain = new Color(0.93f, 0.91f, 0.85f);
        static readonly Color TextDim = new Color(0.7f, 0.7f, 0.72f);
        static readonly Color BtnMain = new Color(0.8f, 0.55f, 0.25f);
        static readonly Color BtnSub = new Color(0.3f, 0.38f, 0.52f);

        /// <summary>把完整花名册构建进 parent（VerticalLayout 容器）。onChanged = 操作后的整体刷新回调。</summary>
        public static void BuildInto(RectTransform parent, GameManager gm, System.Action onChanged)
        {
            if (parent == null || gm == null || gm.State == null) return;
            BalanceDef bal = ConfigService.Balance;

            AddLine(parent,
                $"帮厨白天自动开火；采集员可夜派（疲劳 +{bal.dispatchFatigueCost}，≥{bal.fatigueDispatchLimit} 须休息；留守每晚恢复 {bal.fatigueRecoverPerNight}）。",
                TextDim, 16);

            int dispatchCount = 0;
            foreach (StaffState s in gm.State.Data.staff)
                if (s != null && s.dispatchTonight) dispatchCount++;

            if (ConfigService.Staffs != null)
            {
                foreach (StaffDef def in ConfigService.Staffs)
                {
                    if (def == null) continue;
                    StaffState st = gm.State.FindStaff(def.id);
                    string roleText = def.role == "Cook" ? "帮厨" : "采集员";

                    if (st == null)
                    {
                        // —— 未雇佣：招聘行 ——
                        bool affordable = gm.State.Gold >= def.hireCost;
                        string gap = affordable ? "" : $"（还差 {def.hireCost - gm.State.Gold} 金币）";
                        AddRow(parent,
                            $"{def.displayName}（{roleText}）　日薪 {def.dailyWage}　签约费 {def.hireCost}{gap}",
                            affordable ? "雇佣" : "金币不足", affordable, () =>
                            {
                                if (gm.State.TryHireStaff(def))
                                {
                                    SfxSynth.Play(SfxSynth.Id.Coin, 0.4f);
                                    gm.UI.Toast($"{def.displayName} 入职了！明日清晨到岗。");
                                    onChanged?.Invoke();
                                }
                            }, BtnMain);
                    }
                    else if (def.role == "Gatherer")
                    {
                        // —— 已雇佣采集员：派遣开关 + 疲劳状态 ——
                        bool tooTired = st.fatigue >= bal.fatigueDispatchLimit;
                        string status = tooTired ? "　——累瘫休息中，今晚必须留守"
                            : st.dispatchTonight ? "　——已列入今晚派遣队" : "";
                        AddRow(parent,
                            $"{def.displayName}（{roleText}）　疲劳 {st.fatigue}/100{status}",
                            st.dispatchTonight ? "取消派遣" : "今晚派遣",
                            !tooTired || st.dispatchTonight, () =>
                            {
                                st.dispatchTonight = !st.dispatchTonight;
                                SfxSynth.Play(SfxSynth.Id.Click, 0.35f);
                                gm.UI.Toast(st.dispatchTonight
                                    ? $"{def.displayName} 今晚外出采集。"
                                    : $"{def.displayName} 今晚留守休息。");
                                onChanged?.Invoke();
                            }, st.dispatchTonight ? BtnSub : BtnMain);
                    }
                    else
                    {
                        // —— 已雇佣帮厨：纯状态行 ——
                        AddRow(parent, $"{def.displayName}（{roleText}）　已在岗，自动开火做菜", null, false, null);
                    }
                }
            }

            AddLine(parent,
                $"今晚派遣：{dispatchCount} 人（黎明带回食材并结算疲劳）　|　当前金币：{gm.State.Gold}",
                new Color(1f, 0.85f, 0.35f), 17);
        }

        static void AddLine(Transform parent, string text, Color color, int size)
        {
            Text t = UiFactory.Label(parent, text, size, color);
            UiFactory.SetLayoutSize(t, 0, 28);
        }

        static void AddRow(Transform parent, string desc, string btnLabel, bool btnEnabled,
            UnityEngine.Events.UnityAction onClick, Color? btnColor = null)
        {
            RectTransform row = UiFactory.HorizontalGroup(parent, 12f, "Row");
            UiFactory.SetLayoutSize(row, 0, 40);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = RowBg;

            Text label = UiFactory.Label(row, "  " + desc, 18, TextMain);
            UiFactory.SetLayoutSize(label, 700, 40);

            if (btnLabel != null)
            {
                Button b = UiFactory.TextButton(row, btnLabel, onClick, btnColor ?? BtnMain, Color.white, 17);
                UiFactory.SetLayoutSize(b, 150, 34);
                b.interactable = btnEnabled;
                if (!btnEnabled)
                {
                    var img = b.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.3f, 0.3f, 0.33f);
                }
            }
        }
    }
}
