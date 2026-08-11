# DoomSurvivor Godot Migration Status

更新时间：2026-08-11

## 当前交付

- Godot 4.7.1 Mono/C# 工程可构建，已补 Windows Desktop 导出预设。
- APP 默认横屏，设计分辨率为 `1280 x 720`。
- 已有可运行菜单与战斗闭环：角色选择、快速开始、固定步长移动、触控摇杆、自动攻击、六武器、五被动、升级暂停、敌人追踪、击杀、经验、地图事件、波次/Boss、HUD、暂停、胜负结果。
- Core / Domains / Infrastructure 的基础契约已落地，配置与存档路径已预留替换点。
- Unity 配置和参考资源已复制到 Godot `resources/`，Unity 工程保持未修改。

## 验证结果

- `dotnet build --no-restore DoomSurvivor.godot.csproj`：0 warnings，0 errors。
- `dotnet test --no-restore tests/DoomSurvivor.Tests.csproj`：19/19 通过。
- `node tools/sync-from-unity.mjs --check`：config 7/7、art 58/58、models 13/13，缺失/哈希不一致均为 0。
- Godot 4.7.1 Mono headless：Bootstrap 主场景无错误；Battle smoke 输出 `wave=1/5, enemies=5, kills=3, weapons=1, events=21`。
- Windows Desktop 导出预设已验证到模板检查阶段；本机缺少 `4.7.1.stable.mono` Windows debug/release 模板，因此尚未生成独立 EXE。

## 未完成范围

未完成范围：Windows 独立 EXE 需要本机安装 Godot 4.7.1 Mono 导出模板；手动键鼠/触控验收仍需在桌面窗口完成。Android 导出和真机触控不在本轮阶段 11 的 Windows P0 门禁内。

## 阶段 3 / 4 交付与阶段 5 起点

- 阶段 3 已完成：配置源可注入测试，Builtin/Remote/Cache 回退、缓存损坏隔离、存档路径注入、原子写入和损坏存档备份均有测试覆盖。
- 阶段 4 已完成：横屏主菜单已加入配置来源/版本、角色与皮肤浏览、Stage 信息、Normal/Quick Run；设置面板已按 Audio、Display、Crates、Hidden Crates、Altar、Map、Waves、Skills 八个页签组织，并把对应设置持久化到 `GameSettings`。
- 阶段 4 场景流已落地：`AppRoot` 持久组合根通过 `PresentationSceneRouter` 加载 `main_menu.tscn` / `battle.tscn` 子场景，菜单与战斗之间传递不可变 `RunLoadout`。
- 阶段 5 已完成：`BattleSimulator` 只负责固定步长调度与快照；空间哈希、移动、刷怪、接触/自动攻击、晶体经验拾取、胜负与回收均为独立纯 C# gameplay systems。
- 阶段 6–8 已完成：六武器、五被动、升级三选一/暂停、五类地图事件、Normal/Quick 波次、2.2 秒清场、Boss 预警与 Boss 生命周期已接入配置快照。
- 阶段 9–10 已完成：HP/EXP/武器/被动/Boss/波次 HUD、Esc 暂停、F2 性能开关、设置应用、程序化音效和 `_Draw` 特效已落地。
- 阶段 11 进行中：当前单元测试总数 19 个，全部通过；资源 check 和 Bootstrap/Battle headless 均通过；独立 Windows 导出受缺失模板阻塞。
