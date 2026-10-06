# 像素游戏对话系统 · 使用说明

一套给 2D 像素游戏用的对话系统：**靠近 NPC → 头顶出现按键提示 → 按键 → 玩家头顶弹出聊天气泡 → 打字机逐字显示 → 平滑消失**。

---

## 一、文件清单

```
Assets/Scripts/Dialogue/
├── DialogueData.cs            对话数据（ScriptableObject）：一个 NPC 一套对话
├── DialogueTrigger.cs         挂在 NPC 身上：登记自己、提供提示锚点、发起对话
├── DialogueInteractor.cs      挂在玩家身上：找最近的 NPC、显示提示、监听互动键
├── InteractionBubble.cs       NPC 头顶的"按 E"提示泡泡（平滑淡入淡出 + 呼吸浮动）
├── DialogueBubbleView.cs      聊天气泡的表现层：淡入淡出、弹出、打字机、跟随
├── DialogueSession.cs         一段对话的状态机（逐句推进、等待输入）
├── DialogueManager.cs         总控单例：Play()、键位、节奏、玩家锁定
├── DialogueInput.cs           输入兼容层：新 Input System / 旧 Input Manager 都能用
├── PlayerMovementLock.cs      对话时锁住玩家移动
├── Editor/
│   └── DialogueSetupWizard.cs 编辑器菜单：一键搭好场景对象
└── Examples/
    ├── ExampleQuestNpc.cs         示例：任务 NPC，对话后推进任务、换下一套台词
    └── ExampleAutoDialogueZone.cs 示例：走进区域自动触发旁白
```

---

## 二、安装（3 分钟）

1. 把 `Assets/Scripts/Dialogue` 整个文件夹拷进你 Unity 项目的 `Assets/Scripts/` 下（或只拷 `Dialogue` 文件夹，路径随意，但 `Editor` 子文件夹的名字必须是 `Editor`，否则打包会报错）。
2. 确认项目里装了 **TextMeshPro**：菜单 `Window > TextMeshPro > Import TMP Essential Resources` 点一次即可（只需一次）。
3. 等 Unity 编译通过（Console 无红色报错）。

> 依赖只有 `UnityEngine.UI` 和 `TextMeshPro`，两者都是 Unity 自带包，不需要装第三方插件。

---

## 三、搭场景（两种方式，选一种）

### 方式 A：一键搭建（推荐）

菜单 **`Tools > PixelDialogue > 创建对话系统`**。

它会生成一个 `DialogueSystem` 物体，里面包含：

```
DialogueSystem
├── BubbleCanvas      (世界空间 Canvas，缩放 0.01，100 像素 = 1 世界单位)
│   └── Bubble_Follow  ← DialogueBubbleView 挂在这里，气泡位置 = 玩家位置 + 偏移
│       └── Bubble_Root (CanvasGroup + 缩放动画)
│           ├── Panel (九宫格底图 + 自动宽高)
│           │   ├── Portrait (头像，默认隐藏)
│           │   └── BodyText (TextMeshProUGUI，打字机文字)
│           ├── NameRow  (名字条，位于气泡上方)
│           └── ContinueIndicator ("▼" 继续箭头，气泡右下角)
└── PromptCanvas      (世界空间 Canvas，排序在气泡之上)
    └── InteractionBubble ← NPC 头顶的"按 E"提示
```

> `Bubble_Follow` 所在的点就是**气泡中心**。想让气泡整体更低/更高，只改 `DialogueBubbleView > World Offset` 即可。
>
> 像素画想更锐利：把 `Panel` 和 `PromptCanvas/InteractionBubble` 上的 `Image` 组件的 **Pixels Per Unit Multiplier 调成 1**（或把九宫格边框改成你美术素材的像素数）。

然后：

1. **玩家**身上加组件 `DialogueInteractor`（也可以选中玩家后用菜单 `Tools > PixelDialogue > 给选中对象添加 DialogueInteractor`）。
2. 确认玩家对象的 **Tag 是 `Player`**；如果不想改 Tag，就把玩家拖到 `DialogueManager.player` 字段上。
3. **玩家**身上把移动脚本（你的 `PlayerController` 之类）拖到 `DialogueManager > Player Behaviours To Disable` 数组里 —— 这样对话时玩家不会一边走一边说话。不填也能用，只是不会锁人。

### 方式 B：手动搭

想自己控制 UI 长什么样，就手动建：

1. 建一个**世界空间 Canvas**（Render Mode = World Space），`Rect Transform` 的 Scale 设成 `0.01`。
2. 在 Canvas 下建一个空物体命名 `Bubble_Follow`，挂上 **`DialogueBubbleView`**。
3. 在它下面搭气泡本体（Image + 文字），然后把引用填进 `DialogueBubbleView`：
   - `Bubble Root` → 承载缩放的节点
   - `Canvas Group` → 淡入淡出用
   - `Body Text` → 文字（**必须是 TextMeshProUGUI**，否则打字机不生效）
   - `Name Text` / `Name Row` → 名字（可空）
   - `Continue Indicator` → 打字结束后的闪烁箭头（可空）
4. 场景里放一个空物体挂 **`DialogueManager`**，把上面的 `DialogueBubbleView` 拖到 `Bubble` 字段。
5. 再建第二个世界空间 Canvas + 一个 Image 挂 **`InteractionBubble`**，作为 NPC 头顶的按键提示。

---

## 四、以后每次要做新对话，只需 4 步

### 第 1 步：创建对话资产

Project 窗口右键 → **`Create > PixelDialogue > Dialogue Data`**，命名成 `Dialogue_铁匠` 这样。

填内容：

| 字段 | 说明 |
|---|---|
| `Speaker Name` | 说话者名字，显示在气泡上方的名字条里 |
| `Portrait` | 头像（可空） |
| `Lines` | 对话内容数组，一句一个元素 |
| `Line > Wait For Input` | 勾上 = 打完这句停下等玩家按 E；取消 = 自动继续下一句 |
| `Line > Auto Advance Delay` | 取消等待输入时，停留几秒（-1 = 用全局默认 1.2 秒） |
| `Line Delay` / `End Delay` | 本段对话的句间/结尾停顿覆盖值，-1 = 用全局默认 |

**每句话都支持 TMP 富文本**，比如：

```text
<color=#FFD24A>黄金</color>可不是白给的，<b>先帮我找回 3 块矿石</b>。
```

打字机会自动跳过标签、只逐字显示可见文字，不会出现 `<color=...>` 被一个字一个字打出来的尴尬。

### 第 2 步：给 NPC 挂触发器

选中 NPC → 添加组件 **`DialogueTrigger`**：

| 字段 | 说明 |
|---|---|
| `Dialogue` | 拖入第 1 步创建的对话资产 |
| `Quick Test Text` | 没填 Dialogue 时的临时测试台词，方便快速验证 |
| `Prompt Offset` | 提示泡泡相对 NPC 的位置，默认 `(0, 0.9, 0)` 即头顶 |
| `Interact Radius` | 玩家离多近能互动（会画黄色 Gizmo，方便调） |
| `One Shot` | 只允许触发一次 |
| `Invoke Event After Dialogue` | 是否在对话结束时广播事件（接任务等） |

### 第 3 步：确认玩家有互动脚本

玩家身上有 `DialogueInteractor` 就行（方式 A 已加）。此时运行游戏，走近 NPC 会看到提示泡泡，按 **E** 开始对话。

### 第 4 步：想在代码里主动触发对话

```csharp
using PixelDialogue;
using UnityEngine;

public class SomeScript : MonoBehaviour
{
    public DialogueData myDialogue;
    public Transform player;

    void Start()
    {
        // 播一段对话（返回 false 表示此刻已有对话在进行）
        DialogueManager.Instance.Play(myDialogue, player);

        // 快速测试：直接一句话
        DialogueManager.Instance.Play("你好，旅行者！", player);

        // 临时几句，不落资源文件
        var temp = DialogueData.CreateTemp("第一句", "第二句", "第三句");
        DialogueManager.Instance.Play(temp, player);
    }
}
```

> 第二个参数是**气泡挂点**。传玩家 Transform = 气泡在玩家头顶；传 NPC Transform = 气泡在 NPC 头顶（想做 NPC 自己冒泡也行）。

---

## 五、运行时 API 速查

| 成员 | 作用 |
|---|---|
| `DialogueManager.Instance.Play(data, anchor, freeze)` | 播放一段对话，返回 `bool` |
| `DialogueManager.Instance.Play("文本", anchor)` | 播一句话 |
| `DialogueManager.Instance.ForceEnd()` | 强制打断当前对话（切场景、被打断时用） |
| `DialogueManager.IsActive` | 当前是否有对话在进行 |
| `DialogueManager.Instance.CurrentSession` | 当前对话会话，可订阅 `Finished` 事件 |
| `DialogueTrigger.DialogueFinished` | 事件：这个 NPC 的对话结束了（参数是它自己） |
| `DialogueTrigger.TryStartDialogue(player)` | 手动开始某个 NPC 的对话 |
| `DialogueTrigger.All` | 场景中所有 NPC 触发器的列表 |
| `DialogueBubbleView.Show / SetContent / CompleteTyping / Hide` | 直接控制气泡表现 |
| `PlayerMovementLock.IsLocked` | 玩家当前是否被对话锁住 |

**任务推进的标准写法**（详见 `Examples/ExampleQuestNpc.cs`）：

```csharp
void OnEnable()  => trigger.DialogueFinished += OnTalked;
void OnDisable() => trigger.DialogueFinished -= OnTalked;

void OnTalked(DialogueTrigger t)
{
    // 说完这句话之后：给道具、换台词、开门……
    t.dialogue = nextDialogue;
}
```

---

## 六、想调手感，改这些参数

### 打字机速度
`DialogueBubbleView > Characters Per Second`（默认 34）。

- **按住**互动键 → 按 `DialogueManager > Hold To Speed Multiplier`（默认 ×4）加速打字；
- **按一下**互动键 → 立刻显示整句（跳过打字）；
- 打完后再按一下 → 翻到下一句。

三种行为都在 `DialogueSession.Run()` 里，逻辑很短，想改手感直接读那个协程。

### 气泡出现 / 消失过渡
`DialogueBubbleView` 上：

| 参数 | 作用 |
|---|---|
| `Fade In / Fade Out Duration` | 淡入淡出时长（默认 0.18 / 0.14 秒） |
| `Pop From Scale` | 从多小弹出来（默认 0.72） |
| `Pop Overshoot` | 弹出时的过冲（默认 1.06，>1 会有 Q 弹感） |
| `Rise Distance` | 出现时从下往上升的世界距离（默认 0.22） |
| `Smooth Follow` + `Follow Smooth Time` | 平滑跟随玩家；像素游戏建议 0.03~0.08，太大看起来会"飘" |
| `World Offset` | 气泡相对玩家的偏移，默认 `(0, 1.6, 0)` |

### 提示泡泡（按 E）
`InteractionBubble` 上：

| 参数 | 作用 |
|---|---|
| `Fade In / Fade Out Duration` | 淡入淡出 |
| `Pop From Scale` / `Pop Overshoot` | 弹出感 |
| `Float Amplitude` / `Float Speed` | 待机时上下浮动 |
| `Pulse Amplitude` / `Pulse Speed` | 待机时呼吸缩放（设为 0 关闭） |
| `Follow Smooth Time` | 切换最近 NPC 时的跟随平滑 |
| `Key Label Text` | 显示什么按键名，比如 `E` / `Space` |

所有过渡和打字都用 **`Time.unscaledDeltaTime`**，所以即使你 `Time.timeScale = 0` 暂停游戏，气泡动画和打字依然流畅。

### 键位
`DialogueManager`：

- `Advance Key`：继续 / 跳过打字，默认 `E`
- `Skip Typing Key`：按住加速，默认 `E`
- `DialogueInteractor > Interact Key`：互动键，默认 `E`

支持写 `E`、`F`、`Space`、`Return`、`Enter`、`Mouse0`、`Mouse1` 等名字。
输入层（`DialogueInput.cs`）**同时兼容新的 Input System 包和旧的 Input Manager**，用反射访问新输入系统，所以不管你的项目用哪套方案都能编译、都能跑。

### 多个 NPC 挤在一起
`DialogueInteractor` 每 `Search Interval` 秒（默认 0.08）重搜一次，**永远选离玩家最近的那个** NPC 显示提示，不会出现两个提示同时亮。

---

## 七、常见问题排查

| 现象 | 原因 / 解决 |
|---|---|
| 按 E 没反应 | 玩家没有 `DialogueInteractor`；或玩家 Tag 不是 `Player` 且 `DialogueManager.player` 没赋值；或 NPC 的 `Interact Radius` 太小（选中 NPC 看黄色 Gizmo 圈） |
| 提示泡泡不出现，但对话能触发 | 场景里没有 `InteractionBubble`；或 `DialogueInteractor` 的 `Awake` 时它还没实例化（把 `DialogueSystem` 放在场景里即可，`Start` 前所有 `Awake` 都会跑完） |
| 对话能触发但看不见文字 | `Body Text` 不是 `TextMeshProUGUI`，或距离摄像机太远（气泡 Canvas 缩放要是 `0.01`，且 Canvas 的 `Sorting Order` 要比背景高） |
| 文字一个一个出现但整句瞬间跳完 | `Characters Per Second` 太大，或者你按住了互动键（按住即加速） |
| 打包报错 `Editor` 相关 | `DialogueSetupWizard.cs` 必须在名为 `Editor` 的文件夹里 |
| 气泡跟着玩家一直抖 | 把 `Follow In Late Update` 打开（默认开），并让 `Follow Smooth Time` ≥ 0.03；如果玩家是 `Rigidbody2D` 插值移动，气泡在 `LateUpdate` 跟最稳 |
| 对话时玩家还在走 | 把玩家移动脚本拖进 `DialogueManager > Player Behaviours To Disable` |
| 连按 E 会把整段对话快速跳完 | 这是刻意做的、和经典 RPG 一致：打字中按一下 = 立刻显完整句，再按 = 翻页。想加冷却，把 `DialogueSession` 里等待输入前的 `yield return null;` 换成 `yield return new WaitForSecondsRealtime(0.12f);` |
| 编辑器里跑完一次对话后玩家动不了 | 已经做了兜底（`OnApplicationQuit` / `OnDestroy` 会复位 `PlayerMovementLock`）；如果还有问题，检查是不是你有别的脚本也在禁用同一个组件 |

---

## 八、下一步可以扩展的方向

- **多语言**：给 `DialogueData` 加一个 `LocalizedString` 或在 `GetLine()` 里按语言索引取文本。
- **选项分支**：给 `DialogueLine` 加 `DialogueChoice[] choices`，在 `DialogueSession` 的等待输入分支里改成弹出按钮列表。
- **音效**：在 `DialogueBubbleView.ApplyVisibleChars()` 里，每当 `count` 增加时播放一个"嘀"声，就是经典的打字音。
- **表情**：`DialogueLine` 里加 `Sprite portraitOverride`，在 `SetContent` 时替换头像。
- **存档**：`DialogueTrigger.HasPlayed` 已经是可读状态，存/读它即可记录剧情进度。
