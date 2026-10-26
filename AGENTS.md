[GUIDE: Apply ASD-STE100 to source code. Use Mise-en-place for code design and reasoning: prepare complete, independent responsibilities, then compose them explicitly. Prefer flat dependencies and autonomous, message-oriented objects inspired by Smalltalk and the Actor Model, even without concurrency. Start with minimal complete parts; strengthen them independently in small, verified steps, using removable, decorator-inspired enhancements where they simplify the whole. Adapt Gall’s Law, Chesterton’s Fence, and Poka-yoke. Invoke Goodhart’s Law and Via Negativa when needed. Treat these principles as Wittgenstein’s Ladder. Build the real thing, not its imitation. Try to falsify it.] [For natural-language discussion, follow Fei Xiaotong’s plain precision and Kunihiko Kodaira’s quiet clarity. Avoid officialese, performative rhetoric, translationese, and textbook cadence. Let the subject shape the prose.]

# 使用约定

本模板不预设项目、语言、运行时或技术栈。Mise-en-place 在这里指设计和思考的工作方式：先弄清这一小步要做什么，把事实、职责、状态归属和验证办法准备到位，再组织合作。对象自治说明每一部分如何独立成立，显式组合说明它们如何合作，可拆增强说明它们如何演进。这是一组相互配合的设计选择，不把这些术语当作同义词。

规则只有三种身份。**AXIOM** 陈述使用者或开发者真正在乎的事，并写明理由；**LEMMA** 引用前提，写清推理及适用条件；**SPEC** 是可由生产或测试代码验证的 LEMMA，写清验收与证明。不能从现有前提推出的选择，应明示新增偏好或假设，不把技术习惯写成必然结论。

证明栏记录实际代码或测试位置、验证结果和覆盖范围；尚未验证写 `[TODO]`，部分验证写明剩余边界。测试证据不冒充对所有情况的数学证明；不适用的条款应说明原因。事实、推断、计划分开写，改了规则不等于改了实现。

代码、测试与文档都应能追溯到这些规则。根目录只保留一份 GUIDE 和通用规则；独立职责的局部规则、实现与测试就近放置，引用根规则，不复制整套体系。不要求每个函数写一份论证，也不为填写三个标题建立三套文档。

# AXIOM — 在乎什么

1. [使用者、开发者] 软件解决真实问题；规则必须能追溯到人在乎的结果，并允许真实使用和反例修正它。
    - 理由：实现、术语和指标都不是目的。结果不对时，需要查明规则、前提或实现哪里错了，而不是维护一套自洽却无用的说法。

2. [开发者] 在兑现必要承诺的前提下，总体复杂度越低越好。
    - 理由：成本包括理解、修改、验证、运行和交付，也包括接口、接线、文档与例外。把复杂度移到别处，或缩短文件而增加间接层，不是简化。

3. [开发者] 每个独立职责都应是完整个体，能单独理解、实现、验证和使用；它的内部变化与它同别人合作的方式，应能分别改变。
    - 理由：修一处问题不应先读懂整套系统。改变合作对象或合法协作顺序，不应迫使参与者一起重写。

4. [使用者、开发者] 工作应分成能兑现收益的小步；每一步之后，已有能力仍然可用，成果可验证，并且可以停下来。
    - 理由：长期维护不能依赖一次遥远的大切换。最简实现也必须兑现当前承诺，不能把正确性留给未来的加强版本。

5. [使用者] 系统承担实现细节的认知负担；用户只需理解用法及真实后果。
    - 理由：用户不必按内部事件和组件理解操作，但成功、失败、未知及不可撤销后果不能被友好表述掩盖。封装隐藏机制，不歪曲事实。

6. [使用者] 在用户已明确授予的权限范围内，信任其决定，不重复请求同一授权，也不增加与目标无关的操作限制；防呆针对真实误操作及其后果。
    - 理由：反复审批已决定的事会打断工作。权限范围由具体项目和用户明确，通用模板不预授无限权限；边界内优先让错误难以发生，让可撤销操作易于恢复。

# LEMMA — 如何组织与演进

1. **规则也可以重构。** [AXIOM: 1, 2]
    接手或修改系统时，沿实现追问原始需要，重新判断一条规则是 AXIOM、LEMMA 还是 SPEC；可以合并、改写或删除重复前提。不满意不自动意味着增加规则：先区分实现错误、推理错误和前提缺失；确实无法由现有前提判断时，再从使用事实归纳新 AXIOM 并写理由。
    - 推理：规则为真实结果服务（AXIOM 1），自身也计入总体复杂度（AXIOM 2）。因此既不能以现有实现证明其必要，也不能遇到反例就不断叠加条款。

2. **先各就其位，再组织合作。** [AXIOM: 2, 3, 4]
    每次只为下一条可运行闭环做好准备：确认目标和已有事实，划出职责，明确各自的输入、结果、状态与不变量，找出谁负责协作，选定能推翻方案的验证。输入缺失就观察或试验，不用默认值补成事实；优先验证可能推翻边界的假设。能独立准备的部分可以分别推进，有因果关系的动作由协作协议安排；没有并发时也照此组织。
    - 推理：完整个体要求边界可说清（AXIOM 3），小步要求只准备当前需要的范围（AXIOM 4）。把未知和关系提前摆明，才不会在实现中用全局状态和临时接线补洞；预先设计全部模块则违背 AXIOM 2。

3. **对象拥有行为，也拥有状态。** [AXIOM: 2, 3]
    对象不是供外部流程摆弄的数据容器。私有字段、集合、缓存及跨调用状态可以直接可变，不必先改写成不可变状态传递；局部指所有权边界，不只指函数作用域。对象自行维护不变量，外部通过公开消息请求行为、取得结果，不代它修改内部状态。跨边界不共享可被双方修改的内部别名；可用不可变值、复制或明确的所有权转移来保证这一点。
    - 推理：若合法状态依赖外部同时照看，就无法独立判断正确性（AXIOM 3）。约束应落在归属和可观察行为上，不落在可变字段数量上（AXIOM 2）。普通成员调用和返回值足以承载消息；同步调用也要守封装与重入边界，不为采用此风格建设 Actor 运行时。

4. **个体拥有自身，组合者拥有协作。** [AXIOM: 3; LEMMA: 3]
    对声明独立的 A、B，由 C 分别发出消息、接收结果并安排后续动作；A、B 不持有或调用对方，也不认识 C 的工作流。协议表达本身的请求、事实和结果，必要转换由 C 负责；不能把对 B 的调用脚本藏进 A 的返回值，再让 C 机械转发。把 B 交给 C，与把 B 的接口、泛型服务、闭包或发送器注入 A、仍由 A 调度，是两件事。
    C 也是完整个体，拥有这一段合作的顺序、结果关联及必要的在途状态；A、B 保留领域判断，C 不复制它们的业务账本。轻型组合指不重复参与者的职责，不是没有状态或限制代码行数。组合出的整体可以成为更外层的参与者，但只有形成了真实的整体能力才值得再包一层。
    - 推理：AXIOM 3 要求合作方式能在参与者之外改变，故这份职责必须另有归属；它不能藏在服务注入或全局设施里。把参与者的判断也搬到 C，只是转移大泥球，并没有保住 LEMMA 3 的对象自治。

5. **优先扁平依赖，不追求层数指标。** [AXIOM: 2, 3; LEMMA: 4]
    完整职责优先并列摆放，由组合者直接使用各自的公开能力，避免形成跨职责的逐层转发链。A、B 之间没有依赖边，C 依赖二者；必要的基础设施与协议依赖如实声明，不藏进公共大上下文、万能基础库或共享服务接口树。目录平不代表依赖平，没有 import 也不代表没有运行时调用依赖。
    - 推理：理解一项职责所需追踪的其他职责越少，越接近 AXIOM 3；没有独立行为的转发层只增加成本（AXIOM 2）。有用的组合与增强可以形成必要层次，不为了压平图而制造巨型 C，也不把依赖深度、文件数或模块数设成进度目标。

6. **先拆成完整个体，再各自加强。** [AXIOM: 2, 3, 4; LEMMA: 2, 4]
    先把当前能力拆成少量有明确职责的部分，分别做成最简完整实现，再通过真实组合跑通当前闭环；随后逐个改进，不先把整个系统加固成更难拆的整体。边界尚不清楚时，用小的真实场景校正，不先造一批空壳库。这里的“先”是下一步的施工次序，不是必须先拆完全部系统才能使用或改进。小步按能否独立闭环衡量，不按代码行数或文件数衡量。
    - 推理：独立改进以独立职责为前提（AXIOM 3），但拆完才能有收益又违背 AXIOM 4。故拆分、实现、接回、验证应在小范围内闭合。最简完整可以承诺较小范围，却不能缺少该范围内必要的正确性、失败处理与资源收尾。

7. **增强应可拆，不必凡改进都套层。** [AXIOM: 2, 3, 4; LEMMA: 6]
    基础实现先独立兑现公开契约。需要额外能力时，优先采用组合或 decorator-inspired 的可拆增强：基础对象不知道增强者，增强者只使用公开协议、拥有自己的附加状态，不夺取基础状态的控制权。移除增强后，基础契约仍成立；增强特有的收益不必保留，也不能继续宣称存在。
    可替代的增强必须保住基础契约中的结果、错误、次序与副作用保证，而不只是保持方法签名。若改变公开承诺，就承认这是新契约；基础正确性不能靠一个“可选”外层补齐。多个增强不预设可交换或彼此独立，验证实际支持的组合及顺序；不能独立拆除的耦合应明确建模或登记，不冒称正交。直接简化内部实现更清楚时，就直接改内部，不为优化强造包装层、开关矩阵和插件框架。
    - 推理：可拆增强允许分别演进（AXIOM 3），保留可用基线使每步收益可兑现（AXIOM 4）。但装饰本身也有成本，若只是增加间接层，就不满足 AXIOM 2。组合优于继承是默认方向，不是新的形式主义。

8. **抽象要有收益，删除要懂后果。** [AXIOM: 1, 2, 3]
    抽象应指出当下隔离的独立职责、外部契约、重复或实测成本；第二个使用方是证据之一，不是封装职责的先决条件。找不到实际收益就先用具体实现。删除前查清旧代码承担的真实义务，再删多余层、重复事实和过期路径；暂时删不掉要区分真实需要与尚未解开的耦合，不能把后者直接提升为公理。
    - 推理：独立职责本身就能降低维护范围（AXIOM 3），不必等复制第二遍才封装；但“抽象”这个名称不证明收益（AXIOM 2）。旧代码也不能仅凭存在就证明必要，是否保留应回到使用后果（AXIOM 1）。

9. **例外只登记一处，并有出口。** [AXIOM: 1, 2; LEMMA: 8]
    临时兼容层、开关、废弃接口和未解耦关系，统一写在本文《例外清单》：使用方、保留理由、可验证的解除条件。条件满足就删除；无实际使用方的不建、不继续保留。若确无解除条件，应重新判断它是不是长期需求，明确修订相关规则和独立性声明，不虚构期限，也不靠改名为“上下层”宣告解耦。
    - 推理：必要义务应有依据（LEMMA 8），临时成本应可盘点和退出（AXIOM 2）。登记不是豁免，也不能把尚未满足的规则变成已完成。


10. **底座已有的能力直接使用，不造仿制品。** [AXIOM: 2, 5; LEMMA: 8]
    已声明的基础依赖（运行时、标准库、界面框架）若已提供某项机制（按钮与开关交互、焦点流转、浮层定位与外部点击关闭、列表虚拟化、样式资源下发、属性过渡动画），直接使用并配置该机制。不因为不熟悉其配置方式，就退回底层图元（裸边框、裸定时器、手工坐标计算）重新拼装一个功能残缺的仿制品。
    - 推理：成熟底座已承担了大量跨平台与无障碍的边界条件（AXIOM 5）。用底层图元手搓仿制品，表面上少了框架配置，实际上要用成倍的业务代码和补丁去应付焦点、重入、输入法与触控边界，直接违背 AXIOM 2。

11. **完整个体有粒度下限：拆职责，不拆条件分支与算式。** [AXIOM: 2, 3; LEMMA: 3, 4]
    一个库（libs/）应对应一项能独立成立的领域职责或一个契约完整的组件。同一个状态机里的单个判断分支、纯算式、常量表或辅助函数，属于该状态机的内部实现，不单独拆成库。若拆出一个库后，外层组合者必须维护更多同步步骤或传递更多布尔标志，说明拆碎了同一个职责，应合回原处。
    - 推理：把一个完整状态机切碎成十几个无状态小函数库，并没有减少依赖，只是把状态不变量拆散成组合者手里的隐式时序约定，违背 AXIOM 2 与 AXIOM 3。

12. **状态单一归属，界面是状态的纯投影。** [AXIOM: 2, 3; LEMMA: 3]
    同一份业务事实在内存中只有一个主人；用互斥类型表达合法状态，不用多个布尔或可空字段的排列组合暗示状态。展示组件不私藏业务状态的副本，也不提供多个具有调用先后依赖的 `Set*` 方法；组件接收完整视图状态并幂等地投影到界面。焦点转移与滚动定位作为状态转移的结果统一执行，不在子组件事件中各自异步投递。
    - 推理：多处保存同一事实或靠调用顺序拼凑界面，必然在异步事件交错时产生分裂状态（AXIOM 3）。让非法状态在类型上不可表示，才能写出「明显没有缺陷」的代码（AXIOM 2）。

# SPEC — 用什么证据验收

1. **规则可追溯。** [LEMMA: 1, 8, 9]
    每条 AXIOM 有理由，每条 LEMMA 有有效前提和推理，每条 SPEC 对应可检查的行为及证明范围；显式推导不得循环自证。实现和测试承接规则，但不要求逐行注释规则编号。证据能检验真实行为，不只检查名称、类型形状或文件存在。
    - 证明：[TODO] 检查引用、理由和验收映射；选取有代表性的规则，验证实现与测试确实承接，并能拒绝一个违反该规则的案例。记录未覆盖项；未运行、局部通过和整体通过分开陈述。

2. **职责与交付独立。** [LEMMA: 4, 5]
    移除兄弟职责和组合者的源码、实例及服务回调后，只凭本库、工具链与声明依赖，仍能构建并验证本库公开行为；不塞入模拟对方调用流程的替身补洞。声明独立的 A、B 不相互依赖，整体依赖图无环。交付物能按声明的使用方式脱离开发工作区运行，不回退读取未交付文件。
    - 证明：[TODO] 逐库做隔离构建和行为测试，再以实际交付物做干净消费验证；检查源码、交付依赖和运行时服务调用。测试可以提供协议允许的事实输入，不能提供一个偷偷扮演对方流程的服务。构建独立、协作独立、交付独立分别记录结果。

3. **状态确有主人。** [LEMMA: 3, 4, 7]
    每份可变状态只有一个负责其合法修改的对象，不能靠调用方记住额外步骤才保持正确。外部修改输入或返回值不能暗改对象内部；C 的协作状态与参与者的业务状态各有归属，不是两份可互相覆盖的权威。
    - 证明：[TODO] 验证实例隔离、输入与返回值的别名边界、非法消息及错误后的状态；若有异步等待、回调重入或并发，验证这些实际边界。运行方式是同步不能代替此项证明，也不要求为了测试凭空引入并发。

4. **协作由真实组合证明。** [LEMMA: 4, 5, 6]
    C 的规则明确参与者选择、消息顺序、结果关联及结束条件；不代替 A、B 作领域判断。改变一种实际需要的合法合作方式时，不修改 A、B 的内部实现。仅替换一个同接口实例，不足以证明协作职责已经移出。
    真实组合覆盖成功和失败；涉及外部效果时，按实际协议覆盖重复、晚到、未知结果及必要的恢复。发送、接收、持久提交与业务完成不能相互冒充；没有证据的结果保持未知，不能自动转成再次执行的许可。
    - 证明：[TODO] 使用真实参与者完成最小场景，再针对实际风险注入故障、重复或中断。替身可以辅助定位和证伪，但不能代替所声称的外部能力证明。记录已验证的组合、运行环境及未证明的边界。

5. **增强经得起拆除。** [LEMMA: 5, 7]
    基础实现单独通过基础契约测试；加入增强后仍通过同一组基础契约测试，并有增强自身的证据；移除增强的代码或依赖后，基础实现仍能独立构建和使用。测试检验承诺，不要求内部步骤一致；实际支持的多增强组合及顺序有明确验证，不能假定交换顺序结果不变。
    - 证明：[TODO] 在隔离环境中分别验证基础版、增强版和实际组合；若增强有额外依赖，验证去掉它们也不影响基础版。检查外层失败、移除或换序是否泄漏为基础契约变化。必要保证一旦随外层消失，就不能称该外层为可选增强。

6. **每步形成真实闭环。** [LEMMA: 2, 6, 8, 9]
    一次改动围绕一项可说明的职责或增强，结束时有可运行能力、对应验证和剩余边界；不是只建了目录、接口或尚未接回的另一套实现。先跑最小相关验证，再检查受影响的真实组合；不以全量测试数量代替风险判断。迁移分期时，分别记录“独立能力完成”和“实际接线、旧职责退出完成”，不能混写。
    - 证明：[TODO] 从变更、交付物和测试记录核对本步成立的行为；迁移时确认真实调用方采用新实现，以及被替代职责的退出状态。确认没有遗留无使用方的抽象、过期兼容项或未登记的双重实现。中途停止时仍能使用的成果，才算已经兑现。

7. **界面闭环与不变式证明（本项目补充）。** [AXIOM: 1, 5; SPEC: 3, 4, 6]
    界面验证以用户的真实操作闭环为准，不再新增与旧仓库死代码比对的金样测试（Golden Test）。
    - 状态闭环证明：对核心用户旅程（首次配对 → 引导配模型 → 发送含附件消息 → 流式增量与工具审批 → 中途停止或失败重试 → 切换 / 搜索 / 删除恢复对话 → 断线重连），向工作区状态机连续输入真实事件序列，验证每一步的视图状态投影无冲突、无残留在途锁死（Pending Wedge）。
    - 原生交互证明：在 Headless 窗口中验证焦点流转（Tab 序、Esc 退栈后焦点归位、删除后邻位聚焦）与无障碍角色均由 Avalonia 原生控件及统一焦点调度兑现，无未处理的异步重入竞态。
    - 证明：**部分证据**（逐条核实到用例体，2026-10-21 起逐轮回填，2026-10-24 终局实测收口）。`compose/AppShell/tests/ComposerTests.fs` 有六条名为「旅程 N」的用例与若干接线用例；**用例名不等于覆盖**，以各轮实测为准：旅程 3 已按主机 `attachment.committed` 的确认门控发送（未就绪时发送被挡住、草稿与附件都留着）并断言真正发出（旅程内 AttachFiles 上传序列、未 ACK 门控拦截、committed ACK 与发件带附件元数据）；旅程 5 的「失败重试」已补真实重试按钮走通重试路径的用例（错误态触发 Regenerate 并经新生成完成清除错误）；旅程 6 的**到期真删**已有证据（`compose/AppShell/tests/ExpiryDeletionTests.fs`：用注入的假时钟（`ShellPorts.Now`）推进过宽限期，断言客户端发出**恰好一条** `conversation.delete` 命令、待删批次清空，且**同一条**命令送进服务端事件存储后该对话从投影里消失）；底层状态机纯逻辑回归用例（3 条）在 `libs/WorkspaceState/tests/WorkspaceStateTests.fs` 同步补齐。「原生交互证明」在顶层无头窗口下验证控件树原生 Tab 焦点迁移、Esc 搜索清空与删除邻位聚焦（名称与角色的**整树遍历断言**由 `compose/AppShell/tests/A11yTraversalTests.fs` 承担，画廊另有 `compose/Gallery/A11yAudit.fs` 审查）。画廊像素基线门禁（G3）已补齐全部 41/41 条目（深浅双主题共 82 张 PNG 基线，Linux 容器 `wx2-linux-gallery-env:local` 实测 82/82 通过，见 `compose/Gallery/AGENTS.md`）；浏览器侧已发布产物真实交互 e2e 冒烟（`tools/smoke_published.sh`）28/28 项断言全部通过（含 ToolTip / 消息卡 ContextMenu / 输入框 MenuFlyout 三类弹出层中文 OCR 字形与业务词条核验、命令面板执行落盘、设置页开关持久化、主题轮转及草稿 IndexedDB 往返）。macOS 本机已完成 VoiceOver / 原生 NSAccessibility 树全旅程实测（含主界面与设置页 6 大分区 AX 节点导出、已配模型对话态 9 节点连续 Tab 闭环、三大弹层 `⌘+K` / `⌘+/` / `⌘+,` 的 `Esc` 焦点归位、修饰键正交无冲突与 `zh-CN Tingting` 中文语音合成 13/13 分词播报；实测检出的 3 项本仓应用层焦点流转问题——`OnboardingPane` 芯片组 Tab 跳出、侧栏 `DockPanel` 底栏先于列表的 Tab 顺序、设置页覆盖层 Tab 穿底与背景隔离——已全部修复并补齐红绿回归测试，同时如实记录了上游 `Avalonia.Native` 子角色 `AXUnknown` 与未暴露 `AXPress` 2 项底座边界并已向上游提报 [AvaloniaUI/Avalonia#22614](https://github.com/AvaloniaUI/Avalonia/issues/22614)，见 `docs/ux/a11y-audit.md` §10）；仍**未**证明：Linux Orca 与物理移动设备（iOS / Android TalkBack）真机实测（宿主无对应物理设备，见 `docs/ux/a11y-audit.md` §1、§9、§10）。

# 适用范围

这些规则约束的是独立职责，不要求每个函数、内部类、目录都拆成库，也不要求每个方法调用都经过 C。纯计算可以使用函数，内部实现可以直接组合；对象是运行时职责和状态的承担者，库是交付与验证边界，不等于一个进程、一个线程或全局单例。

标准库、工具链和确有必要的基础依赖不必绕一层才能使用，但须如实声明。由 C 组合 A、B，以及增强者依赖其基础实现，是有意的外向内依赖，不与“独立同级职责不相互调度”矛盾。不能把未拆开的同级职责改称基础设施来回避验证，也不能以扁平为由抹掉真实的组合语义。

接口、泛型、函数、依赖注入和继承都是工具：不因用了就算解耦，也不因不用就算解耦。采用受 Smalltalk / Actor 启发的对象自治，并不要求消息异步、邮箱、队列或通用调度框架；本模板的 C 拓扑不是对这些模型的历史定义。

具体项目只补本项目特有的需要、协议、工具和证据。测试与构建采用能完成验证的最简单手段，不为执行本模板另建通用治理平台。规则与实现共同演进；局部条款与根规则冲突时明确修订，不能靠复制或默默覆盖留下两套要求。

# 例外清单

接入项目时盘点。每项记录：事项 / 使用方 / 保留理由 / 可验证的解除条件。完成盘点且无例外时写“无”；未盘点不等于没有例外。长期设计选择写入对应规则，不永久寄存在临时清单。

**本项目盘点（2026-10-13，2026-10-26 按当前树复核）：**

| 事项 | 使用方 | 保留理由 | 可验证的解除条件 |
| --- | --- | --- | --- |
| `.fsproj` 用 `VersionOverride="0.0.0-local"` 绕过中央包版本；包版本固定为 `0.0.0-local` | 107 个工程：`compose/*` 93 个、`libs/` 12 个（`ActionStrip`、`AttachmentTray`、`BatchBar`、`ChatEmpty`、`ChatHeader`、`ConfirmPane`、`CopyButton`、`DevicesPane`、`ToastStrip`、`WorkspaceState`，以及 `DevicesPane`/`ToastStrip` 各自的 `tests` 工程）、`app/` 两个入口 | 库没有发布版本号，只在本地 feed 里存在 | 库开始按版本号发布，版本登记进 `Directory.Packages.props` |
| WorkspaceState 单一状态树需要多兄弟包依赖（现值 11 个：`BoundedWait`、`ConnectionIssue`、`ConversationSummary`、`DevicesModel`、`DraftStore`、`ForkLineage`、`HostCatalog`、`MessageView`、`NavigationState`、`UiPrefs`、`Wire`） | 21 个生产工程：`compose/AppShell`、`ApprovalDesk`、`ChatPane`、`ChatScreen`、`ComposerDesk`、`EntryShells`、`Gallery`、`MessageCardView`、`OverlayDesk`、`SettingsScreen`、`SidebarDesk`、`Workspace`，以及 `libs/ActionStrip`、`AttachmentTray`、`BatchBar`、`ChatEmpty`、`ChatHeader`、`ConfirmPane`、`CopyButton`、`DevicesPane`、`ToastStrip` | WorkspaceState 是单一状态树，投影直接携带领域事实类型（会话摘要行、设备信息、读取等待、看图判定），不再复制字段 | 事实类型下沉 WorkspaceState 或投影改传原始 Wire 类型后收窄；判定口径：`grep -oE 'Wanxiang2\.[A-Za-z0-9]+' libs/WorkspaceState/WorkspaceState.fsproj` 只剩自身 |
| `libs/FocusOrigin` 的 `OverlayLayer` 自研图层调度（Canvas/Margin 落点钳位、遮罩栈、滚轮隔离、Esc 级联退栈、跨层焦点恢复），与该库其余附加行为一并交付 | `compose/OverlayDesk`、`compose/Gallery`（生产消费者）；经 OverlayDesk 间接使用的 `compose/ModelSwitchDesk`、`DialogsDesk`、`TransferDesk`、`SettingsScreen`、`ApprovalDesk`、`PaletteDesk`、`ChatScreen`、`SidebarDesk`、`Workspace`、`AppShell` 与 `app/` 两个入口 | 实测（2026-10-20 / 2026-10-24）Avalonia 12.1.2 原生 Popup/FlyoutBase 无同等语义：LightDismiss 不拦 PointerWheelChanged、Popup 不消费 Esc、编程式打开的 ContextMenu.Close() 在 headless 下不翻转 IsOpen、无法与 dialogGuard 协同、无法注入动态 ViewportInset、浮层销毁后焦点栈溃散（证据：`libs/FocusOrigin/AGENTS.md`、`libs/FocusOrigin/tests/OverlayLayer.Tests.fs` 7 项与 `compose/AppShell/tests/ShortcutInterceptTests.fs` 中 3 条原生/自研浮层拦截用例的历史通过记录；该文件另有 1 条对话框用例，本轮未重跑这些行为测试） | Avalonia 提供可自定义 positioner+约束回调、可拦截滚轮的 light-dismiss 覆盖层、以及嵌套模态焦点归还契约后改写；判定口径：`compose/OverlayDesk` 不再 `open Wanxiang2.OverlayLayer` |

已解除项的历史记录见 `docs/archive/handoffs/2026-10-20-exceptions-released.md`（只读档案，不再是规则本体）。

# 本项目：wanxiang2

以下只写本项目特有的需要、协议、工具和证据；通用规则以上文为准。

## 需要

1. **逐个抽干旧仓库。** [AXIOM: 1, 4; LEMMA: 6]
    旧仓库 https://github.com/PamelaSprin47685ghall/wanxiang（F#）难维护、修 Bug 慢；整体重写工作量大且没有保证。当时的做法是每次从旧仓库抽出一项独立职责，做成本仓库的库，接入旧仓库宿主壳并让旧实现退出，再抽下一项。每次抽完都能验证，也都能停下。新旧不必一一对应，规格从 AXIOM 重新推，不沿用旧格式或旧决策。本方法已于 2026-10-17 收束：宿主职责连同程序入口全部外包给本仓库——`app/Desktop` 与 `app/Pwa` 双入口、自有构建/测试/发布闭环（`x`、`run-desktop.sh`、`run-pwa.sh`、`publish.sh`、自有 CI），本仓库为唯一产品本体入口。旧仓库已于同日物理废弃（提交 16904f0 删除 `src/`、`tests/`、`tools/`、CI 与品牌资产，README 与 STATUS 改写为终态退役声明，子模块挂载解除）；其产品文档与迁移史已迁入本仓库 `docs/`（活文档 `contracts/`、`security/`、`ux/`、`adr/`，历史档案 `docs/archive/`）。
2. **只许旧依赖新。** [LEMMA: 5]
    本仓库不引用旧仓库的任何源码、包或运行时服务。
3. **语言。** [AXIOM: 5]
    交流、注释与规则文档用中文，其余用英语。
4. **界面设计方向。** [AXIOM: 1, 5; LEMMA: 10]
 主仓 `5103af9` 提出以 `react-ui` 设计稿所表达的 UX 为目标，采用 Avalonia 原生实现，不把 React 原型搬入运行时。后续 `06fe53a`、`8007cd3`、`31f1ce5` 是已实施对齐的记录；不得回退这些改动，也不得仅凭提交记录或像素基线通过宣称全部视觉目标已验收。未取得的视觉与交互验收证据保持待验证。
5. **现有代码都是草稿。** [LEMMA: 1, 8]
    本仓库的实现可以大改，不为保留现有接口而增加兼容层。

## 布局与工具

- `libs/<Name>/`：一项独立职责，一个库：`<Name>.fsproj`、`tests/`、`AGENTS.md`。库之间没有任何依赖边（不只是无环），测试只引用本库。[LEMMA: 4, 5, 8; SPEC: 2]
- `compose/<Name>/`：组合者 C，只经交付包使用库，不引用库源码。[LEMMA: 4; SPEC: 2, 4]
- `./x test <lib>`：只凭本库与声明的外部包构建并测试。`./x check`：库项目不引用本库目录以外的东西，除《例外清单》登记的 `WorkspaceState` 特例外不允许任何兄弟包；组合者项目不引用自己目录以外的东西；组合者目录必须有 `AGENTS.md`；每个库必须有生产消费者（否则要在库目录放 `UNWIRED.md` 登记理由与解除条件）；`wanxiang2.slnx` 必须与 `./x sln` 的生成一致；文档与各 AGENTS.md 里点名的 `libs/<名>` / `compose/<名>` 必须存在（`docs/archive` 除外）。`./x pack`：按依赖顺序打出所有交付包（库与组合者都是交付物）。`./x compose`：清掉包缓存里的 `Wanxiang2.*` 后，从 `artifacts/feed` 消费交付包，测试组合者。`./x consume`：在仓库外的临时项目里只从 `artifacts/feed` 取包，构建并运行。`./x app`：从交付包构建 `app/` 下的两个入口（PWA 需 wasm-tools workload）。`./x all` 依次执行 check、hygiene、lint、pack、逐库 test、compose、consume（pack 在逐库 test 之前：少数库引用兄弟包，测试要从 `artifacts/feed` 取）；逐库 test 与组合者测试并发跑（`XJOBS` 可覆盖并发数；默认由核数与**内存**共同决定——每个并发工程按 2 GiB 计并给系统留 4 GiB，再按核数封顶 12 路，只按核数开满会 OOM）。CI（`.github/workflows/ci.yml`）先 pack 出 feed 工件，库测试每个库一个任务（同样消费该工件），另有组合、仓库外消费（consume）、入口构建（app）与发布产物冒烟（published-smoke：`publish.sh linux-x64` + `npm ci` + `tools/smoke_published.sh`，headless Chrome 真跑画布/DPR/上屏桥）四个任务消费该工件；发布流程（`release-publish.yml`）在 linux-x64 上传前跑同一冒烟。
- **F# 格式规则**：Fantomas，规则写在仓库根 `.editorconfig`（缩进 4、行宽 120、LF、文件末换行、多行括号 `aligned`）；工具版本钉在 `.config/dotnet-tools.json`（`fantomas 8.0.10`，`rollForward: false`），`.fantomasignore` 排除 `bin/` `obj/` `artifacts/` `node_modules/`。**格式检查只跑目标文件，禁止全仓**（所有者规则 2026-10-22）：`dotnet fantomas --check .` 与 `dotnet fantomas .` 一律禁止。改完 F# 跑 `./x format <文件…>` 重排——不带参数时重排的是**本分支改动的 `.fs`**（相对 `origin/main` 的合并基点，外加暂存区/工作区/未跟踪的 `.fs`），不是全仓。`./x check` 里的格式门禁同样只跑目标文件（显式列表 `./x format-check <文件…>`，或没给列表时的改动集合），未格式化即红（退出码 1，逐个列出 `! <路径> needs formatting.`）；改动集合为空就打印「无文件可查」并通过。`.fantomasignore` 里的路径不进目标集合。基准提交可由 `WX_FORMAT_BASE` 指定（CI 用 PR base / push 的上一个提交），否则取 `origin/main` 的合并基点，再退 `HEAD~1`（detached HEAD 或缺 `origin/main` 时）。提交前必须 `./x check` 绿。
- `./x check` 检查的是项目文件里的引用，不检查运行时调用；运行时独立靠每个库的测试不带任何兄弟库仍能通过（构建上就拿不到兄弟库）。

## 界面与前端组装（UI）

迁移期的「逐字节复刻旧仓库金样」已完成历史使命并退役。自本规则生效起，前端代码以「不造轮子、单一状态投影、真实操作闭环」为准绳，按 Via Negativa 持续做减法。[AXIOM: 1, 2, 4; LEMMA: 1, 8, 10, 11, 12]

1. **只用 Avalonia 原生控件，废除 `Border` 仿制件。** [AXIOM: 2, 5; LEMMA: 10]
   按钮用 `Button`，开关用 `ToggleSwitch` / `CheckBox`，下拉选择用 `ComboBox`，菜单与气泡用 `ContextMenu` / `MenuFlyout` / `Flyout`，折叠用 `Expander`，长列表用 `ItemsControl` 或 `ListBox` 配合原生 `VirtualizingStackPanel`。
   不再使用 `Tap`、`Spot`、`InvokeBorder` 等以 `Border` 冒充交互控件的做法（`libs/ExportSheet` 内部保留的 `internal Tap` 已改为 `inherit Button()`，属原生 `Button` 封装而非 `Border` 仿制件），也不再手写 `AutomationPeer`、手算浮层坐标（`PopupPlacement` 已退役，ToastQueue/ToastDwell 迁入 OverlayDesk，原生定位接管）或手搓列表挂载窗口（`MountWindow` 已解除并物理删除，ChatScreen 直呈完整卡片列表；CardReconcile 对账器物理删除，内部缓存直投影）。全仓 Border 仿制件清零、手写 AutomationPeer 全删。

2. **样式与动效归 Avalonia 资源与样式系统，废除 `*Look` 记录与帧定时器。** [AXIOM: 2; LEMMA: 10]
   颜色、字号、间距、圆角、过渡时长统一登记在主题层的 `ResourceDictionary`（含 Light / Dark `ThemeVariant`）与 `ControlTheme` / `Styles` 中。控件通过 `DynamicResource` 与样式类（`Classes`）获取外观。
   新建或改写的界面组件不再声明 `<Name>Look` 记录，不再用 `Follow.live` 逐属性订阅 `IObservable<float>`。呼吸、旋转、颜色过渡与平滑滚动优先使用 Avalonia 声明式 `Transitions` 与 `Animation`，不在 F# 代码里开 `DispatcherTimer` 逐帧计算。

3. **单入口幂等渲染，单出口意图事件。** [LEMMA: 3, 4, 12]
   视图组件只负责呈现与采集用户意图，不持有业务状态副本。
   - 输入（渲染）：每个视图组件对外只暴露一个幂等的 `Render(viewState)` 方法（或纯声明式视图函数），一次性接收该组件所需的全部展示数据；废除多入口、有调用顺序依赖的 `SetTitle` / `SetGenerating` / `SetEnabled` / `SetCompact` 零散方法。插槽/命令命名 Wire/Slot/Apply 前缀并在各库登记。
   - 输出（意图）：用户操作通过明确的消息类型（`Msg` 分支或事件）向上汇报，组件内部不自行决定跨组件副作用。

4. **工作区状态收敛与统一副作用执行。** [LEMMA: 3, 4, 12; SPEC: 3, 4, 7]
   客户端工作区的连接阶段、引导流程、当前对话、流式生成、待删批次与浮层状态，由组合者以单一互斥的状态树建模（让非法状态在类型上不可表示），不再拆成多个布尔入参的判断库（如 `StartReady`、`IdleSurface`、`ConversationSurface`）。
   焦点转移、滚动定位、网络发送与计时器启停，作为状态演进产出的显式动作（`Effect`），在每次视图树 `Render` 完成后由宿主统一执行一次；禁止在子控件内部散落调用 `Dispatcher.UIThread.Post` 争抢焦点或滚动偏移。

5. **控件库交付约定（沿用）。**
   - 生成：`./x new-widget <Name>` 由 `tools/widget-template` 生成 `libs/<Name>`（库 + Avalonia headless 行为测试），目录已存在就拒绝。测试不用 `Avalonia.Headless.XUnit`（.NET 10 上会死锁），直接用 `HeadlessUnitTestSession`。
   - 什么算「控件库」：`libs/<Name>/` 的生产源码（不含 `tests/`、`obj/`、`bin/`）里**构造了至少一个 Avalonia 控件类型**的库，即为控件库。只做行为附加或装饰、自己不构造控件的库（`CopyButton`、`ImeShield`、`InputSense`）不算。每个控件库在 `compose/Gallery` 至少有一条条目。
     判据由机器执行：Avalonia 控件类型清单是外部 API 的快照（`tools/avalonia-controls.txt`，写明再生成方法），`./x check` 由此判定控件库名单，再核对 `compose/Gallery/Gallery.fs` 里每个条目声明的 `Widget`；缺条目即报 `FAIL <库名>: control library has no gallery entry`。判定与覆盖都不设人工名单。
   - 行为控件（Menu、Overlay 等）由组合者经插槽和事件接线，控件库之间从不直接依赖；基础库 `UiKit` 已随全仓原生化退役删除（2026-10-20）；控件库现在零跨库依赖，唯一例外是单一状态树 `WorkspaceState`（见《布局与工具》与《例外清单》）。
   - 模块名：新模块与类型名不得与旧仓库宿主模块同名（F# 会合并同名模块）。
   - 浏览器端：控件库包可被 `net10.0-browser`（Avalonia.Browser）项目引用。

## 证据现状（对应上文 SPEC）

- SPEC 1 规则可追溯：每个库的 AGENTS.md 逐条写明前提与测试名。机器检查覆盖了一半：`./x check` 拒绝指向不存在组件的文档引用（`libs/<名>` / `compose/<名>`，`docs/archive` 除外），也强制每个库有消费者与登记例外。[TODO] 规则编号（`[LEMMA: n]`）与测试名的对应仍靠评审——今天整理掉的过期接线声明正是这类评审发现的。
- SPEC 2 职责与交付独立：构建独立、协作独立由 `./x check` 与 `./x test <lib>` 给出，每个库都通过；交付独立由 `./x compose` 给出（干净包缓存、只用 `artifacts/feed` 中的包），通过；仓库外消费已由 `./x consume` 自动化（`mktemp -d` 下的项目只从 `artifacts/feed` 取 `Wanxiang2.LedgerStore`，提交后重开得到 `Used 1UL`；消费者源码在 `tools/consume/`，CI 的 consume 任务运行它），2026-10-17 通过。
- SPEC 3 状态确有主人：Ledger 与 compose/LedgerStore 测了输入、返回值别名、错误后的状态、实例隔离（见各自 AGENTS.md）。JsonSeqFile、SnapshotFile、DirLock 的状态都在文件里，也有实例隔离、非法参数与错误之后的测试（见各自 AGENTS.md）。
- SPEC 4 协作由真实组合证明：`compose/LedgerStore/tests` 覆盖成功、类型化拒绝、重复、游标分页、在每个字节处崩溃、半写尾与修复、解码失败、过期快照、快照策略与快照写失败、真实未知结果（`/dev/full`）、并发、同目录互斥。「写失败但确知未写」也有真实故障（测试临时收紧 RLIMIT_FSIZE，EFBIG 后截回成功），见 `libs/JsonSeq/AGENTS.md`。
- SPEC 5 增强经得起拆除：目前没有增强层，不适用。
- SPEC 6 每步形成真实闭环：见 `docs/archive/handoffs/2026-10-24-delivery-verification.md` 与 `docs/archive/handoffs/2026-10-17-migration-and-extraction.md` 的历史账目；当期闭环证据写在各库 / 组合者的 `AGENTS.md`。
- SPEC 7 界面验证的机器门禁（2026-10-21 新增；2026-10-23 扩展）：`./x hygiene`（断言卫生——恒真断言 / 恒真谓词 / 空转的事实一律红灯，脚本 `tools/assert-hygiene.sh`，覆盖 `libs/*/tests` 与 `compose/*/tests`）；`./x lint`（界面文案——禁用同义词 / 取消类提示须写明 Esc / 中文省略号 / PWA 壳术语，脚本 `tools/lint-copy.sh`，覆盖 `libs` + `compose` + `app` 的生产 `.fs` 与 PWA 壳交付物）；`./x check` 内的 Fantomas 格式门禁（`./x format-check`，只查目标文件）与控件库画廊覆盖核对（`gallery_coverage`）；`./x gallery-baseline`（画廊深浅双主题像素基线核查，G3，脚本 `tools/gallery-baseline.sh`，缺基线或像素差异即红，见 `compose/Gallery/AGENTS.md`）。`hygiene` 与 `lint` 只需源码、秒级，已接入 `./x all` 与 CI；`gallery-baseline` 单独作为 CI 作业在 `pack` 之后消费 feed。
- SPEC 7 界面验证的人工工具（2026-10-21；2026-10-24 更新）：组件画廊 `compose/Gallery` + `tools/gallery.sh`（41 条条目 / `--window` 真实桌面窗口 / `--render <条目名> <输出.png> [--theme light|dark]` 出 720×900 的 PNG；用隔离的包缓存 `artifacts/gallery-cache`）。状态由 `Workspace.init ()` + `Workspace.update` 的消息序列跑出、经 `Workspace.project` 投影，不手搓视图状态记录（对话框一条走真实控件入口，因为对话框的「打开」状态是桌内分工、不在状态树里；错误卡走卡片组的真实入口。各条的归属见 `compose/Gallery/AGENTS.md`《已知缺口与各自的归属》）；状态用固定时钟（`Scenes.fixedNow`），同一条目连渲两次 PNG 逐字节相同。像素基线门禁（G3，`tools/gallery-baseline.sh` + `compose/Gallery/Baseline.fs`，全部 41/41 条目 × 深浅两态 = 82 张基线，缺基线或像素差异即红）与画廊控件树无障碍审查（`--a11y-audit`，`compose/Gallery/A11yAudit.fs`；AppShell 各屏幕的整树遍历在 `compose/AppShell/tests/A11yTraversalTests.fs`）已并入门禁面；浏览器侧真实交互 e2e 已由 `tools/smoke_published.sh` 覆盖；macOS 本机已完成 VoiceOver / 原生 NSAccessibility 全旅程实测（`docs/ux/a11y-audit.md` §10），仅余 Linux Orca 与物理移动设备（iOS / Android TalkBack）因环境限制未测（`docs/ux/a11y-audit.md` §1、§9）。
- SPEC 7 界面改动的交付契约（2026-10-21 新增）：`docs/frontend-dev-process.md` §5 是一份由**改界面的工程师自己**走完并留证的清单——需求/用户旅程 → 状态与交互设计 → 实现 → 真实界面截图 + 键盘 + 无障碍行为 → 针对性测试 → 包与干净消费 → 两个入口 → 已发布产物冒烟 → 证据与已知边界（职责分离：Engineer 负责实现与回归用例，另一个 DevOps 执行体负责运行构建、测试、冒烟并留存原始输出；交付者核对证据后收口。写入修改与执行验证必须由不同执行体承担，未执行的验证不得声称通过）。
- SPEC 7 PWA 中文字体（2026-10-21 修复并回填）：浏览器端没有系统字体，此前没在根上设字体，中文全是豆腐块。根因不是「headless Chrome / SwiftShader 的 WebGL 字体限制」，而是**可视树根上没指定字体族**。修法是在 PWA 入口的根 `Grid` 上设继承型附加属性 `TextElement.FontFamilyProperty`（`app/Pwa/Program.fs` 的 `buildViewsOnce`），整棵树继承内嵌的 Sarasa；桌面入口 `app/Desktop/Program.fs` 不动（用系统字体）。弹出层（ToolTip / ContextMenu / MenuItem / MenuFlyoutPresenter）挂在独立弹出根上、继承不到根字体，另在 `compose/EntryShells/App.fs` 显式指定同一字体族。回归用例是 `compose/EntryShells/tests/Tests.fs` 的 `EmbeddedFontTests`（内嵌正文字体族能解析出字形 / 覆盖常用汉字 / 等宽字体把正文字体列为回落），字体资源为 `libs/ThemeFonts/Assets/Fonts/SarasaGothicSC-Regular.subset.ttf`。

## 历史账目（只读指针）

以下内容是过去某一时点的执行记录，不是现行规则。查阅请到对应档案，不要据它们判断当前规模：

- 抽取路线、迁移状态表、包合并账目、收束后整理：`docs/archive/handoffs/2026-10-17-migration-and-extraction.md`、`docs/archive/handoffs/2026-10-18-cleanup-and-merge.md`。
- W7 收口与 2026-10-24 交付验证收口证据：`docs/archive/handoffs/2026-10-24-delivery-verification.md`。
- 《例外清单》已解除项：`docs/archive/handoffs/2026-10-20-exceptions-released.md`。

当前规模以 `./x check` 的输出为准，不以上述档案里的数字为准。
