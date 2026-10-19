# 前端架构演进与状态治理

| Field | Value |
| :--- | :--- |
| **Exported** | October 19, 2026 at 10:45 PM |
| **Model** | Gemini |
| **Messages** | 7 |

---

## 💬 Conversation

### 👤 User

docs progress_activity 699,228 tokens

---

### 👤 User

目前刚刚进行了一轮架构大整理，现在模块化和组件化都做得有模有样了，治理似乎好多了，但界面的恶性 bug 仍然无法清零，用户实际操作很难闭环。我在想，传统软件开发的前端是怎么解决这类问题的？这个是 AI 开发的，但是我需要一种能处理掉这么一堆包的问题的方法论。我想听到 MVVM, MVC, Elmish, Fable 以及所有前端开发的范式，如何才能真正清晰明确地写出明显没有 bug 的前端代码，而不是没有明显缺陷，一运行全是毛病。

---

### 🤖 Gemini

你正处在一个极其典型、但又具有高度时代特征的架构困境中。

计算机科学巨匠 C.A.R. Hoare（图灵奖得主）有一句名言，精准地概括了你现在的痛点：

> **“软件设计有两种方式：一种是让它简单到明显没有缺陷（obviously no deficiencies）；另一种是让它复杂到没有明显的缺陷（no obvious deficiencies）。”**

AI 是人类历史上最强大的**“制造第二种软件”**的机器。

我仔细通读了你提供的这 70 多个 libs/ 下的 F# 源码和测试。你的代码库目前处于一种**“局部极致精美、全局完全失控”**的状态：每一个小包都有 Headless 测试、有无障碍（AutomationPeer）断言、有 Golden Master（金样）比对，测试全绿；但当你把它们组装起来运行时，焦点乱飞、按钮卡死、状态不同步、用户操作无法闭环。

下面我将先为你做一次**基于你当前源码的“病理切片”**（看看这堆包到底病在哪里），然后用 **MVC、MVVM、React/Fable、Elmish (MVU)** 四大前端范式的演进史来回答传统与现代前端是如何根治这个问题的，最后给你一套**不需要推倒重来、能立刻收服这 80 个包的工程方法论**。

---

#### 一、 病理切片：为什么你的 80 个包“单测全绿，运行全崩”？

看你的代码，AI 在执行“架构大整理 / 拆包解耦”时，掉进了四个致命的工程陷阱：

##### 1. 布尔爆炸与“组合子地狱”（Combinatorial Explosion）

看看你代码库里的 libs/ChangeWatch/ConversationSurface.fs 和 IdleSurface.fs：

```
// ConversationSurface.fs —— 8 个布尔值入参！
let decide (messagesEmpty: bool) (streaming: bool) (hasRunError: bool) (hasPartial: bool)
           (generating: bool) (unanswered: bool) (authenticated: bool) (hasGenerationId: bool) = ...

// IdleSurface.fs —— 6 个布尔值入参！
let decide (authenticated: bool) (connectionError: string option) (rePair: bool)
           (hasSavedCredentials: bool) (catalogReady: bool) (onboardingBusy: bool) = ...
```

8 个布尔值意味着 $2^8 = 256$**
种状态组合**！你的 ConversationSurface.Tests.fs 甚至用四重循环跑了这 256 种组合去比对金样（golden: output matches the old code）。

- **病根**：在现实业务中，streaming = true 且 generating = false 是什么鬼状态？messagesEmpty = true 且 hasPartial = true 又是什么状态？这 256 种组合里，**至少有 240 种在现实中根本不应该存在（非法状态）**！但因为状态被拆散成了游离的 bool，你的代码必须在每一帧去猜这些布尔值的排列组合。只要上游漏更新了一个布尔值，界面就会掉进那 240 种未知黑洞里，导致操作无法闭环。

##### 2. 状态碎片化与“多脑症”（Split-Brain State）

在你的代码库里，“当前正在生成回复（Generating）”这个单一事实，同时活在多少个对象的私有 mutable 字段里？

- ChatHeader 里有 let mutable generating = false
- ComposerBox 里有 let mutable generating = false
- ScrollFollow 里有 let mutable wasStreaming = false
- 外层的 AppShell 里还有 generationId: Guid option
- ExchangeJump、MountWindow、DeferredReveal、RowFocus、SearchSession、PendingDeletion、GenerationDraft、OnboardingFlow、ReconnectLoop……**你有超过 30 个独立的 class，每个都持有自己的 let mutable 状态机！**
- **病根**：当你把一个大文件拆成 80 个小包时，AI 把原本耦合在一起的变量拆成了 30 个微小的独立状态机，却把**同步这 30 个状态机的责任**全部推给了最外层的宿主（Combinator / AppShell）。 $N$
   个有状态对象之间有 $\frac{N(N-1)}{2}$
   条同步路径。当“切换对话”或“断线重连”发生时，宿主必须按极其精确的顺序调用 scroller.CancelPrependAnchor(), mountWindow.Reset(), exchangeJump.Reset(), scrollFollow.Reset(), header.SetGenerating(false), composer.SetGenerating(false)……**只要漏调一个，或者异步回调顺序颠倒，整个 UI 就楔死了（Wedge）！**

##### 3. 多入口的命令式 UI 突变（Setter 陷阱）

对比一下你代码库里两种截然不同的控件写法：

- **好写法（如 ExportSheet.fs、DevicesPane.fs）**：对外只有一个 member _.Render(state: ExportSheetState)。所有 UI 元素的 IsVisible、IsEnabled、Text 都在这一个函数里一次性算完并赋值。
- **坏写法（如 ChatHeader.fs、ComposerBox.fs）**：
   ChatHeader 暴露了 SetTitle、SetGenerating、SetConversationChrome、SetCompact、SetInset、SetCrumb、BeginRename、CommitRename……
   ComposerBox 暴露了 SetEnterSends、SetCompact、SetText、ApplySend、SetGenerating、SetEnabled、ShowContext、HideContext……
- **病根**：当一个控件有 8 个 SetX() 方法时，它的最终视觉呈现取决于**宿主调用这 8 个方法的历史先后顺序（时序耦合）**！比如先调 SetCompact(true) 再调 ShowContext(...)，和反过来调，结果可能完全不同。这就是为什么界面 bug 改完一个又冒出另一个。

##### 4. Dispatcher.UIThread.Post 造成的焦点与滚动竞态

在你的 ConversationRows.fs、ChatScroller.fs、OverlayLayer.fs、ChatHeader.fs 中，到处都是各自为战的 Dispatcher.UIThread.Post(..., DispatcherPriority.Input / Background)。

- 当用户按下一个键（比如删除对话并关闭弹窗），OverlayLayer Post 了一个恢复焦点，ConversationRows Post 了一个聚焦邻行，ShellColumns 又同步转移了焦点。三个异步队列在事件循环里赛跑，谁跑赢了焦点就在谁身上——这就是“恶性 UI bug 无法清零”的直接物理原因。

---

#### 二、 前端架构演进史：传统与现代软件工程是如何解决这个问题的？

你遇到的问题，正是过去 40 年所有 GUI 工程师（从 Smalltalk、WinForms、WPF 到 Web 前端 React、Elm）浴血奋战的核心问题：**如何管理随时间变化的复杂交互状态？**

我们来看看四大范式是如何一步步消灭这类 Bug 的：

##### 1. MVC (Model - View - Controller) —— 你目前的实际状态（Passive View 变体）

- **机制**：Controller 监听 View 的事件，修改 Model，然后**手动调用 View 的各种 Setter 方法**去更新界面。
- **为什么它在大前端里被淘汰了？**
   因为随着界面变复杂，Controller 会膨胀成一个包含成百上千条“如果状态 A 变了，记得去把 View1 的按钮禁掉、把 View2 的文字改掉、把 View3 的动画停掉”的超级胶水层。
   **你现在的架构，本质上就是把 View 和小碎片拆成了 80 个包，但外层的 Combinator 依然是一个人肉同步所有状态的 MVC Controller！** 只要是人（或 AI）手写同步命令，就一定会有遗漏。

##### 2. MVVM (Model - View - ViewModel) —— WPF / Avalonia 的传统解法

- **机制**：消灭 Controller 手动推数据的过程。引入 **双向数据绑定（Data Binding）** 和 INotifyPropertyChanged / IObservable。View 声明式地绑定到 ViewModel 的属性上（如 IsEnabled="{Binding CanSend}"）。
- **它解决了什么？**
   解决了“忘记调用 button.SetEnabled”的问题。因为状态和视图属性绑定了，状态变，视图自动变。
- **为什么重度 MVVM 依然会写出满身 Bug 的软件？双向绑定的级联风暴**：A 属性变了触发 B 控件更新，B 控件的 TextChanged 反向改了 ViewModel 的 C 属性，C 属性又触发了异步校验清掉了 D……你根本不知道一个状态是被谁在什么时候改掉的（你在 FieldCheck.fs 注释里写的 “聚焦切换时 Avalonia 会重放同一文本并抛 TextChanged，不比较旧值就会把刚写好的提示自己清掉” 就是最典型的 MVVM/事件级联痛点！）。**状态依然是可变的（Mutable）且分散在各个子 ViewModel 里**，依然存在“多脑症”和非法布尔组合。

##### 3. React / Fable 范式 —— 划时代的公式： $\text{UI} = f(\text{State})$


2013 年 React 的出现彻底颠覆了前端。它提出了一个极其激进但极其正确的洞察：

> **UI 之所以有 Bug，是因为我们在写“状态迁移边（Edge）”的代码（怎么从状态 A 变到状态 B）。如果我们彻底放弃修改 UI，而是每次状态变化时，像刷电影胶片一样，用纯函数把整个 UI 重新算一遍，所有的同步 Bug 都会原地蒸发！**

- 在 **Fable（F# 编译到 JS 的前端生态）** 中，这意味着没有 SetTitle、没有 SetEnabled、没有 ShowEmpty。只有一个纯函数 view (state: State)。
- 如果 state 是 Disconnected，函数返回断线卡片；如果 state 是 Generating，函数返回带停止按钮的输入框。**在数学上，界面绝对不可能处于“既显示空态卡片，又显示正在生成”的分裂状态**，因为 f(state) 在同一时刻只能返回一棵树！

##### 4. Elmish / MVU (Model - View - Update) —— F# 语言的终极武器

如果说 React 解决了“怎么从 State 画出 UI（ $V = f(M)$
）”，那么源自 Elm 语言、在 F# 社区发扬光大的 **Elmish (MVU)** 则彻底解决了“State 怎么安全地演进”和“异步副作用怎么不打架”。

MVU 只有四个核心概念，组成一个**绝对严格的单向闭环**：

```
┌──────────────────────────────────────────┐
       ▼                                          │
  [ Model ] ──(纯函数 view)──► [ View / UI ]      │
  (唯一事实)                        │              │
       │                      (用户点击/按键)      │
  (纯函数 update)                   ▼              │
       │                        [ Msg ]           │
       ▼                      (意图代数类型)       │
 [ 新 Model + Cmd(副作用指令) ] ─────┴──────────────┘
```

1. **Model（不可变的唯一事实来源）**：整个应用（或整个完整工作区）的状态是一棵**不可变的 F# Record + Discriminated Union（可区分联合）树**。任何子控件**不允许私藏业务状态**。
2. **Msg（所有可能发生的事情的总和）**：用户的点击、按键、WebSocket 推过来的 WireEvent、定时器到点，统统是 type Msg = ... 的一个分支。
3. **update : Msg -> Model -> Model * Cmd<Msg>（唯一的纯函数大脑）**：输入旧 Model 和 Msg，返回新 Model 和**要执行的副作用描述（Cmd）**。**注意：update 里绝对不执行任何 IO、不发网络请求、不调 control.Focus()！** 它只返回一个数据的列表（比如 [ Effect.SendWire cmd; Effect.FocusControl "composer-input" ]）。
4. **view : Model -> (Msg -> unit) -> View（无脑的纯投影）**：只负责把 Model 映射成画面。

---

#### 三、 如何写出“明显没有 Bug”的前端代码？（四大铁律）

结合你的 F# 技术栈和现有代码库，要从“到处漏水”变成“明显没有 Bug”，必须执行以下四条铁律：

##### 铁律 1：用 F# 的可区分联合（DU）杀死“布尔爆炸”（Make Illegal States Unrepresentable）

不要再用 6 个 bool 和 3 个 option 去拼凑状态，更不要让 AI 为 256 种布尔组合写金样测试！用 F# 最强大的 **和类型（Sum Types / DU）** 把业务生命周期锁死在编译器里。

**❌ 你现在的写法（分布在 IdleSurface、ConversationSurface、StartReady、AppShell）：**

```
// 状态散落在各处：
mutable authenticated: bool
mutable connectionError: string option
mutable catalogReady: bool
mutable onboardingBusy: bool
mutable activeConvId: Guid option
mutable convLoading: bool
mutable streaming: bool
mutable generationId: Guid option
```

这会导致成百上千种荒谬的组合，比如 authenticated = false 但 streaming = true。

**✅ “明显没有 Bug”的写法（用代数数据类型建模状态机）：**

```
type ConnectionPhase =
    | Disconnected of lastError: ConnectionIssue option * hasSavedCreds: bool
    | Connecting of attempt: int
    | Pairing of PairingState
    | Authenticated of instanceId: string * catalog: CatalogState

and CatalogState =
    | LoadingCatalog
    | NeedsOnboarding of OnboardingFlowState // 没配模型服务时，只可能在这个分支！
    | Ready of Catalog * ActiveWorkspace

and ActiveWorkspace =
    | NewConversationDraft of draft: ComposerDraft
    | LoadingConversation of convId: Guid * skeletonArmed: bool
    | LoadedConversation of conv: ConversationModel * runState: RunState

and RunState =
    | Idle of lastError: GenerationError option * partialReply: JsonNode option
    | Generating of runId: Guid * streamBuffer: MessageView * awaitingApprovals: Map<Guid, ApprovalAsk>
```

**你看懂这棵树的威力了吗？**

1. 当连接断开时（变成 Disconnected），ActiveWorkspace 和 Generating **在内存里直接灰飞烟灭了**！你根本不需要写代码去重置 generating <- false、不需要去清理 skeletonArmed、不需要去关停 awaitingApprovals！
2. 编译器会强迫你在 match model.connection with 时处理且仅处理合法的分支。
3. **非法状态在类型系统层面根本无法构造出来**——这就是“明显没有 Bug”的第一性原理。

---

##### 铁律 2：将控件彻底“去脑化”——统一为 Render(ViewState) + OnEvent(Msg)

你不需要扔掉你辛辛苦苦写的 libs/UiKit、Spot、Tap 和无障碍支持。但你必须**剥夺 UI 控件私藏业务状态的权力**。

在你的代码库中，BatchBar、ExportSheet、DevicesPane 之所以稳，是因为它们采用了 **单入口投影模式（Single-Entry Render）**；而 ChatHeader、ComposerBox、ConversationRows 之所以容易出 Bug，是因为它们采用了 **多入口命令式修改模式（Multi-Setter Mutation）**。

**整改法则：任何一个 UI 组件库，对外只准有一个 Render(state) 方法，和一个 Event<Msg> 出口！**

**❌ 现在的 ChatHeader（多入口，状态分散）：**

```
header.SetTitle("新标题", true)
header.SetGenerating(true, "生成中")
header.SetConversationChrome(true)
header.SetCompact(false)
header.SetCrumb(Some crumb)
```

**✅ 改造后的 ChatHeader（单入口幂等投影）：**

```
type ChatHeaderState = {
    Visible: bool
    Compact: bool
    Inset: float
    Title: TitleMode // Viewing of text: string * canEdit: bool | Editing of draft: string
    GeneratingStatus: string option // None = 不显示, Some "生成中" = 显示
    ShowMoreButton: bool
    Crumb: CrumbView option
}

// 控件内部只是把 ChatHeaderState 幂等地投影到 Avalonia 属性上（Diff 赋值）
member _.Render(s: ChatHeaderState) = ...
```

当 ChatHeader 变成单入口的 Render(state) 后，你可以写一个纯函数 HeaderState.ofWorkspace (model: WorkspaceModel) : ChatHeaderState。**只要 model 是对的，顶栏在任何一帧都 100% 是对的，绝对不可能出现“切换了对话但顶栏还留着上一个对话的面包屑或改名框”的 Bug！**

---

##### 铁律 3：把“焦点（Focus）”和“滚动（Scroll）”收归为显式的副作用（Effects）

你在桌面/Web 前端里遇到的最恶心的交互不闭环（比如：重命名按回车后焦点没了、删除对话后焦点没落到下一行、展开详情后页面乱跳、打开弹窗再关掉回不到原按钮），90% 都是因为**各个子组件自己在 KeyDown 或 Detached 事件里偷偷调用了 Focus() 或 Dispatcher.UIThread.Post**。

在 Elmish / 现代前端架构中，**视图组件绝不能自己抢焦点或滚屏幕**！焦点和滚动是状态转移产生的**声明式副作用（Effect）**：

```
[<RequireQualifiedAccess>]
type UiEffect =
    | Focus of target: FocusTarget // 如：ComposerInput | SidebarSearch | Row of Guid | DialogInitial
    | ScrollChat of ScrollCommand  // 如：ToEnd | ToHome | ToCommit of uint64 | RestorePrependAnchor
    | ShowToast of text: string * ToastTone
    | SendWire of WireEvent
```

1. 当用户在第 2 行按 Delete 键，控件只发出 Msg.DeleteConversation id。
2. 纯函数 update 计算出新的列表，同时通过 RowFocus 算出下一个焦点是谁，返回：
   newModel, [ UiEffect.Focus (FocusTarget.Row nextId); UiEffect.ShowToast ("已删除...", Neutral) ]
3. 外层**唯一的渲染循环（Render Loop）先同步调用所有组件的 .Render(newModel)（此时 DOM / 视觉树已经完全更新完毕！），然后再由唯一的 Effect Runner** 统一在一个 Dispatcher.UIThread.Post(..., DispatcherPriority.Input) 里执行 Focus 和 ScrollChat。
4. **结果**：再也没有 5 个 Dispatcher.Post 互相抢焦点的竞态条件！而且你的焦点转移逻辑可以在**完全不启动 Avalonia 窗口**的情况下，用纯函数单元测试 100% 测穿：Assert.Contains(UiEffect.Focus (FocusTarget.Row nextId), effects)！

---

##### 铁律 4：停止让 AI 写“金样复刻测试”，改写“状态机闭环属性测试”

你现在的测试套件里有很多这样的代码：

```
/// 金样由旧代码（从旧 AppShell.fs 照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())
```

这种测试只在“重构搬家”的那一瞬间有用，搬完家之后它就是**负资产**。因为它测试的是“新代码和旧代码犯一模一样的错”，并且它测的是孤立的碎片，根本没测“用户操作能不能闭环”。

**如何测试“用户操作闭环”？**
一旦你按铁律 1、2、3 建立了顶层的纯函数 update : Msg -> Model -> Model * UiEffect list，你就可以在不启动任何 UI 线程的情况下，用毫秒级的速度测试**完整的用户旅程（User Journey）和不变式（Invariants）**：

```
[<Fact>]
let ``用户旅程闭环：首次连接 -> 引导配模型 -> 发送首条消息 -> 流式生成 -> 断线 -> 重连恢复`` () =
    let s0 = Workspace.init ()
    // 1. 连上主机但没模型 -> 必须进入 Onboarding 且输入框禁用
    let s1, _ = Workspace.step s0 (Msg.Wire (AuthAccepted {| instanceId = "h1" |}))
    let s2, _ = Workspace.step s1 (Msg.Wire (CatalogSnapshot emptyCatalog))
    Assert.Equal(IdleEmpty.Onboarding, (Workspace.view s2).Chat.EmptyState)
    Assert.False((Workspace.view s2).Composer.SendEnabled)

    // 2. 完成引导 -> 必须切入可输入状态，且自动焦点到输入框
    let s3, effs = Workspace.step s2 (Msg.Onboarding (OnboardingMsg.Applied ("openai", "gpt-4o")))
    let s4, _ = Workspace.step s3 (Msg.Wire (CatalogSnapshot readyCatalog))
    Assert.True((Workspace.view s4).Composer.Enabled)
    Assert.Contains(UiEffect.Focus FocusTarget.ComposerInput, effs)

    // 3. 输入文字并发送 -> 创建会话、输入框清空、进入生成态
    ...
```

你甚至可以用 FsCheck（Property-Based Testing，属性测试）随机生成成千上万条乱序的 Msg 序列（比如：狂点发送的同时拔掉网线、同时切换主题、同时收到旧会话的 GenerationDelta、同时按 Esc），然后对每一步断言**全局不变式（Global Invariants）**：

- 不变式 A：只要 model 不在 Generating 状态，界面上的停止按钮就绝不可见，流式光标就绝不存在。
- 不变式 B：任何时刻，焦点目标必须指向一个当前 IsVisible = true && IsEnabled = true 的控件。
- 不变式 C：任何发出去的命令（CommandFeedbackTracker / ConfigFeedbackTracker），在经历 Disconnected 或 Timeout 后，PendingCount 必须归零，绝不能有任何按钮永远卡在 "正在保存…"（Pending 楔死）。

---

#### 四、 针对你当前项目的实操拯救方案（两步走）

你现在手握 70 多个已经写好、视觉和无障碍细节都很扎实的底层包。**千万不要推倒重写**，也不要让 AI 继续盲目拆小包了！你需要做的是**“收拢大脑，统一管道”**。

在 F# + Avalonia 中，你有两条路径可选：

##### 路径 A：如果你愿意引入一个轻量官方生态库 —— Avalonia.FuncUI (Elmish)

F# 社区在 Avalonia 上做 MVU 的标准方案是 **Avalonia.FuncUI**。它让你直接用 F# 纯函数写 Avalonia 视图（就像 Fable 写 React 一样），底层自动帮你做控件树的 Diff 和事件绑定，内置完整的 Elmish (Program.mkProgram init update view)。

##### 路径 B：零新依赖，直接用你现有的包组装一个“MVU 内核”（最推荐！）

因为你的底层控件库（DevicesPane、ExportSheet、BatchBar、ConversationRows 等）已经写成了高质量的自定义控件，你完全不需要换 UI 框架，只需要**改变组装层（Host / AppShell）的架构模式**。

具体执行以下三步手术：

把目前散落在外的那 20 多个微型逻辑包：

- StartReady、IdleSurface、ConversationSurface、EscapeChain、PendingDeletion、SearchSession、RowFocus、ExchangeJump、MountWindow、ScrollFollow、DeferredReveal、CommandFeedback、ConfigFeedback、HostRequests、ReconnectLoop……

在顶层建立一个 WorkspaceModel.fs 和 WorkspaceUpdate.fs，把它们收编为一个**单一状态树 WorkspaceModel** 和一个**单一入口函数**：

```
val update : now:DateTimeOffset -> Msg -> WorkspaceModel -> WorkspaceModel * Effect list
```

- **立竿见影的效果**：之前宿主里几千行面条式的事件回调（ws.OnEvent 改一堆变量、sidebar.OnSelect 改一堆变量、composer.OnSubmit 改一堆变量），全部变成只有一行：store.Dispatch(Msg.Wire ev)！所有状态转换的先后顺序都在纯函数 update 里，一览无余。

写一个纯函数：

```
val project : WorkspaceModel -> WorkspaceViewState
```

其中 WorkspaceViewState 包含所有子面板需要的状态记录：

- Sidebar: SidebarViewState（内含 BatchBarState、RowItem list、EmptyCardState）
- Header: ChatHeaderState
- Chat: ChatViewState
- Composer: ComposerViewState（内含 SendView、TrayItem list、ModelChipText）
- Settings: SettingsViewState option
- Overlay: OverlayViewState

检查你所有的 Avalonia 控件库，消灭散装的 SetX() 状态修改方法（或者只把它们作为内部 private 辅助函数）。
在每次 store.Dispatch(msg) 之后：

```
let nextModel, effects = WorkspaceUpdate.update DateTimeOffset.UtcNow msg currentModel
currentModel <- nextModel
let viewState = WorkspaceProjection.project currentModel

// 1. 同步、确定性地刷新整个视图树（幂等：值没变就不触发重排）
shellColumns.Apply viewState.Shell
sidebar.Render viewState.Sidebar
chatHeader.Render viewState.Header
chatView.Render viewState.Chat
composer.Render viewState.Composer

// 2. 视图树稳定后，统一执行副作用（焦点、滚动、网络发送、定时器）
EffectRunner.run effects
```

---

#### 总结：如何指挥 AI 帮你完成这次蜕变？

AI 最擅长的是**在给定严格类型签名（Type Signatures）的约束下填补实现**，最不擅长的是**在跨文件的命令式可变状态和异步回调中保持全局时序一致**。

你之前让 AI “模块化、拆包”，AI 顺着命令式代码的纹理，把一团乱麻切成了 80 段小乱麻，虽然每一段都配了金样测试，但段与段之间的接口全是隐式的时序假设。

接下来，请你亲自（或让 AI 在你的严格指令下）**先定义好顶层的 type WorkspaceModel（用 DU 消除非法状态）、type Msg、type Effect 和 type WorkspaceViewState**。一旦这四个类型的骨架立起来了：

1. **状态只能通过 update 演进**（杜绝状态不同步和楔死）；
2. **界面只能通过 Render(viewState) 投影**（杜绝残留脏 UI）；
3. **焦点和滚动只能通过 Effect 驱动**（杜绝异步抢焦点竞态）。

做完这一步，你会发现不仅那 80 个包瞬间被串联成了一台精密咬合的钟表，而且你的代码将真正达到 Hoare 所说的境界——**简单、直接、单向流动，明显没有 Bug。**

---

### 🤖 Gemini

你的直觉准得可怕。**“没有代码就是最好的代码”**——你一句话直接戳破了这套代码库最荒诞的真相。

我刚才在通读你这 80 个包的时候，越看越觉得脊背发凉：**AI 在 Avalonia 这个极其成熟、重量的工业级 GUI 引擎之上，竟然用最原始的 Border（边框）和 DispatcherTimer（定时器），手搓了半个劣质的山寨 GUI 引擎！**

你之所以觉得代码“稀巴烂”、运行起来全是恶性 Bug，根本不是因为你的聊天业务有多复杂，而是因为 **AI 抛弃了 Avalonia 原生千锤百炼的组件系统，自己造了一堆残废轮子，然后再写几万行代码去修补这些轮子的洞！**

不信？我直接从你的代码库里把 AI “手搓轮子”的罪证给你拎出来看看，看完你就明白为什么你会抓狂了：

---

#### 一、 触目惊心的“手搓轮子”罪证清单

##### 罪证 1：不用原生的 Button、ToggleSwitch、ComboBox，全部用 Border 手搓！

打开你的 libs/UiKit/UiKit.fs 和 libs/PopupMenu/PopupMenu.fs 看看：

- **手搓按钮（Tap / Spot / InvokeBorder）**：AI 居然没有用 Avalonia.Controls.Button！它继承了 Border，然后自己监听 PointerReleased、自己判断 Key.Enter 和 Key.Space、自己设置手型光标、自己写 SetEnabled 去改 Focusable，最后为了过无障碍测试，还手写了一个 TapPeer 去伪装成按钮（AutomationControlType.Button）！
- **手搓开关（Kit.Toggle）**：Avalonia 明明有开箱即用的 <ToggleSwitch>！AI 不用，它用两个 Border（一个叫 track，一个叫 knob），手动在点击时把 knob.HorizontalAlignment 在 Left 和 Right 之间切来切去，再手动打上 AutomationControlType.CheckBox 的标签！
- **手搓下拉框（SelectButton）**：Avalonia 明明有 <ComboBox>！AI 不用，它拿一个 Border 塞个文本和箭头，假装自己是 ComboBox，然后自己去算弹窗坐标！

**后果**：原生的 Button 和 ToggleSwitch 处理了成百上千种边缘情况（比如：鼠标按下后移出按钮再松开不算点击、空格键按下时的视觉反馈、失焦取消、触屏长按、屏幕阅读器标准焦点行为）。AI 用 Border 手搓的假按钮，在 PointerReleased 里只要鼠标在上面松开就算点击！这种底层控件的手感能好吗？能没有恶性 Bug 吗？

##### 罪证 2：不用原生的 Flyout / ContextMenu，手搓弹窗定位、焦点陷阱与滚轮拦截！

看看 libs/PopupPlacement、libs/FocusOrigin/OverlayLayer.fs、libs/PopupMenu：

- Avalonia 原生自带 Flyout、MenuFlyout、ContextMenu。它们在底层（C++ / 操作系统级或内置 Popup 定位器）早就完美解决了：自动翻转（下方放不下翻到上方）、视口边缘防溢出、点击外部自动关闭（Light Dismiss）、菜单内上下方向键循环聚焦（Roving Focus）、关闭后自动归还焦点。
- **AI 干了什么？**写了个 PopupPlacement.fs 库，自己用加减乘除算锚点坐标 anchorY + anchorHeight + gap；在 OverlayLayer.fs 里铺了一个透明的 popupCatcher（Border）来抓外部点击；自己监听全局鼠标滚轮 PointerWheelChanged 去关菜单；自己写 rememberPopupFocus、lastPopupAnchor、trapTab 去手动管焦点栈！
- **后果**：自己管焦点栈是 GUI 开发里最容易翻车的深渊。弹窗套菜单、菜单开对话框时，previousDialogFocus 和 previousPopupFocus 一打架，焦点直接丢失。

##### 罪证 3：不用原生的虚拟化列表，手搓 DOM Diff 和滑动窗口！

看看 libs/BreadcrumbAction/MountWindow.fs、libs/CardReconcile/CardReconcile.fs、libs/BlockDiff/BlockDiff.fs：

- Avalonia 的 ItemsControl / ListBox 配合自带的 VirtualizingStackPanel，天生就能流畅渲染成千上万条消息，自动回收滚出屏幕的控件。
- **AI 干了什么？** 在聊天视图里，它用了一个最笨的普通容器，然后为了不卡死，自己写了：MountWindow.fs：手动算只挂载最后 40 张卡片（Initial = 40, Step = 60），滚到顶再手动扩窗口；CardReconcile.fs：手动写了个简陋的虚拟 DOM 对账器（Reconciler），算共同前缀 common；ChatScroller.fs：因为自己往列表头部插元素会导致画面乱跳，又手写了一个 BeginPrependAnchor / RestorePrependAnchorDeferred 去算高度差补滚轮偏移！
- **后果**：滚动条乱跳、“回到最新”按钮闪现、往上翻历史时画面抽搐，全是因为这套手搓的半吊子虚拟化跟 Avalonia 自己的布局周期（Layout Pass）对着干！

##### 罪证 4：不用原生的样式与动画系统，手搓 DispatcherTimer 物理引擎和 Look 注入！

这才是最离谱的。看看这些包：

- libs/ScrollTween/ScrollTween.fs：AI 为了做平滑滚动，居然手写了一个**牛顿二分法求三次贝塞尔曲线**（solveX lo hi），然后开一个 16ms 的 DispatcherTimer 在 F# 里一帧一帧改 ScrollViewer.Offset！
- libs/ColorBlend/ThemeTween.fs：为了切换深浅色主题时有 200ms 过渡，AI 居然开了个定时器，在 CPU 里手动对所有画笔的 R、G、B 通道做线性插值（LerpChannel）！
- libs/BreathPhase / CaretBreath / BusyRing：为了光标闪烁和加载转圈，开了三个 DispatcherTimer 在 UI 线程上每 40ms/50ms 算一次 Math.Sin！
- 每个控件库都要定义一个三四十个字段的 *Look Record（如 GeneralPaneLook、ComposerBoxLook），再通过 Follow.live 手动订阅 IObservable<float> 去设 FontSize、Padding、CornerRadius！
- **Avalonia 原生是怎么做的？**
   Avalonia 有极其强大的 **Styles（类似 CSS）**、**DynamicResource** 和 **KeyFrame Animations / Transitions**！
   字号、边距、颜色写在全局 Styles.axaml 里，深浅色切换用 ThemeDictionaries，动画用 <DoubleTransition> 或样式表里的 <Animation>（由底层渲染线程合成，零业务代码参与）。AI 却用几千行 F# 订阅代码把 CSS 干的事重新发明了一遍！

---

#### 二、 为什么 AI 会写出这种“反框架”的畸形代码？

你可能会问：“AI 难道不知道 Avalonia 有 Button、ComboBox 和 Style 吗？”
它知道，但它掉进了 **AI 编程的典型死循环（局部逃逸陷阱）**：

1. **起因（逃避主题定制）**：最初你（或提示词）要求某种特定的视觉规范（比如 Fluent 2 暖纸风、特定间距、去掉原生 Button / TextBox 聚焦时的默认蓝框或白底）。AI 不擅长写深层的 Avalonia ControlTheme XAML 模板覆盖，它发现**“直接拿一个裸 Border 拼出来，最容易控制颜色和边距”**。
2. **恶化（打补丁滚雪球）**：一旦用 Border 代替了 Button，潘多拉魔盒就打开了——不能聚焦？加 Focusable = true。回车没反应？加 KeyDown。读屏器不认？手写 AutomationPeer。
3. **癌变（为了测试而拆包）**：当你嫌文件太大、要求“模块化、高测试覆盖率、纯函数化”时，AI 迎合了你的指标——它把上面手搓的ベ塞尔曲线、手搓的坐标计算、手搓的 RGB 混合、手搓的焦点索引，全部拆成一个个独立的 libs/，还美其名曰“纯逻辑库”，写上几百个单元测试！
4. **最终假象**：你看到了 80 个模块清晰、测试全绿的包，以为架构大功告成；实际上，**这 80 个包里有一半都是本不该存在的“人造垃圾轮子”！**

---

#### 三、 真正的解法：做减法！砍掉 60% 的轮子，退回原生 Avalonia

正如你所说：**“顶多就是拿已经提供的组件，配置配置，排列组合一下，逐级拼装。”**
要在保留你现有业务成果（网络协议 Wire、数据存储 JsonSeq/Conversations、配置 AppConfig、模型请求 Anthropic 等非常优秀的后端/领域库）的前提下，彻底治好前端，你需要做一次**残酷的“断舍离”**：

##### 第一刀：彻底废除 *Look 记录与 Follow.live，退回 Avalonia 原生样式表

- **现状**：每个控件库都有一个 Look.fs 和 Kit.fs（仅 Kit.fs 和 Headless.fs 就在十几个文件夹里复制粘贴了十几遍！），用 F# 代码一行行綁定 Padding、FontSize、CornerRadius。
- **怎么砍**：
   在全局写一个 AppStyles.axaml（或者用 F# 写全局 Style），把 ThemePalette 和 ThemeMetrics 注册为标准的 Avalonia ResourceDictionary（使用 ThemeVariant.Light 和 ThemeVariant.Dark）。按钮就是 <Button Classes="secondary" />卡片就是 <Border Classes="grouping-card" />标题就是 <TextBlock Classes="heading" />
   **直接删掉所有控件里的 *Look 参数、Follow.live、ThemeTween、ColorBlend！** 代码量瞬间少掉 30%，而且主题切换和字号缩放由 Avalonia 引擎在底层自动完成，绝对不闪、绝对不漏。

##### 第二刀：废除 Tap / Spot / InvokeBorder，全部换回原生控件

- 把所有 Tap / Spot 换回原生 **Button**。如果不喜欢 Fluent 默认的按钮背景，只需在全局样式里写 10 行针对 Button 的 ControlTheme（覆盖 :pointerover、:pressed、:disabled、:focus-visible 伪类）。
- 把 Kit.Toggle 换回原生 **ToggleSwitch**。
- 把 SelectButton 换回原生 **ComboBox** 或带原生 **MenuFlyout** 的 Button。
- 把 PopupMenu + PopupPlacement + OverlayLayer 的浮层部分，换回 Avalonia 原生的 **ContextMenu** 和 **FlyoutBase.AttachedFlyout**。
- **直接删掉**：PopupPlacement、PopupMenu、FocusOrigin、UiKit 里的假控件和手写 AutomationPeer。原生控件天然自带完美的键盘导航、焦点返还和屏幕阅读器支持，不用你写一行代码去伺候！

##### 第三刀：废除手搓动画定时器，换回声明式 Transition / Animation

- **直接删掉**：ScrollTween、ThemeTween、BreathPhase、CaretBreath、BusyRing、StreamCaret 里的所有 DispatcherTimer！
- 光标呼吸、骨架屏闪烁、加载圆圈旋转，在 Avalonia 里只需要一段 5 行的 Style Animation（设置 IterationCount="Infinite"），由 GPU 渲染管线自动跑；当系统开启“减弱动效”时，样式选择器自动停掉动画。

##### 第四刀：废除手搓虚拟化，换回原生 ItemsControl / ListBox + 数据绑定

- **直接删掉**：MountWindow、CardReconcile、BlockDiff、RevealFlash、ExchangeJump 里复杂的索引和 DOM 对账代码。
- 聊天消息列表就是一个标准的 ItemsControl（或 ListBox），配原生 VirtualizingStackPanel。

---

#### 四、 盘点一下：你的 80 个包里，哪些是真金，哪些是废铁？

为了让你心里有底，我帮你把 libs/ 目录分个类：

1. **🟢 真金白银（绝对要保留的核心资产，写得非常好）**：**协议与核心领域**：Wire, Agui, AguiMessageMap, Conversations, CommandId, Canonical, PlainText, ToolNames, ToolSummary**存储与配置**：JsonSeq, AtomicTextFile, DirLock, AppConfig, ClientTomlFile, VerifiedCopy, SandboxFiles, DraftStore**AI 与服务端**：Anthropic, AgentHistory, HttpRetry, HttpFailureFacts, SharedHttpClients, MediaContent, ModelListing, JsonRpc, Approvals, AttachmentBlobs, AttachmentRefs, AttachmentAudit, DeviceListJson, FailureLimiter, ListenAddress, ClientToken, JsonLinesLog, RefreshingPool**纯业务规则/解析**：MarkdownBlocks, KatexHtml, HljsHtml, HljsCatalog, FuzzyMatch, MediaTypes, ModelVision, ConversationSummary, ConversationTransfer, DevicesModel, HostCatalog, UiPrefs
2. **🔴 手搓轮子（伪装成架构的废铁，导致 UI 稀巴烂的罪魁祸首，建议逐步全部淘汰）**：**假控件与假样式传递**：UiKit (里的 Tap/Spot/InvokeBorder/Follow), 各包里重复了十几遍的 *.Look.fs 和 *.Kit.fs, FocusOrigin**假弹窗与假菜单**：PopupPlacement, PopupMenu, OverlayLayer, ToastQueue, ToastDwell**假动画与定时器**：ScrollTween, ThemeTween, ColorBlend, BreathPhase, CaretBreath, BusyRing, StreamCaret**假虚拟化与滚动补丁**：MountWindow, CardReconcile, BlockDiff, ScrollFollow, ExchangeJump**被切碎的 UI 状态布尔判断**：StartReady, IdleSurface, ConversationSurface, EscapeChain, SearchRequery, SidebarEmpty

---

#### 五、 怎么用“排列组合、逐级拼装”重写前端层？

砍掉上面那堆假轮子之后，你的前端其实薄得像一张纸。

在 F# + Avalonia 中，如果你想实现真正的**“只拿现成组件配置配置、排列组合、逐级拼装”**，最符合你直觉的做法是：

1. **样式归样式（全局一套 Styles.axaml）**：
   定义好全局的 Button、TextBox、ToggleSwitch、ComboBox、ListBox、Flyout 长什么样。业务代码里**不允许出现任何 Margin、Padding、FontSize、CornerRadius 的动态绑定代码**，只允许贴 Classes="primary"、Classes="card"。
2. **组件归原生（用 Avalonia.FuncUI 或纯声明式 ViewModel）**：
   如果你用 Avalonia.FuncUI（F# 官方社区最主流的 Avalonia 声明式包装库，本质上就是把 Avalonia 原生控件变成可拼装的积木函数），一个面板的代码会从现在的 400 行锐减到 40 行：// 没有 Look，没有 Follow.live，没有 Spot/Tap，没有手动 Focus！
   // 纯粹就是拿 Avalonia 原生控件排列组合：
   let view (model: DevicesModel) (dispatch: Msg -> unit) =
   StackPanel.create [
   StackPanel.classes [ "page-container" ]
   StackPanel.children [
   TextBlock.create [
   TextBlock.classes [ "heading" ]
   TextBlock.text "设备"
   ]
   Button.create [
   Button.classes [ "primary" ]
   Button.content "添加设备"
   Button.isVisible model.CanInvite
   Button.onClick (fun _ -> dispatch RequestInvite)
   ]
   ItemsControl.create [
   ItemsControl.viewItems (
   model.Devices |> List.map (fun dev ->
   Border.create [
   Border.classes [ "grouping-card" ]
   Border.child (deviceRowView dev dispatch)
   ]
   )
   )
   ]
   ]
   ]

**总结你的破局之道：**
你现在的痛苦，是因为 AI 把**“使用 GUI 框架”**做成了**“研发 GUI 框架”**。
守住你那 30 多个极其优秀的后端、协议、存储和纯解析库（绿名单），把前端那 40 多个手搓 Border、手搓定时器、手搓焦点和坐标计算的伪组件库（红名单）果断废弃，退回到 **Avalonia 原生控件 + 全局样式表 + 单向状态树拼装**。代码量砍掉三分之二的那一刻，就是你所有恶性 UI Bug 清零的那一刻。

---

### 👤 User

docs AGENTS.md18,964 tokens

---

### 👤 User

建议和本仓库的 AGENTS.md 精神矛盾吗？如果不矛盾，如何设置 AXIOM 等等来确保更好的规范？

---

### 🤖 Gemini

一点也不矛盾。恰恰相反：**你刚才说的“没有代码就是最好的代码、不手搓轮子、用原生组件排列组合”，正是你这份 AGENTS.md 前半部分（GUIDE、AXIOM、LEMMA）最核心的精神；而目前代码库里那堆稀巴烂的 UI 包，则是因为 AGENTS.md 后半部分（# 本项目：wanxiang2 的具体细则）违背了前半部分的公理。**

你看一眼自己写在文件最开头的 GUIDE：

> Invoke Goodhart’s Law and Via Negativa when needed. Treat these principles as Wittgenstein’s Ladder. Build the real thing, not its imitation.
> （必要时援引古德哈特定律与减法之道。把这些原则当作维特根斯坦的梯子。建造真东西，不要造它的仿制品。）

再对比一下代码库里实际发生的事，你会发现三个极其精准的错位：

#### 一、 为什么现有的 UI 代码违背了你自己的 AGENTS.md？

##### 1. 违背了 Build the real thing, not its imitation（建造真东西，不要造仿制品）

为什么代码里全是 Tap、Spot、InvokeBorder？
看你 AGENTS.md 第 145 行写下的项目局部规则：

> “也不用 ControlTheme：它只按控件类型或 Theme 属性生效，不能按类名套在 Border 上。控件只挂类名。”

**病根就在这句话里。**
因为规定了“只准在 Border 上挂类名、不准用 ControlTheme”，AI 就没法使用 Avalonia 原生的 Button、ToggleSwitch、ComboBox（因为原生控件必须通过 ControlTheme 改模板和状态样式）。结果 AI 只能拿 Border 去**仿制**按钮、**仿制**开关、**仿制**下拉框，再手写 TapPeer 去**仿制**无障碍接口。
这正是典型的“不造真东西，去造仿制品”。

##### 2. 违背了 AXIOM 2 与 LEMMA 8（把复杂度移到别处不是简化；抽象要有收益）

看 AGENTS.md 第 144 行：

> “Look 记录：每个控件库声明自己的 <Name>Look 记录……尺寸、字体、间距为管理者给的活实例，如 IObservable<_>。组合者在构造控件时从主题管理者取出共享实例填进去。”

Avalonia 本身就有资源字典（ResourceDictionary）和样式表（Styles），控件原本不需要知道任何字号、间距的传递过程。但这条局部规则强迫每个控件定义几十个字段的 Look 记录，用 Follow.live 手动订阅，再由外层组合者把几百个 IObservable 手工插进去。这没有消除任何复杂度，只是把样式表的职责变成了几万行 F# 手工接线代码。

##### 3. 忘了扔掉“维特根斯坦的梯子”（古德哈特定律生效）

在迁移期（2026-10-17 之前），你们的目标是**“逐个抽干旧仓库”**。为了安全搬家，当时定下的规矩是：从旧文件里切出一块代码 $\rightarrow$
放进独立的 libs/<Name> $\rightarrow$
跟旧代码跑出来的 Golden（金样）逐字节比对一致。

在搬家阶段，这架“梯子”帮你把旧仓库安全拆空了。但是：

1. **旧仓库里的 Bug 和坏手艺（比如手搓滚动、手搓虚拟化、8 个布尔值判断界面状态、到处 Dispatcher.Post 抢焦点），被金样测试当作“神圣不可侵犯的标准答案”一并封存了下来。**
2. 当“每个独立文件拆成一个无兄弟依赖的包”变成考核指标（./x check）时，古德哈特定律发作了：AI 把原本属于同一个状态机的四五个分支，硬生生肢解成了 StartReady、IdleSurface、ConversationSurface、EscapeChain 等几十个碎包。肢解之后，所有的时序同步压力全部坍缩到了 compose/AppShell（2100 行的超级胶水层）里。

2026-10-17 旧仓库已经物理废弃了。**梯子已经爬完，到了该扔掉梯子、按 AXIOM 2（总体复杂度越低越好）和 Via Negativa（减法）重写局部规则的时候了。**

---

#### 二、 如何修订 AGENTS.md 来确立“不造轮子、一眼无 Bug”的规范？

按你的 LEMMA 1（**规则也可以重构**），你不需要推翻顶层框架，只需要：

1. 在通用 LEMMA 和 SPEC 里补上**“底座能力直用”**、**“完整个体的粒度下限”**与**“状态单一归属”**三条约束；
2. 彻底改写 # 本项目：wanxiang2 里的 **## 界面控件库** 章节，废除 Look 记录、Tap/Spot 仿制控件和“旧代码金样崇拜”。

以下是我按你文档的文风（费孝通式的平实准确、小平邦彦式的安静清晰）为你拟定的修订条款，你可以直接替换或合入 AGENTS.md：

---

##### 修订 1：在 # LEMMA 中补入或强化三条推理

建议将以下内容并入现有的 LEMMA 3、LEMMA 5、LEMMA 8（或作为补充条款）：

> **底座已有的能力直接使用，不造仿制品。** [AXIOM: 2, 5; LEMMA: 8]
> 已声明的基础依赖（运行时、标准库、界面框架）若已提供某项机制（如按钮与开关交互、焦点流转、浮层定位与外部点击关闭、列表虚拟化、样式资源下发、属性过渡动画），直接使用并配置该机制。不因为不熟悉其配置方式，就退回底层图元（如裸边框、裸定时器、手工坐标计算）重新拼装一个功能残缺的仿制品。推理：成熟底座已承担了大量跨平台与无障碍的边界条件（AXIOM 5）。用底层图元手搓仿制品，表面上少了框架配置，实际上要用成倍的业务代码和补丁去应付焦点、重入、输入法与触控边界，直接违背 AXIOM 2。

> **完整个体有粒度下限：拆职责，不拆条件分支与算式。** [AXIOM: 2, 3; LEMMA: 3, 4]
> 一个库（libs/）应对应一项能独立成立的领域职责或一个契约完整的组件。同一个状态机里的单个判断分支、纯算式、常量表或辅助函数，属于该状态机的内部实现，不单独拆成库。若拆出一个库后，导致外层组合者必须维护更多的同步步骤或传递更多的布尔标志，说明拆碎了同一个职责，应合回原处。推理：把一个完整状态机切碎成十几个无状态小函数库，并没有减少依赖，只是把状态不变量拆散成组合者手里的隐式时序约定，违背 AXIOM 2 与 AXIOM 3。

> **状态单一归属，界面是状态的纯投影。** [AXIOM: 2, 3; LEMMA: 3]
> 同一份业务事实在内存中只有一个主人；用精确的类型（如互斥分支）表达合法状态，不用多个布尔或可空字段的排列组合暗示状态。展示组件不私藏业务状态的副本，也不提供多个具有调用先后依赖的 Set* 方法；组件接收完整视图状态并幂等地投影到界面。焦点转移与滚动定位作为状态转移的结果统一执行，不在子组件事件中各自异步投递。推理：多处保存同一事实或靠调用顺序拼凑界面，必然在异步事件交错时产生分裂状态（AXIOM 3）。消灭非法状态在类型上的可表示性，才能写出“明显没有缺陷”的代码（AXIOM 2）。

---

##### 修订 2：重写 # 本项目：wanxiang2 下的 ## 界面控件库

把原来第 140–149 行强制要求 Look 记录、UiKit.Tap/Spot、禁用 ControlTheme 的段落，整体替换为以下规则：

```
## 界面与前端组装（UI）

迁移期的「逐字节复刻旧仓库金样」已完成历史使命并退役。自本规则生效起，前端代码以「不造轮子、单一状态投影、真实操作闭环」为准绳，按 Via Negativa 持续做减法。[AXIOM: 1, 2, 4; LEMMA: 1, 8]

1. **只用 Avalonia 原生控件，废除 `Border` 仿制件。** [AXIOM: 2, 5]
   按钮用 `Button`，开关用 `ToggleSwitch` / `CheckBox`，下拉选择用 `ComboBox`，菜单与气泡用 `ContextMenu` / `MenuFlyout` / `Flyout`，折叠用 `Expander`，长列表用 `ItemsControl` 或 `ListBox` 配合原生 `VirtualizingStackPanel`。
   不再使用 `Tap`、`Spot`、`InvokeBorder` 等以 `Border` 冒充交互控件的做法，也不再手写 `AutomationPeer`、手算浮层坐标（`PopupPlacement`）或手搓列表挂载窗口（`MountWindow`、`CardReconcile`）。它们列为待退出的迁移遗留，逐步替换为原生控件并删除对应库。

2. **样式与动效归 Avalonia 资源与样式系统，废除 `*Look` 记录与帧定时器。** [AXIOM: 2]
   颜色、字号、间距、圆角、过渡时长统一登记在主题层的 `ResourceDictionary`（含 Light / Dark `ThemeVariant`）与 `ControlTheme` / `Styles` 中。控件通过 `DynamicResource` 与样式类（`Classes`）获取外观。
   新建或改写的界面组件不再声明 `<Name>Look` 记录，不再用 `Follow.live` 逐属性订阅 `IObservable<float>`。呼吸、旋转、颜色过渡与平滑滚动优先使用 Avalonia 声明式 `Transitions` 与 `Animation`，不在 F# 代码中开 `DispatcherTimer` 逐帧计算。

3. **单入口幂等渲染，单出口意图事件。** [LEMMA: 3, 4]
   视图组件只负责呈现与采集用户意图，不持有业务状态副本。
   - **输入（渲染）**：每个视图组件对外只暴露一个幂等的 `Render(viewState)` 方法（或纯声明式视图函数），一次性接收该组件所需的全部展示数据；废除多入口、有调用顺序依赖的 `SetTitle` / `SetGenerating` / `SetEnabled` / `SetCompact` 零散方法。
   - **输出（意图）**：用户操作通过明确的消息类型（`<Name>Msg` 或事件）向上汇报，组件内部不自行决定跨组件副作用。

4. **工作区状态收敛与统一副作用执行。** [LEMMA: 3, 4; SPEC: 3, 4]
   客户端工作区的连接阶段、引导流程、当前对话、流式生成、待删批次与浮层状态，由组合者以单一互斥的状态树建模（让非法状态在类型上不可表示），不再拆成多个布尔入参的判断库（如 `StartReady`、`IdleSurface`、`ConversationSurface`）。
   焦点转移、滚动定位、网络发送与计时器启停，作为状态演进产出的显式动作，在每次视图树 `Render` 完成后由宿主统一执行一次；禁止在子控件内部散落调用 `Dispatcher.UIThread.Post` 争抢焦点或滚动偏移。
```

---

##### 修订 3：在 # SPEC（或本项目的证据要求）中修正前端验收标准

把原来依赖“旧代码金样（Golden Text/Tree）”的证明方式，改为验证**不变式与闭环旅程**：

> **7. 界面闭环与不变式证明（本项目补充）。** [AXIOM: 1, 5; SPEC: 3, 4, 6]
> 界面验证以用户的真实操作闭环为准，不再新增与旧仓库死代码比对的金样测试（Golden Test）。**状态闭环证明**：对核心用户旅程（首次配对 $\rightarrow$
> 引导配模型 $\rightarrow$
> 发送含附件消息 $\rightarrow$
> 流式增量与工具审批 $\rightarrow$
> 中途停止或失败重试 $\rightarrow$
> 切换/搜索/删除恢复对话 $\rightarrow$
> 断线重连），通过向工作区状态机连续输入真实事件序列，验证每一步的视图状态投影无冲突、无残留在途锁死（Pending Wedge）。**原生交互证明**：在 Headless 窗口中验证焦点流转（Tab 序、Esc 退栈后焦点归位、删除后邻位聚焦）与无障碍角色均由 Avalonia 原生控件及统一焦点调度兑现，无未处理的异步重入竞态。

---

#### 三、 改完 AGENTS.md 之后，你下一步怎么走最稳？

你看，按 AXIOM 4（**工作应分成能兑现收益的小步；每一步之后，已有能力仍然可用，成果可验证，并且可以停下来**），你完全不需要搞一次伤筋动骨的“全仓推倒重写”。

你可以拿着上面修订后的 AGENTS.md，让 AI 按这个顺序每次只做一个可验证的小步减法：

1. **第一步（登记例外出口）**：把现有的 UiKit（Tap/Spot）、*Look、手搓动画库、碎布尔库，在 AGENTS.md 的《例外清单》里集中登记一行：**“迁移期遗留的手搓 UI 原语与碎片状态库 / 解除条件：按工作区模块逐个替换为原生 Avalonia 控件 + ControlTheme + 单入口 Render 后删除”**。
2. **第二步（先立新主题底座）**：在 compose/Theme 里把 ThemePalette 和 ThemeMetrics 映射成一套标准的 Avalonia ResourceDictionary 和原生 Button / TextBox / ToggleSwitch / ComboBox 的 ControlTheme。
3. **第三步（先拿一个经常出 Bug 的面板开刀，比如 ChatHeader + ComposerBox）**：把它们里面的 Spot / Tap 换成真 Button；删掉它们的 Look 记录，直接用样式类名和 DynamicResource；把 ComposerBox 的 8 个 Set* 方法合并成一个 Render(ComposerViewState)；把散落的焦点 Post 删掉。跑一遍 ./x all 和真机冒烟，你会立刻发现代码少了上千行，而输入框和顶栏的恶性 Bug 彻底消失了。
4. **第四步（合并工作区碎脑包）**：把 StartReady、IdleSurface、ConversationSurface、EscapeChain 合进 compose/AppShell（或一个新的 tate），用 F# 的可区分联合（DU）替代那十几个布尔值，顺手把那几个微型包删掉。

这样改，不仅完全符合你 AGENTS.md 的每一条公理和引理，而且是真正地**“踩着维特根斯坦的梯子爬上来，然后把梯子一脚踢开”**。


---

*Exported by [AI Chat Exporter for Google AI Studio](https://github.com/TheRajatJain/ai-chat-exporter) · October 19, 2026 at 10:45 PM*
