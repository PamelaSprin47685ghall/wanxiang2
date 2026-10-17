namespace Wanxiang.UI

open System
open System.IO

/// 命令面板「最近使用」的本地读写（宿主桥，迁自旧 src/Wanxiang.UI/Model/CommandPalette.fs
/// 的 loadRecent / saveRecent / recentFile）。落盘格式归包内 PaletteRecent（decode / encode / fileName）；
/// 这里读写文件：与 ui.json 同目录，原子写（临时文件 + rename），失败静默（与旧实现一致）。
/// 旧宿主模块名 PaletteRecent 与包内同名模块冲突，故更名 PaletteRecentFile。
module PaletteRecentFile =

    let recentFile () =
        IO.Path.Combine(IO.Path.GetDirectoryName(UiPrefsFile.filePath ()), PaletteRecent.fileName)

    let loadRecent () : string list =
        try
            let path = recentFile ()
            if IO.File.Exists path then PaletteRecent.decode (IO.File.ReadAllText path) else []
        with _ -> []

    let saveRecent (recent: string list) =
        try
            let path = recentFile ()
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName path) |> ignore
            let tmp = path + ".tmp"
            IO.File.WriteAllText(tmp, PaletteRecent.encode recent)
            IO.File.Move(tmp, path, true)
        with _ -> ()
