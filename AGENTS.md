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
| `compose/*.fsproj` 用 `VersionOverride="0.0.0-local"` 绕过中央包版本；包版本固定为 `0.0.0-local` | `./x compose`；旧仓库 `tools/wanxiang2-feed.sh` | 库没有发布版本号，只在本地 feed 里存在 | 库开始按版本号发布，版本登记进 `Directory.Packages.props` |

已解除：2026-10-13 删去「`compose/LedgerStore` 与旧仓库 `Store.fs` 双重实现」一项。旧仓库分支 `extract-ledger@84017ef` 改为消费 `Wanxiang2.LedgerStore` 包，`Store.fs` 只剩领域接线。


# 落地起点

从一个真实问题开始：补明人在乎的结果，选出最可能推翻设计的边界，按 LEMMA 2 准备下一小步。由具体场景落实最简独立职责及真实组合，再逐个加强。证明填入相应 SPEC，尚未成立的保留 `[TODO]`；不必先设计全系统，也不必先填完这份模板才动手。

# 本项目：wanxiang2

以下只写本项目特有的需要、协议、工具和证据；通用规则以上文为准。

## 需要

1. **逐个抽干旧仓库。** [AXIOM: 1, 4; LEMMA: 6]
    旧仓库 https://github.com/PamelaSprin47685ghall/wanxiang（F#）难维护、修 Bug 慢；整体重写工作量大且没有保证。做法是每次从旧仓库抽出一项独立职责，做成本仓库的库，接回旧仓库并让旧实现退出，再抽下一项。每次抽完都能验证，也都能停下。新旧不必一一对应，规格从 AXIOM 重新推，不沿用旧格式或旧决策。
2. **只许旧依赖新。** [LEMMA: 5]
    本仓库不引用旧仓库的任何源码、包或运行时服务。
3. **语言。** [AXIOM: 5]
    交流、注释与规则文档用中文，其余用英语。
4. **现有代码都是草稿。** [LEMMA: 1, 8]
    本仓库的实现可以大改，不为保留现有接口而增加兼容层。

## 布局与工具

- `libs/<Name>/`：一项独立职责，一个库：`<Name>.fsproj`、`tests/`、`AGENTS.md`。库之间没有任何依赖边（不只是无环），测试只引用本库。[LEMMA: 4, 5; SPEC: 2]
- `compose/<Name>/`：组合者 C，只经交付包使用库，不引用库源码。[LEMMA: 4; SPEC: 2, 4]
- `./x test <lib>`：只凭本库与声明的外部包构建并测试。`./x check`：库项目不引用本库目录以外的东西，也不引用兄弟库的包；组合者项目不引用自己目录以外的东西。`./x pack`：打出交付包。`./x compose`：清掉包缓存里的 `Wanxiang2.*` 后，从 `artifacts/feed` 消费交付包，测试组合者。`./x all` 依次执行全部。CI（`.github/workflows/ci.yml`）每个库一个任务，另有一个组合任务。
- `./x check` 检查的是项目文件里的引用，不检查运行时调用；运行时独立靠每个库的测试不带任何兄弟库仍能通过（构建上就拿不到兄弟库）。

## 证据现状（对应上文 SPEC）

- SPEC 1 规则可追溯：每个库的 AGENTS.md 逐条写明前提与测试名。[TODO] 没有机器检查引用编号；靠评审。
- SPEC 2 职责与交付独立：构建独立、协作独立由 `./x check` 与 `./x test <lib>` 给出，每个库都通过；交付独立由 `./x compose` 给出（干净包缓存、只用 `artifacts/feed` 中的包），通过；`Wanxiang2.LedgerStore` 也已在仓库外的临时项目里消费过一次（手动）。[TODO] 仓库外消费没有自动化。
- SPEC 3 状态确有主人：Ledger 与 compose/LedgerStore 测了输入、返回值别名、错误后的状态、实例隔离（见各自 AGENTS.md）。[TODO] JsonSeqFile、SnapshotFile、DirLock 的状态只有文件本身，没有单独的别名测试。
- SPEC 4 协作由真实组合证明：`compose/LedgerStore/tests` 覆盖成功、类型化拒绝、重复、游标分页、在每个字节处崩溃、半写尾与修复、解码失败、过期快照、快照策略与快照写失败、真实未知结果（`/dev/full`）、并发、同目录互斥。[TODO] 「写失败但确知未写」没有真实故障可触发。
- SPEC 5 增强经得起拆除：目前没有增强层，不适用。
- SPEC 6 每步形成真实闭环：见下表。

## 迁移状态

| 职责 | 独立能力 | 接入旧仓库并退出旧实现 |
| --- | --- | --- |
| 事件账本（JsonSeq、Ledger、Snapshot、DirLock、Canonical，组合 LedgerStore） | 完成：5 个库和 `Wanxiang2.LedgerStore` 交付包各自测试通过，真实组合测试通过 | 已接线：旧仓库分支 `extract-ledger@84017ef` 消费交付包，旧 Replay/Coordinator 已退出，旧仓库 Release 全量 1484 通过、0 失败、1 跳过。[TODO] 合入旧仓库 `main` |
| 配置（AtomicTextFile、AppConfig，组合 ConfigStore） | 完成：两个库和 `Wanxiang2.ConfigStore` 交付包测试通过；旧 `ConfigTests.fs` 中 ConfigStore 和格式部分（34 个）已搬来并通过 | 未开始：旧仓库删掉 `src/Wanxiang.Config/{ConfigModel,TomlCodec,ConfigStore}.fs`，改为引用 `Wanxiang2.ConfigStore` 包；命名空间与 API 不变 |
| 认证与配对（ClientToken、DesktopClient、PairingCode、FailureLimiter、WsHandshake，组合 PairingDesk） | 完成：5 个库和 `Wanxiang2.PairingDesk` 交付包测试通过，令牌哈希有金标准向量 | 未开始：旧仓库删掉 `src/Wanxiang.Config/Auth.fs` 后，整个 `Wanxiang.Config` 工程可以删除 |
| 领域状态与命令（Conversations、CommandId、PlainText、ToolSummary，组合 ChatDomain） | 完成：4 个库和 `Wanxiang2.ChatDomain` 交付包测试通过；提交行、快照正文、commandId 与旧实现生成的金样逐字节相同 | 未开始：旧仓库删掉 `src/Wanxiang.Core` 剩下的源码，改为引用 `Wanxiang2.ChatDomain`、`PlainText`、`ToolSummary`；需要先完成线协议接线 |
| 模型提供方传输（Sse、HttpRetry、JsonPick、Anthropic、Gemini，组合 ProviderTransports） | 完成：5 个库和 `Wanxiang2.ProviderTransports` 交付包测试通过；请求和解码结果与旧实现生成的金样逐字节相同 | 未开始：需要先完成配置和线协议接线 |
| MCP 客户端（JsonRpc、McpProtocol、LineProcess、StreamableHttp，组合 McpClient） | 完成：4 个库和 `Wanxiang2.McpClient` 交付包测试通过；发出的行、HTTP 请求和结果文本与旧实现生成的金样逐字节相同 | 未开始：旧仓库删除 McpTransport.fs、McpClient.fs，改引用包 |
| 服务端编排的独立部分（Agui、Approvals，组合 AguiWire、ToolApproval、PartialReplies） | 完成：AG-UI 承载与工具审批的线上字节与旧实现生成的金样逐字节相同 | 未开始：旧仓库删除 Agui 工程、WireAgui.fs、ToolApproval.fs、PartialReplyStore.fs，改引用包 |
| 搜索、附件、主机日志（TextMatch、MessageText、MediaSniff、AttachmentBlobs、JsonLinesLog，组合 Search、HostLog、Attachments） | 完成：结果页、附件元数据、错误文字、stderr 行格式与旧实现生成的金样逐字节相同 | 未开始：旧仓库删除 SearchService.fs、AttachmentStore.fs、Stderr.fs，改引用包 |
| 存储与诊断（HostProbe、AttachmentRefs，组合 EventStore、Doctor） | 完成：旧存储测试原样通过；doctor 金样与旧实现逐字节相同 | 未开始：旧仓库删除 src/Wanxiang.Store、Doctor.fs、SupportBundle.fs，改引用包；ServerModel.attachmentRefsOf 转发 AttachmentRefs.ofPayload |
| 消息与工具（MessageSerde、MediaContent、SandboxFiles、ToolNames、RefreshingPool，组合 AgentContent、BuiltinTools、ToolRegistry） | 完成：消息 JSON、附件内容、工具目录与调用结果与旧实现生成的金样逐字节相同 | 未开始：旧仓库删除 Agent/MessageSerde.fs、AttachmentContent.fs、Server/BuiltinTools.fs、ToolRegistry.fs，改引用包 |
| Agent 运行时（HttpFailureFacts、SharedHttpClients、AgentHistory、AguiMessageMap，组合 ProviderFailure、AgentRuntime） | 完成：请求体、流式增量、错误归类与旧实现生成的金样逐字节相同 | 未开始：旧仓库删除 Wanxiang.Agent 工程里 HistoryProvider.fs、MessageMap.fs、AgentRuntime.fs（连同 8.3 的两个文件，整个工程可删），改引用包 |
| 聊天编排（ContextTrim、TitleText，组合 ChatOrchestrator） | 完成：真实组合金样（事件、记账、请求体）与旧实现逐字节相同；旧 ChatOrchestratorTests 等迁入通过 | 未开始：旧仓库删除 GenerationContext.fs、TitleGenerator.fs、ChatOrchestrator.fs，改引用包 |
| 服务端视图与配置（PayloadFields、ModelListing，组合 ServerModel、ProviderCatalog、ConfigMutation、HostStatus） | 完成：ServerModel/ConfigMutation/HostStatus 金样与旧实现逐字节相同；旧 ProviderCatalogTests 迁入通过 | 未开始：旧仓库删除 ServerModel.fs、ProviderCatalog.fs、ConfigMutation.fs、HostStatus.fs，改引用包 |
| WebSocket 连接（DeviceSightings、DeviceRules，组合 WsConnection） | 完成：认证零写配置真实组合测试；DeviceRules 单测 | 未开始：旧仓库删除 WsConnection.fs，ServerApp 传入 DeviceSightings 并 Seed 迁移 |
| 主机（ListenAddress，组合 ServerApp） | 完成：旧主机/连接/设备/诊断等 16 个测试文件 158 条迁入通过；hostVersion 由宿主注入 | 未开始：旧仓库删除 Server 项目剩余源文件与已迁测试，改引用 Wanxiang2.ServerApp |
| 客户端连接（ReconnectBackoff、ConnectionIssue、TextSocket、CommitTimeline，组合 WsClient） | 完成：旧 ClientStateAndWsClientTests 41 条迁入组合者、6 条退避用例迁入 ReconnectBackoff，本机真实 WebSocket 测试通过 | 未开始：旧仓库删除 src/Wanxiang.Client 全部源文件，改引用 Wanxiang2.WsClient、ReconnectBackoff、ConnectionIssue；UI 把 ConnectionIssue 分支改成限定名 |
| 程序入口的非界面逻辑（CliArgs、RunModes、PwaAssets、ShutdownSignals、ClientTomlFile，组合 LaunchPlan、StoreLaunch、DesktopPairing） | 完成：真实磁盘、真实 EventStore/ConfigStore 上的组合测试通过；日志与错误文字与旧入口逐字相同 | 未开始：旧 Wanxiang.App/Program.fs 改为调用这些包，只留 Avalonia 启动、单实例与链接转发（UI）、信号注册 |
| UI 纯模型 10.1（PinyinInitials、FuzzyMatch、MessageLink、ModelVision、MediaTypes，组合 CommandPalette） | 完成：旧 R20/ContinueLinkUndo/AttachmentVision 中的纯逻辑用例按名迁入通过 | 未开始：旧仓库删除 Model/MessageLink.fs、ModelVision.fs、MediaTypes.fs，CommandPalette.fs 只留 loadRecent/saveRecent（改名模块 PaletteRecent，AppShell 两处调用随改），引用包 |
| 线协议（Wire） | 完成：`Wanxiang2.Wire` 交付包，线格式字节兼容并有金样，旧编解码测试 33 个搬来通过 | 未开始：见 `libs/Wire/AGENTS.md` 与本轮报告的接线步骤；AG-UI 映射（旧 `WireAgui.fs`）暂留旧仓库 |

## 抽取路线（按顺序，可修订）

按「独立、价值大、调用面小」排序。每一项都要同时完成独立能力、接线、旧实现退出三步。

1. 账本：已接线，待合入旧 `main`。
2. 配置：本轮完成独立能力。
3. 认证与配对：本轮完成独立能力。
4. 线协议：本轮完成独立能力（`libs/Wire`）。AG-UI 映射已在第 8 项完成（`compose/AguiWire`）。
5. 领域状态与命令：本轮完成独立能力（`libs/Conversations`、`CommandId`、`PlainText`、`ToolSummary`，组合 `compose/ChatDomain`）。
6. 模型提供方传输：本轮完成独立能力（`libs/Sse`、`HttpRetry`、`JsonPick`、`Anthropic`、`Gemini`，组合 `compose/ProviderTransports`）。OpenAI 兼容传输用的是外部包，留在旧仓库。
7. MCP 客户端：本轮完成独立能力（`libs/JsonRpc`、`McpProtocol`、`LineProcess`、`StreamableHttp`，组合 `compose/McpClient`）。McpFunction 和 ToolRegistry 依赖工具注册，留在旧仓库。
8. 服务端编排：本轮完成可独立的部分（`libs/Agui`、`Approvals`，组合 `compose/AguiWire`、`ToolApproval`、`PartialReplies`）。`ChatOrchestrator`、`WsConnection`、`ServerApp` 的协作者（AgentRuntime、ToolRegistry、Store、Doctor、附件、搜索）还在旧仓库，暂不能以包组合；等这些协作者抽出后再拆。第 8.1 步完成搜索、附件、主机日志（`compose/Search`、`Attachments`、`HostLog`）。第 8.2 步完成存储与诊断（`compose/EventStore`、`Doctor`）。第 8.3 步完成消息与工具（`compose/AgentContent`、`BuiltinTools`、`ToolRegistry`）。库里对外公开、带通用名字（Failed、Invalid、Updates 等）的联合类型一律加 `[<RequireQualifiedAccess>]`，避免泄漏进组合者的公开命名空间（已改 GeminiStep、AnthropicStep、BlobError；其余库扫描无同名冲突）。从 8.2 起，每个库/组合的 AGENTS.md 逐条列出它迁入或覆盖的旧测试，旧仓库按名单删除。第 8.4 步完成 Agent 运行时（`compose/ProviderFailure`、`AgentRuntime`）；编排三件（ChatOrchestrator、WsConnection、ServerApp）的协作者至此都已是包。第 8.5 步完成 ChatOrchestrator。第 8.6 步完成服务端视图与配置（`compose/ServerModel`、`ProviderCatalog`、`ConfigMutation`、`HostStatus`，拆出库 PayloadFields、ModelListing）；引用 UI 的旧测试（R16、R17、Onboarding、R26、ConversationExport）与 ServerStoreMutationTests 留到 ServerApp 阶段处理。设备 lastSeen 移出用户配置：新库 DeviceSightings（主机自有 `<数据目录>/device-sightings.json`），AppConfig 不再写 lastSeen（仍读，启动时 Seed 一次性迁移）；WsConnection 迁移时（8.7）直接用它。第 8.7 步完成 WsConnection（拆出 DeviceRules；认证零写配置）；8.6 的 ServerModel 漏了 exportPage，8.7 补回并加进金样。第 8.8 步完成 ServerApp（拆出 ListenAddress；hostVersion 由宿主注入）；旧 Server 项目至此全部是包。待拆项完成：DeviceListJson（设备条目形状）、VerifiedCopy（证据包复制核对）、SecretText（配置脱敏）、ProviderCheck/McpCheck/SnapshotCheck/AttachmentAudit（诊断按检查项）；Store↔LedgerStore 复核把「更新版本日志」从错误文字前缀改为类型化的 Health.NewerFormat（Repair 拒绝截断）。ChatOrchestrator 的用量累加与 PartialText 已拆成 UsageTally、ContinuableReply。第 9 步完成 WsClient（拆出 ReconnectBackoff、ConnectionIssue、TextSocket、CommitTimeline）。下一步是路线图 10：UI 纯模型。
   - 待拆（EventStore、Doctor 原样迁入，仍含独立语义）：RecoveryBundle 的「复制文件 + sha256 清单」→ 库；SupportBundle 的配置脱敏 → 库（与 Stderr.redact 同一规则）；Doctor 各项检查（provider、MCP、快照、附件）的判断 → 按检查拆成库，Doctor 只编排；Store 的 Inspect/Repair 结论类型与 Ledger 的映射复核。
9. 客户端（旧 `Client/WsClient.fs`）。
10. UI（约 26k 行）：先抽纯模型（`DraftStore`、`Catalog`、`ConversationSummary`），视图最后处理。10.1 完成命令面板检索、消息链接、看图判断、附件类型（库 PinyinInitials、FuzzyMatch、MessageLink、ModelVision、MediaTypes，组合 CommandPalette）。
