// 此文件由 scripts/build_webdemo_config.mjs 自动生成，请勿手改——
// 数值唯一源是 Assets/Resources/Configs/*.json（与 Unity 工程共用）。
(function (root, factory) {
  if (typeof module === "object" && module.exports) module.exports = factory();
  else root.GAME_CONFIG = factory();
})(typeof self !== "undefined" ? self : this, function () {
  return {
  "balance": {
    "gameMinutesPerRealSecond": 10,
    "dayStartHour": 8,
    "dayEndHour": 18,
    "nightStartHour": 19,
    "nightEndHour": 8,
    "startingGold": 60,
    "satietyMax": 100,
    "satietyBaseDecayPerSecond": 0.5,
    "satietyLoadDecayFactor": 1.5,
    "playerMaxHp": 100,
    "playerMoveSpeed": 4.5,
    "playerJumpSpeed": 7.5,
    "gravity": 20,
    "playerAttackDamage": 12,
    "playerAttackRange": 1.3,
    "playerAttackCooldown": 0.35,
    "starveHpLossPerSecond": 2,
    "maxCarryWeight": 30,
    "passOutLootLossPercent": 50,
    "newbieNights": 2,
    "newbieDamageMultiplier": 0.6,
    "patienceBaseSeconds": 22,
    "customerEatSeconds": 3,
    "baseSpawnIntervalSeconds": 6,
    "attractionSpawnDivisor": 50,
    "seasonStyleCoef": 1,
    "eventBonusCoef": 0,
    "tipFastServePercent": 25,
    "autoCookBaseInterval": 0.9,
    "dispatchFatigueCost": 40,
    "fatigueRecoverPerNight": 30,
    "fatigueDispatchLimit": 80,
    "dispatchYieldMin": 2,
    "dispatchYieldMax": 4,
    "darkCuisineSalvageGold": 5,
    "dispatchLootPool": [
      "walking_mushroom",
      "dungeon_herb",
      "honey_fruit",
      "slime_jelly"
    ],
    "startingIngredients": [
      {
        "id": "walking_mushroom",
        "count": 8
      },
      {
        "id": "dungeon_herb",
        "count": 5
      },
      {
        "id": "honey_fruit",
        "count": 3
      }
    ]
  },
  "ingredients": [
    {
      "id": "walking_mushroom",
      "displayName": "走路菇",
      "category": "Main",
      "flavors": [
        {
          "tag": "土",
          "value": 2
        },
        {
          "tag": "鲜",
          "value": 1
        }
      ],
      "weight": 2,
      "satietyRestore": 12,
      "basePrice": 4,
      "colorHex": "#C8A24B"
    },
    {
      "id": "mimic_meat",
      "displayName": "拟态怪嫩肉",
      "category": "Main",
      "flavors": [
        {
          "tag": "鲜",
          "value": 2
        },
        {
          "tag": "脆",
          "value": 1
        }
      ],
      "weight": 3,
      "satietyRestore": 18,
      "basePrice": 8,
      "colorHex": "#B06AC9"
    },
    {
      "id": "scorpion_claw",
      "displayName": "巨蝎螯肉",
      "category": "Main",
      "flavors": [
        {
          "tag": "鲜",
          "value": 3
        }
      ],
      "weight": 4,
      "satietyRestore": 22,
      "basePrice": 14,
      "colorHex": "#C9573F"
    },
    {
      "id": "dragon_loin",
      "displayName": "龙之里脊",
      "category": "Main",
      "flavors": [
        {
          "tag": "鲜",
          "value": 4
        },
        {
          "tag": "辛",
          "value": 1
        }
      ],
      "weight": 6,
      "satietyRestore": 40,
      "basePrice": 40,
      "colorHex": "#D94F4F"
    },
    {
      "id": "dungeon_herb",
      "displayName": "地穴香草",
      "category": "Side",
      "flavors": [
        {
          "tag": "脆",
          "value": 1
        },
        {
          "tag": "甘",
          "value": 1
        }
      ],
      "weight": 1,
      "satietyRestore": 5,
      "basePrice": 2,
      "colorHex": "#5FBF6E"
    },
    {
      "id": "honey_fruit",
      "displayName": "蜜露果",
      "category": "Side",
      "flavors": [
        {
          "tag": "甘",
          "value": 2
        }
      ],
      "weight": 1,
      "satietyRestore": 8,
      "basePrice": 3,
      "colorHex": "#E8C44D"
    },
    {
      "id": "slime_jelly",
      "displayName": "史莱姆凝胶",
      "category": "Seasoning",
      "flavors": [
        {
          "tag": "滑",
          "value": 2
        }
      ],
      "weight": 1,
      "satietyRestore": 6,
      "basePrice": 3,
      "colorHex": "#7FD3C8"
    },
    {
      "id": "ember_pepper",
      "displayName": "烬火椒",
      "category": "Seasoning",
      "flavors": [
        {
          "tag": "辛",
          "value": 2
        }
      ],
      "weight": 1,
      "satietyRestore": 4,
      "basePrice": 6,
      "colorHex": "#E06A3B"
    }
  ],
  "recipes": [
    {
      "id": "roasted_mushroom_skewer",
      "displayName": "烤走路菇串",
      "requiredFlavors": [
        {
          "tag": "土",
          "value": 2
        }
      ],
      "price": 12,
      "cookSeconds": 2,
      "defBuff": 5,
      "satietyBuff": 0,
      "unlockedAtStart": true
    },
    {
      "id": "herb_salad",
      "displayName": "翠叶精灵沙拉",
      "requiredFlavors": [
        {
          "tag": "脆",
          "value": 1
        },
        {
          "tag": "甘",
          "value": 1
        }
      ],
      "price": 8,
      "cookSeconds": 1.5,
      "defBuff": 0,
      "satietyBuff": 10,
      "unlockedAtStart": true
    },
    {
      "id": "mushroom_meat_pot",
      "displayName": "地牢风味菇菇鲜肉煲",
      "requiredFlavors": [
        {
          "tag": "土",
          "value": 2
        },
        {
          "tag": "鲜",
          "value": 2
        }
      ],
      "price": 30,
      "cookSeconds": 4,
      "defBuff": 20,
      "satietyBuff": 20,
      "unlockedAtStart": false
    },
    {
      "id": "slime_pudding",
      "displayName": "史莱姆果冻甜点",
      "requiredFlavors": [
        {
          "tag": "滑",
          "value": 2
        },
        {
          "tag": "甘",
          "value": 2
        }
      ],
      "price": 22,
      "cookSeconds": 3,
      "defBuff": 10,
      "satietyBuff": 10,
      "unlockedAtStart": false
    },
    {
      "id": "scorpion_soup",
      "displayName": "巨蝎浓汤",
      "requiredFlavors": [
        {
          "tag": "鲜",
          "value": 3
        },
        {
          "tag": "滑",
          "value": 1
        }
      ],
      "price": 45,
      "cookSeconds": 5,
      "defBuff": 35,
      "satietyBuff": 25,
      "unlockedAtStart": false
    },
    {
      "id": "dragon_steak",
      "displayName": "龙息厚切排",
      "requiredFlavors": [
        {
          "tag": "鲜",
          "value": 4
        },
        {
          "tag": "辛",
          "value": 2
        }
      ],
      "price": 120,
      "cookSeconds": 8,
      "defBuff": 60,
      "satietyBuff": 50,
      "unlockedAtStart": false
    }
  ],
  "monsters": [
    {
      "id": "mushroom_walker",
      "displayName": "走路菇怪",
      "maxHp": 20,
      "damage": 5,
      "moveSpeed": 1.2,
      "aggroRange": 3.5,
      "dropIngredientId": "walking_mushroom",
      "dropMin": 1,
      "dropMax": 2,
      "colorHex": "#C8A24B"
    },
    {
      "id": "slime_blob",
      "displayName": "软糊史莱姆",
      "maxHp": 14,
      "damage": 4,
      "moveSpeed": 1,
      "aggroRange": 3,
      "dropIngredientId": "slime_jelly",
      "dropMin": 1,
      "dropMax": 2,
      "colorHex": "#7FD3C8"
    },
    {
      "id": "mimic_crawler",
      "displayName": "拟态小怪",
      "maxHp": 35,
      "damage": 10,
      "moveSpeed": 1.8,
      "aggroRange": 4.5,
      "dropIngredientId": "mimic_meat",
      "dropMin": 1,
      "dropMax": 1,
      "colorHex": "#B06AC9"
    },
    {
      "id": "giant_scorpion",
      "displayName": "巨尾蝎",
      "maxHp": 60,
      "damage": 16,
      "moveSpeed": 1.5,
      "aggroRange": 5,
      "dropIngredientId": "scorpion_claw",
      "dropMin": 1,
      "dropMax": 1,
      "colorHex": "#C9573F"
    }
  ],
  "decor": [
    {
      "id": "wood_sign",
      "displayName": "木质招牌",
      "baseAttraction": 5,
      "cost": 50,
      "colorHex": "#8B5A2B"
    },
    {
      "id": "street_bench",
      "displayName": "临街长椅",
      "baseAttraction": 6,
      "cost": 90,
      "colorHex": "#A47B4F"
    },
    {
      "id": "welcome_lantern",
      "displayName": "迎宾提灯",
      "baseAttraction": 8,
      "cost": 120,
      "colorHex": "#E8A33D"
    },
    {
      "id": "fairy_plant",
      "displayName": "精灵盆栽",
      "baseAttraction": 12,
      "cost": 250,
      "colorHex": "#5FBF6E"
    }
  ],
  "staff": [
    {
      "id": "rabbit_cook",
      "displayName": "兔耳帮厨·莉珂",
      "shortName": "莉珂",
      "role": "Cook",
      "look": "spine:ak_amiya",
      "diligence": 5,
      "stamina": 4,
      "dailyWage": 15,
      "hireCost": 120,
      "colorHex": "#F2B8C6"
    },
    {
      "id": "novice_adventurer",
      "displayName": "见习冒险者·加恩",
      "shortName": "加恩",
      "role": "Gatherer",
      "look": "spine:ak_peacok",
      "diligence": 3,
      "stamina": 6,
      "dailyWage": 20,
      "hireCost": 150,
      "colorHex": "#7B9CD9"
    },
    {
      "id": "veteran_gatherer",
      "displayName": "风行猎手·薇尔",
      "shortName": "薇尔",
      "role": "Gatherer",
      "look": "spine:ak_platnm",
      "diligence": 7,
      "stamina": 5,
      "dailyWage": 35,
      "hireCost": 400,
      "colorHex": "#8FBF6B"
    }
  ],
  "shopLevels": [
    {
      "level": 1,
      "title": "破土开张",
      "upgradeCost": 0,
      "maxCustomersPerDay": 15,
      "difficultyFactor": 1,
      "stoveSlots": 2
    },
    {
      "level": 2,
      "title": "名震小镇",
      "upgradeCost": 800,
      "maxCustomersPerDay": 35,
      "difficultyFactor": 1.4,
      "stoveSlots": 3
    },
    {
      "level": 3,
      "title": "地穴巨擘",
      "upgradeCost": 3500,
      "maxCustomersPerDay": 70,
      "difficultyFactor": 2,
      "stoveSlots": 4
    }
  ]
};
});
