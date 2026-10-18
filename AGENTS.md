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

# 适用范围

这些规则约束的是独立职责，不要求每个函数、内部类、目录都拆成库，也不要求每个方法调用都经过 C。纯计算可以使用函数，内部实现可以直接组合；对象是运行时职责和状态的承担者，库是交付与验证边界，不等于一个进程、一个线程或全局单例。

标准库、工具链和确有必要的基础依赖不必绕一层才能使用，但须如实声明。由 C 组合 A、B，以及增强者依赖其基础实现，是有意的外向内依赖，不与“独立同级职责不相互调度”矛盾。不能把未拆开的同级职责改称基础设施来回避验证，也不能以扁平为由抹掉真实的组合语义。

接口、泛型、函数、依赖注入和继承都是工具：不因用了就算解耦，也不因不用就算解耦。采用受 Smalltalk / Actor 启发的对象自治，并不要求消息异步、邮箱、队列或通用调度框架；本模板的 C 拓扑不是对这些模型的历史定义。

具体项目只补本项目特有的需要、协议、工具和证据。测试与构建采用能完成验证的最简单手段，不为执行本模板另建通用治理平台。规则与实现共同演进；局部条款与根规则冲突时明确修订，不能靠复制或默默覆盖留下两套要求。

# 例外清单

[TODO] 接入项目时盘点。每项记录：事项 / 使用方 / 保留理由 / 可验证的解除条件。完成盘点且无例外时写“无”；未盘点不等于没有例外。长期设计选择写入对应规则，不永久寄存在临时清单。

**本项目盘点（2026-10-13）：**

| 事项 | 使用方 | 保留理由 | 可验证的解除条件 |
| --- | --- | --- | --- |
| `.fsproj` 用 `VersionOverride="0.0.0-local"` 绕过中央包版本；包版本固定为 `0.0.0-local` | `compose/*`（消费库与组合者包）；`libs/*` 对 `UiKit` 的引用；`./x compose`、`./x test`、`./x app`；旧仓库 `tools/wanxiang2-feed.sh` | 库没有发布版本号，只在本地 feed 里存在 | 库开始按版本号发布，版本登记进 `Directory.Packages.props` |

已解除：2026-10-13 删去「`compose/LedgerStore` 与旧仓库 `Store.fs` 双重实现」一项。旧仓库分支 `extract-ledger@8f39e84` 改为消费 `Wanxiang2.LedgerStore` 包，`Store.fs` 只剩领域接线。


# 落地起点

从一个真实问题开始：补明人在乎的结果，选出最可能推翻设计的边界，按 LEMMA 2 准备下一小步。由具体场景落实最简独立职责及真实组合，再逐个加强。证明填入相应 SPEC，尚未成立的保留 `[TODO]`；不必先设计全系统，也不必先填完这份模板才动手。

# 本项目：wanxiang2

以下只写本项目特有的需要、协议、工具和证据；通用规则以上文为准。

## 需要

1. **逐个抽干旧仓库。** [AXIOM: 1, 4; LEMMA: 6]
    旧仓库 https://github.com/PamelaSprin47685ghall/wanxiang（F#）难维护、修 Bug 慢；整体重写工作量大且没有保证。当时的做法是每次从旧仓库抽出一项独立职责，做成本仓库的库，接入旧仓库宿主壳并让旧实现退出，再抽下一项。每次抽完都能验证，也都能停下。新旧不必一一对应，规格从 AXIOM 重新推，不沿用旧格式或旧决策。本方法已于 2026-10-17 收束：宿主职责连同程序入口全部外包给本仓库——`app/Desktop` 与 `app/Pwa` 双入口、自有构建/测试/发布闭环（`x`、`run-desktop.sh`、`run-pwa.sh`、`publish.sh`、自有 CI），本仓库为唯一产品本体入口。旧仓库已于同日物理废弃（提交 16904f0 删除 `src/`、`tests/`、`tools/`、CI 与品牌资产，README 与 STATUS 改写为终态退役声明，子模块挂载解除）；其产品文档与迁移史已迁入本仓库 `docs/`（活文档 `contracts/`、`security/`、`ux/`、`adr/`，历史档案 `docs/archive/`）。
2. **只许旧依赖新。** [LEMMA: 5]
    本仓库不引用旧仓库的任何源码、包或运行时服务。
3. **语言。** [AXIOM: 5]
    交流、注释与规则文档用中文，其余用英语。
4. **现有代码都是草稿。** [LEMMA: 1, 8]
    本仓库的实现可以大改，不为保留现有接口而增加兼容层。

## 布局与工具

- `libs/<Name>/`：一项独立职责，一个库：`<Name>.fsproj`、`tests/`、`AGENTS.md`。库之间没有任何依赖边（不只是无环），测试只引用本库；唯一例外是基础库 `UiKit`（2026-10-18 起）：控件库可以声明依赖它，`./x check` 只放行这一个包名，其余兄弟包一律拒绝。[LEMMA: 4, 5, 8; SPEC: 2]
- `compose/<Name>/`：组合者 C，只经交付包使用库，不引用库源码。[LEMMA: 4; SPEC: 2, 4]
- `./x test <lib>`：只凭本库与声明的外部包构建并测试（控件库的 `UiKit` 包若不在 feed 里会先自动 pack 一次）。`./x check`：库项目不引用本库目录以外的东西，只允许 `UiKit` 这一个兄弟包；组合者项目不引用自己目录以外的东西；组合者目录必须有 `AGENTS.md`；每个库必须有生产消费者（否则要在库目录放 `UNWIRED.md` 登记理由与解除条件）；`wanxiang2.slnx` 必须与 `./x sln` 的生成一致。`./x pack`：按依赖顺序打出所有交付包（库与组合者都是交付物）。`./x compose`：清掉包缓存里的 `Wanxiang2.*` 后，从 `artifacts/feed` 消费交付包，测试组合者。`./x consume`：在仓库外的临时项目里只从 `artifacts/feed` 取包，构建并运行。`./x app`：从交付包构建 `app/` 下的两个入口（PWA 需 wasm-tools workload）。`./x all` 依次执行 check、逐库 test、pack、compose、consume；逐库 test 与组合者测试并发跑（`XJOBS` 可覆盖并发数，默认 min(8, 核数)）。CI（`.github/workflows/ci.yml`）先 pack 出 feed 工件，库测试每个库一个任务（同样消费该工件），另有组合、仓库外消费（consume）与入口构建（app）任务消费该工件。
- `./x check` 检查的是项目文件里的引用，不检查运行时调用；运行时独立靠每个库的测试不带任何兄弟库仍能通过（构建上就拿不到兄弟库）。

## 界面控件库（widget lib）

库可以依赖界面框架（Avalonia、IconPacks.Avalonia.Lucide 等，版本登记在 `Directory.Packages.props`，与旧仓库一致）。控件库连控件、行为带状态一起拥有；跨库依赖只有下边那条基础库 `UiKit`，与其余兄弟库没有任何依赖边。[LEMMA: 3, 4, 5, 8]

- **基础库 `UiKit`（跨库依赖的唯一出口）：** 控件库共用的原语都在这里——`Follow`（挂上可视树才订阅、摘下退订的活值跟随）、`Tap` / `Spot` / `InvokeBorder`（可点 Border 与读屏 Invoke）、`Tone` / `TagTone`、`KitLook` + `Kit`（旧 `Controls/Primitives.fs` 的字段组、输入壳、开关、设置行、对话框页脚）。控件库自己的 `Kit.fs` 只剩一张「本库 Look → `KitLook`」的映射。本库不依赖任何 `Wanxiang2.*` 包，也不放领域规则与控件行为（那些留在各控件库）；两件事都由 `./x check` 盯住。2026-10-18 之前，这些原语在 36 个控件库各复制了一份（约 1.1 万行），见「收束后整理」。

- **生成：** `./x new-widget <Name>` 由 `tools/widget-template` 生成 `libs/<Name>`（库 + Avalonia headless 行为测试），目录已存在就拒绝。测试不用 `Avalonia.Headless.XUnit`（.NET 10 上会死锁），直接用 `HeadlessUnitTestSession`，与旧仓库相同。CI 的 `widget-template` 任务每次生成一个控件库并测试，还验证 `./x check` 拒绝库对库引用。
- **样式值都是可变的，不写死。** 控件库不含任何字面样式值（颜色、画笔、尺寸、字体、间距、圆角、时长）。这些值由主题管理者（`compose/Theme` 或一个主题管理库）拥有，是活的可变实例；管理者改值，所有控件跟着变。
- **Look 记录：** 每个控件库声明自己的 `<Name>Look` 记录，列出它需要的外观项（画笔为 `IBrush`，尺寸、字体、间距为管理者给的活实例，如 `IObservable<_>`）。组合者在构造控件时从主题管理者取出共享实例填进去；同一画笔实例由多个控件共享。控件不认识主题管理者。
- **共享外观：** 按钮、卡片等共同外观用类选择器样式（如 `Border.button-primary`），由主题管理者（`compose/Theme` 的 `ThemeStyles`）只安装一次；Setter 里放管理者的活实例：画笔直接用活画笔，尺寸、间距、焦点环颜色经 `ToBinding` 绑到活值。样式从不重建：实测清空再重填 `Styles` 不会让已显示的控件重新取值；也不用 `ControlTheme`：它只按控件类型或 `Theme` 属性生效，不能按类名套在 `Border` 上。控件只挂类名。每个库的 AGENTS.md 列出它用到的样式类。
- **行为控件（Menu、Overlay 等）：** 由组合者经插槽（传入的控件或工厂）和事件接线，控件库之间从不直接依赖。
- **模块名：** 新模块与类型名不得与旧仓库宿主模块同名（如 `Tokens`、`Menu`、`Dialogs`、`Icons`、`Interaction`、`MessageCard`、`MotionPolicy`、`Palette`、`ThemePreference` 等），因为 F# 会合并同名模块，见旧仓库的 ConversationTransfer 等先例。
- **浏览器端：** 控件库包可被 `net10.0-browser`（Avalonia.Browser）项目引用；已在旧仓库本地验证。

## 证据现状（对应上文 SPEC）

- SPEC 1 规则可追溯：每个库的 AGENTS.md 逐条写明前提与测试名。[TODO] 没有机器检查引用编号；靠评审。
- SPEC 2 职责与交付独立：构建独立、协作独立由 `./x check` 与 `./x test <lib>` 给出，每个库都通过；交付独立由 `./x compose` 给出（干净包缓存、只用 `artifacts/feed` 中的包），通过；仓库外消费已由 `./x consume` 自动化（`mktemp -d` 下的项目只从 `artifacts/feed` 取 `Wanxiang2.LedgerStore`，提交后重开得到 `Used 1UL`；消费者源码在 `tools/consume/`，CI 的 consume 任务运行它），2026-10-17 通过。
- SPEC 3 状态确有主人：Ledger 与 compose/LedgerStore 测了输入、返回值别名、错误后的状态、实例隔离（见各自 AGENTS.md）。JsonSeqFile、SnapshotFile、DirLock 的状态都在文件里，也有实例隔离、非法参数与错误之后的测试（见各自 AGENTS.md 的 SPEC 11 / SPEC 7 / SPEC 6）。
- SPEC 4 协作由真实组合证明：`compose/LedgerStore/tests` 覆盖成功、类型化拒绝、重复、游标分页、在每个字节处崩溃、半写尾与修复、解码失败、过期快照、快照策略与快照写失败、真实未知结果（`/dev/full`）、并发、同目录互斥。「写失败但确知未写」也有真实故障（测试临时收紧 RLIMIT_FSIZE，EFBIG 后截回成功），见 JsonSeq SPEC 8 与 compose/LedgerStore SPEC 15。
- SPEC 5 增强经得起拆除：目前没有增强层，不适用。
- SPEC 6 每步形成真实闭环：见下表。

## 收束后整理（2026-10-18，抽取路线收束之后的第一次全仓整理）

1. **孤儿库退役（8 个）。** 全仓扫出 8 个零消费者的库：`CopyConfirm`、`RovingIndex`、`SidebarRowState`、`StartLayout`、`SendKey`、`UiPrimitives`、`ProbeCounter`、`BrowserProbe`。前五个是早期迁出的纯逻辑库，其规则在控件库重写时被内联进真正的行为拥有者（`CopyButton`、`PopupMenu`/`SettingsFrame`、`ConversationRows`、`ShellLayout`、`ComposerBox`），原库成了同一事实的第二份；后三个分别是被控件库取代的历史原语库与从未接线的探针链（`?wxprobe=1` / `WANXIANG_PARITY_PROBE`，见 `docs/ux/r28-pwa-parity.md`）。处置：删除，不建兼容层；被删库的旧用例其等价断言在各行为拥有者的 headless 测试里（`SendKey.sends` 的 8 种组合并入 `ComposerBox` 的 Enter 矩阵；`StartLayout` 两条规则在 `ShellLayout` 测试；`SidebarRowState` 三条规则在 `ConversationRows` 测试）。
2. **`ComposerBox` 的发送判定修正。** 内联判定原先漏了 `not shift`：Enter 发送档里 Ctrl+Shift+Enter 会误发送；已改为与旧 `SendKey.sends` 相同的 `not shift && (enterSends <> ctrl)`，并由测试钉住两种模式的全部组合。
3. **`libs/UiKit` 建立，36 份复制件合并成 1 份。** 控件库各自的 `Kit.fs`（18 份 309 行全量版 + 18 份 62 行 Follow/Spot 版 + `SkeletonList` 的 38 行版）、`PopupMenu`/`ToastStrip` 的 `InvokeBorder`、三个对话框页的 `Footer`、`ApprovalPanel`/`ExportSheet`/`ImportSheet` 的 `Tap`，以及 `MarkdownView`、`MessageCardView`、`MathFigure` 各自的 `Follow` 副本，统一归 `libs/UiKit`。纯删除约 1.1 万行；`./x test` 对 37 个受影响库逐个通过。
4. **工具与 CI。** `./x check` 增加「无消费者库」检查与 `UNWIRED.md` 例外（登记理由与解除条件），模板 `tools/widget-template` 自带该文件；`./x pack` 改为库与组合者统一按依赖顺序打包（`UiKit` 自然排在被依赖方前）；`./x all` 的逐库测试与组合者测试并行（`XJOBS`）；CI 的库测试矩阵与 widget-template 任务改为消费 pack 出的 feed 工件。
5. **例外清单同步。** `VersionOverride="0.0.0-local"` 一行补上新的使用方（库对 `UiKit` 的包引用）。
6. **`ControlStroke`：输入类控件的边界描边过 WCAG 1.4.11。** `docs/ux/a11y-audit.md` §2 一直记着「以描边作唯一边界的输入框」不达标（`border` 浅 1.33~1.47、深 1.46~1.92）。实测确认边界只靠描边（`FieldSurface` 与 `Surface` 同值，没有填充差），于是 `libs/ThemePalette` 补 `ControlStroke`（浅 `#8D8A7E`、深 `#76767F`；对 Surface / SurfaceRaised / SurfaceContainer / Canvas 两套主题都 ≥ 3:1），22 处 `FieldEdge` 与 `ComposerBox` 的 `BoxInk.ControlStroke` 改用它，聚焦态仍是 `Accent`。机械断言 `compose/Theme/tests/A11yRegression.fs` 的 `控件边界描边在四种底上都过 1.4.11`；`ComposerDesk` / `Workspace` 整树金样里 7 行壳描边按新值改（有意偏离，已在这两个组合者的 AGENTS.md 记明）。`border` / `line` 数值不变，仍作装饰性分割。

7. **看过的、决定不动的：测试工程的 `Headless.fs` / `Probe.fs` / `Dump.fs` 副本**（libs 与 compose 的 tests 目录共约 74 / 18 / 7 份，逐字节相同的部分约 4–5 千行）保留按库复制，不进基础库。理由：它们是测试脚手架，各库测试自足（SPEC 2「只凭本库与声明依赖」）；副本漂移只影响本库且会立刻在本地测试里响，没有 `UiKit` 那种「36 份行为必须一致」的正确性压力；合并需要再开一类「测试支撑包」依赖并为此在 `./x check` 再开一个口子，代价大于收益（Axiom 2、LEMMA 8）。将来若真出现跨库同步的测试基建需求，再按 LEMMA 1 重新判断。

**证据（2026-10-18，本机）：** `XJOBS=10 ./x all` —— `check ok: 247 libs with no sibling references; 72 composer(s) using packages only`；逐库测试 247 个工程全部通过；`./x compose` 67 个工程全部通过；`./x consume` 通过（仓库外消费者提交后重开得到 `Used 1UL`）。`./x app` 两个入口构建成功。新检查的负例也跑过：`./x new-widget CiSmoke` 生成后 `./x check` 通过（模板自带 `UNWIRED.md`），删掉该文件即失败并给出指引，库引用兄弟包（`Wanxiang2.BatchBar`）被拒；`./x test BatchBar` 在 feed 里没有 UiKit 包时会先自动 pack。本轮删掉的 8 个库、`UiKit` 的 37 个受影响库、`ControlStroke` 的两个金样（`ComposerDesk`、`Workspace`）都在上述命令的覆盖里。

## 迁移状态

> 接线状态属 `assist/deletion-cutover` 分支：Release 构建与全量 571 项用例（UI 346 + 非 UI 225，570 通过、1 既有跳过、0 失败）已在该分支验证；逐行证据（[E1]–[E7]）见 `docs/archive/handoffs/2026-10-15-migration-table-audit.md`（随旧仓库退役从其 `docs/handoffs/` 迁入本仓库），数字基线以该审计为准。本表口径（2026-10-17）：48 行抽吸全部完成，表为迁移执行账目（历史），不再是进行中的工作定义；「已接线」列与文中「宿主」均指旧仓库 `src/` 宿主壳，它已随旧仓库物理废弃（提交 16904f0）；程序入口在本仓库 `app/Desktop` 与 `app/Pwa`，本仓库为唯一产品本体入口。

| 职责 | 独立能力 | 接入旧仓库宿主壳并退出旧实现 |
| --- | --- | --- |
| 事件账本（JsonSeq、Ledger、Snapshot、DirLock、Canonical，组合 LedgerStore） | 完成：5 个库和 `Wanxiang2.LedgerStore` 交付包各自测试通过，真实组合测试通过 | 已接线：旧仓库分支 `extract-ledger@8f39e84` 消费交付包，旧 Replay/Coordinator 已退出，旧仓库 Release 全量 1484 通过、0 失败、1 跳过。合入旧仓库 `main` 属过渡链遗留：旧仓库已物理废弃，不再适用 |
| 配置（AtomicTextFile、AppConfig，组合 ConfigStore） | 完成：两个库和 `Wanxiang2.ConfigStore` 交付包测试通过；旧 `ConfigTests.fs` 中 ConfigStore 和格式部分（34 个）已搬来并通过 | 已接线：`src/Wanxiang.Config/{ConfigModel,TomlCodec,ConfigStore}.fs` 已随 `Wanxiang.Config` 旧工程整体退出，宿主改引 `Wanxiang2.ConfigStore` 包，命名空间与 API 不变 |
| 认证与配对（ClientToken、DesktopClient、PairingCode、FailureLimiter、WsHandshake，组合 PairingDesk） | 完成：5 个库和 `Wanxiang2.PairingDesk` 交付包测试通过，令牌哈希有金标准向量 | 已接线：`src/Wanxiang.Config/Auth.fs` 与整个 `Wanxiang.Config` 旧工程均已退出，宿主经 `Wanxiang2.PairingDesk` 包消费认证与配对 |
| 领域状态与命令（Conversations、CommandId、PlainText、ToolSummary，组合 ChatDomain） | 完成：4 个库和 `Wanxiang2.ChatDomain` 交付包测试通过；提交行、快照正文、commandId 与旧实现生成的金样逐字节相同 | 已接线：`src/Wanxiang.Core` 旧工程已整体退出，宿主改引 `Wanxiang2.ChatDomain`、`PlainText`、`ToolSummary`；前置线协议接线（222 行）已完成 |
| 模型提供方传输（Sse、HttpRetry、JsonPick、Anthropic、Gemini，组合 ProviderTransports） | 完成：5 个库和 `Wanxiang2.ProviderTransports` 交付包测试通过；请求和解码结果与旧实现生成的金样逐字节相同 | 已接线：前置配置与线协议均已接；宿主引用 `Wanxiang2.ProviderTransports` 包 |
| MCP 客户端（JsonRpc、McpProtocol、LineProcess、StreamableHttp，组合 McpClient） | 完成：4 个库和 `Wanxiang2.McpClient` 交付包测试通过；发出的行、HTTP 请求和结果文本与旧实现生成的金样逐字节相同 | 已接线：`McpTransport.fs`、`McpClient.fs` 已随 Server/Agent 旧工程退出，宿主改引 `Wanxiang2.McpClient` 包 |
| 服务端编排的独立部分（Agui、Approvals，组合 AguiWire、ToolApproval、PartialReplies） | 完成：AG-UI 承载与工具审批的线上字节与旧实现生成的金样逐字节相同 | 已接线：Agui 旧工程与 `WireAgui.fs`、`ToolApproval.fs`、`PartialReplyStore.fs` 已退出，宿主经 `AguiWire` 与包图 `ToolApproval ← PartialReplies ← ChatOrchestrator` 消费 |
| 搜索、附件、主机日志（TextMatch、MessageText、MediaSniff、AttachmentBlobs、JsonLinesLog，组合 Search、HostLog、Attachments） | 完成：结果页、附件元数据、错误文字、stderr 行格式与旧实现生成的金样逐字节相同 | 已接线：`SearchService.fs`、`AttachmentStore.fs`、`Stderr.fs` 已随 Server 旧工程退出，宿主经 `WsConnection` 下游的 `Search`、`HostLog`、`Attachments` 包消费 |
| 存储与诊断（HostProbe、AttachmentRefs，组合 EventStore、Doctor） | 完成：旧存储测试原样通过；doctor 金样与旧实现逐字节相同 | 已接线：`src/Wanxiang.Store` 旧工程与 `Doctor.fs`、`SupportBundle.fs` 已退出，宿主改引 `Wanxiang2.EventStore`、`Doctor` 包；`ServerModel.attachmentRefsOf` 经包转发 `AttachmentRefs.ofPayload` |
| 消息与工具（MessageSerde、MediaContent、SandboxFiles、ToolNames、RefreshingPool，组合 AgentContent、BuiltinTools、ToolRegistry） | 完成：消息 JSON、附件内容、工具目录与调用结果与旧实现生成的金样逐字节相同 | 已接线：`Agent/MessageSerde.fs`、`AttachmentContent.fs`、`Server/BuiltinTools.fs`、`ToolRegistry.fs` 已随旧工程退出，宿主经 `AgentContent`、`BuiltinTools` 与包图 `ToolRegistry ← AgentRuntime` 消费 |
| Agent 运行时（HttpFailureFacts、SharedHttpClients、AgentHistory、AguiMessageMap，组合 ProviderFailure、AgentRuntime） | 完成：请求体、流式增量、错误归类与旧实现生成的金样逐字节相同 | 已接线：`Wanxiang.Agent` 旧工程整体退出（含 `HistoryProvider.fs`、`MessageMap.fs`、`AgentRuntime.fs` 与 8.3 两个文件），宿主经 `ProviderFailure`、`AgentRuntime` 包消费 |
| 聊天编排（ContextTrim、TitleText，组合 ChatOrchestrator） | 完成：真实组合金样（事件、记账、请求体）与旧实现逐字节相同；旧 ChatOrchestratorTests 等迁入通过 | 已接线：`GenerationContext.fs`、`TitleGenerator.fs`、`ChatOrchestrator.fs` 已随 Server 旧工程退出，宿主经 `ChatOrchestrator` 包（`ServerApp` 包图内）消费 |
| 服务端视图与配置（PayloadFields、ModelListing，组合 ServerModel、ProviderCatalog、ConfigMutation、HostStatus） | 完成：ServerModel/ConfigMutation/HostStatus 金样与旧实现逐字节相同；旧 ProviderCatalogTests 迁入通过 | 已接线：`ServerModel.fs`、`ProviderCatalog.fs`、`ConfigMutation.fs`、`HostStatus.fs` 已退出，四组合者均在 `WsConnection` 下游，宿主经包消费 |
| WebSocket 连接（DeviceSightings、DeviceRules，组合 WsConnection） | 完成：认证零写配置真实组合测试；DeviceRules 单测 | 已接线：`WsConnection.fs` 已随 Server 旧工程退出，宿主按 `ServerApp → WsConnection` 包组合；`DeviceSightings` 传入与 Seed 迁移经 `Wanxiang2.WsConnection` 落地 |
| 主机（ListenAddress，组合 ServerApp） | 完成：旧主机/连接/设备/诊断等 16 个测试文件 158 条迁入通过；hostVersion 由宿主注入 | 已接线：Server 项目剩余源文件已退出，`Wanxiang2.ServerApp` 为组合根，宿主改引该包 |
| 客户端连接（ReconnectBackoff、ConnectionIssue、TextSocket、CommitTimeline，组合 WsClient） | 完成：旧 ClientStateAndWsClientTests 41 条迁入组合者、6 条退避用例迁入 ReconnectBackoff，本机真实 WebSocket 测试通过 | 已接线：`src/Wanxiang.Client` 旧工程整体退出，宿主改引 `Wanxiang2.WsClient`、`ReconnectBackoff`、`ConnectionIssue` 包；UI 的 `ConnectionIssue` 分支已改限定名经包消费 |
| 程序入口的非界面逻辑（CliArgs、RunModes、PwaAssets、ShutdownSignals、ClientTomlFile，组合 LaunchPlan、StoreLaunch、DesktopPairing） | 完成：真实磁盘、真实 EventStore/ConfigStore 上的组合测试通过；日志与错误文字与旧入口逐字相同 | 已接线：旧 `Wanxiang.App/Program.fs` 的非界面逻辑已改调 `LaunchPlan`、`StoreLaunch`、`DesktopPairing` 等包（`Wanxiang.App.fsproj` 引用 25 个 `Wanxiang2.*` 包，为入口消费面），只留 Avalonia 启动、单实例与链接转发（UI）、信号注册 |
| 富文本渲染后端（库 HljsHtml、KatexHtml、HljsCatalog、BoundedCache、RichScripts、JintScriptHost；C# 库 BrowserCredentials、BrowserDrafts、BrowserPage、BrowserRich、BrowserImage、BrowserProbe、BrowserLocalStore；组合 RichAssets、RichJint、RichBrowser） | 完成：RichJint 输出与旧 RichJint.fs 生成的黄金文件逐字节相同（17 段代码、12 个公式）；JS 函数名与模块名与旧 BrowserBridge 一致 | 已接线：旧 UI 的 `Highlight`、`CredentialStore`、`DraftStore`、`ParityProbe`、`AppShell`、`Pwa/RichBrowser.fs`、`BrowserImageShrink.fs` 已改用 `RichAssets`、`RichJint`、`RichBrowser` 等包（`CredentialStore.fs:15` 已是 `Wanxiang2.BrowserCredentials.CredentialStore` 别名壳）；main.js 已取 `BrowserPage.dll`、App 已加 `IRichBackend` 适配，对应旧测试已删 |
| UI 视图层纯逻辑（组合 MathTypeset、DesignRhythm；库 MarkdownBlocks、LinkChunks、SpacingRamp、SearchSegments、ApprovalCardText、DevicesModel） | 完成：公式排版在回放旧字形度量时与旧 MathLayout 逐位相同（16 个公式）；Markdown 块、链接分段、间距与布局常量、搜索分段、审批文案、设备文案与二维码与旧代码生成的金样相同 | 已接线：旧 `MathLayout` 已改为 `MathTypesetter` 加 FormattedText 量尺；旧 `Markdown.fs`、`Spacing.fs`、`LayoutPolicy.fs`、`ReadingRhythm`、`SidebarText`、`DevicesModel` 已删除改用包；`ToolApprovalCard` 与 `MarkdownRenderer` 调用新函数；`InviteTarget` 调用点已用限定名（`Wanxiang.UI.fsproj` 约 170 个 `Wanxiang2.*` 包引用覆盖各点名库） |
| UI 视图层纯逻辑第二批（组合 ConnectIssueText；库 ToolErrorSummary、ToolArgsPreview、DurationText、GenerationErrorTitle、MessageTimestamp、ExportFileName、TranscriptMarkdown、PairingHint、ChatEmptyText、OnboardingRules、PromptHistory、SendKey、MathBaseline、PopupPlacement、ColorBlend） | 完成：与旧代码生成的金样相同；搜索分段的区间合并按 SearchSegments SPEC 2 有意修正；MathTypeset 金样改由完整公式 HTML 生成 | 已接线：旧 `MessageCard`、`Brand`（Export）、`Dialogs`、`ChatView`、`OnboardingCard`、`Composer`、`MathVisual`、`OverlayHost`、`Interaction` 已改调 `ConnectIssueText` 及各点名库包，对应旧代码已删（UI 约 170 包覆盖） |
| UI 视图层纯逻辑第三批（库 ToastQueue、ToastDwell、GenerationForm、GenerationDraft、IdleSurface、StartReady、DraftSummaryText） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试；生成载荷 JSON 字节不变 | 已接线：旧 `OverlayHost` 提示队列与计时、`SettingsGeneral` 生成分组校验与草稿状态、`AppShell` 空态与 startReady 与草稿摘要已改调 `ToastQueue`、`ToastDwell`、`GenerationForm`、`GenerationDraft`、`IdleSurface`、`StartReady`、`DraftSummaryText` 包，对应旧代码已删 |
| UI 视图层纯逻辑第四批（库 ComposerSendState、ToolCallState、CaretBreath、SidebarEmpty） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `Composer` 发送门控与附件状态字、`MessageCard` resolveToolCallStatusWith 与呼吸数学、`Sidebar` 空态分支已改调 `ComposerSendState`、`ToolCallState`、`CaretBreath`、`SidebarEmpty` 包，对应旧代码已删 |
| UI 视图层纯逻辑第五批（库 McpForm、ToolsPolicy、ProviderForm） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试；MCP 与工具设置载荷字节不变 | 已接线：旧 `SettingsTools` 编辑器校验与载荷、`SettingsProviders` 编辑器校验与 isLoopbackUrl 已改调 `McpForm`、`ToolsPolicy`、`ProviderForm` 包，对应旧代码已删 |
| UI 视图层纯逻辑第六批（库 MessageMenu、SidebarSelection、ProviderProbeText、ExportView、ImportView） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `MessageCard` 菜单门控、`Sidebar` 多选与批量条、`SettingsProviders` 看图说明与探活回执、`ExportDialog` 进度呈现与节流、`ImportDialog` 按键与回执取舍已改调 `MessageMenu`、`SidebarSelection`、`ProviderProbeText`、`ExportView`、`ImportView` 包，对应旧代码已删 |
| UI 视图层纯逻辑第七批（库 RovingIndex、StartLayout、PairingCodeInput、ConversationSurface） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `Menu` 方向键与 `SettingsView` 分区循环、`MainLayout` 居中与侧栏钳宽、`Dialogs` 配对码归一与自动提交、`AppShell` 已打开对话分支与主机状态行已改调 `RovingIndex`、`StartLayout`、`PairingCodeInput`、`ConversationSurface` 包，对应旧代码已删 |
| UI 视图层纯逻辑第八批（库 ExchangeJump、RevealFlash、BreadcrumbAction、ModelPickerList） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `ChatView` 问答跳转、定位高亮与面包屑，`ModelSwitcherView` 列表分组与键盘移动已改调 `ExchangeJump`、`RevealFlash`、`BreadcrumbAction`、`ModelPickerList` 包，对应旧代码已删 |
| UI 连接流程记账（库 ReconnectLoop） | 完成：与照抄旧记账生成的金样相同，另有真实行为测试 | 已接线：旧 `AppShell` 的 `connectAttempt`、`reconnectCts`、`reconnectDelayMs` 已换为 `ReconnectLoop`（`AppShell.fs:95-96` 实构造，含 Connect、ScheduleReconnect、ResumeReconnectNow 与两处退避归零） |
| UI 视图层纯逻辑第九批（库 MountWindow、ScrollFollow、SidebarRowState、SearchRequery、ToolOverview） | 完成：与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `ChatView` 挂载窗口与贴底跟随记账、`Sidebar` 行状态与搜索防抖判定、`MessageCard` 工具概览已改调 `MountWindow`、`ScrollFollow`、`SidebarRowState`、`SearchRequery`、`ToolOverview` 包，对应旧代码已删 |
| UI 纯模型 10.1（PinyinInitials、FuzzyMatch、MessageLink、ModelVision、MediaTypes，组合 CommandPalette） | 完成：旧 R20/ContinueLinkUndo/AttachmentVision 中的纯逻辑用例按名迁入通过 | 已接线：旧 `Model/MessageLink.fs`、`ModelVision.fs`、`MediaTypes.fs` 已删除引用包；`CommandPalette.fs` 只留 loadRecent/saveRecent（改名模块 `PaletteRecent`，AppShell 两处调用随改）；UI 约 170 包含 `CommandPalette` |
| UI 纯模型 10.2（ConversationSummary、SearchHit、RevealTarget、ForkLineage、PendingDeletion、HostHealth、LocalDraftControls、ProviderPresets，组合 ConversationList） | 完成：8 个库单测通过；组合者用 ChatDomain+ServerModel 生成真实列表快照，旧 LibraryTests 谱系用例 5 条迁入通过 | 已接线：旧 `Model/ConversationSummary.fs`、`PendingDeletion.fs`、`HostHealth.fs`、`ProviderPresets.fs` 已删除引用包；旧 `ProviderKind` 已删（无生产使用方）；UI 约 170 包含 `ConversationList` |
| UI 纯模型 10.3 模型层（MessageView、UiPrefs、HostCatalog、ProbeFailureKind、ModelSwitcher、DraftStore、ComposerDrafts、DownloadBuffers、ConfigFeedback、CommandFeedback、NavigationState、ShortcutKeys，组合 Catalog、ConversationRuns、Outbox） | 完成：12 个库单测通过，MessageView、UiPrefs、HostCatalog、DraftStore、ShortcutKeys 有旧程序集生成的金标准；3 个组合者迁入旧用例通过，Catalog 用真实 AppConfig+ProviderCatalog 核对预设传输类型与探测错误码 | 已接线：旧 `Model/MessageModel.fs`、`Catalog.fs`、`UiPrefs.fs`、`DraftStore.fs` 的纯逻辑与 `UiControllers.fs` 已删除引用包；宿主保留 `UiPrefs` 文件读写、草稿落点、Avalonia 按键 → `ShortcutKey` 映射；UI 约 170 包含 `Catalog`、`ConversationRuns`、`Outbox` |
| UI 非视图余项（ConversationTransfer、ErrorRecovery、UrlSchemeRegistration、ImageFit、ProbeCounter、StartupTrace、ClientConnections，组合 ConversationExport、ImageShrink，并入 ConversationList、ConversationRuns、CommandPalette） | 完成：7 个库都有旧程序集生成的金标准（ImageFit 经 Skia 真实缩小、ClientConnections 真实读 client.toml）；组合者迁入旧用例通过，ConversationExport 用真实 ChatDomain+ServerModel 分页 | 已接线：旧 `Model/ConversationTransfer.fs`、`ErrorRecovery.fs`、`ConversationExport.fs` 与 `ImageShrink`/`DesktopLinks`/`StartupTrace`/`ParityProbe`/`CommandPalette`/`CredentialStore` 的纯部分已删除引用包；宿主保留 Skia 编解码、文件与 IndexedDB 读写、环境变量、stderr、单实例套接字；`CredentialStore.fs` 纯部分已是包别名壳 |
| UI 壳层 AppShell 纯逻辑（ServerErrorRoute、HostRequests、EscapeChain；PendingDeletion、ConfigFeedback 增补；组合 ConversationRuns 的 GenerationOutcome、CommandPalette 的 ShellPalette） | 完成：MainView 私有成员的方法体原样摘到各自 tools/Old*.fs，生成金标准；新旧同一份 Render.fs 比对通过 | 已接线：旧 `AppShell.fs` 的 HandleEvent / HandleShortcut / PaletteItems / RunPaletteItem / NeighborExcluding / ExpireHostRequests 已改调 `ServerErrorRoute`、`HostRequests`、`EscapeChain`、`PendingDeletion`、`ConfigFeedback`、`GenerationOutcome`、`ShellPalette` 包，对应私有逻辑与字段已删（AppShell 已是包消费壳） |
| UI 壳层 AppShell 第二批（UploadFacts；DraftStore 增补 DraftRestore；组合 Catalog 的 idleModelChip） | 完成：旧代码原样摘录生成金标准或逐例差分，新旧一致 | 已接线：旧 `AppShell.fs` 的 附件上传/下载/粘贴、RestoreLocalDrafts/PruneDeletedConversationDrafts/UpdateDraftSummary/ClearLocalDrafts、Render 空闲分支已改调 `UploadFacts`、`DraftRestore`、`idleModelChip` 包 |
| UI 壳层 AppShell 协议事件（ConnectionIssue.ofPairingFailure 与配对状态文案、DownloadBuffers.AppendBase64、HostCatalog 的 ProbeModels、ApprovalCardText.otherConversationWaitingText、ConfigFeedback 的 ConfigIssueNotice） | 完成：与旧 HandleEvent 分支原样摘录逐例差分通过 | 已接线：旧 `AppShell.HandleEvent` 的 PairingStarted/Succeeded/Failed、AttachmentDownloadBegin/Chunk、ProviderProbeResult、ToolApprovalRequested、ConfigChanged/HostConfigRejected 已改调 `ConnectionIssue.ofPairingFailure`、`DownloadBuffers.AppendBase64`、`HostCatalog.ProbeModels`、`ApprovalCardText`、`ConfigFeedback` 包（`AppShell.fs:1514` `HandleEvent(ev: WireEvent)` 事件面经包类型消费） |
| UI 壳层 AppShell 余项（ForkAnchor、OlderPageLoad；UiPrefs 的 ThemePreference.next/switchedText；MessageView 的 RegenerateGate/ReplyGate；NavigationState 的 NavigationSnapshot.autofocusDrawer） | 完成：ForkAnchor 有旧代码摘录生成的金标准（含上线 JSON 字节），其余与旧代码原样摘录逐例差分通过 | 已接线：旧 `AppShell` 的 ForkFrom、CycleTheme、RegenerateLatest/CopyLatestReply、CheckOlderPageTimeout/FailOlderPage/ShowOlderPageRetry、ApplyResponsiveLayout 已改调 `ForkAnchor`、`OlderPageLoad`、`UiPrefs`、`RegenerateGate`/`ReplyGate`、`NavigationSnapshot.autofocusDrawer` 包 |
| UI 有状态对象（库 DeferredReveal、LinkInbox、ChangeWatch、BoundedWait、OnboardingFlow、ProbeSlot、SectionMemory、SearchSession、RowFocus；PendingDeletion 增补到期跟踪；组合 DraftSession） | 完成：对象自己持有状态，计时器与 IO 留宿主（对象收 now、回数据）；旧用例按名迁入或覆盖其状态部分，状态文字逐字断言 | 已接线：旧 `AppShell` 骨架延迟、视口轮询、待删计时、本地草稿字段，`DesktopLinks.ExternalLinks`，`SettingsDevices` 读取等待与检查主机超时，`OnboardingCard` 步骤，`SettingsProviders` 探活归属，`SettingsView` 分区缓存与滚动，`Sidebar` 搜索会话与行焦点已改调 `DeferredReveal`、`LinkInbox`、`ChangeWatch`、`BoundedWait`、`OnboardingFlow`、`ProbeSlot`、`SectionMemory`、`SearchSession`、`RowFocus`、`DraftSession` 包，对应旧字段已删 |
| UI 有状态对象第二批（库 CopyConfirm、CountdownTicker、ScrollTween、BreathPhase、CardReconcile、BlockDiff、ThemeTween、ProgressThrottle、HighlightQueue） | 完成：对象自己持有状态，计时器、控件与线程留宿主（对象收 now、回数据）；与照抄旧分支生成的金样相同，另有真实行为测试 | 已接线：旧 `MessageCard` 与 `MarkdownRenderer` 复制确认、限流倒计时，`ChatView` 平滑滚动、骨架呼吸、卡片对账，`StreamingMarkdown` 增量，`Tokens` 主题补间，`ExportDialog` 进度节流，`Highlight` 排队已改调 `CopyConfirm`、`CountdownTicker`、`ScrollTween`、`BreathPhase`、`CardReconcile`、`BlockDiff`、`ThemeTween`、`ProgressThrottle`、`HighlightQueue` 包（`CopyConfirm` 经 `MessageCardView`/`ComposerDesk` 组合消费），对应旧字段已删；`ExportView.refresh` 已随删 |
| UI 控件第一批（写手二：库 PopupMenu、OverlayLayer、ToastStrip、FieldCheck、CopyButton、ImeShield、KeyChord、FocusOrigin、InputSense；组合 OverlayDesk） | 完成：样式全走各自 Look 的活实例；旧 OverlayMenuTests、UiStabilityTests、DismissFocusTests、R21ImeTests、SettingsSurfaceTests 相关用例按名迁入各库或组合者并通过 | 已接线：旧 `OverlayHost`、`Menu`、`Ui.setFieldError` 等、`CopyFeedback`、`ImeGuard`、`Interaction` 与 `KeyShortcut` 的行为部分已改用 `PopupMenu`、`OverlayLayer`、`ToastStrip`、`FieldCheck`、`CopyButton`、`ImeShield`、`KeyChord`、`FocusOrigin`、`InputSense`、`OverlayDesk` 包，主题管理者填 Look，对应旧代码已删 |
| UI 控件第二批（写手二：库 BrandMark、ApprovalPanel、PalettePanel、ModelSwitchPanel、ImportSheet、ExportSheet、ShellLayout；组合 ApprovalDesk、PaletteDesk、ModelSwitchDesk、TransferDesk、ShellDesk） | 完成：外观走主题活实例与样式类；旧 Brand、ToolApprovalCard、CommandPaletteView、ModelSwitcherView、ImportDialog、ExportDialog、MainLayout 相关测试（含 R21 的 386）按名迁入库或组合者并通过 | 已接线：旧七个 Views 已删除，`AppShell` 已改用 `ApprovalDesk`、`PaletteDesk`、`ModelSwitchDesk`、`TransferDesk`、`ShellDesk` 五个组合者（入口见各组合者 AGENTS.md） |
| UI 控件第三批（写手二：库 SettingsFrame、ProvidersPane、ToolsPane、GeneralPane、DevicesPane、OnboardingPane；组合 SettingsScreen） | 完成：外观走主题活实例与样式类（新增主题条目 Space.X6l/X8l、SettingsDialogWidth、SettingsSmallDialogWidth），改主题度量即更新已显示的设置控件；旧 DevicesUiTests、SettingsViewTests、OnboardingUiTests 相关用例按名迁入库或组合者并通过 | 已接线：旧 `SettingsView`、`SettingsProviders`、`SettingsTools`、`SettingsGeneral`、`SettingsDevices`、`OnboardingCard` 已删除，`AppShell` 已改用 `SettingsScreen` 与 `OnboardingDesk`（入口见组合者 AGENTS.md） |
| UI 控件第四批（写手二：库 ConversationRows、SidebarSearch、BatchBar、HostSwitcher、SidebarBrand、ListEmptyCard、ConfirmPane、PromptPane、TextEditPane、ConnectPane、SessionPrefsPane、ShortcutHelpPane；组合 SidebarDesk、DialogsDesk） | 完成：外观走主题活实例与样式类（新增主题条目 Confirm/Connect/SessionDialogWidth、UiBodyLineHeight、ShortcutKeyMinWidth、SidebarSkeletonBarHeight、Space.TrackingDisplay、DerivedBrush.HoverSelected）；旧 DialogsTests、CraftsmanshipPolish/SelectionOperability/KeyConvergence/InteractabilityPolish/LibraryTests/UiStability/SearchHighlight 相关用例按名迁入并通过 | 已接线：旧 `Views/Sidebar.fs`、`Views/Dialogs.fs` 已删除，`AppShell` 已改用 `SidebarDesk` 与 `DialogsDesk`（入口见组合者 AGENTS.md） |
| UI 主题第一批（库 ThemePalette、ThemeMetrics、MotionSwitch、IconSet；组合 Theme） | 完成：配色、度量、时长、图标映射与照抄旧代码生成的金样相同；Theme 的 headless 用例覆盖切主题后已建控件改色、补间接管与代次、改尺寸即时生效、样式类几何取旧值 | 已接线：旧 `Design/Tokens`、`Palette`、`Metrics`、`MotionPolicy`、`Icons` 与 `Controls/Primitives`、`Interaction` 的外观部分已改为从 `Theme` 取实例和类名，对应旧代码已删（`Icons.fs:8` 已是 `Wanxiang2.IconSet.IconName` 别名壳） |
| UI 正文第二批（库 ThemeFonts；组合 MathFigure、CodeTint、MarkdownView、ReadingLook） | 完成：Markdown 正文、流式面板、公式控件与量尺、代码着色排队；用旧代码生成的金样逐行（控件树 744 行）、逐像素（公式）相同，旧用例按名迁入；外观全部来自主题管理者的活实例，Theme 改用 ThemeFonts 内嵌字体 | 已接线：旧 `Controls/MarkdownRenderer`、`Rich/MathLayout`、`Rich/MathVisual`、`Rich/Highlight` 已改调 `ThemeFonts`、`MathFigure`、`CodeTint`、`MarkdownView`、`ReadingLook` 包并删除；宿主保留 `RichBackend` 接口与 App/Pwa 适配，给 `CodeTint` 传后台与计时、给 `MarkdownView` 传 MathHtml |
| UI 消息卡第三批（库 RevealPane、FoldHeader、BusyRing、ThinkingBlock、ToolCallCard、AttachmentChip、ActionStrip、ErrorPanel、UserBubble、StreamCaret、UsageFoot；组合 MessageCardView） | 完成：旧 Views/MessageCard.fs 拆成 11 个控件库加组合者；用旧代码生成的金样逐行相同（控件树 553 行，10 条消息 + 3 张错误卡），旧用例按名迁入；外观全部来自主题管理者的活实例；模块级可变量改为显式的 CardActions / CardHost | 已接线：旧 `MessageCard` 与 `ChatView` 的 cardCache 已改调 `MessageCardView`（MessageCards、CardDeck）并删除；宿主给 `CardHost`（CodeTint、MathHtml、CaretClock、CardTouch、Now）与 `CardActions`（含 CopyLink、Recover） |
| UI 控件第四批（写手一：库 SkeletonList、ChatEmpty、ChatScroller、ExchangeNav、ComposerBox、AttachmentTray、ModelChip；组合 ChatPane、ComposerDesk） | 完成：旧 ChatView 滚动/空态/骨架/问答跳转与 Composer 输入区拆成 7 个控件库加 2 个组合者；外观走主题活实例；逻辑库 BreathPhase、ChatEmptyText、ScrollFollow、ScrollTween、ExchangeJump、ComposerSendState、ModelVision 由组合者接线；旧用例按名迁入并通过 | 已接线：旧 `ChatView`、`Composer` 已改调 `ChatPane`、`ComposerDesk` 并删除；宿主给消息卡插槽、挂载窗口、待发送面板、`PromptHistory`、`ImeShield` |
| UI 第五批（写手一：库 ChatHeader；组合 ChatScreen、Workspace；ComposerDesk 迁入 BusyRing 与未确认区，ChatPane 与 ComposerDesk 整树金样） | 完成：旧 ChatView 整体与对话列接线归 ChatScreen（渲染次序、挂载窗口、流式原位更新、更早一页前置锚点、定位 / 跳转滚动、钉住重对齐、顶栏菜单），顶栏与面包屑拆成 ChatHeader；旧 AppShell 布局段与 ApplyResponsiveLayout 归 Workspace；ChatPane（8 态）与 ComposerDesk（7 态）整树金样由旧代码内嵌字体生成并逐行一致 | 已接线：旧 `AppShell` 已改用 `Workspace` / `ChatScreen`（`AppShell.fs:22-23` `open Wanxiang2.ChatScreen / Wanxiang2.Workspace`），`Views/ChatView.fs`、`Views/Composer.fs` 与布局段已删；宿主给 `ChatScreenActions.After` 计时、浏览器视口轮询 |
| UI 第五批（写手二：组合 ProtocolDispatch、ShellCommands） | 完成：旧 AppShell.HandleEvent 的模型部分归 ProtocolDispatch（ClientState、ConversationRuns、Outbox、命令/配置回执、下载、搜索、探测、server.error），界面面经 ProtocolSurface 事实通知；旧 HandleShortcut/RunPaletteItem/CycleTheme 归 ShellCommands（Escape 链、浮层拦截、Ctrl+1..9、面板分派与最近使用、主题轮转）；旧 AppShellTests 的 HandleEvent/HandleShortcut 用例按名迁入并通过 | 已接线：旧 `AppShell` 已改调 `ProtocolDispatch`、`ShellCommands` 并删除 HandleEvent、HandleShortcut（`AppShell.fs:1514` `HandleEvent(ev: WireEvent)` 由协议事件包类型驱动）；认证成功步骤序列仍在宿主 `Apply`（入口见组合者 AGENTS.md） |
| 线协议（Wire） | 完成：`Wanxiang2.Wire` 交付包，线格式字节兼容并有金样，旧编解码测试 33 个搬来通过 | 已接线：宿主直接消费 `Wanxiang2.Wire` 交付包（`AppShell.fs:1514` `HandleEvent(ev: WireEvent)`，Wire 包命名空间沿用 `Wanxiang.Protocol`/`Wanxiang.Core`），src 无旧 Wire 工程/文件；AG-UI 映射经 `compose/AguiWire`（见 180 行，旧 `WireAgui.fs` 随 Agui 工程退出）；`libs/Wire/AGENTS.md` 的「旧仓库接线时确认」：宿主壳接线已随收束完成，该旧仓库测试口径项已随宿主壳退役，归 libs/Wire 文档同步与宿主壳处置 |
| UI 壳层接线收束（`compose/AppShell` 编排补全 + 桌面 / PWA 入口的端口与视图接线） | 完成：`compose/AppShell` 达 2100 行、18 条 headless 测试（假端口钉住 `RequireFeature` 门、附件归属与上传事件序列、偏好扇出、命令回执两态、断开重连调度、设置焦点归宿）；`app/Desktop` 真机冒烟全绿（`tools/smoke_desktop.sh`：`startup.firstFrame` / `startup.listReady` 打点、无异常、锁已释放、退出码 0）；`app/Pwa` 从交付包编译通过（wasm-tools，含 avalonia.js 补丁） | 已接线：旧 `AppShell.fs` 里此前在新入口留白的编排全部落地——设置 / 引导 hooks（模型服务、MCP、生成参数、预设、设备）、侧栏与聊天动作（重命名、置顶、归档、批量、删除即撤销、分叉、删除消息、下载附件、复制消息链接、恢复建议、版本切换、搜索命中定位）、传输面板（导出 / 导入工厂与事件路由）、主题与偏好扇出（主题、动效、折行、归档可见性、回车发送、草稿开关）、焦点归宿、附件 IO（选择 / 拖入 / 粘贴 / 缩略图 / 保存下载）、连接关闭与退避重连、`StartupTrace` 打点与采样退出。动作记录由组合者工厂给出，入口只提供端口（StorageProvider / Clipboard / Launcher / PlatformSettings / 计时器 / 焦点 / 订阅）与视图装配；旧壳的 `ParityProbe` 打点无对应物，未迁（证据：`compose/AppShell/AGENTS.md`、`tools/smoke_desktop.sh`、`./x app`） |

## 抽取路线（按顺序；2026-10-17 全部完成收束）

按「独立、价值大、调用面小」排序。每一项都要同时完成独立能力、接线、旧实现退出三步。

1. 账本：已接线（旧仓库宿主壳）；「待合入旧 `main`」已归旧仓库退役处置（见「需要」一节收束说明）。
2. 配置：本轮完成独立能力。
3. 认证与配对：本轮完成独立能力。
4. 线协议：本轮完成独立能力（`libs/Wire`）。AG-UI 映射已在第 8 项完成（`compose/AguiWire`）。
5. 领域状态与命令：本轮完成独立能力（`libs/Conversations`、`CommandId`、`PlainText`、`ToolSummary`，组合 `compose/ChatDomain`）。
6. 模型提供方传输：本轮完成独立能力（`libs/Sse`、`HttpRetry`、`JsonPick`、`Anthropic`、`Gemini`，组合 `compose/ProviderTransports`）。OpenAI 兼容传输用的是外部包，留在旧仓库。
7. MCP 客户端：本轮完成独立能力（`libs/JsonRpc`、`McpProtocol`、`LineProcess`、`StreamableHttp`，组合 `compose/McpClient`）。McpFunction 和 ToolRegistry 依赖工具注册，留在旧仓库。
8. 服务端编排：本轮完成可独立的部分（`libs/Agui`、`Approvals`，组合 `compose/AguiWire`、`ToolApproval`、`PartialReplies`）。`ChatOrchestrator`、`WsConnection`、`ServerApp` 的协作者（AgentRuntime、ToolRegistry、Store、Doctor、附件、搜索）还在旧仓库，暂不能以包组合；等这些协作者抽出后再拆。第 8.1 步完成搜索、附件、主机日志（`compose/Search`、`Attachments`、`HostLog`）。第 8.2 步完成存储与诊断（`compose/EventStore`、`Doctor`）。第 8.3 步完成消息与工具（`compose/AgentContent`、`BuiltinTools`、`ToolRegistry`）。库里对外公开、带通用名字（Failed、Invalid、Updates 等）的联合类型一律加 `[<RequireQualifiedAccess>]`，避免泄漏进组合者的公开命名空间（已改 GeminiStep、AnthropicStep、BlobError；其余库扫描无同名冲突）。从 8.2 起，每个库/组合的 AGENTS.md 逐条列出它迁入或覆盖的旧测试，旧仓库按名单删除。第 8.4 步完成 Agent 运行时（`compose/ProviderFailure`、`AgentRuntime`）；编排三件（ChatOrchestrator、WsConnection、ServerApp）的协作者至此都已是包。第 8.5 步完成 ChatOrchestrator。第 8.6 步完成服务端视图与配置（`compose/ServerModel`、`ProviderCatalog`、`ConfigMutation`、`HostStatus`，拆出库 PayloadFields、ModelListing）；引用 UI 的旧测试（R16、R17、Onboarding、R26、ConversationExport）与 ServerStoreMutationTests 留到 ServerApp 阶段处理。设备 lastSeen 移出用户配置：新库 DeviceSightings（主机自有 `<数据目录>/device-sightings.json`），AppConfig 不再写 lastSeen（仍读，启动时 Seed 一次性迁移）；WsConnection 迁移时（8.7）直接用它。第 8.7 步完成 WsConnection（拆出 DeviceRules；认证零写配置）；8.6 的 ServerModel 漏了 exportPage，8.7 补回并加进金样。第 8.8 步完成 ServerApp（拆出 ListenAddress；hostVersion 由宿主注入）；旧 Server 项目至此全部是包。待拆项完成：DeviceListJson（设备条目形状）、VerifiedCopy（证据包复制核对）、SecretText（配置脱敏）、ProviderCheck/McpCheck/SnapshotCheck/AttachmentAudit（诊断按检查项）；Store↔LedgerStore 复核把「更新版本日志」从错误文字前缀改为类型化的 Health.NewerFormat（Repair 拒绝截断）。ChatOrchestrator 的用量累加与 PartialText 已拆成 UsageTally、ContinuableReply。第 9 步完成 WsClient（拆出 ReconnectBackoff、ConnectionIssue、TextSocket、CommitTimeline）。路线图 10（UI 纯模型起，含视图层、控件、主题各批次）已按其后续条目与「迁移状态」表执行完毕；本路线于 2026-10-17 全部收束，本仓库成为唯一产品本体入口，旧仓库已物理废弃。
   - 原「待拆（EventStore、Doctor 原样迁入）」各项已完成（对照命令与测试名见 compose/EventStore/AGENTS.md、compose/Doctor/AGENTS.md）：证据包复制与 SHA-256 核对归库 `VerifiedCopy`（清单与诊断 JSON 形状仍留组合者），配置脱敏归库 `SecretText`（与 `Stderr.redact` 同一规则），设备条目形状归库 `DeviceListJson`，诊断按检查项归库 `ProviderCheck`/`McpCheck`/`SnapshotCheck`/`AttachmentAudit`（Doctor 只编排），Store 的 Inspect/Repair ↔ LedgerStore 映射改为类型化 `Health.NewerFormat`（按错误文字前缀判断「更新版本日志」已删，Repair 只截 Damaged）。
   - 迁移状态表与本节同口径：表是执行账目（历史），本节是路线记录（历史）；新的产品工作不再属于「抽取路线」，由根 SPEC 6 与各组合者 AGENTS.md 记账。
9. 客户端（旧 `Client/WsClient.fs`）。
10. UI（约 26k 行）：先抽纯模型（`DraftStore`、`Catalog`、`ConversationSummary`），视图最后处理。10.1 完成命令面板检索、消息链接、看图判断、附件类型（库 PinyinInitials、FuzzyMatch、MessageLink、ModelVision、MediaTypes，组合 CommandPalette）。10.2 完成对话列表、搜索命中、分叉谱系、待删批次、主机状态、模型服务预设（组合 ConversationList）。10.3 模型层完成消息视图、界面偏好、主机目录、草稿、输入框与发件箱、导航、快捷键（组合 Catalog、ConversationRuns、Outbox）；草稿落盘去掉端口式后端，改为返回 DraftWrite 由宿主执行。 10.3（视图层纯逻辑）完成第一批：MathTypeset、DesignRhythm、MarkdownBlocks、LinkChunks、SpacingRamp、SearchSegments、ApprovalCardText、DevicesModel；第二批：MessageCard（工具错误摘要、参数预览、耗时、错误标题、时间）、Brand（导出文件名与转录）、Dialogs（配对提示、连接错误文案）、ChatView（空态）、OnboardingCard、Composer（发送历史、回车）、MathVisual（基线）、OverlayHost（浮层落点）、Interaction（颜色叠加）；第三批：SettingsGeneral（生成分组校验与草稿）、AppShell（空态、startReady、草稿摘要）、OverlayHost（提示队列与计时）；第四批：Composer（发送门控与附件状态）、MessageCard（工具调用状态、光标呼吸）、Sidebar（空态）；第五批：SettingsTools（MCP 编辑器、工具策略）、SettingsProviders（模型服务编辑器校验）；第六批：MessageCard（菜单门控）、Sidebar（多选）、SettingsProviders（看图说明、探活回执）、ExportDialog、ImportDialog；第七批：Menu 与 SettingsView（键盘移动）、MainLayout、Dialogs（配对码）、AppShell（已打开对话分支）；第八批：ChatView（问答跳转、定位高亮、面包屑）、ModelSwitcherView（列表）。
