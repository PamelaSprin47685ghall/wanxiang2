namespace Wanxiang.UI

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang2.IconSet
open Wanxiang2.Theme
open Wanxiang2.ThemeMetrics

/// 图标集：**Lucide 官方集**（ISC）的语义命名映射层（迁自宿主 src/Wanxiang.UI/Design/Icons.fs）。
///
/// 字形几何与 DrawingImage 缓存全部由 wanxiang2 的 `IconSet` 承担：
/// 同一画笔、同一字形返回同一实例（ConditionalWeakTable 按画笔引用回收），
/// 画笔是主题管理者的活实例，原地改色即跟随主题。
/// 这里只保留宿主 Icons 的语义函数签名（`IBrush -> Control`），内部转调 `IconSet.image`。
///
/// **MUST NOT 自绘路径、MUST NOT 调 `Geometry.Parse`、MUST NOT 自己读 SVG。**
/// 规范权威：`.agents/skills/wanxiang-ui/SKILL.md` §1。
module Icons =

    /// 固定尺寸档（宿主旧 `Spacing.Glyph.sm / lg` 是静态值；标准档取主题活度量）。
    let private fixedValue (v: float) = LiveValue v :> IObservable<float>

    /// 标准字形档（`Metric.IconGlyph` = 16）：全应用绝大多数图标用这一档，跟随主题度量。
    let private glyph (kind: Glyph) (brush: IBrush) : Control =
        IconSet.image kind brush (SharedTheme.manager.Metric Metric.IconGlyph)

    /// 小字形档（旧 `Spacing.Glyph.sm` = 14）：chip / tag 内、行内提示。
    let small (kind: Glyph) (brush: IBrush) : Control =
        IconSet.image kind brush (fixedValue 14.0)

    /// 大字形档（旧 `Spacing.Glyph.lg` = 20）：空态锚点、功能入口。
    let large (kind: Glyph) (brush: IBrush) : Control =
        IconSet.image kind brush (fixedValue 20.0)

    // ---------- 语义映射 ----------
    // 名的是形状（官方惯例），注释写的是万象里的用途。

    /// 搜索
    let search : IBrush -> Control = glyph Glyph.Search
    /// 关闭
    let close : IBrush -> Control = glyph Glyph.Close
    /// 确认
    let check : IBrush -> Control = glyph Glyph.Check
    /// 跳到开头
    let chevronsUp : IBrush -> Control = glyph Glyph.ChevronsUp
    /// 跳到结尾
    let chevronsDown : IBrush -> Control = glyph Glyph.ChevronsDown
    /// 展开
    let chevronDown : IBrush -> Control = glyph Glyph.ChevronDown
    /// 下一级
    let chevronRight : IBrush -> Control = glyph Glyph.ChevronRight
    /// 上一级
    let chevronLeft : IBrush -> Control = glyph Glyph.ChevronLeft
    /// 附件
    let paperclip : IBrush -> Control = glyph Glyph.Paperclip
    /// 发送（纸飞机；官方无线上发送实心图标）
    let send : IBrush -> Control = glyph Glyph.Send
    /// 停止生成
    let stop : IBrush -> Control = glyph Glyph.Stop
    /// 复制
    let copy : IBrush -> Control = glyph Glyph.Copy
    /// 派生对话
    let fork : IBrush -> Control = glyph Glyph.Fork
    /// 刷新
    let refresh : IBrush -> Control = glyph Glyph.Refresh
    /// 删除
    let trash : IBrush -> Control = glyph Glyph.Trash
    /// 编辑
    let pencil : IBrush -> Control = glyph Glyph.Pencil
    /// 置顶
    let pin : IBrush -> Control = glyph Glyph.Pin
    /// 归档
    let archive : IBrush -> Control = glyph Glyph.Archive
    /// 配置项
    let sliders : IBrush -> Control = glyph Glyph.Sliders
    /// 主机
    let server : IBrush -> Control = glyph Glyph.Server
    /// 认证令牌
    let key : IBrush -> Control = glyph Glyph.Key
    /// 设备（手机 + 显示器）：设置「设备」分区用；钥匙易被读成「密钥/API Key」
    let devices : IBrush -> Control = glyph Glyph.Devices
    /// 下载
    let download : IBrush -> Control = glyph Glyph.Download
    /// 导入（决策 313）
    let upload : IBrush -> Control = glyph Glyph.Upload
    /// 说明
    let info : IBrush -> Control = glyph Glyph.Info
    /// 侧栏
    let panelLeft : IBrush -> Control = glyph Glyph.PanelLeft
    /// 消息
    let message : IBrush -> Control = glyph Glyph.Message
    /// 更多操作
    let more : IBrush -> Control = glyph Glyph.More
    /// 链接（复制消息链接；与「复制」区分开，视觉走查 r8）
    let link : IBrush -> Control = glyph Glyph.Link
    /// 文件
    let file : IBrush -> Control = glyph Glyph.File
    /// 图片
    let image : IBrush -> Control = glyph Glyph.Image
    /// 时间
    let clock : IBrush -> Control = glyph Glyph.Clock
    /// 快捷键
    let keyboard : IBrush -> Control = glyph Glyph.Keyboard
    /// 数据目录
    let database : IBrush -> Control = glyph Glyph.Database
