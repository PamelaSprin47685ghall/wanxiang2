#!/usr/bin/env node
// 旧仓库决策 411 / 展望.md §1.5 / §3 G4：对「发布产物」做 PWA 真实端到端冒烟。
// 运行环境：headless Chrome、DPR 2、真实产品入口交互（无状态机后门）。
// 覆盖范围：
//   1) 页面渲染 Avalonia 画布（非空白、无未捕获异常）；
//   2) 画布像素尺寸 = CSS 尺寸 × 2（DPR 补丁在产物中生效）；
//   3) 认证态预置与 WebSocket 连接建立；
//   4) 真实键盘物理按键输入（page.keyboard.type）与输入法增量上屏桥（CDP Input.insertText）；
//   5) 菜单真实交互后果：输入框右键唤出 MenuFlyoutPresenter，全选后执行「剪切」，断言草稿清空且剪贴板留存；
//   6) Esc 键级联退栈与回焦（关闭菜单后焦点精准回到输入框）；
//   7) 命令面板真实交互后果：Ctrl+K 打开、键盘输入过滤并回车执行命令，断言写入最近使用持久化记录（localStorage）；
//   8) 设置弹窗真实交互后果：Ctrl+, 打开设置大弹窗、切换分区、操作真实偏好并断言 localStorage 持久化变更；
//   9) 主题三态轮转（Ctrl+Shift+S 触发 ThemePreference.next、持久化至 localStorage、画布物理明暗切换）；
//   10) 页面重开草稿恢复与追加验证（IndexedDB 草稿往返、localStorage 访问计数递增）。
// 用法：node tools/smoke_published_pwa.js --base http://127.0.0.1:<port> [--out <dir>] [--chrome /usr/bin/google-chrome]

const puppeteer = require('puppeteer-core');
const { mkdirSync, writeFileSync } = require('node:fs');
const { execFileSync } = require('node:child_process');

const argv = process.argv.slice(2);
const arg = (k, d) => {
    const i = argv.indexOf(`--${k}`);
    return i >= 0 && argv[i + 1] ? argv[i + 1] : d;
};

const BASE = arg('base', '');
const OUT = arg('out', '/tmp/wanxiang-smoke');
const CHROME = arg('chrome', process.env.CHROME_PATH || '/usr/bin/google-chrome');

if (!BASE) {
    console.error('--base required');
    process.exit(2);
}

mkdirSync(OUT, { recursive: true });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// 种子对话标题：由 tools/smoke_published.sh 写入台账，脚本按它在界面上定位与验收。
// 顶栏标题与侧栏会话行显示同一段文字，提示条也显示它，所以只留一处事实。
const SEED_TITLE = '中文冒烟';

// 截图文字识别：返回**带包围盒**的词，供脚本按文字定位真实控件。
// macOS 用系统 Vision（tools/smoke-ocr.swift）；Linux 用 CI 安装的 Tesseract 中文模型。
// 两者的输出都归一到 TSV 的同一批列，Node 侧只有一套解析。
//
// 界面不是连续文字段落：侧栏小字、正文、留白混在一页上。定位用的识别统一走
// `ocrShot`——先裁出目标区域、放大到固定宽度，再按版面切分（见 OCR_LAYOUT_PSM）。
// 坐标换算在 CSS 尺度上完成，两个平台用同一套算术。
const OCR_SCRIPT = require('node:path').resolve(__dirname, 'smoke-ocr.swift');
// 识别前把裁剪区放大到至少这个宽度：Tesseract 在中文小字上需要足够的像素高度。
const OCR_UPSCALE_WIDTH = 1200;
// 界面截图的默认切分模式：psm 4（把图当作若干可变大小的文字块）。
// psm 6 假设整页是**一个**连续文字块，界面里侧栏小字、正文、留白混在一起，
// 版面分析会把侧栏标题切碎（实测 CI 整图与裁剪图都在 psm 6 下读不出「中文冒烟」，
// psm 4 下能读出并给出坐标）。psm 3 自动版面分析同样能读出，但漏掉「中文」两字，
// 会把「同一段文字在同一行」的判据打散。取实测两个平台、两类输入都命中的 4。
const OCR_LAYOUT_PSM = '4';
// 弹出层使用稀疏文本模式：菜单标签与快捷键分列，强行整行分词会破坏短中文标签。
const POPUP_ITEM_PSM = '11';
// 整张画布识别时的固定放大倍数（画布 1200 CSS 宽 → 2400 像素，与原口径一致）。
const OCR_SCALE_BASE = 2;
const runOcr = (path, pageSegmentation = OCR_LAYOUT_PSM) => {
    let raw;
    if (process.platform === 'darwin' && process.env.WX_SMOKE_OCR !== 'tesseract') {
        raw = execFileSync('swift', [OCR_SCRIPT, path], { encoding: 'utf8', timeout: 60000 });
    } else {
        raw = execFileSync('tesseract', [path, 'stdout', '-l', 'chi_sim+eng', '--psm', pageSegmentation, 'tsv'],
            { encoding: 'utf8', timeout: 30000 });
    }
    const words = [];
    for (const line of raw.split('\n')) {
        const cols = line.split('\t');
        if (cols.length < 12) continue;
        const text = cols.slice(11).join('\t').trim();
        const left = Number(cols[6]), top = Number(cols[7]);
        const width = Number(cols[8]), height = Number(cols[9]), conf = Number(cols[10]);
        if (!text || !Number.isFinite(left) || !Number.isFinite(top)) continue;
        words.push({ text, left, top, width, height, conf });
    }
    return words;
};
// 弹出层裁剪放大后的识别统一用 psm 6：菜单是均匀的文字列（标签+快捷键两列）。
// 整页界面截图用 psm 4（OCR_LAYOUT_PSM）。两者不能混用：psm 4 会把菜单两列拆散
// （实测「复制」读成「复骨」），psm 6 会把整页侧栏小字并成乱段（「中文冒烟」读丢）。
const ocrText = (path, psm = '6') => runOcr(path, psm).map((w) => w.text).join('').replace(/\s/g, '');
// 在识别结果里找含某段文字的词；box 为 CSS 坐标（OCR 来源已在 ocrShot 归一）。
const findWord = (words, needle) => words.find((w) => w.text.includes(needle)) || null;
// 两个 OCR 的切词粒度不同：Vision 给整词（「新建对话」一个框），Tesseract 给单字
// （「新」「建」「对」「话」四个框）。所以要能在同一行把相邻词拼起来再匹配，
// 并返回合并后的包围盒。判据只认「这段文字确实出现在同一行」。
const findPhrase = (words, needle, minLeft = 0) => {
    const rows = new Map();
    for (const w of words) {
        if (w.left < minLeft) continue;
        // 同一行：纵向中心相差不超过较高者的一半。
        const key = [...rows.keys()].find((k) => Math.abs(k - (w.top + w.height / 2)) <= Math.max(w.height, 12) / 2);
        const at = key === undefined ? w.top + w.height / 2 : key;
        if (!rows.has(at)) rows.set(at, []);
        rows.get(at).push(w);
    }
    for (const row of rows.values()) {
        row.sort((a, b) => a.left - b.left);
        for (let i = 0; i < row.length; i++) {
            let text = '';
            for (let j = i; j < row.length; j++) {
                text += row[j].text;
                if (text.includes(needle)) {
                    const span = row.slice(i, j + 1);
                    const left = Math.min(...span.map((w) => w.left));
                    const top = Math.min(...span.map((w) => w.top));
                    const right = Math.max(...span.map((w) => w.left + w.width));
                    const bottom = Math.max(...span.map((w) => w.top + w.height));
                    return { text, left, top, width: right - left, height: bottom - top };
                }
            }
        }
    }
    return null;
};
// 同一段文字在侧栏（会话标题 / 消息预览）与主内容区都会出现。要精确点到主内容区的
// 那张卡，必须按横向位置排除侧栏，否则右键开出来的是会话行菜单。
const findPhraseInMain = (words, needle, minLeft = 300) => findPhrase(words, needle, minLeft);
// 识别结果统一归一成 **CSS 坐标**（ocrShot 已按 OCR_SCALE 与 DPR 折算）。
const wordCenterCss = (w) => ({ x: w.left + w.width / 2, y: w.top + w.height / 2 });

const readablePopupText = (name, text) => {
    // 每个弹出层只对**它自己真有**的文案作验收，不能拿另一类菜单的文案当统一判据：
    // 提示条显示的是被悬停控件自己的文字（这里是顶栏对话标题），
    // 消息卡右键菜单是「复制消息 / 编辑并分叉 / …」，输入框 ContextFlyout 才是「剪切 / 复制 / 粘贴」。
    const { labels, need } = name === 'tooltip'
        ? { labels: [SEED_TITLE], need: 1 }
        : name === 'contextmenu'
            ? { labels: ['复制消息', '编辑并分叉', '重新生成', '删除这条消息'], need: 1 }
            : { labels: ['剪切', '复制', '粘贴'], need: 2 };
    return labels.filter(label => text.includes(label)).length >= need;
};

// 输入框 ContextFlyout 的文案特征：点错位置时会打开它，不能拿它冒充消息卡菜单。
const isTextFlyoutText = (text) =>
    text.includes('剪切') || text.includes('Ctrl+X') || text.includes('粘贴');

// 声明式条件等待：按谓词 probe 轮询，避免盲目写死长延时
const until = async (probe, timeoutMs = 20000, intervalMs = 200) => {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
        try {
            const res = await probe();
            if (res) return res;
        } catch (e) {
            // Node 层 ReferenceError / TypeError 是脚本自身缺陷，不能静默吞掉：
            // 吞掉会把「断言永远等不到」伪装成「条件未满足」，让 until 超时返回 false。
            if (e instanceof ReferenceError || e instanceof TypeError) throw e;
            // 其余（如页面尚未就绪时的 evaluate 失败）按瞬态继续重试。
        }
        await sleep(intervalMs);
    }
    return false;
};

let failed = 0;
let skipped = 0;
const check = (name, ok, detail) => { if (!ok) failed++; console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`); };
// SKIP 不算失败：用于「这个环境本身渲染不出中文」的情形（如本机 headless Chrome 的字体限制），
// 判据要诚实，不能把环境缺陷算成产品缺陷，也不能用 SKIP 掩盖真缺陷。
const checkSkip = (name, reason, detail) => { skipped++; console.log(`SKIP ${name} — ${reason}${detail ? ' — ' + detail : ''}`); };

(async () => {
    const browser = await puppeteer.launch({
        executablePath: CHROME,
        headless: 'new',
        args: ['--no-sandbox', '--disable-dev-shm-usage', '--enable-unsafe-swiftshader']
    });

    try {
        // 预先授权剪贴板读取与写入权限，供菜单剪切/复制行为真实验证
        const context = browser.defaultBrowserContext();
        try {
            await context.overridePermissions(BASE, ['clipboard-read', 'clipboard-write', 'clipboard-sanitized-write']);
        } catch (_) {}

        const page = await browser.newPage();
        // 反例校验：缺字方框与空白图不能被中文可读性门禁接受。
        const counterexample = `${OUT}/ocr-missing-glyphs.png`;
        await page.setContent('<canvas width="640" height="160"></canvas>');
        await page.evaluate(() => {
            const c = document.querySelector('canvas');
            const ctx = c.getContext('2d');
            ctx.fillStyle = 'white'; ctx.fillRect(0, 0, c.width, c.height);
            ctx.strokeStyle = 'black'; ctx.lineWidth = 2;
            for (let i = 0; i < 8; i++) ctx.strokeRect(20 + i * 70, 35, 40, 60);
        });
        await page.screenshot({ path: counterexample });
        const missingText = ocrText(counterexample);
        check('中文截图门禁拒绝缺字方框与空白',
            !readablePopupText('tooltip', missingText) && !readablePopupText('contextmenu', missingText)
            && !readablePopupText('flyout', missingText)
            && !readablePopupText('tooltip', '') && !readablePopupText('contextmenu', ''));
        const errors = [];
        page.on('pageerror', (e) => errors.push(String(e)));

        await page.setViewport({ width: 1200, height: 800, deviceScaleFactor: 2 });
        await page.goto(BASE, { waitUntil: 'domcontentloaded', timeout: 60000 });

        // ---- 阶段 1：首屏启动与画布就绪 ----
        const canvasReady = await page.waitForFunction(() => {
            const c = document.querySelector('canvas');
            return c && c.width > 0 && c.clientWidth > 0 && document.querySelector('.avalonia-input-element');
        }, { timeout: 120000, polling: 250 }).then(() => true, () => false);

        check('Avalonia 画布与输入桥元素出现', canvasReady);
        if (!canvasReady) {
            await page.screenshot({ path: `${OUT}/smoke-timeout.png` });
            return;
        }

        // 确保外层容器具备可聚焦能力（决策 454 兜底）
        await page.evaluate(() => {
            const out = document.getElementById('out');
            if (out && out.tabIndex !== -1) out.tabIndex = -1;
        });

        // 首帧排版稳定检查
        await until(async () => {
            const d = await page.evaluate(() => {
                const c = document.querySelector('canvas');
                return c && c.width > 0 && Math.abs(c.width - c.clientWidth * 2) <= 2;
            });
            return Boolean(d);
        }, 10000, 200);

        const dims = await page.evaluate(() => {
            const c = document.querySelector('canvas');
            return { w: c.width, h: c.height, cw: c.clientWidth, ch: c.clientHeight, dpr: window.devicePixelRatio };
        });
        check('DPR 2 画布像素 = CSS × 2', dims.dpr === 2 && Math.abs(dims.w - dims.cw * 2) <= 2 && Math.abs(dims.h - dims.ch * 2) <= 2, JSON.stringify(dims));

        const shot = `${OUT}/smoke-render.png`;
        await page.screenshot({ path: shot });

        // 非纯色判定：采样 64x64 画布不同色值数（排除空白或纯色崩塌）
        const distinct = await page.evaluate(async () => {
            const c = document.querySelector('canvas');
            const off = document.createElement('canvas'); off.width = 64; off.height = 64;
            const ctx = off.getContext('2d'); ctx.drawImage(c, 0, 0, 64, 64);
            const d = ctx.getImageData(0, 0, 64, 64).data;
            const set = new Set();
            for (let i = 0; i < d.length; i += 4) {
                set.add(((d[i] >> 4) << 8) | ((d[i + 1] >> 4) << 4) | (d[i + 2] >> 4));
            }
            return set.size;
        });
        check('画布有实际内容（非纯色）', distinct >= 3, `distinct colors=${distinct}`);

        // CDP 会话与输入桥聚焦探针
        const cdp = await page.target().createCDPSession();
        const bridgeFocused = () => page.evaluate(`(() => {
            const e = document.querySelector('.avalonia-input-element');
            return !!e && document.activeElement === e && getComputedStyle(e).display !== 'none';
        })()`);
        // 先用公开快捷键聚焦，再从原生输入桥读取光标位置；不探点起手式。
        let composerCoord = { x: 732, y: 380 };
        const focusComposer = async () => {
            await page.keyboard.down('Control');
            await page.keyboard.press('l');
            await page.keyboard.up('Control');
            await sleep(150);
            if (!await bridgeFocused()) return false;
            const caret = await page.evaluate(() => {
                const e = document.querySelector('.avalonia-input-element');
                const r = e.getBoundingClientRect();
                return { x: r.left + Math.max(1, r.width / 2), y: r.top + Math.max(1, r.height / 2) };
            });
            composerCoord = caret;
            return true;
        };

        await focusComposer();
        await page.screenshot({ path: `${OUT}/smoke-insert.png` });

        // ---- 阶段 2：预置凭据 → 认证态交互全链路 ----
        const token = process.env.WX_SMOKE_TOKEN || '';
        const instance = process.env.WX_SMOKE_INSTANCE || '';

        if (token && instance) {
            const wsUrl = BASE.replace(/^http/, 'ws').replace(/\/$/, '') + '/ws';
            const visits = () => page.evaluate(() => Number(localStorage.getItem('wanxiang.visits') || 0));
            const visitsBefore = await visits();

            const seeded = await page.evaluate((args) => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang', 1);
                req.onupgradeneeded = () => {
                    try { req.result.createObjectStore('connections', { keyPath: 'instanceId' }); } catch (_) {}
                };
                req.onsuccess = () => {
                    const db = req.result;
                    try {
                        const tx = db.transaction('connections', 'readwrite');
                        tx.objectStore('connections').put({
                            instanceId: args.instance,
                            url: args.wsUrl,
                            token: args.token,
                            name: 'PWA',
                            updatedAt: Date.now()
                        });
                        tx.oncomplete = () => resolve(true);
                        tx.onerror = () => resolve(false);
                    } catch (_) { resolve(false); }
                };
                req.onerror = () => resolve(false);
            }), { instance, wsUrl, token });
            check('预置客户端凭据', seeded);

            const canvasUp = () => page.waitForFunction(() => {
                const c = document.querySelector('canvas');
                return c && c.width > 0 && document.querySelector('.avalonia-input-element');
            }, { timeout: 120000, polling: 250 }).then(() => true, () => false);

            const draftText = (want) => page.evaluate(`(() => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang-drafts', 1);
                req.onupgradeneeded = () => { try { req.result.createObjectStore('drafts'); } catch (_) {} };
                req.onsuccess = () => {
                    const db = req.result;
                    try {
                        const g = db.transaction('drafts', 'readonly').objectStore('drafts').get('snapshot');
                        g.onsuccess = () => resolve((g.result || '').indexOf(${JSON.stringify(want)}) >= 0);
                        g.onerror = () => resolve(false);
                    } catch (_) { resolve(false); }
                };
                req.onerror = () => resolve(false);
            }))()`);

            const readDraftRaw = () => page.evaluate(`(() => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang-drafts', 1);
                req.onsuccess = () => {
                    try {
                        const g = req.result.transaction('drafts', 'readonly').objectStore('drafts').get('snapshot');
                        g.onsuccess = () => resolve(typeof g.result === 'string' ? g.result : '');
                        g.onerror = () => resolve('');
                    } catch (_) { resolve(''); }
                };
                req.onerror = () => resolve('');
            }))()`);

            // 重载进入已认证主界面
            await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
            const back = await canvasUp();
            const visitsAfter = await visits();
            await page.evaluate(() => {
                const out = document.getElementById('out');
                if (out && out.tabIndex !== -1) out.tabIndex = -1;
            });

            // 认证态：等待连上主机、加载会话列表并完成焦点就绪
            const focusedNow = await until(() => focusComposer(), 20000, 300);
            check('认证态：输入框能被点聚焦', Boolean(focusedNow), `coord=(${composerCoord.x},${composerCoord.y})`);

            // ---- 阶段 3：真实键盘物理按键输入与组合输入上屏 ----
            // A. 通过 CDP 输入法桥输入中文前缀
            await cdp.send('Input.insertText', { text: '万象' });
            await sleep(200);
            await cdp.send('Input.insertText', { text: 'ok' });
            await sleep(200);

            // B. 通过真实键盘物理击键输入 ASCII 分隔与字符（触发原生 keydown / textinput 路径）
            await page.keyboard.type('｜键盘');
            await sleep(200);

            // C. 追加草稿后缀
            await cdp.send('Input.insertText', { text: '草稿往返' });

            const savedToDb = await until(() => draftText('草稿往返'), 20000, 300);
            const draftRaw = await readDraftRaw();
            const EXPECTED = '万象ok｜键盘草稿往返';
            const typedCorrectly = draftRaw === EXPECTED;
            check('真实键盘输入与上屏桥：组合键与增量均精确落盘', typedCorrectly,
                `draft=${JSON.stringify(draftRaw.slice(0, 160))} expected=${JSON.stringify(EXPECTED)}`);
            await page.screenshot({ path: `${OUT}/smoke-draft.png` });

            // ---- 弹出层（ToolTip / 上下文菜单 / 输入框右键菜单）的像素检查 ----
            // avalonia.js 没有可用的 DOM 无障碍树，弹出层也没有 DOM 节点，
            // 所以判据只能是**像素**：开/关弹出层各截一张合成器画面，求差得到弹出层的
            // 外接矩形，再在这块矩形里统计字形笔画与识别文字。
            //
            // WebGL 画布在未开启 preserveDrawingBuffer 时，drawImage / toDataURL 读到的是
            // 陈旧帧或清空后的缓冲；统一用 page.screenshot 从浏览器合成器取真实帧。
            const GRID_W = 240, GRID_H = 160, CELL = 10; // 网格覆盖整张画布，1 格 = 10 设备像素
            const snapScreenB64 = () => page.screenshot({ encoding: 'base64' });
            const snapAll = async () => {
                const b64 = await snapScreenB64();
                const grid = await page.evaluate(async (pngB64) => {
                    const img = new Image();
                    img.src = 'data:image/png;base64,' + pngB64;
                    await img.decode();
                    const off = document.createElement('canvas');
                    off.width = 240; off.height = 160;
                    const ctx = off.getContext('2d');
                    ctx.drawImage(img, 0, 0, 240, 160);
                    return Array.from(ctx.getImageData(0, 0, 240, 160).data);
                }, b64);
                return [grid];
            };
            // 按连通分量（邻域半径 3 格、颜色差 > 8）找变化像素最多的区域外接矩形：
            // 浅色主题下主背景 #F5F4ED 与浮层背景 #FFFFFF 差值约 10，阈值 > 8 能把浮层底板
            // 识别为实心连通块；按连通分量聚类可隔离远处孤立的光标闪烁或旧悬停消退像素，
            // 避免单一全局 min/max 把两处不相连的小变化拉成全屏矩形。
            const diffBbox = (a, b) => {
                if (!a || !b) return null;
                const total = GRID_W * GRID_H;
                const changed = new Uint8Array(total);
                for (let p = 0; p < total; p++) {
                    const i = p * 4;
                    if (Math.abs(a[i] - b[i]) > 8 || Math.abs(a[i + 1] - b[i + 1]) > 8 || Math.abs(a[i + 2] - b[i + 2]) > 8) {
                        changed[p] = 1;
                    }
                }
                const vis = new Uint8Array(total);
                let best = null;
                for (let p = 0; p < total; p++) {
                    if (!changed[p] || vis[p]) continue;
                    const q = [p];
                    vis[p] = 1;
                    let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1, mass = 0;
                    while (q.length > 0) {
                        const cur = q.pop();
                        const cx = cur % GRID_W, cy = (cur / GRID_W) | 0;
                        mass++;
                        if (cx < x0) x0 = cx; if (cx > x1) x1 = cx;
                        if (cy < y0) y0 = cy; if (cy > y1) y1 = cy;
                        for (let dy = -3; dy <= 3; dy++) {
                            const ny = cy + dy;
                            if (ny < 0 || ny >= GRID_H) continue;
                            for (let dx = -3; dx <= 3; dx++) {
                                const nx = cx + dx;
                                if (nx < 0 || nx >= GRID_W) continue;
                                const np = ny * GRID_W + nx;
                                if (changed[np] && !vis[np]) {
                                    vis[np] = 1;
                                    q.push(np);
                                }
                            }
                        }
                    }
                    if (mass >= 4 && (!best || mass > best.mass)) {
                        best = { mass, x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
                    }
                }
                return best ? { x: best.x, y: best.y, w: best.w, h: best.h } : null;
            };
            const opaqueBbox = (a) => {
                if (!a) return null;
                let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
                for (let i = 0; i < a.length; i += 4) {
                    if (a[i + 3] > 24) {
                        const p = i / 4, x = p % GRID_W, y = (p / GRID_W) | 0;
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
                }
                return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
            };
            const popupRegion = async (before) => {
                const after = await snapAll();
                let best = null;
                for (let i = 0; i < after.length; i++) {
                    const bb = i < before.length ? diffBbox(before[i], after[i]) : opaqueBbox(after[i]);
                    if (!bb) continue;
                    const area = bb.w * bb.h;
                    if (!best || area > best.bb.w * best.bb.h) best = { canvas: i, bb };
                }
                return best;
            };
            // 在弹出层矩形里统计笔画：背景取众数色，其余像素算笔画。
            const regionStats = async (canvasIdx, bb) => {
                const b64 = await snapScreenB64();
                return page.evaluate(async (args) => {
                    const img = new Image();
                    img.src = 'data:image/png;base64,' + args.b64;
                    await img.decode();
                    const cell = 10;
                    // 内缩（去掉弹出层描边）不能吃掉小块：按尺寸自适应。
                    const shrink = Math.max(0, Math.min(2, Math.floor(Math.min(args.bb.w, args.bb.h) / 3)));
                    const sx = (args.bb.x + shrink) * cell, sy = (args.bb.y + shrink) * cell;
                    const sw = Math.max(1, (args.bb.w - shrink * 2) * cell), sh = Math.max(1, (args.bb.h - shrink * 2) * cell);
                    const off = document.createElement('canvas');
                    off.width = sw; off.height = sh;
                    const ctx = off.getContext('2d');
                    ctx.drawImage(img, sx, sy, sw, sh, 0, 0, sw, sh);
                    const d = ctx.getImageData(0, 0, sw, sh).data;
                    const counts = new Map();
                    for (let i = 0; i < d.length; i += 4) {
                        const k = ((d[i] >> 3) << 16) | ((d[i + 1] >> 3) << 8) | (d[i + 2] >> 3);
                        const c = counts.get(k);
                        if (c) c.n++;
                        else counts.set(k, { n: 1, r: d[i], g: d[i + 1], b: d[i + 2] });
                    }
                    let bg = null;
                    for (const c of counts.values()) if (!bg || c.n > bg.n) bg = c;
                    const br = bg.r, bgc = bg.g, bb2 = bg.b;
                    const rowRuns = [];
                    const rowShapes = new Set();
                    let ink = 0;
                    for (let y = 0; y < sh; y++) {
                        let runs = 0, prev = false, shape = '';
                        for (let x = 0; x < sw; x++) {
                            const i = (y * sw + x) * 4;
                            const on = (Math.abs(d[i] - br) + Math.abs(d[i + 1] - bgc) + Math.abs(d[i + 2] - bb2)) > 60;
                            if (on) ink++;
                            if (on && !prev) runs++;
                            prev = on;
                            shape += on ? '1' : '0';
                        }
                        if (runs > 0) { rowRuns.push(runs); rowShapes.add(shape); }
                    }
                    const meanRuns = rowRuns.length ? rowRuns.reduce((a, b) => a + b, 0) / rowRuns.length : 0;
                    const glyphRows = rowRuns.filter(runs => runs >= 4);
                    const glyphMeanRuns = glyphRows.length
                        ? glyphRows.reduce((a, b) => a + b, 0) / glyphRows.length
                        : meanRuns;
                    return { w: sw, h: sh, ink, inkFrac: ink / (sw * sh), rows: rowRuns.length, meanRuns, glyphMeanRuns, distinctShapes: rowShapes.size };
                }, { b64, bb });
            };
            // 弹出层的截图（CSS 坐标裁剪，1 网格格 = 10 设备像素 = 5 CSS 像素）。
            const shotRegion = async (name, bb) => {
                const path = `${OUT}/popup-${name}.png`;
                await page.screenshot({
                    path,
                    clip: { x: bb.x * 5, y: bb.y * 5, width: bb.w * 5, height: bb.h * 5 },
                });
                return path;
            };
            // 弹出层验收：两帧差异证明出现，截图识别证明指定中文确实可读。
            // 像素统计只作诊断，不以不同长度文字的笔画数比值判定缺字。
            let bodyRef = null; // 正文中文的笔画基准：{ meanRuns, distinctShapes }
            let measuredBodyWord = null;
            const measureBodyChinese = async () => {
                if (!measuredBodyWord) return null;
                // 以已识别的正文为锚点，不假定消息在阅读列左半侧。
                const bb = {
                    x: Math.max(0, Math.floor(measuredBodyWord.left / 5) - 1),
                    y: Math.max(0, Math.floor(measuredBodyWord.top / 5) - 1),
                    w: Math.ceil(measuredBodyWord.width / 5) + 2,
                    h: Math.ceil(measuredBodyWord.height / 5) + 2,
                };
                const s = await regionStats(0, bb);
                if (!s || s.inkFrac <= 0.005) return null;
                return { meanRuns: s.glyphMeanRuns, distinctShapes: s.distinctShapes, inkFrac: s.inkFrac };
            };
            // 把弹出层区域放大导出成图片（每格 = 10 设备像素，相对设备像素放大 2 倍 = 相对 CSS 放大 4 倍；
            // 放大 4 倍会让字高超过 100px，超出 Tesseract LSTM 的字符尺度窗口导致把汉字拆成碎笔画）。
            const popupCrop = async (name, region) => {
                const ocrPath = `${OUT}/popup-${name}-ocr.png`;
                const b64 = await snapScreenB64();
                const image = await page.evaluate(async ({ b64, bb }) => {
                    const img = new Image();
                    img.src = 'data:image/png;base64,' + b64;
                    await img.decode();
                    const output = document.createElement('canvas');
                    output.width = bb.w * 20;
                    output.height = bb.h * 20;
                    const ctx = output.getContext('2d');
                    ctx.drawImage(img, bb.x * 10, bb.y * 10, bb.w * 10, bb.h * 10,
                        0, 0, output.width, output.height);
                    return output.toDataURL('image/png').split(',')[1];
                }, { b64, bb: region.bb });
                writeFileSync(ocrPath, Buffer.from(image, 'base64'));
                return ocrPath;
            };
            // 把一个弹出层区域放大后识别成文字。扫描阶段用它分辨「点到的是哪个菜单」，
            // 验收阶段用它证明指定中文可读。
            const popupText = async (name, region) => ocrText(await popupCrop(name, region), POPUP_ITEM_PSM);
            // 同一张裁剪图上的词元换算成 CSS 坐标：每格 = 10 设备像素 = 5 CSS 像素，
            // 裁剪图相对设备像素放大 2 倍（相对 CSS 放大 4 倍），所以 OCR 像素 ÷ 4 = CSS 位移。
            // 菜单标签与快捷键分列，按稀疏文本识别，避免强行整行分词破坏短中文标签。
            const popupWords = async (name, region) => {
                const path = await popupCrop(name, region);
                const { bb } = region;
                return runOcr(path, POPUP_ITEM_PSM).map((w) => ({
                    ...w,
                    left: bb.x * 5 + w.left / 4,
                    top: bb.y * 5 + w.top / 4,
                    width: w.width / 4,
                    height: w.height / 4,
                }));
            };
            const assertPopup = async (name, region, bb, recognized) => {
                if (!region) { check(`弹出层 ${name} 出现`, false, '开/关两帧没有差异区域'); return null; }
                const stats = await regionStats(region.canvas, region.bb);
                const path = await shotRegion(name, region.bb);
                check(`弹出层 ${name} 出现`, true, `canvas=${region.canvas} rect=${region.bb.w}x${region.bb.h} 截图=${path}`);
                if (!stats || stats.inkFrac <= 0.01) {
                    check(`弹出层 ${name} 中文文字可读`, false, '弹出层区域没有可见文字');
                    return stats;
                }
                const detail = `inkFrac=${stats.inkFrac.toFixed(3)}`;
                // 短标签与长正文的每行字数不同，不能用笔画总数比值判定缺字。
                // 直接读取当前截图的中文；空白或重复方框不能满足明确文字契约。
                const text = typeof recognized === 'string' ? recognized : await popupText(name, region);
                const readable = readablePopupText(name, text);
                check(`弹出层 ${name} 中文文字可读`, readable,
                    `识别=${JSON.stringify(text)} ${detail}`);
                return stats;
            };
            const closePopups = async () => {
                await page.mouse.click(5, 5);
                await page.keyboard.press('Escape');
                await sleep(400);
            };

            // ---- 阶段 4：右键菜单呼出与真实「粘贴」行为 ----
            // 1. 预置剪贴板唯一标记
            const CLIP_TOKEN = '【菜单粘贴验证' + Date.now() + '】';
            await page.evaluate((text) => navigator.clipboard.writeText(text), CLIP_TOKEN);
            await sleep(200);
            // 2. 右键点击输入框呼出 ContextFlyout（先左键落焦稳定边框，再截基准帧并右键同一点）
            await page.mouse.click(composerCoord.x, composerCoord.y);
            await sleep(250);
            const menuBefore = await snapAll();
            await page.mouse.click(composerCoord.x, composerCoord.y, { button: 'right' });
            await sleep(500);
            // 3. 截图供人工核验中文字形
            await page.screenshot({ path: `${OUT}/smoke-menu-font.png` });
            // 4. 真实物理点击菜单里的「粘贴」。
            //    菜单贴在光标处，位置随窗口与内容变化；写死坐标会点到菜单外的空白，
            //    把「没点到」误报成「菜单没反应」。这里复用同一套区域识别：
            //    开/关两帧求差拿到菜单外接矩形，在矩形里读出「粘贴」的包围盒再点它。
            const pasteRegion = await popupRegion(menuBefore);
            await assertPopup('flyout', pasteRegion, pasteRegion && pasteRegion.bb);
            let pastePoint = null;
            if (pasteRegion) {
                const items = await popupWords('menu-items', pasteRegion);
                const paste = findPhrase(items, '粘贴');
                if (paste) pastePoint = wordCenterCss(paste);
            }
            if (pastePoint) {
                await page.mouse.click(pastePoint.x, pastePoint.y);
            } else {
                check('菜单定位：在弹出菜单里识别到「粘贴」并得到可点坐标', false,
                    `菜单区=${pasteRegion ? JSON.stringify(pasteRegion.bb) : '两帧无差异'}`);
                // 没有坐标准确点击；保持后续步骤对空结果的容错。
                await page.keyboard.press('Escape');
            }
            await sleep(500);
            // 5. 验证剪贴板标记真实进入草稿
            const pasteOk = await until(async () => (await readDraftRaw()).includes(CLIP_TOKEN), 8000, 300);
            check('菜单真实行为后果：右键菜单点击「粘贴」后剪贴板标记入库', Boolean(pasteOk),
                pastePoint ? `点=(${pastePoint.x.toFixed(0)},${pastePoint.y.toFixed(0)})` : '未定位到「粘贴」');
            const draftAfterPaste = await readDraftRaw();
            // 6. 再次右键呼出并按 Esc 关闭
            await page.mouse.click(composerCoord.x, composerCoord.y, { button: 'right' });
            await sleep(400);
            await page.keyboard.press('Escape');
            await sleep(300);
            await focusComposer();
            const escRefocused = await until(async () => await bridgeFocused(), 5000, 200);
            await page.screenshot({ path: `${OUT}/smoke-esc-focus.png` });
            check('Esc 回焦：Esc 关闭菜单并恢复输入框焦点', Boolean(escRefocused));

            // ---- 阶段 5：命令面板真实交互（从产品 UI 入口进入）----
            // 使用已声明的快捷键，避免底部固定坐标误点其他操作。
            await page.keyboard.down('Control');
            await page.keyboard.press('k');
            await page.keyboard.up('Control');
            await sleep(600);
            await cdp.send('Input.insertText', { text: '切换主题' });
            await sleep(400);
            await page.screenshot({ path: `${OUT}/smoke-palette.png` });
            const getRecents = () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('wanxiang.local:palette-recent.json') || '[]'); } catch (_) { return []; } });
            const recentsBefore = await getRecents();
            await page.keyboard.press('Enter');
            const recentPersisted = await until(async () => { const list = await getRecents(); return Array.isArray(list) && list.includes('act:theme'); }, 8000, 250);
            check('命令面板真实交互后果：过滤并回车执行命令后写入最近使用持久化记录', Boolean(recentPersisted), `recentBefore=${JSON.stringify(recentsBefore)} recentAfter=${recentPersisted ? 'recorded' : 'failed'}`);

            // ---- 阶段 6：设置弹窗真实交互（从产品 UI 入口进入）----
            const getStoredPrefs = () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('wanxiang.local:ui.json') || '{}'); } catch (_) { return {}; } });
            // 未保存的字段按产品 UiPrefs.defaults 解释，不能把缺省值当作未知后跳过比较。
            const prefsBefore = {
                enterSends: true, autoCollapseReasoning: true, codeWrap: false,
                reduceMotion: false, showArchived: false,
                ...await getStoredPrefs(),
            };
            await page.keyboard.down('Control');
            await page.keyboard.press(',');
            await page.keyboard.up('Control');
            // 浏览器里的 Avalonia 键盘入口始终是隐藏 input，不能拿 DOM activeElement
            // 判断设置导航是否持焦。等打开后的布局稳定，再把方向键交给设置页。
            await sleep(900);
            // 分区会记住上次位置，先回到首项再定位外观，避免相对移动落到其他分区。
            await page.keyboard.press('Home');
            for (let i = 0; i < 4; i++) {
              await page.keyboard.press('ArrowDown');
              await sleep(200);
            }
            await page.screenshot({ path: `${OUT}/smoke-settings-appearance.png` });
            const settingsShot = `${OUT}/smoke-settings-appearance.png`;
            // 开关行按识别到的**行标题**定位，不写死 y：
            // 行高与字体度量随平台字体而变（macOS 系统字体与 Linux Noto 不同），
            // 固定坐标会点进两行之间的空隙，把「没点到」误判成「开关无反应」。
            // 截图是 CSS × DPR，识别坐标除以 DPR 换回 CSS。
            const shotDpr = await page.evaluate(() => window.devicePixelRatio || 1);
            const settingsWords = runOcr(settingsShot, OCR_LAYOUT_PSM).map((w) => ({
              ...w,
              text: w.text.replace(/\s+/g, ''),
              left: w.left / shotDpr,
              top: w.top / shotDpr,
              width: w.width / shotDpr,
              height: w.height / shotDpr,
            }));
            const switchRows = ['Enter直接发送', '完成后收起思考过程', '代码块长行折行', '在侧栏显示已归档对话', '减少动态效果'];
            const prefsChanged = async () => {
              const cur = await getStoredPrefs();
              for (const key of ['enterSends', 'autoCollapseReasoning', 'codeWrap', 'reduceMotion', 'showArchived']) {
                if (cur[key] !== undefined && prefsBefore[key] !== undefined && cur[key] !== prefsBefore[key]) {
                  return `${key}: ${prefsBefore[key]} -> ${cur[key]}`;
                }
              }
              return false;
            };
            let prefToggled = false, toggledKey = '';
            const prefMissed = [];
            for (const label of switchRows) {
              const hit = findPhrase(settingsWords, label);
              if (!hit) { prefMissed.push(`${label}:未识别`); continue; }
              // 点标题文字本身：标题在开关行的 Border 内，指针释放冒泡到行上完成切换。
              const at = { x: hit.left + Math.min(hit.width / 2, 60), y: hit.top + hit.height / 2 };
              // 一次点击不稳：整页 Render 会重排开关行，按下与释放若跨在被替换的实例上，
              // 行收不到成对的指针事件（旧口径只点一次、只等一次，抖一次就整条判失败）。
              // 同一点连点两次，每次各自等待持久化结果。
              for (let attempt = 0; attempt < 2 && !prefToggled; attempt++) {
                await page.mouse.click(at.x, at.y);
                const changed = await until(() => prefsChanged(), 5000, 200);
                if (changed) {
                  prefToggled = true;
                  toggledKey = changed;
                  // 复原刚切的那一项：后续阶段拿到的偏好与本轮开始时一致。
                  await page.mouse.click(at.x, at.y);
                  await until(async () => !(await prefsChanged()), 5000, 200);
                }
              }
              if (prefToggled) break;
              prefMissed.push(`${label}:@(${at.x.toFixed(0)},${at.y.toFixed(0)})点了未变`);
            }
            check('设置弹窗真实交互后果：操作外观开关并观察到 localStorage 持久化变更', Boolean(prefToggled),
              prefToggled ? `toggled=${toggledKey}` : `未切换；${prefMissed.join('; ')}`);
            await page.keyboard.press('Escape');
            await sleep(400);
            const composerBack = await until(() => focusComposer(), 5000, 200);
            check('设置弹窗 Esc 回焦：关闭设置大面板后安全恢复工作区与输入框', Boolean(composerBack));

            // ---- 阶段 7：主题三态轮转与物理亮度对比 ----
            const getCanvasLuminance = () => page.evaluate(() => {
                const c = document.querySelector('canvas');
                if (!c) return 255;
                const off = document.createElement('canvas'); off.width = 32; off.height = 32;
                const ctx = off.getContext('2d'); ctx.drawImage(c, 0, 0, 32, 32);
                const d = ctx.getImageData(0, 0, 32, 32).data;
                let sum = 0;
                for (let i = 0; i < d.length; i += 4) {
                    sum += (d[i] * 0.299 + d[i+1] * 0.587 + d[i+2] * 0.114);
                }
                return sum / (32 * 32);
            });

            const themeBefore = (await getStoredPrefs()).theme || 'system';
            await focusComposer();

            // 触发 Ctrl+Shift+S
            await page.keyboard.down('Control');
            await page.keyboard.down('Shift');
            await page.keyboard.press('KeyS');
            await page.keyboard.up('Shift');
            await page.keyboard.up('Control');

            let themeNow = await until(async () => {
                const t = (await getStoredPrefs()).theme;
                return t !== themeBefore ? t : false;
            }, 6000, 200);

            if (themeNow !== 'dark') {
                await page.keyboard.down('Control');
                await page.keyboard.down('Shift');
                await page.keyboard.press('KeyS');
                await page.keyboard.up('Shift');
                await page.keyboard.up('Control');
                themeNow = await until(async () => {
                    const t = (await getStoredPrefs()).theme;
                    return t === 'dark' ? t : false;
                }, 6000, 200);
            }

            // 辅助检查画布是否正常有内容（杜绝崩溃到全黑纯色伪造通过）
            const checkCanvasAlive = () => page.evaluate(() => {
                const c = document.querySelector('canvas');
                if (!c) return { alive: false, distinct: 0, sum: 0 };
                const off = document.createElement('canvas'); off.width = 64; off.height = 64;
                const ctx = off.getContext('2d'); ctx.drawImage(c, 0, 0, 64, 64);
                const d = ctx.getImageData(0, 0, 64, 64).data;
                const set = new Set();
                let sum = 0;
                for (let i = 0; i < d.length; i += 4) {
                    sum += d[i] + d[i+1] + d[i+2];
                    set.add(((d[i] >> 4) << 8) | ((d[i + 1] >> 4) << 4) | (d[i + 2] >> 4));
                }
                return { alive: set.size >= 3 && sum > 1000, distinct: set.size, sum };
            });
            const darkLuminance = await until(async () => {
                const lum = await getCanvasLuminance();
                const alive = await checkCanvasAlive();
                return (lum < 100 && alive.alive && errors.length === 0) ? lum : false;
            }, 6000, 200);

            await page.screenshot({ path: `${OUT}/smoke-theme-dark.png` });

            // 切回浅色
            await page.keyboard.down('Control');
            await page.keyboard.down('Shift');
            await page.keyboard.press('KeyS');
            await page.keyboard.up('Shift');
            await page.keyboard.up('Control');

            const lightThemeNow = await until(async () => {
                const t = (await getStoredPrefs()).theme;
                return t !== 'dark' ? t : false;
            }, 6000, 200);

            const lightLuminance = await until(async () => {
                const lum = await getCanvasLuminance();
                return lum > 140 ? lum : false;
            }, 6000, 200);

            await page.screenshot({ path: `${OUT}/smoke-theme-light.png` });

            check('主题切换：Ctrl+Shift+S 触发主题轮转并持久化到 localStorage',
                Boolean(themeNow === 'dark' && lightThemeNow),
                `initial=${themeBefore} -> dark -> ${lightThemeNow}`);
            const darkAlive = await checkCanvasAlive();
            check('主题切换物理调色板变动：深色模式平均亮度明显低于浅色（非纯黑死锁、语义与对比度待视觉走查）',
                Boolean(darkLuminance !== false && lightLuminance !== false && darkAlive.alive),
                `darkLum=${typeof darkLuminance === 'number' ? darkLuminance.toFixed(1) : darkLuminance} lightLum=${typeof lightLuminance === 'number' ? lightLuminance.toFixed(1) : lightLuminance}`);

            // ---- 阶段 8：重开页面恢复草稿验证 ----
            // 菜单粘贴已修改草稿；恢复必须保留重开前的完整文本，而非粘贴前的旧值。
            const draftBeforeReload = await readDraftRaw();
            check('面板与设置操作不改写草稿',
                Boolean(pasteOk) && draftBeforeReload === draftAfterPaste,
                `draft=${JSON.stringify(draftBeforeReload.slice(0, 160))}`);
            await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
            await canvasUp();
            await page.evaluate(() => {
                const out = document.getElementById('out');
                if (out && out.tabIndex !== -1) out.tabIndex = -1;
            });

            const focusBack = await until(() => focusComposer(), 20000, 300);
            // 光标位置不能依赖「点空白处=落到文字末尾」：不同平台字体宽度不同，
            // 点击点可能落在文字中间，后缀就会插进正文。聚焦后先按 End（无修饰键，
            // 不与 Ctrl+End 的滚动快捷键冲突）把光标显式移到末尾，再做追加。
            await page.keyboard.press('End');
            await sleep(200);
            const SUFFIX = '｜恢复验证后缀';
            await cdp.send('Input.insertText', { text: SUFFIX });
            await sleep(1000);

            const restoredRaw = await until(async () => {
                const raw = await readDraftRaw();
                return raw.includes(SUFFIX) ? raw : false;
            }, 15000, 300);

            const EXPECTED_AFTER = draftBeforeReload + SUFFIX;
            const restored = focusBack && restoredRaw === EXPECTED_AFTER;
            await page.screenshot({ path: `${OUT}/smoke-restored.png` });

            const visitsThird = await visits();

            check('localStorage：重开页面后访问计数继续递增',
                visitsBefore >= 1 && visitsAfter === visitsBefore + 1 && visitsThird === visitsAfter + 1,
                `visits ${visitsBefore} -> ${visitsAfter} -> ${visitsThird}`);
            check('认证态：草稿落进 IndexedDB', back && savedToDb, `back=${back} saved=${savedToDb}`);
            // 「支持追加」：SUFFIX 是重开后**追加**的，草稿 = 旧文 + 后缀精确相等，
            // 证明重开恢复的不是空输入框而是可继续编辑的草稿。
            check('认证态：重开页面草稿回到输入框并支持追加', restored,
                `draft=${JSON.stringify((restoredRaw || '').slice(0, 160))} expected=${JSON.stringify(EXPECTED_AFTER)}`);


            // (0) 先打开种子对话（主内容区出现中文正文）。
            //     判据不能只看「主内容区有像素变化」：侧栏点击会让选中态高亮变化，
            //     空态页面同样有差异，于是「没打开对话」会被判成打开。这里用截图的
            //     文字包围盒定位侧栏里的对话标题并点它，再用正文文字确认已打开。
            const shotPath = async (name) => {
                const p = `${OUT}/${name}.png`;
                await page.screenshot({ path: p });
                return p;
            };
            // 定位用的识别统一走这里：可选裁出目标区域，再放大导出、识别。
            // 返回的包围盒始终是**整张画布的 CSS 坐标**：裁剪区左上角 + OCR 像素 ÷ 放大倍数。
            //
            // 默认不裁剪，用 psm 4（OCR_LAYOUT_PSM）做版面切分。`region` 留给
            // 确实需要限定搜索范围的场景（例如只在侧栏里找），放大倍数由区域宽度决定，
            // 保证 OCR 看到的宽度不小于 OCR_UPSCALE_WIDTH。
            const ocrShot = async (name, pageSegmentation = OCR_LAYOUT_PSM, region = null) => {
                const p = `${OUT}/${name}-ocr.png`;
                const area = region || { x: 0, y: 0, w: null, h: null };
                const upscale = area.w ? Math.max(1, Math.min(4, Math.round(OCR_UPSCALE_WIDTH / area.w)))
                    : OCR_SCALE_BASE;
                const fullB64 = await snapScreenB64();
                const shot = await page.evaluate(async ({ fullB64, area, upscale }) => {
                    const c = document.querySelector('canvas');
                    if (!c) return null;
                    const img = new Image();
                    img.src = 'data:image/png;base64,' + fullB64;
                    await img.decode();
                    const cssW = c.clientWidth, cssH = c.clientHeight;
                    const x = area.w ? area.x : 0;
                    const y = area.h ? area.y : 0;
                    const w = area.w || cssW;
                    const h = area.h || cssH;
                    const out = document.createElement('canvas');
                    out.width = Math.max(1, Math.round(w * upscale));
                    out.height = Math.max(1, Math.round(h * upscale));
                    // 合成器截图位图是 CSS × DPR，按 CSS 坐标乘 DPR 取源区域。
                    const dpr = img.width / cssW;
                    out.getContext('2d').drawImage(
                        img, Math.round(x * dpr), Math.round(y * dpr), Math.round(w * dpr), Math.round(h * dpr),
                        0, 0, out.width, out.height);
                    return { b64: out.toDataURL('image/png').split(',')[1], x, y, upscale };
                }, { fullB64, area, upscale });
                if (!shot || !shot.b64) return [];
                writeFileSync(p, Buffer.from(shot.b64, 'base64'));
                // 裁剪区的左上角加上 OCR 像素 ÷ 放大倍数，就是整张图的 CSS 坐标。
                // 界面包含彼此分离的标签，不是一个连续文字段落。
                const words = runOcr(p, pageSegmentation).map((w) => ({
                    ...w,
                    left: shot.x + w.left / shot.upscale,
                    top: shot.y + w.top / shot.upscale,
                    width: w.width / shot.upscale,
                    height: w.height / shot.upscale,
                }));
                // 保存原始识别事实，失败时能区分误点、未加载与切词差异。
                writeFileSync(`${OUT}/${name}-ocr.json`, JSON.stringify(words, null, 2));
                return words;
            };
            await shotPath('smoke-before-popups');
            const sidebarWords = await ocrShot('smoke-sidebar');
            const seedTitle = findPhrase(sidebarWords, SEED_TITLE);
            let opened = false;
            let openedShot = `${OUT}/smoke-opened.png`;
            if (seedTitle) {
                const pt = wordCenterCss(seedTitle);
                await page.mouse.click(pt.x, pt.y);
                // CI 的加载速度不固定；只点击一次，等待主内容区确实出现正文。
                // 侧栏预览也有「你好」，不能把它作为打开成功的证据。
                const body = await until(async () => {
                    const openedWords = await ocrShot('smoke-opened');
                    return findPhraseInMain(openedWords, '你好');
                }, 15000, 500);
                opened = Boolean(body);
                measuredBodyWord = body || null;
                check('侧栏点「中文冒烟」后主内容区出现该对话正文', opened,
                    `标题包围盒=${JSON.stringify(seedTitle)} 正文=${JSON.stringify(body ? body.text : '')}`);
            } else {
                check('侧栏点「中文冒烟」后主内容区出现该对话正文', false, '截图里没有识别到侧栏对话标题');
            }
            // 正文的语义判据已由上面的文字识别给出；这里的像素基准只作诊断对照。
            bodyRef = await measureBodyChinese();
            check('界面正文有可测的中文（自校准基准）', !!bodyRef,
                bodyRef ? `meanRuns=${bodyRef.meanRuns.toFixed(2)} distinctShapes=${bodyRef.distinctShapes} inkFrac=${bodyRef.inkFrac.toFixed(3)}` : '主内容区没有测到文字');

            // (1) ToolTip：悬停顶栏的对话标题，提示是该标题的全文。
            //     原先悬停「新建对话」按钮：那个按钮在 react-ui 对齐后只剩图标「+」，
            //     静态截图里没有「新建对话」四个字，OCR 定位不到（读成「十」且置信度不稳）。
            //     顶栏标题既有可见中文、又挂了同文案的提示；标题是 TextBlock，
            //     没有悬停底色，所以开/关两帧的唯一差异就是提示条本身。
            //     顶栏标题左侧紧挨折叠侧栏图标（x=280..320），整页 psm 4 会把图标与标题混读；
            //     单独裁出标题区（CSS x=320, y=8, w=100, h=32）按 psm 6 放大识别。
            let tipFound = null;
            // 先把鼠标移到主内容区空白处，让侧栏点击残留的悬停态和提示条完全消退。
            await page.mouse.move(500, 200);
            await sleep(300);
            const tipAnchor = findPhrase(
                await ocrShot('smoke-tooltip-anchor', '6', { x: 320, y: 8, w: 100, h: 32 }),
                SEED_TITLE);
            if (tipAnchor) {
                const pt = wordCenterCss(tipAnchor);
                // titleAction 是 Stretch 宽度的 ghost Button，移上去会立即产生 pointerover 底色，
                // 约 400ms 后才弹出 ToolTip。先移上去等 120ms 让悬停底色稳定再截 before，
                // 这样 before 与 after 的唯一差异就是提示条本身。
                await page.mouse.move(pt.x, pt.y);
                await sleep(120);
                const before = await snapAll();
                await sleep(1200);
                const region = await popupRegion(before);
                // 提示条是扁矩形：比高宽得多，且不占满整屏。具体尺寸随文案与字体变，
                // 不用写死数字阈值。
                if (region && region.bb.w > region.bb.h * 1.2 && region.bb.w < 120) tipFound = region;
                check('悬停对话标题出现提示条', Boolean(tipFound),
                    `标题包围盒=${JSON.stringify(tipAnchor)} 提示区=${region ? `canvas=${region.canvas} ${region.bb.w}x${region.bb.h}` : '无差异'}`);
            } else {
                check('悬停对话标题出现提示条', false, '顶栏没识别到对话标题');
            }
            await assertPopup('tooltip', tipFound, tipFound && tipFound.bb);
            await closePopups();

            // (2) 上下文菜单：在消息卡上右键。
            //     消息卡的位置随窗口与内容变化，写死坐标或盲扫网格都会点到别的控件
            //     （输入区开出的是 Fluent 的「剪切/复制/粘贴」文本浮层，侧栏会话行开出的是
            //     会话菜单，两者同样有差异区域，却不是消息卡菜单）。这里用**主内容区**里
            //     正文文字的包围盒定位那张卡，再在它身上右键。
            const bodyWords = await ocrShot('smoke-contextmenu-anchor');
            const bodyWord =
                findPhraseInMain(bodyWords, '你好') || findPhraseInMain(bodyWords, '世界');
            let menuRegion = null, menuText = null;
            if (bodyWord) {
                const pt = wordCenterCss(bodyWord);
                const before = await snapAll();
                await page.mouse.click(pt.x, pt.y, { button: 'right' });
                await sleep(700);
                const region = await popupRegion(before);
                if (region && region.bb.h >= 6) {
                    menuText = await popupText('contextmenu', region);
                    if (isTextFlyoutText(menuText)) {
                        check('消息卡右键开出的是消息卡菜单（不是输入框文本浮层）', false,
                            `识别=${JSON.stringify(menuText)} 右键点=(${pt.x.toFixed(0)},${pt.y.toFixed(0)})`);
                        menuText = null;
                    } else {
                        menuRegion = region;
                    }
                }
            } else {
                check('消息卡右键开出的是消息卡菜单（不是输入框文本浮层）', false, '正文卡片位置未识别到');
            }
            // 先验收再关闭：closePopups 会把菜单关掉，之后截图就是空的。
            await assertPopup('contextmenu', menuRegion, menuRegion && menuRegion.bb, menuText);
            await closePopups();

            // 文本浮层已在真实右键粘贴步骤中验收出现、中文与行为后果。
        }
        if (skipped > 0) console.log(`SKIP 小计 ${skipped} 条（环境限制，不计入失败）`);
        check('无未捕获页面异常', errors.length === 0, errors.slice(0, 3).join(' | '));
    } finally {
        await browser.close();
    }

    process.exit(failed ? 1 : 0);
})().catch((e) => {
    console.error(e);
    process.exit(1);
});
