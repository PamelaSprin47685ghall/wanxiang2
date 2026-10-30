namespace Wanxiang.UI

open System.Threading.Tasks

/// 文件选择器必须在调用当下、调用线程上启动。
/// macOS 的 NSOpenPanel 不能在后台线程创建；把启动放进 Async.Start 会在调用返回后才打开。
module FilePick =
    let startOnCaller (openPicker: unit -> Task<'a>) (continueWith: Task<'a> -> unit) =
        let started = openPicker ()
        continueWith started
