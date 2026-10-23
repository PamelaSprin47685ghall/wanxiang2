#!/usr/bin/env python3
"""Patch Avalonia Browser avalonia.js for reliable WebGL on AMD/ANGLE Chrome.

- failIfMajorPerformanceCaveat: false — caveat=true can yield a context that never presents Skia frames
- preserveDrawingBuffer: true — keeps frames readable for diagnostics; cheap on desktop GPUs
Does NOT enable Software2D.
- ResizeObserver DPR guard (UI 决策 213): Avalonia trusts devicePixelContentBoxSize unconditionally.
  Some engines (Chrome device emulation, some WebViews/zoom states) report it in CSS px while
  devicePixelRatio > 1, so the canvas backing store stays CSS-sized and the whole UI renders
  at DPR x scale (model chip overflow, half-width layout). Only trust it when it agrees with
  contentBoxSize * devicePixelRatio (±2px); otherwise fall back to the CSS size * DPR path.
- Non-composition text bridge (UI 决策 390): upstream only turns keydown (key.length==1) and
  compositionend into TextInput; `beforeinput` insertText / insertReplacementText / insertFromPaste /
  insertFromDrop (CDP Input.insertText, iOS autocorrect, voice dictation, long-press paste) is dropped.
  We forward those exactly once via InputHelper.OnCompositionEnd(data) (-> RawTextEvent), first
  mapping the replaced range into the Avalonia selection via the existing deleteContentBackward
  selection sync. Only insertReplacementText remaps the selection (others trust Avalonia's own).
  Skipped while composing and for paste right after a Ctrl/Cmd+V keydown (Avalonia pastes that itself).
- 2026-10-21: the bridge used to preventDefault() every intercepted insert, so the browser never
  wrote into the hidden <input>. That is fatal: Avalonia's own beforeinput handler
  (InputHelper.OnBeforeInput) reads the text from the DOM element's **value**, so with the default
  insertion blocked the value stayed "" and the composer never received any text (measured:
  value="" and no TextInput reached Avalonia). We now let the browser insert for the plain text
  inputs (insertText / insertReplacementText) and only preventDefault for the ones that carry no
  DOM text (paste / drop / dictation / yank) — those keep going through OnCompositionEnd.
"""
from __future__ import annotations
import hashlib, pathlib, re, sys

# 决策 400：补丁按内容版本化。版本 = 本脚本内容的 sha256 前 16 位；输出首行写入
# /*wx-patch-version:<ver>*/。已是同一版本 -> 什么都不做（不改时间戳）；版本不同或文件
# 来自旧补丁 -> 先从 NuGet 包里的原始 avalonia.js（第二个参数 / --pristine）重新开始，
# 绝不在旧补丁之上叠补丁，也不信任残留标记。
PATCH_VERSION = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()[:16]
VERSION_HEADER = f"/*wx-patch-version:{PATCH_VERSION}*/\n"
_VERSION_RE = re.compile(r"^/\*wx-patch-version:([0-9a-f]+)\*/\n")

args = [a for a in sys.argv[1:] if a != "--version"]
if "--version" in sys.argv[1:]:
    print(PATCH_VERSION)
    raise SystemExit(0)
path = pathlib.Path(args[0])
pristine = pathlib.Path(args[1]) if len(args) > 1 and args[1] else None
current = path.read_text()
m = _VERSION_RE.match(current)
if m and m.group(1) == PATCH_VERSION:
    print(f"avalonia.js patch v{PATCH_VERSION} already applied {path}")
    raise SystemExit(0)
if pristine is not None and pristine.exists():
    t = pristine.read_text()
    if _VERSION_RE.match(t) or "/*wx-" in t:
        raise SystemExit(f"pristine avalonia.js is itself patched: {pristine}")
elif m or "/*wx-" in current:
    raise SystemExit(f"stale avalonia.js patch in {path} and no pristine source given; refusing to stack patches")
else:
    t = current
old = "alpha:!0,depth:!0,stencil:!0,antialias:!1,premultipliedAlpha:!0,preserveDrawingBuffer:!1,failIfMajorPerformanceCaveat:!0"
new = "alpha:!0,depth:!0,stencil:!0,antialias:!1,premultipliedAlpha:!0,preserveDrawingBuffer:!0,failIfMajorPerformanceCaveat:!1"
# also handle already partially patched
old2 = "alpha:!1,depth:!0,stencil:!0,antialias:!1,premultipliedAlpha:!0,preserveDrawingBuffer:!0,failIfMajorPerformanceCaveat:!1"
new2 = "alpha:!0,depth:!0,stencil:!0,antialias:!1,premultipliedAlpha:!0,preserveDrawingBuffer:!0,failIfMajorPerformanceCaveat:!1"
if old in t:
    t = t.replace(old, new)
    print(f"patched defaults -> {path}")
elif old2 in t:
    t = t.replace(old2, new2)
    print(f"normalized alpha+caveat -> {path}")
elif "failIfMajorPerformanceCaveat:!1" in t and "preserveDrawingBuffer:!0" in t:
    print(f"already patched {path}")
else:
    raise SystemExit(f"avalonia.js attrs pattern not found in {path}")
DPR_OLD = "r.devicePixelContentBoxSize?(s=r.devicePixelContentBoxSize[0].inlineSize"
DPR_MARK = "/*wx-dpr-guard*/"
DPR_NEW = (DPR_MARK + "r.devicePixelContentBoxSize&&Math.abs(r.devicePixelContentBoxSize[0].inlineSize-"
           "(r.contentBoxSize&&r.contentBoxSize[0]?r.contentBoxSize[0].inlineSize:r.contentRect.width)*i)<=2"
           "?(s=r.devicePixelContentBoxSize[0].inlineSize")
if DPR_MARK in t:
    print(f"dpr guard already present {path}")
elif DPR_OLD in t:
    t = t.replace(DPR_OLD, DPR_NEW)
    print(f"patched dpr guard -> {path}")
else:
    raise SystemExit(f"avalonia.js ResizeObserver pattern not found in {path}")
BRIDGE_OLD = 'e.addEventListener("beforeinput",n);'
BRIDGE_MARK = "/*wx-insert-bridge*/"
BRIDGE_NEW = BRIDGE_OLD + BRIDGE_MARK + (
    'e.addEventListener("compositionstart",()=>{e.__wxComp=!0});'
    'e.addEventListener("compositionend",()=>{e.__wxComp=!1});'
    'globalThis.__wxPasteHook||(globalThis.__wxPasteHook=1,globalThis.document&&globalThis.document.addEventListener("keydown",'
    'k=>{(k.ctrlKey||k.metaKey)&&(k.key==="v"||k.key==="V")&&(globalThis.__wxPasteKey=Date.now())},!0));'
    'e.addEventListener("beforeinput",o=>{let y=o.inputType;'
    'if(o.isComposing||e.__wxComp||!/^(insertText|insertReplacementText|insertFromPaste|insertFromPasteAsQuotation|insertFromDrop|insertFromDictation|insertFromYank)$/.test(y))return;'
    'if(y.startsWith("insertFromPaste")&&Date.now()-(globalThis.__wxPasteKey||0)<1e3)return;'
    'let d=o.data,x=o.target&&o.target.setSelectionRange?o.target:e;d==null&&o.dataTransfer&&(d=o.dataTransfer.getData("text/plain"));'
    # 纯文本输入（insertText / insertReplacementText）：
    #   Avalonia 浏览器端的 `OnBeforeInput` **根本不插文本**——反编译它的实现：
    #     if (inputType != "deleteByComposition") {
    #         if (inputType == "deleteContentBackward") { start = selectionStart; end = selectionEnd; }
    #         else { start = -1; end = -1; }
    #     }
    #     if (start != -1 && end != -1 && _client != null) _client.Selection = new TextSelection(start, end);
    #   也就是说 `OnBeforeInput` 只会**挪光标**，一个字符都不插。
    #   真正把文本插进 TextBox 的只有 `OnCompositionEnd(data)`：
    #     _client.SetPreeditText(null); if (data != null) _inputHandler.RawTextEvent(data);
    #   `RawTextEvent` 才是发 `RawTextInputEventArgs`、最终被 TextBox 收下变成 `Text` 的那一步。
    #
    #   所以纯文本也走 `OnCompositionEnd`，**不能**用 `OnBeforeInput`。
    #   拦下浏览器的默认插入（preventDefault）：DOM input 的 value 是 Avalonia 文本的**镜像**
    #   （Avalonia 用 setSurroundingText 往里推），让浏览器自己插会让镜像与真值脱节。
    #   转发用 `o.data`（本次插入的增量），**不是** DOM 的整个 value——
    #   用整个 value 会把已有的内容再插一遍（实测会出现 "万象ok万象ok"）。
    # 不带 DOM 文本的那几种（粘贴 / 拖放 / 听写 / yank）：同样拦下并当场转发。
    'if(y==="insertFromPaste"||y==="insertFromDrop"||y==="insertFromDictation"||y==="insertFromYank"){o.preventDefault();'
    'd&&l.InputHelper.OnCompositionEnd(t,d.replace(/\\r\\n?/g,"\\n"));return;}'
    'if(y==="insertText"||y==="insertReplacementText"){'
    'if(d==null)return;'
    # 替换一段：先把选区挪到被替换的范围（浏览器还没插，DOM 里还是旧的）。
    'if(y==="insertReplacementText"){let g=o.getTargetRanges&&o.getTargetRanges();'
    'g&&g.length&&g[0].startOffset!==g[0].endOffset&&x.setSelectionRange&&x.setSelectionRange(g[0].startOffset,g[0].endOffset);'
    'x.selectionStart!=null&&x.selectionStart!==x.selectionEnd&&l.InputHelper.OnBeforeInput(t,"deleteContentBackward",-1,-1)}'
    'o.preventDefault();'
    'l.InputHelper.OnCompositionEnd(t,d.replace(/\\r\\n?/g,"\\n"));return;}'
    'e.__wxPending=d});')
if BRIDGE_MARK in t:
    print(f"insert bridge already present {path}")
elif BRIDGE_OLD in t:
    t = t.replace(BRIDGE_OLD, BRIDGE_NEW, 1)
    print(f"patched insert bridge -> {path}")
else:
    raise SystemExit(f"avalonia.js beforeinput pattern not found in {path}")
path.write_text(VERSION_HEADER + t)
print(f"avalonia.js patch v{PATCH_VERSION} written -> {path}")
