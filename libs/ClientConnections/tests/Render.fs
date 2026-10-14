/// 金标准渲染：同一份源码分别对旧程序集与新库编译。
/// 旧侧把每份 client.toml 写进临时 WANXIANG_HOME，再调旧 tryLoadClientToml（真的读文件）；新侧直接解析同样的行。
/// 旧 defaultServerUrl 在桌面上返回固定地址，对照 desktopServerUrl。浏览器专用的两项（IndexedDB、页面地址）旧代码在桌面上跑不到，由 Tests.fs 的移植用例覆盖。
module Render

open System
open System.IO
open Wanxiang.UI

let files =
    [ "url = \"ws://127.0.0.1:8765/ws\"\ntoken = \"abc\""
      "url=\"ws://h/ws\"\ntoken=\"t\""
      "  token = \"only\""
      "url = \"ws://h/ws\"\ntoken = \"\""
      "tokens = \"x\"\nurl = \"u\"\ntoken = 'q'"
      "# url = \"c\"\nurl = \"a\"\nurl = \"b\"\ntoken = \"k\""
      "" ]

#if INTERACTIVE
let private load (text: string) =
    let home = Path.Combine(Path.GetTempPath(), "wx-golden-" + string (Guid.NewGuid()))
    Directory.CreateDirectory home |> ignore
    File.WriteAllText(Path.Combine(home, "client.toml"), text)
    Environment.SetEnvironmentVariable("WANXIANG_HOME", home)
    let r = CredentialStore.tryLoadClientToml ()
    Directory.Delete(home, true)
    r
let private defaultUrl () = CredentialStore.defaultServerUrl ()
#else
let private load (text: string) = CredentialStore.parseClientToml (File.ReadAllLines(let p = Path.GetTempFileName() in File.WriteAllText(p, text); p))
let private defaultUrl () = CredentialStore.desktopServerUrl
#endif

let all () =
    [ for f in files -> sprintf "%A" (load f)
      yield defaultUrl () ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
