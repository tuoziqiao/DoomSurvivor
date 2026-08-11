# DoomSurvivor Unity → Godot 重构计划

> 目标：将 `DoomSurvivor.unity`（Unity 6000.5.4f1）完整迁移至 `doom-survivor.godot`（Godot **4.7.1** + .NET），保持玩法、数值、配置与存档兼容，验收标准对齐 `docs/UNITY_ACCEPTANCE.md`。
>
> **框架优先**：首期交付单机 survivor，但目录、接口与依赖方向必须为后续能力预留扩展点——远程配置热更新、角色/皮肤系统、多地图、多人对战等。禁止把未来能力硬编码进战斗主循环。

---

## 0. 环境路径

| 项 | 路径 |
|----|------|
| Godot 安装目录 | `C:\MyApp\Godot` |
| Godot 编辑器（Mono / C#） | `C:\MyApp\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe` |
| Godot 控制台（调试） | `C:\MyApp\Godot\Godot_v4.7.1-stable_win64_console.exe` |
| 游戏项目目录 | `C:\MyProject\Game\DoomSurvivor\DoomSurvivor.godot` |
| Unity 参照工程 | `C:\MyProject\Game\DoomSurvivor\DoomSurvivor.unity` |

---

## 1. 现状盘点

### 1.1 Unity 工程（源）

| 模块 | 路径 | 职责 | 规模参考 |
|------|------|------|----------|
| Core | `Assets/DoomSurvivor/Core` | 状态机 DTO、伤害/经验公式、空间哈希、存档迁移 | `GameRules.cs`、`ConfigModels.cs`、`GameContracts.cs` |
| Gameplay | `Assets/DoomSurvivor/Gameplay` | 固定步长战斗、实体池、五武器/五被动、Boss、地图事件 | `BattleController.cs` ~3200 行 |
| Infrastructure | `Assets/DoomSurvivor/Infrastructure` | 配置加载（内置/缓存/远端）、原子存档 v1→v4 | `ConfigService.cs`、`SaveService.cs` |
| Presentation | `Assets/DoomSurvivor/Presentation` | AppRoot、UI Toolkit 菜单/HUD、输入桥接、程序化音效 | `MainMenuController.cs` ~1700 行 |
| Editor | `Assets/DoomSurvivor/Editor` | 幂等工程设置、Windows IL2CPP 构建 | `ProjectSetup.cs` |
| Tests | `Assets/DoomSurvivor/Tests` | EditMode 6 组 + PlayMode 3 组 | NUnit |
| 配置 | `Assets/StreamingAssets/GameConfig` | 7 个 JSON 分片 | characters/skins/enemies/weapons/skills/stages/balance |
| 美术 | `Assets/DoomSurvivor/Presentation/Resources/Art` | 武器/地图/技能/拾取 Sprite | PNG |
| 立绘 | `Assets/DoomSurvivor/Presentation/Resources/Models` | 角色 p1–p7、敌人 b1–b6 | PNG |

### 1.2 Godot 工程（目标）

- 已初始化 Godot **4.7.1**（`config/features` 含 `4.7`），启用 **.NET / C#**（`project.godot` 中 `[dotnet]` 段）。
- 项目路径：`doom-survivor.godot/`，与 Unity 工程并列存放。
- 业务脚本、场景与资源目录尚未落地；共享根目录 JSON 配置与美术资源（通过拷贝或符号链接）。

### 1.3 范围分层

| 层级 | 内容 | 约束 |
|------|------|------|
| **本期实现** | 单机 survivor：菜单、战斗、配置/存档、角色/皮肤选择、地图皮肤、波次/Boss | 行为对齐 Unity 验收 |
| **架构必须预留（本期只留接口/空实现）** | 远程配置热更新管道、内容 Catalog 抽象、对战 Session/网络门面、多 Stage 切换契约 | 不得阻塞首期；不得写死本地-only / 单人-only 假设 |
| **明确延期** | 登录、支付、商城、排行榜、JWT、云存档、正式美术替换、完整联机玩法 | 不实现业务，但依赖方向要能接入 |

---

## 2. 迁移与工程原则

1. **逻辑与表现分离**：`core` / 纯 `gameplay` 逻辑不依赖 `Godot.*` 类型，便于单测与 Unity 行为对照。
2. **配置驱动、零硬编码内容**：角色、皮肤、敌人、武器、技能、关卡、数值一律来自 JSON；代码只认 ID + Catalog。
3. **接口朝外、实现可替换**：配置源、存档、输入、网络、资源加载均通过接口注入，禁止 Presentation/Gameplay 直接 `new` 具体基础设施。
4. **C# 优先直迁**：Core/Infrastructure 可近乎逐文件移植；`MonoBehaviour` → Godot Node 脚本 + 固定步长累加器。
5. **分阶段可验收**：每阶段可启动、可手测、有自动化门禁。
6. **Unity 工程只读参照**：除非用户明确要求，不修改 `DoomSurvivor.unity`。
7. **扩展优先于捷径**：宁可多一个接口/一层目录，也不要把「以后再拆」写进战斗主文件。
8. **先竖切、后填肉**：阶段 0 之后优先跑通 Godot 可见闭环（占位角色可动）；美术与完整配置管道后置，禁止阻塞「能看见、能跑」。

---

## 3. 框架设计规范（强制）

> Agent / 开发者每次新增模块前先对照本节。违反依赖方向的 PR / 改动视为不合格。

### 3.1 分层与依赖方向

```
presentation  →  application  →  gameplay / domains  →  core
       ↓              ↓
 infrastructure ←（仅由 application 组装注入）
```

| 层 | 职责 | 允许依赖 | 禁止 |
|----|------|----------|------|
| `core` | 纯 DTO、公式、状态枚举、事件契约、无 IO | 无 | Godot、文件、网络、UI |
| `domains` | 角色/皮肤/地图/装备等业务领域服务（读 Catalog、校验解锁） | `core` | Godot、HTTP、UI |
| `gameplay` | 战斗仿真、波次、武器、碰撞（确定性优先） | `core`、domains 接口 | UI、网络协议细节、具体存档路径 |
| `application` | 用例编排：开局、切场景、加载配置、组局 | 上层接口 + domains | 直接操作 Node 树细节 |
| `infrastructure` | JSON/HTTP/本地文件/Godot Resource 加载实现 | `core` 契约 | 反向依赖 gameplay/presentation |
| `presentation` | 场景、UI、输入桥接、音效、视图绑定 | application 门面 + 只读 ViewModel | 写入存档逻辑、战斗公式 |
| `net`（预留） | 会话、同步、RPC 适配 | `core` 消息 DTO | 直接改 UI；绕过 Session |

**单向依赖**：下层不知上层；同层通过接口协作，避免循环引用。

### 3.2 解耦硬规则

1. **禁止上帝类**：禁止再出现 Unity 式 3000+ 行 `BattleController`；战斗按子系统拆分，主控制器只做调度。
2. **数据与视图分离**：`*Runtime`（纯数据）与 `*View`（`Node2D`/`Control`）分开；视图只订阅状态，不写规则。
3. **面向 ID，不面向资源路径**：业务层传 `characterId` / `skinId` / `stageId`；路径解析只在 Catalog / ResourceProvider。
4. **确定性仿真边界**：战斗步进输入为 `IBattleInput` + 配置快照；便于日后回放/锁步/主机权威。
5. **事件优于硬回调**：跨模块用领域事件 / 信号总线（如 `IEventBus`），避免 UI 直接调武器系统内部方法。
6. **功能开关**：远程配置与联机相关能力用 `FeatureFlags` / 空实现 stub，默认关闭，不污染主路径。

### 3.3 内容系统契约（角色 / 皮肤 / 地图）

首期就要建立稳定契约，后续只加实现与条目，不改调用方：

```csharp
// 示意：放在 core 或 domains 契约中
interface ICharacterCatalog {
    IReadOnlyList<CharacterConfig> All { get; }
    bool TryGet(string characterId, out CharacterConfig cfg);
}
interface ISkinCatalog {
    IReadOnlyList<SkinConfig> ForCharacter(string characterId);
    bool TryGet(string skinId, out SkinConfig cfg);
}
interface IStageCatalog {
    IReadOnlyList<StageConfig> All { get; }
    bool TryGet(string stageId, out StageConfig cfg);
}
interface IContentUnlockService {
    bool IsCharacterUnlocked(string characterId);
    bool IsSkinUnlocked(string skinId);
    // 未来：赛季、任务、商城解锁源可替换实现
}
```

| 系统 | 配置源 | 运行时职责 | 扩展点 |
|------|--------|------------|--------|
| 角色 | `characters.json` | 属性、默认武器、默认皮肤 | 新角色 = 新 JSON + 美术，不改战斗核心 |
| 皮肤 | `skins.json` | 按 `characterId` 绑定立绘/调色板 | 皮肤包可远程下发；解锁与展示分离 |
| 地图 / Stage | `stages.json` + map skin | 布局、刷怪表、事件权重、视觉皮肤 | 多地图 = 多 Stage 条目；战斗读 Stage 快照 |
| 武器 / 被动 | `weapons.json` / `skills.json` | 等级效果表 | 新武器注册到 WeaponSystem 表驱动 |
| 数值平衡 | `balance.json` | 全局倍率 | 远程覆盖本地内置 |

**规则**：UI 只展示 Catalog + Unlock 结果；开局把「选中的 Character/Skin/Stage」打成不可变 `RunLoadout` 快照传入 Battle，战斗内禁止再读可变全局选中状态。

### 3.4 远程配置热更新（管道预留）

目标形态：`内置 res://` → `远端 CDN/API` → `本地缓存`，可按分片版本增量更新。

```
IConfigRepository
  ├─ BuiltinConfigSource      // 包内 JSON
  ├─ RemoteConfigSource       // HTTP，支持 ETag / version
  └─ CachedConfigSource       // user:// 或 OS.GetUserDataDir 缓存

ConfigService.LoadAsync()
  → 合并分片 → Schema 校验 → 生成 GameConfigBundle 不可变快照
  → 发布 ConfigReloaded 事件（菜单可提示「配置已更新，下局生效」）
```

| 要求 | 说明 |
|------|------|
| 分片独立版本号 | 与现有 JSON `Version` 字段对齐 |
| Schema 校验失败回退 | 损坏远端不覆盖可用缓存；写 `.corrupt-时间戳` |
| 热更新粒度 | **下局生效**（首期）；禁止战斗中途替换 balance 导致不同步 |
| 鉴权占位 | `IRemoteConfigAuth` 空实现，日后接 JWT/签名 URL |
| 内容清单 | 可选 `manifest.json`（url、hash、size），为皮肤包/地图包下载预留 |

首期实现：Builtin + Cache + Remote 失败回退（对齐 Unity Infrastructure）；Remote 成功路径与 manifest 结构在阶段 3 定稿接口。

### 3.5 多人对战预留（不实现玩法，定边界）

| 概念 | 首期 | 后续 |
|------|------|------|
| `GameMode` | 仅 `SoloSurvivor` | `Coop` / `PvP` / `Async` 等枚举扩展 |
| `ISessionService` | 本地 `LocalSessionService` | 主机权威 / 锁步实现 |
| `INetTransport` | `NullNetTransport` | Godot Multiplayer / 自建 UDP |
| 战斗输入 | 本地 `IInputSource` | 远端输入队列写入同一 `IBattleInput` |
| 随机性 | 可注入 `IRng`（种子） | 开局同步 seed |

**禁止**：在 `BattleCombatSystem` 内直接读键盘；在 UI 里写「如果是联机则…」散落判断——统一经 `GameMode` + Session。

### 3.6 目标参考目录结构

> 按实际情况构建即可，这里只做参考

```
doom-survivor.godot/
├── project.godot
├── DoomSurvivor.godot.csproj
├── scenes/
│   ├── bootstrap.tscn
│   ├── main_menu.tscn
│   └── battle.tscn
├── prefabs/
│   ├── ui/
│   └── battle/
├── resources/
│   ├── config/                 # 内置 JSON 分片（可被远端覆盖缓存）
│   ├── art/                    # 武器/地图/技能/拾取等
│   └── models/                 # 角色 p1–p7、敌人 b1–b6
├── scripts/
│   ├── core/                   # DTO、公式、枚举、事件、接口契约
│   ├── domains/                # character / skin / stage / unlock
│   │   ├── characters/
│   │   ├── skins/
│   │   └── stages/
│   ├── gameplay/               # 战斗仿真子系统（无 Godot 优先）
│   │   ├── systems/
│   │   ├── catalogs/
│   │   └── effects/
│   ├── application/            # 用例：Boot、StartRun、ApplySettings
│   ├── infrastructure/         # Config/Save/Http/File 实现
│   │   └── config/
│   ├── net/                    # 预留：Session、Transport、Null 实现
│   └── presentation/           # Godot Node、UI、输入桥、音频
├── tests/
│   ├── core/
│   ├── domains/
│   └── infrastructure/
└── tools/
    └── sync-from-unity.mjs
```

### 3.7 依赖注入与组装

- **唯一组合根**：`AppRoot`（AutoLoad）或 `GameCompositionRoot` 负责 `new` 具体类并注入接口。
- 其他脚本通过构造函数 / 初始化方法接收依赖；禁止到处 `GetNode("/root/...")` 拿服务（视图查找节点除外）。
- 单测可替换：`IConfigService`、`ISaveService`、`IRng`、`ISessionService`。

---

## 4. 架构映射（Unity → Godot）

### 4.1 API 对照

| Unity | Godot 4.7 | 说明 |
|-------|-----------|------|
| `MonoBehaviour` | `Node` / `Node2D` / `Control` + C# 脚本 | `_Ready` / `_Process` / `_PhysicsProcess` |
| UI Toolkit | `Control` 树 + `.tscn` + `Theme` | 不做 UI Toolkit 直译 |
| `SpriteRenderer` | `Sprite2D` | 战斗实体 |
| `InputActionAsset` | `InputMap` | WASD、Esc、E、1/2/3 |
| `Resources.Load` | `ResourceLoader` / `GD.Load` | 经 `IResourceProvider` 封装 |
| `persistentDataPath` | `OS.GetUserDataDir()` | 经 `ISaveService` |
| `UnityWebRequest` | `HttpClient` / `HttpRequest` | 经 `IRemoteConfigSource` |
| 对象池 | 数据池 + Node 复用 | 敌人/子弹/晶体 |
| `FixedUpdate` 60Hz | `_Process` 累加器 `1/60`，`MaxStepsPerFrame = 5` | 与 Unity 一致 |
| `DontDestroyOnLoad` | AutoLoad | `AppRoot` |
| `SceneManager` | `ChangeSceneToFile` | Bootstrap → MainMenu → Battle |
| NUnit EditMode | `dotnet test` | core / domains / infrastructure |
| NUnit PlayMode | `--headless` 冒烟 + 手测 | |

### 4.2 开局数据流（解耦示意）

```
MainMenu (选角色/皮肤/Stage/模式)
  → application.StartRun(RunRequest)
  → domains 校验解锁 + Catalog 解析
  → 生成不可变 RunLoadout + GameConfigBundle 快照
  → SessionService.Create(Solo | 预留 Online)
  → 切 Battle 场景，注入 BattleController(loadout, systems...)
```

---

## 5. 分阶段计划

### 5.0 当前开发进度

> 状态说明：`未开始` → `进行中` → `已完成`。完成某阶段验收后，将本表对应行改为 `已完成`，并把「当前焦点」切到下一阶段。

| 阶段 | 名称 | 状态 | 备注 |
|------|------|------|------|
| 0 | 工程项目搭建 | 已完成 |  |
| 1 | 可运行竖切（占位角色） | 已完成 | 横屏 1280×720 竖切已验证 |
| 2 | Core + Domains 契约 | 已完成 | Core/Domains 契约与测试已落地 |
| 3 | Infrastructure（配置管道） | 已完成 | Builtin/Remote/Cache 回退、原子存档与损坏恢复已测试 |
| 4 | Presentation 壳层 | 已完成 | 横屏菜单、八页签设置与 MainMenu/Battle 场景流已验证 |
| 5 | 战斗核心循环 | 已完成 | 空间哈希、加速度移动、敌人追踪、自动战斗、掉落拾取、生命周期与纯 C# 系统拆分已通过测试/headless 冒烟 |
| 6 | 武器、被动与升级 | 已完成 | 六武器、五被动、配置驱动等级效果、升级三选一与升级暂停已落地 |
| 7 | 地图事件 | 已完成 | 补给箱/隐藏箱/祭坛/毒雾/治疗鸡靠近触发；事件效果与配置/设置接入 |
| 8 | 波次与 Boss | 已完成 | Normal/Quick 波次、2.2s 清场状态、Boss 预警/冲击、Boss 存活阻止提前胜利 |
| 9 | 战斗 HUD 与设置生效 | 已完成 | HP/EXP、武器/被动、波次/Boss/事件状态、升级面板、Esc 暂停、F2 性能开关与显示设置 |
| 10 | 音效与特效 | 已完成 | AudioStreamGenerator 程序化音效、武器/闪电/火区/地图事件绘制、粒子质量效果上限 |
| 11 | 测试与构建 | 进行中 | dotnet build/test、资源一致性 check、Bootstrap/Battle headless 已通过；Windows EXE 等待本机安装 4.7.1 Mono 导出模板 |
| F1 | 远程配置正式化 | 未开始 | 框架预留，本期不排期 |
| F2 | 内容运营（皮肤包/地图包） | 未开始 | 框架预留，本期不排期 |
| F3 | 多地图体验 | 未开始 | 框架预留，本期不排期 |
| F4 | 多人对战 | 未开始 | 框架预留，本期不排期 |

**当前焦点**：阶段 11（测试与构建）

---

### 阶段 0：工程基线

**目标**：目录骨架、组合根、空接口就位；工程可打开。

**验收**：编辑器打开无报错；分层目录存在；`dotnet test` 绿。

---

### 阶段 1：可运行竖切（占位角色）

**目标**：用 Godot 编辑器 / 运行按钮能进入游戏场景；屏幕上有可见角色；可移动、相机跟随。证明 C# 脚本链路通畅。**本阶段不接入正式美术**，用代码占位即可。

| 任务 | 产出 |
|------|------|
| 主运行场景设为启动/可 F5 运行 | 一键 Play 进入 |
| `PlaceholderVisual` / 程序化色块（`Polygon2D` / `ColorRect` / 代码生成 `ImageTexture`） | 玩家可见（如蓝色方块），无需 PNG |
| 玩家节点 + WASD / 方向键移动 | 位置随输入变化 |
| `Camera2D` 跟随玩家 | 镜头跟上 |
| （可选）一个占位敌人色块 | 证明多实体可挂 |
| 固定步长累加器骨架（可极简） | 为后续战斗步进留钩子 |
| 数据与视图分离雏形 | `PlayerRuntime` + `PlayerView`，视图可后换 Sprite |

**明确不做**：正式 Art 导入、完整配置加载、主菜单、武器/波次、远端配置。

**验收**：

1. Godot Mono 打开项目无编译错误。
2. 点击运行出现游戏窗口，可见占位人物。
3. WASD 可移动；相机跟随。
4. 控制台无持续性报错。

---

### 阶段 2：Core + Domains 契约

**目标**：纯 C# 复刻规则；角色/皮肤/Stage Catalog 接口落地。

| Unity 源 | Godot 目标 | 要点 |
|----------|------------|------|
| `GameRules.cs` | `core/GameRules.cs` | 伤害、经验、空间哈希、存档迁移 |
| `ConfigModels.cs` | `core/ConfigModels.cs` | JSON DTO 字段名一致 |
| `GameContracts.cs` | `core/GameContracts.cs` | Session、Settings、Result、`RunLoadout` |
| — | `domains/characters\|skins\|stages` | Catalog + Unlock 接口与内存实现 |

**测试**：伤害/经验/空间哈希/存档迁移/Settings.Clamp；Catalog 按 ID 查询与解锁过滤。

**验收**：`tests/core` + `tests/domains` 通过，对齐 Unity EditMode 对应用例。

---

### 阶段 3：Infrastructure（配置管道）

**目标**：内置 / 缓存 / 远端三源；远程更新接口定稿。

| 模块 | 实现 |
|------|------|
| `ConfigService` | Builtin → Remote → Cache；分片合并；下局生效 |
| `SaveService` | profile / settings 原子写；损坏备份 |
| `ConfigJson` | 校验对齐 Unity `Validate` |
| `IRemoteConfigSource` | 可 mock；失败回退 |

**验收**：单测覆盖内置、缓存命中、远端失败回退、损坏恢复。

---

### 阶段 4：Presentation 壳层

**目标**：Bootstrap → 主菜单；配置来源显示；角色/皮肤/（Stage 入口）可选。占位立绘可继续用色块/字母，正式美术可后换。

| 任务 | Unity 参照 |
|------|------------|
| AppRoot 初始化 Config + Save + StateMachine | `AppRoot.cs` |
| MainMenu：品牌、正常/快速测试、设置 | `MainMenuController.cs` |
| 七角色 + 皮肤 + 属性（经 Catalog/Unlock） | characters / skins |
| 设置六 Tab | `GameSettings` |
| 场景流转 | Build Settings |

**验收**：离线启动显示配置来源；1280×720 下关键 UI 完整可见（对齐验收文档；美术未齐时允许占位）。

---

### 阶段 5：战斗核心循环

**目标**：在阶段 1 竖切之上补全可玩战斗原型；子系统拆分，无上帝类。占位视觉可保留，逻辑对齐 Unity。

| 子系统 | 要点 |
|--------|------|
| `BattleController` | 只调度；固定步长 1/60 |
| `PlayerRuntime` / `EnemyRuntime` | 数据与 View 分离；View 可仍为占位 |
| `SpatialHashGrid` | broad phase |
| `IInputSource` | 仅输入适配，不进规则 |
| `Camera2D` | 跟随；视野外刷怪 |
| Stage 快照 | 布局 + map skin 来自 `RunLoadout`（可先写死一关） |

**验收**：能开一局；敌人追踪；击杀掉落晶体（晶体可用色块）。

---

### 阶段 6：武器、被动与升级

**目标**：六武器、五被动、升级三选一、暂停。

#### 武器（weapons.json）

| ID | 名称 | 机制摘要 |
|----|------|----------|
| `wind_blade` | 风刃 | 直线穿透弹 |
| `rotating_knife` | 飞轮术 | 环绕轨道，满级金轮 |
| `fubo_qin` | 伏波琴 | 光环 AOE + 音波 |
| `fire_bottle` | 火焰瓶 | 抛物线 + 地面持续区 |
| `lightning_chain` | 闪电链 | 链式跳跃 |
| `drone` | 无人机 | 自动追踪射击 |

#### 被动（skills.json）

> 等级数值、权重、稀有度以 `resources/config/skills.json` 为准；下表仅作机制摘要。

| ID | 名称 | 说明 |
|----|------|------|
| `passive_strength` | 力量 | 提高伤害输出（`damageMultiplierBonus`） |
| `passive_swift` | 迅捷 | 提高移动速度（`moveSpeedBonus`） |
| `passive_haste` | 急速 | 提高攻击速度（`attackSpeedBonus`） |
| `passive_magnet` | 磁力 | 提高经验拾取范围（`pickupRadiusBonus`） |
| `passive_toughness` | 坚韧 | 提高最大生命与护甲（`maxHpBonus` / `armorBonus`） |

以上均为 `type: passive`，`maxLevel: 5`，升级三选一池可抽取。

**验收**：均可获得并生效；升级暂停，选完恢复。

---

### 阶段 7：地图事件

补给箱 / 隐藏箱 / 祭坛 / 毒雾；靠近触发；毒雾持续掉血。

---

### 阶段 8：波次与 Boss

正常 10 波 / 快速 3～5 波；清场间隔 **2.2s**；BossIntro → 预警 → 冲击；胜负结算；重开无残留。

---

### 阶段 9：战斗 HUD 与设置生效

HP/EXP、武器栏、Boss 条、飘字、Chip、Esc 暂停、F2 Debug；设置下局生效。

---

### 阶段 10：音效与特效

| 模块 | Godot 方案 |
|------|------------|
| 程序化音效 | `AudioStreamGenerator` / 预生成流 |
| 技能特效 | `GPUParticles2D` / 序列帧 |
| 闪电链 | `Line2D` / `_Draw` |
| 特效质量 | 粒子数量分级 |

---

### 阶段 11：测试与构建

| 类型 | 工具 | 范围 |
|------|------|------|
| 单元测试 | `dotnet test` | core / domains / infrastructure |
| 场景冒烟 | 手动 + `--headless` | Bootstrap、Battle |
| 同步 CI | `sync-from-unity.mjs` | JSON/Art 一致 |

| 平台 | 优先级 |
|------|--------|
| Windows Desktop | P0 |
| Linux Desktop | P2 |
| Web | P3（C# 导出受限，另案） |

**验收**：门禁全绿；Windows 导出离线跑 10 秒无异常。

---

### 后续里程碑（框架已预留，本期不排期）

| 里程碑 | 内容 | 依赖的已有扩展点 |
|--------|------|------------------|
| F1 远程配置正式化 | manifest、签名、灰度、分片差量 | `IConfigService` / Remote / Cache |
| F2 内容运营 | 皮肤包/地图包下载、解锁源扩展 | Catalog + Unlock + ResourceProvider |
| F3 多地图体验 | 多 Stage 选择与差异化事件表 | `IStageCatalog` + `RunLoadout` |
| F4 多人对战 | Coop/PvP、同步、断线 | `GameMode` + `ISessionService` + `INetTransport` + `IRng` |

---

## 6. 风险与对策

| 风险 | 对策 |
|------|------|
| 再造上帝类 | §3 + 阶段 5 强制拆 systems |
| 远程配置与联机后加导致大改 | 阶段 0–3 定接口与空实现；阶段 1 先竖切验证运行时 |
| UI 与规则耦合 | ViewModel / 事件总线；UI 不写公式 |
| 内容硬编码路径 | 一律 ID → Catalog |
| 固定步长被 delta 污染 | 累加器；禁止逻辑直接用渲染 delta |
| 双工程配置漂移 | CI sync + diff |
| Godot Mono / SDK 不匹配 | 固定 4.7.1 Mono 编辑器 |

---

## 7. 里程碑对照

> 进度以 §5.0 为准；本表仅说明里程碑与阶段的对应关系。

| 里程碑 | 对应阶段 | 含义 |
|--------|----------|------|
| M0 框架骨架 | 0 | 目录、组合根、空接口、可开编辑器 |
| M1 竖切可跑 | 1 | Godot 运行可见占位角色，可移动 |
| M2 规则与配置管道 | 2–3 | Core/Domains + 配置/存档 |
| M3 主菜单完整 | 4 | Bootstrap → 主菜单可选角/皮肤 |
| M4 可玩战斗原型 | 5 | 刷怪、碰撞、晶体 |
| M5 完整战斗 | 6–8 | 武器/事件/波次/Boss |
| M6 体验对齐 | 9–10 | HUD、设置、音效特效 |
| M7 发布就绪（单机） | 11 | 测试与 Windows 导出 |
| F1–F4 | 后续 | 远程配置 / 内容运营 / 多地图 / 对战 |

---

## 8. Agent 协作约定

1. **每次迭代**先读本文档 §5.0 进度表与当前焦点；只做当前阶段。**框架预留任务**（接口/空实现）可在阶段 0 / 2–3 完成，不提前做联机/商城业务。阶段验收通过后**立即更新** §5.0 状态与「当前焦点」。
2. **新增功能**先定所在层与接口，再写实现；禁止跨层捷径。
3. **修改配置**必须 sync 或手动保持 Unity/Godot 一致。
4. **单测**优先 core / domains / infrastructure；Gameplay 规则变更补测试。
5. **UI** 对齐 `docs/UNITY_ACCEPTANCE.md`，不借机加新玩法；阶段 1–4 允许代码占位视觉。
6. **提交前** `dotnet test`；阶段 1 起须能 Godot 运行竖切；阶段 5 起完整 Battle 手测通过。
7. **禁止**擅自改 `DoomSurvivor.unity`。
8. **Godot** 使用 Mono 版编辑器，项目路径 `doom-survivor.godot/`。
9. **代码评审自检**：新代码是否把角色/皮肤/地图/配置源/网络写成硬编码？若是，打回解耦。
10. **Godot AI MCP**：处理 Godot 工程的场景检查、运行/调试、节点与资源操作、截图或日志验证时，可以使用 Godot AI MCP；目标项目必须是 `doom-survivor.godot/`，不得修改只读的 Unity 参照工程 `DoomSurvivor.unity`。MCP 操作仍须遵守本文件的分层、阶段范围、验证和最小变更约束。

## 附录 A：游戏内容清单（首期）

- **角色** 7：`gu_chen`、`ye_qing`、`lin_xian`、`su_lan`、`han_duo`、`mu_xue`、`lu_chuan`
- **武器** 6：见阶段 6 武器表
- **被动** 5：力量 / 迅捷 / 急速 / 磁力 / 坚韧（见阶段 6 被动表；数值以 `skills.json` 为准）
- **场景** 3：Bootstrap、MainMenu、Battle
- **地图皮肤** 5：grass_tile_01–04、dry_highland_coast
- **模式** 首期：`SoloSurvivor`；枚举预留扩展
