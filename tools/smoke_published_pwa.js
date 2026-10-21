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
const check = (name, ok, detail) => { if (!ok) failed++; console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`); };
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
        // 之前写死了 (738, 720)——那是输入区**下方**的工具栏，点不到输入框。
        const focusComposer = async () => {
            if (await bridgeFocused()) return true;
            for (const y of [400, 380, 420, 360, 440, 340, 460, 320]) {
                for (const x of [732, 600, 860, 450, 1000]) {
                    await page.mouse.click(x, y);
                    await sleep(120);
                    if (await bridgeFocused()) return true;
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
        }
        check('无未捕获页面异常', errors.length === 0, errors.slice(0, 3).join(' | '));
    } finally { await browser.close(); }
    process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });
