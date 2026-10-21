#!/usr/bin/env node
// 旧仓库决策 411：对「发布产物」做 PWA 冒烟（迁自旧仓库 tools/smoke_published_pwa.js，检查逻辑未改；
// 不是 bin/ 里的开发构建）。headless Chrome、DPR 2：
//   1) 页面渲染出 Avalonia 画布（非空白、无未捕获异常）；
//   2) 画布像素尺寸 = CSS 尺寸 × 2（决策 400 的 DPR 补丁确实在产物里生效）；
//   3) 文本上屏桥：CDP Input.insertText 进输入框只落一次（决策 391）。
// 用法：node tools/smoke_published_pwa.js --base http://127.0.0.1:<port> [--out <dir>] [--chrome /usr/bin/google-chrome]
const puppeteer = require('puppeteer-core');
const { mkdirSync } = require('node:fs');
const argv = process.argv.slice(2);
const arg = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 && argv[i + 1] ? argv[i + 1] : d; };
const BASE = arg('base', '');
const OUT = arg('out', '/tmp/wanxiang-smoke');
const CHROME = arg('chrome', process.env.CHROME_PATH || '/usr/bin/google-chrome');
if (!BASE) { console.error('--base required'); process.exit(2); }
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let failed = 0;
let skipped = 0;
const check = (name, ok, detail) => { if (!ok) failed++; console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`); };
// SKIP 不算失败：用于「这个环境本身渲染不出中文」的情形（如本机 headless Chrome 的字体限制），
// 判据要诚实，不能把环境缺陷算成产品缺陷，也不能用 SKIP 掩盖真缺陷。
const checkSkip = (name, reason, detail) => { skipped++; console.log(`SKIP ${name} — ${reason}${detail ? ' — ' + detail : ''}`); };
(async () => {
    const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--no-sandbox', '--disable-dev-shm-usage', '--enable-unsafe-swiftshader'] });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', (e) => errors.push(String(e)));
        await page.setViewport({ width: 1200, height: 800, deviceScaleFactor: 2 });
        await page.goto(BASE, { waitUntil: 'domcontentloaded', timeout: 60000 });
        // 等 Avalonia 画布出现并有尺寸（事件/条件等待，上限 120 s）。
        const canvasReady = await page.waitForFunction(() => {
            const c = document.querySelector('canvas');
            return c && c.width > 0 && c.clientWidth > 0 && document.querySelector('.avalonia-input-element');
        }, { timeout: 120000, polling: 250 }).then(() => true, () => false);
        check('Avalonia 画布与输入桥元素出现', canvasReady);
        if (!canvasReady) { await page.screenshot({ path: `${OUT}/smoke-timeout.png` }); return; }
        // 首帧之后再量（给渲染一点时间）。
        await sleep(3000);
        const dims = await page.evaluate(() => {
            const c = document.querySelector('canvas');
            return { w: c.width, h: c.height, cw: c.clientWidth, ch: c.clientHeight, dpr: window.devicePixelRatio };
        });
        check('DPR 2 画布像素 = CSS × 2', dims.dpr === 2 && Math.abs(dims.w - dims.cw * 2) <= 2 && Math.abs(dims.h - dims.ch * 2) <= 2, JSON.stringify(dims));
        const shot = `${OUT}/smoke-render.png`;
        await page.screenshot({ path: shot });
        // 非空白：截图里至少有两种以上明显不同的颜色（画布截取像素统计）。
        const distinct = await page.evaluate(async () => {
            const c = document.querySelector('canvas');
            const off = document.createElement('canvas'); off.width = 64; off.height = 64;
            const ctx = off.getContext('2d'); ctx.drawImage(c, 0, 0, 64, 64);
            const d = ctx.getImageData(0, 0, 64, 64).data; const set = new Set();
            for (let i = 0; i < d.length; i += 4) set.add(((d[i] >> 4) << 8) | ((d[i + 1] >> 4) << 4) | (d[i + 2] >> 4));
            return set.size;
        });
        check('画布有实际内容（非纯色）', distinct >= 3, `distinct colors=${distinct}`);
        // 文本上屏桥：先把输入框**点聚焦**，再 insertText。
        //
        // 之前写死了点击坐标 (738, 720)——那是输入区**下方**的工具栏，
        // 点不到输入框，Avalonia 的输入桥（.avalonia-input-element，display:none 的隐藏元素）
        // 不会变成可见/聚焦，文本自然落不进去。
        // 现在改成**扫描**：沿输入区那一带找能让输入桥聚焦的点，再在那个点上打字。
        const cdp = await page.target().createCDPSession();
        // 输入桥是否已聚焦——**所有 DOM 访问都在 page.evaluate 里**（Node 侧没有 document）。
        const bridgeFocused = () => page.evaluate(`(() => {
            const e = document.querySelector('.avalonia-input-element');
            return !!e && document.activeElement === e && getComputedStyle(e).display !== 'none';
        })()`);
        // 输入区在主内容区中下部（左侧栏右边、工具栏上面）。
        // 记下真正聚焦的那个点：弹出层（输入框右键菜单）要右键到**同一个点**上。
        let focusPoint = { x: 732, y: 400 };
        const focusComposer = async () => {
            if (await bridgeFocused()) return true;
            for (const y of [400, 380, 420, 360, 440, 340, 460, 320]) {
                for (const x of [732, 600, 860, 450, 1000]) {
                    await page.mouse.click(x, y);
                    await sleep(120);
                    if (await bridgeFocused()) { focusPoint = { x, y }; return true; }
                }
            }
            return await bridgeFocused();
        };
        const gotFocus = await focusComposer();
        // 未认证时输入区是禁用的，聚焦不上是**正确行为**——所以这里不断言。
        // 真正的文本上屏断言在下面认证态那一段（那时 composer 可用）。
        await page.screenshot({ path: `${OUT}/smoke-insert.png` });

        // ---- 第二段（可选）：预置凭据 → 认证态 → 草稿往返（IndexedDB 落盘 + 重开恢复）----
        // 发布产物冒烟里由 tools/smoke_published.sh 把 WX_SMOKE_TOKEN/INSTANCE 传进来（配置里已登记同一哈希）。
        const token = process.env.WX_SMOKE_TOKEN || '';
        const instance = process.env.WX_SMOKE_INSTANCE || '';
        if (token && instance) {
            const wsUrl = BASE.replace(/^http/, 'ws').replace(/\/$/, '') + '/ws';
            const visits = () => page.evaluate(() => Number(localStorage.getItem('wanxiang.visits') || 0));
            const visitsBefore = await visits();
            const seeded = await page.evaluate((args) => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang', 1);
                req.onupgradeneeded = () => { try { req.result.createObjectStore('connections', { keyPath: 'instanceId' }); } catch (e) { } };
                req.onsuccess = () => {
                    const db = req.result;
                    try {
                        const tx = db.transaction('connections', 'readwrite');
                        tx.objectStore('connections').put({ instanceId: args.instance, url: args.wsUrl, token: args.token, name: 'PWA', updatedAt: Date.now() });
                        tx.oncomplete = () => resolve(true);
                        tx.onerror = () => resolve(false);
                    } catch (e) { resolve(false); }
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
                req.onupgradeneeded = () => { try { req.result.createObjectStore('drafts'); } catch (e) { } };
                req.onsuccess = () => {
                    const db = req.result;
                    try {
                        const g = db.transaction('drafts', 'readonly').objectStore('drafts').get('snapshot');
                        g.onsuccess = () => resolve((g.result || '').indexOf(${JSON.stringify(want)}) >= 0);
                        g.onerror = () => resolve(false);
                    } catch (e) { resolve(false); }
                };
                req.onerror = () => resolve(false);
            }))()`);
            const inputIs = (want) => page.evaluate((w) => document.querySelector('.avalonia-input-element').value === w, want);
            const until = async (probe, timeoutMs) => {
                const deadline = Date.now() + timeoutMs;
                while (Date.now() < deadline) {
                    if (await probe()) return true;
                    await sleep(250);
                }
                return false;
            };
            await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
            const back = await canvasUp();
            await sleep(6000); // 连上、认证、列表
            const visitsAfter = await visits();
            // 认证态：输入区可用，文本能真的打进去。
            const focusedNow = await focusComposer();
            check('认证态：输入框能被点聚焦', focusedNow);
            await cdp.send('Input.insertText', { text: '万象' }); await sleep(700);
            await cdp.send('Input.insertText', { text: 'ok' }); await sleep(700);
            await cdp.send('Input.insertText', { text: '草稿往返' });
            await sleep(800);
            // 断言**产品落点**：草稿快照（IndexedDB，经 DraftWrite effect 写入）里真的有这段文本。
            // 不再读隐藏 input 的 .value——那个值被 avalonia.js 补丁 preventDefault 掉了，恒为空。
            const draftRaw = await page.evaluate(`(() => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang-drafts', 1);
                req.onsuccess = () => {
                    try {
                        const g = req.result.transaction('drafts', 'readonly').objectStore('drafts').get('snapshot');
                        g.onsuccess = () => resolve(typeof g.result === 'string' ? g.result : '');
                        g.onerror = () => resolve('');
                    } catch (e) { resolve(''); }
                };
                req.onerror = () => resolve('');
            }))()`);
            const savedToDb = await until(() => draftText('草稿往返'), 20000);
            // 文本上屏桥：两次 insertText 各落一次（决策 391）。
            // 断的是**草稿快照**（产品落点）里的文本。必须是**精确相等**，
            // 不能用子串包含——子串判据会漏掉「一次 insertText 被派发两次」：
            //   '万象ok万象ok草稿往返' 里**含有** '万象ok草稿往返'，子串判据放行，
            //   而正确的草稿是 '万象ok草稿往返'。
            // 精确相等同时挡住「多插了一遍」与「少插了一段」两类问题。
            const EXPECTED = '万象ok草稿往返';
            const typedOnce = draftRaw === EXPECTED;
            check('文本上屏桥：insertText 各落一次', typedOnce,
                `draft=${JSON.stringify(draftRaw.slice(0, 160))} expected=${JSON.stringify(EXPECTED)}`);
            await page.screenshot({ path: `${OUT}/smoke-draft.png` });

            // ---- 恢复（restore）要证明的是「重开后**输入框里**有上一段的文本」 ----
            // 光看重载后 IndexedDB 里还是那段旧文本**不能**证明恢复：
            // 那只说明快照还在（持久化），没说明它被灌回了输入框。
            // 判据：重载 → 点聚焦真正的输入框 → **追加**一段唯一后缀 → 等落盘 →
            //       草稿快照必须等于「原文 + 后缀」。
            // 若恢复没发生，追加的后缀会**替换**原文，快照就只剩后缀 → 判据失败。
            await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
            await canvasUp();
            await sleep(6000);
            const focusBack = await focusComposer();
            const SUFFIX = '｜恢复验证后缀';
            await cdp.send('Input.insertText', { text: SUFFIX });
            await sleep(1500);
            const restoredRaw = await page.evaluate(`(() => new Promise((resolve) => {
                const req = indexedDB.open('wanxiang-drafts', 1);
                req.onsuccess = () => {
                    try {
                        const g = req.result.transaction('drafts', 'readonly').objectStore('drafts').get('snapshot');
                        g.onsuccess = () => resolve(typeof g.result === 'string' ? g.result : '');
                        g.onerror = () => resolve('');
                    } catch (e) { resolve(''); }
                };
                req.onerror = () => resolve('');
            }))()`);
            const EXPECTED_AFTER = EXPECTED + SUFFIX;
            const restored = focusBack && restoredRaw === EXPECTED_AFTER;
            await page.screenshot({ path: `${OUT}/smoke-restored.png` });
            const visitsThird = await visits();
            // main.js：每次加载把 localStorage 的访问计数 +1（决策 365 的安装提示按它分档）
            check('localStorage：重开页面后访问计数继续递增', visitsBefore >= 1 && visitsAfter === visitsBefore + 1 && visitsThird === visitsAfter + 1,
                `visits ${visitsBefore} -> ${visitsAfter} -> ${visitsThird}`);
            check('认证态：草稿落进 IndexedDB', back && savedToDb, `back=${back} saved=${savedToDb}`);
            check('认证态：重开页面草稿回到输入框', restored,
                `draft=${JSON.stringify(restoredRaw.slice(0, 160))} expected=${JSON.stringify(EXPECTED_AFTER)}`);

            // ---- 弹出层（ToolTip / 上下文菜单 / 输入框右键菜单）的像素检查 ----
            // avalonia.js 没有可用的 DOM 无障碍树，弹出层也没有 DOM 节点，
            // 所以判据只能是**像素**：开/关弹出层各截一张画布，求差得到弹出层的
            // 外接矩形，再在这块矩形里统计字形笔画。
            //
            // 统计量：**每行笔画段数的均值**。豆腐块（.notdef 空心方框）每行只有
            // 左右两段竖笔画 → 均值 ≈ 2；真正的中日韩字形笔画段数明显更多。
            // 阈值 2.8 由两次实跑标定（正常字体 vs 去掉 CJK 字体的坏构建）。
            const GRID_W = 240, GRID_H = 160, CELL = 10; // 网格覆盖整张画布，1 格 = 10 设备像素
            const canvasCount = () => page.evaluate(() => document.querySelectorAll('canvas').length);
            const snapAll = async () => {
                const n = await canvasCount();
                const out = [];
                for (let i = 0; i < n; i++) {
                    out.push(await page.evaluate((idx) => {
                        const c = document.querySelectorAll('canvas')[idx];
                        if (!c) return null;
                        const off = document.createElement('canvas');
                        off.width = 240; off.height = 160;
                        const ctx = off.getContext('2d');
                        ctx.drawImage(c, 0, 0, 240, 160);
                        return Array.from(ctx.getImageData(0, 0, 240, 160).data);
                    }, i));
                }
                return out;
            };
            const diffBbox = (a, b) => {
                if (!a || !b) return null;
                let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
                for (let i = 0; i < a.length; i += 4) {
                    if (Math.abs(a[i] - b[i]) > 24 || Math.abs(a[i + 1] - b[i + 1]) > 24 || Math.abs(a[i + 2] - b[i + 2]) > 24) {
                        const p = i / 4, x = p % GRID_W, y = (p / GRID_W) | 0;
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
                }
                return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
            };
            // 找到「哪块画布、哪块矩形」是这次变化最大的区域。
            const popupRegion = async (before) => {
                const after = await snapAll();
                let best = null;
                for (let i = 0; i < after.length; i++) {
                    const bb = diffBbox(before[i], after[i]);
                    if (!bb) continue;
                    const area = bb.w * bb.h;
                    if (!best || area > best.bb.w * best.bb.h) best = { canvas: i, bb };
                }
                return best;
            };
            // 在弹出层矩形里统计笔画：背景取众数色，其余像素算笔画。
            const regionStats = (canvasIdx, bb) => page.evaluate((args) => {
                const c = document.querySelectorAll('canvas')[args.i];
                if (!c) return null;
                const cell = 10;
                // 内缩（去掉弹出层描边）不能吃掉小块：按尺寸自适应。
                const shrink = Math.max(0, Math.min(2, Math.floor(Math.min(args.bb.w, args.bb.h) / 3)));
                const sx = (args.bb.x + shrink) * cell, sy = (args.bb.y + shrink) * cell;
                const sw = Math.max(1, (args.bb.w - shrink * 2) * cell), sh = Math.max(1, (args.bb.h - shrink * 2) * cell);
                const off = document.createElement('canvas');
                off.width = sw; off.height = sh;
                const ctx = off.getContext('2d');
                ctx.drawImage(c, sx, sy, sw, sh, 0, 0, sw, sh);
                const d = ctx.getImageData(0, 0, sw, sh).data;
                const counts = new Map();
                for (let i = 0; i < d.length; i += 4) {
                    const k = ((d[i] >> 3) << 16) | ((d[i + 1] >> 3) << 8) | (d[i + 2] >> 3);
                    counts.set(k, (counts.get(k) || 0) + 1);
                }
                let bgKey = 0, best = -1;
                for (const [k, v] of counts) if (v > best) { best = v; bgKey = k; }
                const br = (bgKey >> 16) & 255, bgc = (bgKey >> 8) & 255, bb2 = bgKey & 255;
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
                // distinctShapes：这一段文字里**不同形状的行**有多少种。
                // 豆腐块（.notdef 空心方框）所有字一样 → 行形状只有几种；
                // 真正的中日韩字形各不相同 → 行形状很多。
                return { w: sw, h: sh, ink, inkFrac: ink / (sw * sh), rows: rowRuns.length, meanRuns, distinctShapes: rowShapes.size };
            }, { i: canvasIdx, bb });
            // 弹出层的截图（CSS 坐标裁剪，1 网格格 = 10 设备像素 = 5 CSS 像素）。
            const shotRegion = async (name, bb) => {
                const path = `${OUT}/popup-${name}.png`;
                await page.screenshot({
                    path,
                    clip: { x: bb.x * 5, y: bb.y * 5, width: bb.w * 5, height: bb.h * 5 },
                });
                return path;
            };
            // 一个弹出层的完整判据：
            //   1) 出现了（有变化区域）；
            //   2) 文字不是豆腐块 —— 判据**自校准**：拿弹出层里的文字与**同一次渲染里
            //      界面正文的中文**比。豆腐块（.notdef 空心方框）每个字都一样，
            //      所以「每行笔画段数的均值」低、「不同形状的行数」少；
            //      真正的中日韩字形笔画多、形状各异。
            //      判据用**比值**：弹出层笔画密度不应低于正文中文的 70%。
            //      若正文中文本身就是豆腐块（本机 headless Chrome 的字体限制），
            //      这次弹出层检查判为 SKIP，而不是把环境缺陷算成产品缺陷。
            let bodyRef = null; // 正文中文的笔画基准：{ meanRuns, distinctShapes }
            const measureBodyChinese = async () => {
                // 主内容区（已打开的对话正文）左半部分，避开侧栏。
                const bb = { x: 60, y: 20, w: 90, h: 60 };
                const s = await regionStats(0, bb);
                if (!s || s.inkFrac <= 0.005) return null;
                return { meanRuns: s.meanRuns, distinctShapes: s.distinctShapes, inkFrac: s.inkFrac };
            };
            const assertPopup = async (name, region, bb) => {
                if (!region) { check(`弹出层 ${name} 出现`, false, '开/关两帧没有差异区域'); return null; }
                const stats = await regionStats(region.canvas, region.bb);
                const path = await shotRegion(name, region.bb);
                check(`弹出层 ${name} 出现`, true, `canvas=${region.canvas} rect=${region.bb.w}x${region.bb.h} 截图=${path}`);
                if (!stats || stats.inkFrac <= 0.01) {
                    checkSkip(`弹出层 ${name} 文字不是豆腐块`, '弹出层区域没有可见文字', `inkFrac=${stats ? stats.inkFrac.toFixed(3) : 'n/a'}`);
                    return stats;
                }
                const detail = `inkFrac=${stats.inkFrac.toFixed(3)} meanRuns=${stats.meanRuns.toFixed(2)} distinctShapes=${stats.distinctShapes}`;
                if (!bodyRef) {
                    checkSkip(`弹出层 ${name} 文字不是豆腐块`, '界面正文本身没有可测的中文，无法自校准', detail);
                    return stats;
                }
                // 自校准：弹出层的笔画密度至少要达到正文中文的 70%。
                const ratio = bodyRef.meanRuns > 0 ? stats.meanRuns / bodyRef.meanRuns : 0;
                const shapeOk = stats.distinctShapes >= Math.max(3, Math.floor(bodyRef.distinctShapes * 0.5));
                check(`弹出层 ${name} 文字不是豆腐块`, ratio >= 0.7 && shapeOk,
                    `${detail} 正文基准 meanRuns=${bodyRef.meanRuns.toFixed(2)} distinctShapes=${bodyRef.distinctShapes} 比值=${ratio.toFixed(2)}`);
                return stats;
            };
            const closePopups = async () => {
                await page.mouse.click(5, 5);
                await page.keyboard.press('Escape');
                await sleep(400);
            };

            // (0) 先打开种子对话（主内容区出现中文正文），量出**界面正文的中文笔画基准**，
            //     后面三个弹出层都拿它自校准：弹出层是弹出层自己的字体问题，
            //     正文是同一套渲染路径的对照组。
            let opened = false;
            for (const y of [140, 170, 200, 230, 110]) {
                for (const x of [110, 150, 80]) {
                    const before = await snapAll();
                    await page.mouse.click(x, y);
                    await sleep(900);
                    const region = await popupRegion(before);
                    if (region && region.bb.x > 40) { opened = true; break; }
                }
                if (opened) break;
            }
            check('侧栏对话可打开（主内容区有变化）', opened);
            bodyRef = await measureBodyChinese();
            check('界面正文有可测的中文（自校准基准）', !!bodyRef,
                bodyRef ? `meanRuns=${bodyRef.meanRuns.toFixed(2)} distinctShapes=${bodyRef.distinctShapes} inkFrac=${bodyRef.inkFrac.toFixed(3)}` : '主内容区没有测到文字');

            // (1) ToolTip：悬停侧栏「新建对话」按钮（提示是「新建对话（Ctrl / ⌘ + N）」）。
            //     提示条是**扁而宽**的矩形，出现在按钮下方/右侧。
            let tipFound = null;
            for (const y of [150, 120, 180, 90, 210, 60, 240]) {
                for (const x of [110, 60, 160, 200, 30]) {
                    const before = await snapAll();
                    await page.mouse.move(x, y);
                    await sleep(700);
                    const region = await popupRegion(before);
                    if (region && region.bb.w > 12 && region.bb.w > region.bb.h * 1.5 && region.bb.h < 14) {
                        tipFound = region; break;
                    }
                }
                if (tipFound) break;
            }
            await assertPopup('tooltip', tipFound, tipFound && tipFound.bb);
            await closePopups();

            // (2) 上下文菜单：在消息卡上右键。
            let menuRegion = null;
            for (const [x, y] of [[600, 220], [600, 180], [700, 250], [500, 200]]) {
                const before = await snapAll();
                await page.mouse.click(x, y, { button: 'right' });
                await sleep(700);
                menuRegion = await popupRegion(before);
                if (menuRegion && menuRegion.bb.h >= 6) break;
                await closePopups();
            }
            await assertPopup('contextmenu', menuRegion, menuRegion && menuRegion.bb);
            await closePopups();

            // (3) 输入框右键菜单（MenuFlyoutPresenter）：右键到真正聚焦过的那个点。
            await focusComposer();
            let flyoutRegion = null;
            for (const pt of [focusPoint, { x: 600, y: 400 }]) {
                if (!pt) continue;
                const before = await snapAll();
                await page.mouse.click(pt.x, pt.y, { button: 'right' });
                await sleep(700);
                flyoutRegion = await popupRegion(before);
                if (flyoutRegion && flyoutRegion.bb.h >= 5) break;
                await closePopups();
            }
            await assertPopup('flyout', flyoutRegion, flyoutRegion && flyoutRegion.bb);
            await closePopups();
        }
        if (skipped > 0) console.log(`SKIP 小计 ${skipped} 条（环境限制，不计入失败）`);
        check('无未捕获页面异常', errors.length === 0, errors.slice(0, 3).join(' | '));
    } finally { await browser.close(); }
    process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });
