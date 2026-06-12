using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Cooking;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 黄昏经营大盘（GDD §2.1 黄昏结算期）：
    /// 五个页签——今日对账 / 菜品研发 / 店外采购 / 人事派遣 / 战前用餐，
    /// 底部两个夜间行动——亲自下地穴 / 派遣并就寝。
    /// 整面板代码化构建；每次操作后局部重建当前页签以刷新数据。
    /// </summary>
    public class SettlementPanel : MonoBehaviour
    {
        enum Tab { Report, Research, Decor, Staff, Meal }

        GameManager _gm;
        DayReport _report;
        Tab _tab = Tab.Report;

        Text _titleText;
        Text _goldText;
        RectTransform _content;
        readonly List<string> _pot = new(); // 研发大锅中的食材（≤3 个单位）

        static readonly Color WindowBg = new Color(0.10f, 0.10f, 0.15f, 0.97f);
        static readonly Color RowBg = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color BtnMain = new Color(0.8f, 0.55f, 0.25f);
        static readonly Color BtnSub = new Color(0.3f, 0.38f, 0.52f);
        static readonly Color TextMain = new Color(0.93f, 0.91f, 0.85f);
        static readonly Color TextDim = new Color(0.7f, 0.7f, 0.72f);

        /// <summary>构建静态外壳（背景/窗体/页签/底部行动按钮），内容区按页签动态填充。</summary>
        public void BuildShell(GameManager gm)
        {
            _gm = gm;

            // 半透明遮罩：挡住对场景的点击
            var dim = UiFactory.Panel(transform, new Color(0f, 0f, 0f, 0.55f), "Dim");
            UiFactory.FillParent((RectTransform)transform);
            UiFactory.FillParent(dim);

            RectTransform window = UiFactory.Panel(transform, WindowBg, "Window");
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(1000, 600));

            _titleText = UiFactory.Label(window, "黄昏结算", 26, TextMain, TextAnchor.MiddleLeft);
            UiFactory.Place((RectTransform)_titleText.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -34), new Vector2(420, 36));

            _goldText = UiFactory.Label(window, "", 22, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)_goldText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -34), new Vector2(300, 36));

            // 页签行
            RectTransform tabs = UiFactory.HorizontalGroup(window, 10f, "Tabs");
            UiFactory.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -88), new Vector2(952, 40));
            AddTabButton(tabs, "今日对账", Tab.Report);
            AddTabButton(tabs, "菜品研发", Tab.Research);
            AddTabButton(tabs, "店外采购", Tab.Decor);
            AddTabButton(tabs, "人事派遣", Tab.Staff);
            AddTabButton(tabs, "战前用餐", Tab.Meal);

            // 内容区（RectMask2D 裁剪：极端条目数下溢出部分不至于压到底部按钮）
            _content = UiFactory.Panel(window, new Color(1f, 1f, 1f, 0.03f), "Content");
            UiFactory.Place(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -118), new Vector2(952, 388));
            _content.pivot = new Vector2(0.5f, 1f);
            _content.gameObject.AddComponent<RectMask2D>();

            // 底部夜间行动（居中排列）
            RectTransform actions = UiFactory.HorizontalGroup(window, 18f, "Actions", TextAnchor.MiddleCenter);
            UiFactory.Place(actions, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(720, 52));
            Button goBtn = UiFactory.TextButton(actions, "亲自下地穴（动作采集）", () =>
            {
                if (_gm != null) _gm.BeginNightExpedition();
            }, BtnMain, Color.white, 20);
            UiFactory.SetLayoutSize(goBtn, 320, 52);
            Button sleepBtn = UiFactory.TextButton(actions, "派遣并就寝（跳过夜晚）", () =>
            {
                if (_gm != null) _gm.SleepThrough();
            }, BtnSub, Color.white, 20);
            UiFactory.SetLayoutSize(sleepBtn, 320, 52);
        }

        /// <summary>黄昏弹出时由 UIRoot 调用。</summary>
        public void Show(DayReport report)
        {
            _report = report;
            _pot.Clear();
            _tab = Tab.Report;
            Rebuild();
        }

        void AddTabButton(Transform parent, string label, Tab tab)
        {
            Button b = UiFactory.TextButton(parent, label, () =>
            {
                _tab = tab;
                Rebuild();
            }, new Color(0.2f, 0.22f, 0.32f), TextMain, 18);
            UiFactory.SetLayoutSize(b, 178, 40);
        }

        // =====================================================================
        // 页签内容构建
        // =====================================================================

        void Rebuild()
        {
            if (_gm == null || _content == null) return;
            if (_titleText != null) _titleText.text = $"黄昏结算 · 第 {_gm.State.DayIndex} 天";
            if (_goldText != null) _goldText.text = $"金币 {_gm.State.Gold}";

            // 清空内容区
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            switch (_tab)
            {
                case Tab.Report: BuildReportTab(); break;
                case Tab.Research: BuildResearchTab(); break;
                case Tab.Decor: BuildDecorTab(); break;
                case Tab.Staff: BuildStaffTab(); break;
                case Tab.Meal: BuildMealTab(); break;
            }
        }

        RectTransform NewList() =>
            UiFactory.VerticalGroup(_content, 6f, new RectOffset(18, 18, 12, 12), "List");

        void ListFill(RectTransform list) => UiFactory.FillParent(list);

        Text AddLine(Transform parent, string text, Color color, int size = 19)
        {
            Text t = UiFactory.Label(parent, text, size, color);
            UiFactory.SetLayoutSize(t, 0, 28);
            return t;
        }

        /// <summary>通用条目行：左侧说明 + 右侧操作按钮。</summary>
        void AddRow(Transform parent, string desc, string btnLabel, bool btnEnabled,
            UnityEngine.Events.UnityAction onClick, Color? btnColor = null)
        {
            RectTransform row = UiFactory.HorizontalGroup(parent, 12f, "Row");
            UiFactory.SetLayoutSize(row, 0, 40);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = RowBg;

            Text label = UiFactory.Label(row, "  " + desc, 18, TextMain);
            UiFactory.SetLayoutSize(label, 720, 40);

            if (btnLabel != null)
            {
                Button b = UiFactory.TextButton(row, btnLabel, onClick, btnColor ?? BtnMain, Color.white, 17);
                UiFactory.SetLayoutSize(b, 170, 34);
                b.interactable = btnEnabled;
                if (!btnEnabled)
                {
                    var img = b.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.3f, 0.3f, 0.33f);
                }
            }
        }

        // ---- ① 今日对账 ----
        void BuildReportTab()
        {
            RectTransform list = NewList();
            ListFill(list);
            DayReport r = _report ?? new DayReport();

            AddLine(list, $"接待顾客：{r.served} 位　|　气走：{r.angryLeft} 位　|　无菜可点离开：{r.noDishLeft} 位", TextMain);
            AddLine(list, $"营业额 {r.revenue} + 小费 {r.tips} − 员工日薪 {r.wages} ＝ 净利 {r.NetProfit} 金币", new Color(1f, 0.85f, 0.35f));

            float attraction = _gm.State.CurrentAttraction();
            BalanceDef bal = ConfigService.Balance;
            float interval = FormulaLib.CustomerSpawnInterval(bal.baseSpawnIntervalSeconds, attraction, bal.attractionSpawnDivisor);
            AddLine(list, $"店外吸引力 A = {attraction:0.#}（明日客流约每 {interval:0.0} 秒一位）", TextDim);

            ShopLevelDef cur = ConfigService.GetShopLevel(_gm.State.ShopLevel);
            AddLine(list, $"当前评级：{Stars(_gm.State.ShopLevel)} {cur?.title}（日客流上限 {cur?.maxCustomersPerDay}，难度 {cur?.difficultyFactor:0.0}）", TextMain);

            // 升级（GDD §5.2 成长表）
            ShopLevelDef next = (ConfigService.ShopLevels != null && _gm.State.ShopLevel < ConfigService.ShopLevels.Count)
                ? ConfigService.ShopLevels[_gm.State.ShopLevel] : null;
            if (next != null)
            {
                bool affordable = _gm.State.Gold >= next.upgradeCost;
                AddRow(list, $"升级 → {Stars(next.level)} {next.title}：客流上限 {next.maxCustomersPerDay}，灶台 {next.stoveSlots} 口（需 {next.upgradeCost} 金币）",
                    affordable ? "升级！" : "金币不足", affordable, () =>
                    {
                        if (_gm.State.TrySpendGold(next.upgradeCost))
                        {
                            _gm.State.Data.shopLevel = next.level;
                            _gm.State.NotifyChanged();
                            _gm.UI.Toast($"小馆升级为 {Stars(next.level)}「{next.title}」！明日生效。");
                            Rebuild();
                        }
                    });
            }
            else
            {
                AddLine(list, "已是最高评级——地穴巨擘。", TextDim);
            }

            AddLine(list, "", TextDim);
            AddLine(list, "提示：先研发新菜、买装饰、备好战前餐，再选择今晚的行动。", TextDim, 17);
        }

        static string Stars(int n) => new string('★', Mathf.Clamp(n, 1, 5));

        // ---- ② 菜品研发（GDD §4.1 风味矩阵融合） ----
        void BuildResearchTab()
        {
            RectTransform list = NewList();
            ListFill(list);

            AddLine(list, "选 1~3 个食材放入大锅，风味矩阵匹配即解锁新菜谱；失败炼成黑暗料理（回收少量金币）。", TextDim, 17);

            string potDesc = _pot.Count == 0 ? "（空）" : string.Join("、", PotNames());
            AddRow(list, $"大锅：{potDesc}", "清空", _pot.Count > 0, () => { _pot.Clear(); Rebuild(); });

            bool canCook = _pot.Count > 0;
            AddRow(list, $"风味求和：{FlavorSumText()}", "开锅！", canCook, () =>
            {
                ResearchOutcome outcome = RecipeSystem.Research(_gm.State, _pot, out RecipeDef matched);
                switch (outcome)
                {
                    case ResearchOutcome.UnlockedNew:
                        SfxSynth.Play(SfxSynth.Id.Unlock, 0.55f); // 解锁=高光时刻，三音琶音
                        _gm.UI.Toast($"研发成功！解锁新菜谱「{matched.displayName}」（售价 {matched.price}）");
                        break;
                    case ResearchOutcome.AlreadyKnown:
                        _gm.UI.Toast($"这锅炖出来还是「{matched.displayName}」，早就会做了（食材未消耗）。");
                        break;
                    default:
                        _gm.UI.Toast($"咕嘟咕嘟……炼成了微妙的黑暗料理（回收 {ConfigService.Balance.darkCuisineSalvageGold} 金币）。");
                        break;
                }
                _pot.Clear();
                Rebuild();
            }, BtnMain);

            AddLine(list, "── 库存食材（点击放入大锅） ──", TextDim, 17);
            foreach (ItemStack stack in _gm.State.Data.inventory)
            {
                if (stack == null || stack.count <= 0) continue;
                IngredientDef def = ConfigService.GetIngredient(stack.id);
                if (def == null) continue;
                int inPot = CountInPot(stack.id);
                int free = stack.count - inPot;
                AddRow(list, $"{def.displayName} ×{free}　[{FlavorText(def.flavors)}]　负重 {def.weight}",
                    "放入", free > 0 && _pot.Count < 3, () =>
                    {
                        _pot.Add(stack.id);
                        Rebuild();
                    }, BtnSub);
            }
            if (_gm.State.Data.inventory.Count == 0)
                AddLine(list, "库房空空如也……今晚去地穴采集吧。", TextDim);
        }

        IEnumerable<string> PotNames()
        {
            foreach (string id in _pot)
            {
                IngredientDef def = ConfigService.GetIngredient(id);
                yield return def != null ? def.displayName : id;
            }
        }

        int CountInPot(string id)
        {
            int n = 0;
            foreach (string p in _pot) if (p == id) n++;
            return n;
        }

        string FlavorSumText()
        {
            Dictionary<string, int> sum = RecipeSystem.SumFlavors(_pot);
            if (sum.Count == 0) return "（无）";
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in sum) parts.Add($"{kv.Key}{kv.Value}");
            return string.Join(" ", parts);
        }

        static string FlavorText(FlavorEntry[] flavors)
        {
            if (flavors == null || flavors.Length == 0) return "无味";
            var parts = new List<string>();
            foreach (FlavorEntry f in flavors) if (f != null) parts.Add($"{f.tag}{f.value}");
            return string.Join(" ", parts);
        }

        // ---- ③ 店外采购（GDD §4.2 吸引力引擎） ----
        void BuildDecorTab()
        {
            RectTransform list = NewList();
            ListFill(list);
            AddLine(list, $"店外装潢决定基础吸引力（当前 A = {_gm.State.CurrentAttraction():0.#}），明日营业生效。", TextDim, 17);

            if (ConfigService.Decors != null)
            {
                foreach (DecorDef def in ConfigService.Decors)
                {
                    if (def == null) continue;
                    bool owned = _gm.State.OwnsDecor(def.id);
                    bool affordable = _gm.State.Gold >= def.cost;
                    AddRow(list, $"{def.displayName}　吸引力 +{def.baseAttraction}　价格 {def.cost} 金币",
                        owned ? "已拥有" : (affordable ? "购买" : "金币不足"),
                        !owned && affordable, () =>
                        {
                            if (_gm.State.TryBuyDecor(def))
                            {
                                SfxSynth.Play(SfxSynth.Id.Coin, 0.4f);
                                _gm.UI.Toast($"已购入「{def.displayName}」，明天摆到店门口！");
                                Rebuild();
                            }
                        });
                }
            }
        }

        // ---- ④ 人事派遣（GDD §2.1 挂机派遣 / 疲劳值） ----
        void BuildStaffTab()
        {
            RectTransform list = NewList();
            ListFill(list);
            BalanceDef bal = ConfigService.Balance;
            AddLine(list, $"帮厨自动开火做菜；采集员可在夜晚派遣（疲劳 +{bal.dispatchFatigueCost}，≥{bal.fatigueDispatchLimit} 不可派遣；留守每晚恢复 {bal.fatigueRecoverPerNight}）。", TextDim, 16);

            if (ConfigService.Staffs != null)
            {
                foreach (StaffDef def in ConfigService.Staffs)
                {
                    if (def == null) continue;
                    StaffState st = _gm.State.FindStaff(def.id);
                    string roleText = def.role == "Cook" ? "帮厨" : "采集员";

                    if (st == null)
                    {
                        bool affordable = _gm.State.Gold >= def.hireCost;
                        string gap = affordable ? "" : $"（还差 {def.hireCost - _gm.State.Gold} 金币）";
                        AddRow(list, $"{def.displayName}（{roleText}）　日薪 {def.dailyWage}　签约费 {def.hireCost}{gap}",
                            affordable ? "雇佣" : "金币不足", affordable, () =>
                            {
                                if (_gm.State.TryHireStaff(def))
                                {
                                    SfxSynth.Play(SfxSynth.Id.Coin, 0.4f);
                                    _gm.UI.Toast($"{def.displayName} 入职了！明日清晨到岗。");
                                    Rebuild();
                                }
                            });
                    }
                    else if (def.role == "Gatherer")
                    {
                        bool tooTired = st.fatigue >= bal.fatigueDispatchLimit;
                        string desc = $"{def.displayName}（{roleText}）　疲劳 {st.fatigue}/100" + (tooTired ? "　——累瘫了，今晚必须休息" : "");
                        AddRow(list, desc,
                            st.dispatchTonight ? "取消派遣" : "今晚派遣",
                            !tooTired || st.dispatchTonight, () =>
                            {
                                st.dispatchTonight = !st.dispatchTonight;
                                _gm.UI.Toast(st.dispatchTonight
                                    ? $"{def.displayName} 今晚外出采集。"
                                    : $"{def.displayName} 今晚留守休息。");
                                Rebuild();
                            }, st.dispatchTonight ? BtnSub : BtnMain);
                    }
                    else
                    {
                        AddRow(list, $"{def.displayName}（{roleText}）　已入职，白天自动开火做菜", null, false, null);
                    }
                }
            }
        }

        // ---- ⑤ 战前用餐（GDD §5.3 以吃代练：Def_buff 由料理决定） ----
        void BuildMealTab()
        {
            RectTransform list = NewList();
            ListFill(list);

            string current = string.IsNullOrEmpty(_gm.State.NightMealName)
                ? "尚未用餐（今晚裸装下地穴）"
                : $"已食用「{_gm.State.NightMealName}」：减伤 Def+{_gm.State.NightDefBuff}，饱食上限 +{_gm.State.NightSatietyBuff}";
            AddLine(list, current, new Color(0.8f, 0.95f, 0.8f));
            AddLine(list, "吃一道已解锁的菜（消耗对应食材），获得今晚的减伤与饱食加成。", TextDim, 17);

            if (ConfigService.Recipes != null)
            {
                foreach (RecipeDef r in ConfigService.Recipes)
                {
                    if (r == null || !_gm.State.IsRecipeUnlocked(r.id)) continue;
                    bool payable = RecipeSystem.TryPayIngredients(r, _gm.State, consume: false);
                    AddRow(list, $"{r.displayName}　Def+{r.defBuff}　饱食上限+{r.satietyBuff}　[需风味 {FlavorText(r.requiredFlavors)}]",
                        payable ? "食用" : "食材不足", payable, () =>
                        {
                            if (RecipeSystem.TryPayIngredients(r, _gm.State, consume: true))
                            {
                                _gm.State.SetNightMeal(r);
                                SfxSynth.Play(SfxSynth.Id.Serve, 0.35f);
                                _gm.UI.Toast($"饱餐一顿「{r.displayName}」！今晚 Def+{r.defBuff}。");
                                Rebuild();
                            }
                        });
                }
            }
        }
    }
}
