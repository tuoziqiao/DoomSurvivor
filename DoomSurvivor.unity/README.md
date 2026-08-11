# DoomSurvivor Unity

Windows x64 原生迁移工程，固定 Unity `6000.5.4f1`。

## 目录

- `Assets/DoomSurvivor/Core`：状态、DTO、伤害/经验/空间哈希、公共接口
- `Assets/DoomSurvivor/Gameplay`：固定步长战斗控制器、实体数据、对象池、五武器/五被动、Boss 与地图事件
- `Assets/DoomSurvivor/Infrastructure`：共享配置、缓存、原子存档与 v1→v4 迁移
- `Assets/DoomSurvivor/Presentation`：AppRoot、UI Toolkit 菜单/HUD、输入桥接、程序化音效池
- `Assets/DoomSurvivor/Editor`：幂等工程设置与 Windows IL2CPP 构建
- `Assets/DoomSurvivor/Tests`：EditMode/PlayMode 测试

## 开发

1. 使用 Unity Hub 打开本目录。
2. 执行 `DoomSurvivor > Run Project Setup`。
3. 打开 `Assets/DoomSurvivor/Scenes/Bootstrap.unity` 并进入 Play Mode。
4. WASD/方向键移动，Esc 暂停，F2 打开 Development 调试面板。

所有场景、URP 和 Input Actions 生成物均由 `ProjectSetup.Run` 管理；不要手改生成资产来保存关键配置。

## 战斗节奏

- 正常模式默认 10 波；快速测试使用 3～5 波。
- 清空当前波全部小怪后，间隔 2.2 秒进入下一波，不设置生存倒计时。
- 敌人数量随波数按指数增长，后续波次逐步加入快速、肥胖、首领和精英敌人。
- 最终波开始时触发 Boss；全部波次与 Boss 清除后胜利，生命归零则失败。

## 游戏设置

主菜单设置页与旧版一致，分为音效、画面、补给箱、隐藏箱、地图和波次六类。设置保存到
`Application.persistentDataPath/settings.json`，支持恢复默认；补给箱补刷、祭坛耗血、毒雾、
敌人数上限、特效质量、波数、小怪倍率与 Boss 数量会在下一局战斗中生效。

## 构建

- `DoomSurvivor > Build > Windows Development`
- `DoomSurvivor > Build > Windows Release`

两种构建均为 Windows x64 + IL2CPP。Development 输出 `artifacts/windows/DoomSurvivor-Development/`；Release 输出 `artifacts/windows/DoomSurvivor/`。
