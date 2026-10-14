// 金标准生成器（DraftRestore）：tools/OldDraftRestore.fs 是旧 AppShell 私有成员（RestoreLocalDrafts 等）的原样摘录；
// 快照类型取本库源码（旧仓库引用的就是本库的包，类型逐字相同）。
//   dotnet fsi libs/DraftStore/tools/gen-restore-golden.fsx
#load "../DraftStore.fs" "OldDraftRestore.fs" "../tests/RestoreRender.fs"
open OldAppShell
let text =
    RestoreRender.render DraftRestore.knownConversations DraftRestore.restoreNotice DraftRestore.fillComposer
        DraftRestore.summaryCounts DraftRestore.pruneStored DraftRestore.knownAfterPrune DraftRestore.clearedText
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/restore.txt", text)
