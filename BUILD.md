# 源码构建 / Building from source

需要自己安装的《怪物火车2》、BepInEx 5、Trainworks Reloaded 0.7.20、Conductor 0.5.10及.NET 8 SDK。本包不含游戏程序集/反编译代码。

```powershell
dotnet build DevaClan.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Monster Train 2"
```

也可运行 `build.ps1 -GameDir <游戏目录> -DotnetDir <SDK根目录>`，直接调用编译器，不经NuGet。产物在 `bin/Release/netstandard2.1/DevaClan.dll`；部署时DLL与完整json、textures目录同级。

## 结构

- Plugin.cs：注册、充能/业力记忆、回合/能力联动与既有兼容补丁。
- EffectSupport.cs：公共工具、目标校验、原生状态效果子类。
- ChargeEffects.cs / AttackEffects.cs：充能和攻击。
- ChargeAbilities.cs：单位/建筑充能支付与冷却UI；ConversionTargeting.cs：转换器选择测试。
- NativeStatusTooltips.cs：原版停滞/不懈卡牌提示缺失回退。
- ConstructEffects.cs / ResetEffects.cs：造物与重置。
- TemporaryEffects.cs / RebirthEffect.cs：临时状态与复活选择。
- Effects.cs：共享召唤、重置、永久成长等底层操作。
- ChampionStats.cs / RoomAndRelic.cs：英雄属性及房间/遗物。
- json/*.json：按类型拆分的内容；content_io.py为离线工具提供统一读取与重复定义检查。

## 编辑与校验

直接改JSON或同名PNG，不必运行生成器。generate.py现在是export_content.py的兼容入口，只向新空目录导出现有内容：

```powershell
python export_content.py --output D:\DevaClan-export --include-art
```

校验脚本：validate.py（Python jsonschema + 上级trainworks-source/schemas）、validate-patches.ps1、test-combat.ps1、test-localization.ps1、test-content-runtime.ps1。PowerShell测试支持GameDir/DotnetDir参数。内容运行时检查执行已安装框架合并、类型解析、游戏关键词解析，不执行完整Unity初始化/finalizer。

package.py是当前工作区的打包入口：需要上级dotnet、trainworks-source、official-template（许可证）、已安装游戏和Python Pillow/jsonschema。重新编译校验后生成Windows、Thunderstore、源码包及报告到outputs/DevaClan-0.2.9-bugfix。换工作区需调整依赖路径；普通源码编译不需要打包环境。

自动检查不能替代游戏实测。详见BUGFIX-NOTES.md。
