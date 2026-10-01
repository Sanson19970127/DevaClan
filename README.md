# 天人氏族 / Deva Clan — 0.2.9 BUG 修复候选版

作者：sanson。适用于《怪物火车2》，中英文随游戏语言切换。

基于已通过玩家测试的0.2.8代码重构版，处理社区反馈第二部分及重构测试新增问题。本版自动检查已完成，仍需在游戏内按清单验证。

## 本次修改

- 薪火转换器恢复目标选择，保持每次获得1点余烬，禁止预览额外增加余烬。
- 单位和建筑主动能力支持消耗充能提前使用，包括原版建筑和禅修舱室。
- 可支付充能的能力显示原版冷却图标、剩余冷却数字与青色提示，保留点击资格。
- 充能HUD在撤销回合后读取实际资源；业力记忆和六道减费按卡牌实例隔离。
- 蓝图生成的韦陀恢复同一张卡后调用原版回手。
- 魔罗解放路线读取英雄卡升级，恢复保留业力的判断。
- 修罗场授予普通敌人的不懈不再赋予拆层权限；保留首领和天生不懈单位的原版行为。
- 三昧收集停滞词条；停滞/不懈缺少卡牌提示时复用原版单位提示。
- 乐欲盛宴费用由3点下调为2点

详见 `BUGFIX-NOTES.md`；实际检查结果及日志见 `VALIDATION-REPORT.json`。0.2.8结构改动记录保留在 `REFACTOR-NOTES.md`。

## 安装

1. 退出游戏，完整解压Windows ZIP，运行 `Install.cmd`。
2. 默认游戏目录 `D:\SteamLibrary\steamapps\common\Monster Train 2`，找不到时输入目录。
3. 安装器把旧DevaClan目录和旧TianRen DLL移至 `BepInEx/DevaClan-backups/时间戳/`，然后安装新包。
4. 启动游戏检查 `BepInEx/LogOutput.log` 中版本0.2.9、`missing registrations=0` 及中文检查结果。

**手动安装必须先移走整个旧DevaClan目录，再放入新版。不能合并文件夹保留旧 `json/content.json`，不能只替换DLL。** 回退同样恢复整个旧目录。

需要BepInEx 5、Trainworks Reloaded 0.7.20、Conductor 0.5.10。本包不附带依赖或游戏程序集。本次交付仅生成候选包，没有自动安装至游戏。

## 内容与维护

两名英雄、六条路线/十八个英雄升级定义，合计66个卡牌/能力定义、21个单位定义、10个遗物。充能、重置、营造、业力、造物仍是核心机制。

- 数值与双语：编辑 `json/cards.json`、`characters.json`、`effects.json`、`upgrades.json` 等对应文件。
- 关键词：`json/replacement_texts.json`，状态自身词条见 `status_effects.json`。
- 美术：依据 `ART-REPLACEMENT.csv` 与《独立图标替换清单.md》替换同名PNG，保留尺寸和透明通道。
- 核心机制：使用源码包，参阅 `BUILD.md`。

不要在模组JSON目录保留旧副本；加载器读取全部JSON，备份应放在模组目录之外。

请用新开局测试撤销回合。本版不保证重放旧版已经发生状态失配的战斗。

## 版权说明

本项目**源代码使用 MIT 许可证**。
项目内第三方美术、资源素材保留原作者版权，**不适用MIT协议**，使用这些素材请遵守原作者声明。

本MOD代码及大部分美术素材均由AI生成，部分图标素材来自game-icons.net

代码生成：GPT6Astra

美术：Midjourney/Nano Banana2/Seedream 5.0 pro

Mighty force icon by [Delapouite](https://delapouite.com/) under [CC BY 3.0](http://creativecommons.org/licenses/by/3.0/)

Cogsplosion icon by [Lorc](https://lorcblog.blogspot.com/) under [CC BY 3.0](http://creativecommons.org/licenses/by/3.0/)

## English

Version 0.2.9 is a gameplay bugfix candidate based on 0.2.8. It addresses charge-paid unit/building abilities, converter targeting, undo-sensitive memory/UI, blueprint recall, Mara retention, native status tooltips and Asura Arena floor destruction. Automated managed checks passed; Unity gameplay and full undo replay still require testing. Existing art, balance and content IDs are preserved.

Close the game, extract the Windows package and run Install.cmd. For manual installation, move the entire old DevaClan folder aside before copying the new one. Never retain old json/content.json alongside new files. Test undo in a new run. See BUGFIX-NOTES.md and VALIDATION-REPORT.json for scope and verification limits.
