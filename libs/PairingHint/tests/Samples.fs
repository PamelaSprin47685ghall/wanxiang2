module Samples

let urls = [ null; ""; "  "; "ws://127.0.0.1:8765/ws"; "ws://localhost:8765/ws"; " ws://[::1]:1/ws "; "wss://wanxiang.example.com/ws"; "ws://192.168.1.5:8787/ws"; "not a url"; "ws://LOCALHOST/ws" ]
let firsts = [ for t in [ None; Some "t" ] do for h in [ false; true ] do for e in [ None; Some "连接被拒绝" ] -> t, h, e ]
