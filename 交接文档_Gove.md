# 项目交接文档（给 Gove）

## 1. 项目定位与核心规则

这是一个“类 Multisim”的电路拼搭游戏：
玩家在二维坐标网格内，从背包取元器件，拖拽摆放，连接导线，观察/匹配信号波形来完成关卡。

### 1.1 不可改动的底层机制（硬约束）

以下机制是本项目的核心，不要改：

1. 网格化放置机制（元器件与导线关键点按 gridSpacing 吸附对齐）
2. 元器件-端子-导线的连接拓扑机制
3. 关卡的核心交互模型：在网格里搭元器件 -> 连线 -> 传递信号 -> 匹配接收器
4. 关卡依赖 `LevelRoot` + `Resources/Prefabs` 的基础装载范式

除以上底层机制外，UI、美术、关卡内容、交互细节、引导、数值参数、代码结构重构都可以改。

---

## 2. 运行与入口

- 场景入口：`Assets/Scenes/SampleScene.unity`
- Build Settings 当前只有该场景
- 游戏启动后由 `Test` 脚本自动从 `Resources/Prefabs/Level1.prefab` 开始加载

---

## 3. 目录与资源概览

### 3.1 关键脚本目录

- `Assets/Scripts/`
  - `Test.cs`：主流程、GM 面板、关卡加载/保存/重置/清空、背包 UI、胜利判定
  - `BackpackItemSpawner.cs`：背包库存静态状态、从背包生成元器件、LevelRoot 管理
  - `BackpackInventoryState.cs`：库存快照数据组件（用于关卡预制体保存/回放）
  - `WiringManager.cs`：连线输入、预览线、折点、选择/删除导线、锁定切换、鼠标光标
  - `WireConnection.cs`：单根导线数据、折线路径、信号波渲染、波形数学
  - `CircuitElement.cs` / `CircuitElementType.cs`：元器件类型与锁定状态
  - `DraggablePlacedComponent.cs`：元器件拖拽、回收背包
  - `SemiCircleElementSetup.cs`：元器件形状、端子生成、碰撞体配置、颜色/尺寸
  - `ConnectorTerminal.cs`：端子定义与显示控制
  - `GridRenderer2D.cs`：场景网格绘制
  - `VisualColor.cs`、`ColorApplier.cs`：视觉颜色辅助

### 3.2 关键资源目录

- `Assets/Resources/Prefabs/`
  - 元器件预制体：`SemiCircleGenerator`、`SemiCircleReceiver`、`Triangle*`、`Square*`、`*Converter`
  - 关卡预制体：`Level1.prefab`、`Level2.prefab`
- `Assets/Cursor/`：白/黄光标贴图（端子悬停切换）

---

## 4. 游戏系统说明（按功能）

## 4.1 元器件系统

- 元器件类型在 `CircuitElementType` 定义，共 9 种：
  - 三类发生器：半圆、三角、方波
  - 三类接收器：半圆、三角、方波
  - 三类转换器：半圆、三角、方波
- `CircuitElement` 持有：
  - `ElementType`
  - `IsLocked`（中键 GM 锁定）
- `SemiCircleElementSetup` 在运行时保证元器件具备：
  - 正确网格尺度
  - 正确几何形状（圆/三角/方）
  - 正确端子数量与位置
    - 普通器件：四向端子 `Terminal_0~3`
    - 转换器：`Terminal_Input` + `Terminal_Output`
  - 正确碰撞体

## 4.2 背包系统

- 背包库存是 `BackpackItemSpawner` 内的静态字典（运行时状态）
- 左侧背包 UI 在 `Test.OnGUI` 绘制，可滚动
- 点击背包条目会调用 `TrySpawnFromInventory`：
  - 扣减库存
  - 从 `Resources` 加载对应元器件预制体
  - 吸附到网格
  - 自动挂到 `LevelRoot`
  - 自动补齐 `CircuitElement`、`SemiCircleElementSetup`、`DraggablePlacedComponent`
- 把场上元器件拖回背包区域（左侧）会回收并返还库存

## 4.3 连线与信号系统（核心）

- `WiringManager` 管理连线交互：
  - 左键点端子开始连线，再点端子完成连线
  - 连线中左键空白处添加折点
  - 右键或 Esc 取消预览线
  - 左键点近导线可选中，Delete/Backspace 删除
- 导线为正交折线（非斜线），路径由 `WireConnection.BuildOrthogonalPolyline` 生成
- `WireConnection` 负责：
  - 渲染导线本体、选中描边、信号波动画线
  - 识别信号源
  - 计算波形：
    - `EvaluateSemicircleWave`
    - `EvaluateTriangleWave`
    - `EvaluateSquareWave`
- 转换器规则：
  - 输出端必须是 `Terminal_Output`
  - 必须在输入端 `Terminal_Input` 接收到可用输入信号，才允许输出转换后波形

## 4.4 胜利判定与关卡推进

- `Test.Update` 每 `0.2s` 做一次胜利检测
- 判定逻辑：
  - 统计所有接收器（场上 + 背包内）
  - 场上接收器必须至少连接一根带信号导线
  - 信号类型与接收器类型要匹配
  - 全部接收器都匹配才算胜利
- 胜利后自动加载下一关：`Level{n+1}`

---

## 5. GM 功能（重点）

GM 入口脚本是 `Test.cs`，场景对象为 `GMTool`。

### 5.1 打开方式

- 运行时按 `F10` 显示/隐藏 GM 面板

### 5.2 面板功能

1. **添加元器件到背包**
   - 选择 `CircuitElementType`
   - 输入数量
   - 点击“添加到背包”
2. **保存当前关卡为预制体**
   - 输入关卡名
   - 点击保存
   - 若同名已存在，需再点一次确认覆盖
3. **一键重置关卡**
   - 重新加载当前关卡预制体
4. **一键清除关卡**
   - 清空 `LevelRoot` 下子物体
   - 清空背包库存

### 5.3 GM 隐藏操作

- 仅当 GM 面板可见时：
  - **鼠标中键**可对“元器件/导线”切换锁定状态
  - 锁定后，导线删除会受限；元器件拖拽受限

### 5.4 编辑器 ContextMenu

`Test.cs` 里还有右键脚本组件菜单：

- `GM/向背包添加配置数量元器件`
- `GM/保存当前关卡为预制体`

---

## 6. 关卡搭建与保存（重点）

## 6.1 关卡数据载体

关卡以 Prefab 保存，路径约定：

- `Assets/Resources/Prefabs/Level1.prefab`
- `Assets/Resources/Prefabs/Level2.prefab`
- ...

运行时通过 `Resources.Load("Prefabs/LevelX")` 读取。

## 6.2 关卡根节点规范

- 场景中必须有 `LevelRoot`
- 所有关卡对象应在其下（元器件、导线、库存状态对象）
- 保存前会调用 `EnsureSceneObjectsUnderLevelRoot` 做归拢

## 6.3 库存状态的保存方式

- `BackpackInventoryState` 组件保存 `records`：
  - `type`
  - `count`
  - `prefabResourcePath`
- 这保证“关卡开始时背包里有哪些元器件”也能随关卡保存

## 6.4 搭建关卡推荐流程

1. 运行游戏，按 F10 打开 GM
2. 通过 GM 往背包加器件
3. 从背包点击生成器件，拖到网格区摆放
4. 连接导线、加折点，构建目标电路
5. 需要时中键锁定关键器件/导线
6. 在 GM 输入关卡名（建议 `Level数字`）
7. 点击“保存当前关卡为预制体”
8. 到 `Assets/Resources/Prefabs/` 检查 prefab 是否生成/更新

## 6.5 命名与推进规则

- 自动流程按 `Level1 -> Level2 -> Level3 ...` 递增
- 若要接入自动下一关，请遵守 `Level数字` 命名

---

## 7. 场景对象职责（SampleScene）

- `GameSystems`
  - `GridRenderer2D`：绘制右侧网格
  - `WiringManager`：连线与信号交互
- `GMTool`
  - `Test`：主控制 + GM + 关卡
- `BackpackPanel`
  - 作为左侧背包区域显示与交互定位参考
- `LevelRoot`
  - 当前关卡运行态对象容器

---

## 8. 已知注意点

1. `Test` 是主控脚本，命名偏临时，后续可重命名，但行为要保持一致
2. 当前关卡保存仅在 Unity Editor 下可用（依赖 `UnityEditor` API）
3. `SampleScene` 里存在旧序列化字段痕迹（不影响运行），改脚本字段时要注意序列化兼容
4. 库存是运行时静态状态，离开 Play 模式后依赖关卡 prefab 快照恢复
