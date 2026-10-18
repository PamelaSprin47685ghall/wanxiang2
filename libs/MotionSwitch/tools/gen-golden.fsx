// 金样生成器：照抄的旧 MotionLedger（tools/OldMotion.fs）逐项输出。用法：dotnet fsi tools/gen-golden.fsx
// 旧账本没有 OutboxConfirmTimeout。重跑后把本仓库补的这一项插回原位，避免覆盖掉。
#load "OldMotion.fs" "../tests/Render.fs"
let path = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "motionswitch.txt")
let rendered = Render.render OldMotion.all
let text =
    if rendered.Contains "OutboxConfirmTimeout" then rendered
    else rendered.TrimEnd('\n') + "\nOutboxConfirmTimeout 30000\n"
System.IO.File.WriteAllText(path, text)
