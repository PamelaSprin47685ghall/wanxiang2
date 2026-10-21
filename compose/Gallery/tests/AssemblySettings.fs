namespace Wanxiang2.Gallery.Tests

open Xunit

// Avalonia 的平台状态是进程级全局量（样式、资源字典、headless 会话），
// 并行跑两条用例会互相踩。控件库交付约定里的 AssemblySettings.fs 就是这个用途。
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()
