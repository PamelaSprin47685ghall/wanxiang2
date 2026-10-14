// 金样生成器：照抄的旧 MotionLedger（tools/OldMotion.fs）逐项输出。用法：dotnet fsi tools/gen-golden.fsx
#load "OldMotion.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "motionswitch.txt"), Render.render OldMotion.all)
