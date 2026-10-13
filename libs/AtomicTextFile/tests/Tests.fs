module AtomicTextFileTests

open System
open System.IO
open System.Threading
open Xunit
open AtomicTextFile

let tempPath () = Path.Combine(Path.GetTempPath(), "atf-" + Guid.NewGuid().ToString("N"), "a.toml")

// SPEC 1：写后读等于原文；不留临时文件；权限 0600
[<Fact>]
let ``write then read round-trips, leaves no temp file, owner-only`` () =
    let p = tempPath ()
    use f = new AtomicTextFile(p)
    Assert.Equal(Ok(), f.Write "a = 1\n汉字")
    Assert.Equal(Ok "a = 1\n汉字", f.Read())
    Assert.Equal<string[]>([| p |], Directory.GetFiles(Path.GetDirectoryName p))
    if not (OperatingSystem.IsWindows()) then
        Assert.Equal(UnixFileMode.UserRead ||| UnixFileMode.UserWrite, File.GetUnixFileMode p)

// SPEC 2：ExternalEdit 只在磁盘原文与最后已知原文不同时返回
[<Fact>]
let ``external edit is reported until read`` () =
    let p = tempPath ()
    use f = new AtomicTextFile(p)
    f.Write "x" |> ignore
    Assert.Equal(None, f.ExternalEdit())
    File.WriteAllText(p, "y")
    Assert.Equal(Some "y", f.ExternalEdit())
    Assert.Equal(Some "y", f.ExternalEdit())
    f.Read() |> ignore
    Assert.Equal(None, f.ExternalEdit())

// SPEC 3：外部改动在去抖后发出一次 Changed；本对象自己的写也会触发（监视器分不清），由使用方用 ExternalEdit 判断
[<Fact>]
let ``external changes raise Changed once after debounce`` () =
    let p = tempPath ()
    use f = new AtomicTextFile(p, TimeSpan.FromMilliseconds 200.0)
    f.Write "0" |> ignore
    f.Watch()
    Thread.Sleep 300
    let mutable n = 0
    f.Changed.Add(fun () -> Interlocked.Increment &n |> ignore)
    for i in 1..5 do File.WriteAllText(p, string i)
    let deadline = DateTime.UtcNow.AddSeconds 5.0
    while Volatile.Read &n = 0 && DateTime.UtcNow < deadline do Thread.Sleep 20
    Thread.Sleep 500
    Assert.Equal(1, Volatile.Read &n)

// SPEC 4：写失败时原文件不变、临时文件被删、返回 Error
[<Fact>]
let ``failed write leaves the original intact`` () =
    let p = tempPath ()
    use f = new AtomicTextFile(p)
    f.Write "keep" |> ignore
    // 真实故障：目标被占成一个非空目录，改名失败
    let p2 = Path.Combine(Path.GetDirectoryName p, "dir.toml")
    Directory.CreateDirectory(Path.Combine(p2, "x")) |> ignore
    use g = new AtomicTextFile(p2)
    Assert.True((g.Write "lost").IsError)
    Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName p, sprintf ".dir.toml.tmp.%d" Environment.ProcessId)))
    Assert.Equal(Ok "keep", f.Read())

// SPEC 5：读不存在的文件返回 Error；ExternalEdit 为 None
[<Fact>]
let ``missing file reads as error`` () =
    use f = new AtomicTextFile(tempPath ())
    Assert.False f.Exists
    Assert.True((f.Read()).IsError)
    Assert.Equal(None, f.ExternalEdit())

// WriteIfUnchanged：别人在最后一次读写之后改过文件，就不覆盖
[<Fact>]
let ``write-if-unchanged refuses to clobber an edit made after the last read or write`` () =
    let dir = Path.Combine(Path.GetTempPath(), "atf-" + Guid.NewGuid().ToString("N"))
    let path = Path.Combine(dir, "f.txt")
    use f = new AtomicTextFile(path)
    Assert.Equal(Ok(), f.WriteIfUnchanged "a")
    Assert.Equal(Ok(), f.WriteIfUnchanged "b")
    File.WriteAllText(path, "theirs")
    Assert.True(Result.isError (f.WriteIfUnchanged "mine"))
    Assert.Equal("theirs", File.ReadAllText path)
    Assert.Empty(Directory.GetFiles(dir, ".*tmp*"))
    f.Read() |> ignore
    Assert.Equal(Ok(), f.WriteIfUnchanged "mine")
    Assert.Equal("mine", File.ReadAllText path)
