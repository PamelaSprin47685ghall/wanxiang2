module Scenario

open System
open Wanxiang.Protocol
open Wanxiang.Server

/// 配置被拒状态的记、清、读，以及没有/有诊断器时的 RunDoctor。
let all () : string =
    let sb = Text.StringBuilder()
    let line (s: string) = sb.AppendLine s |> ignore
    let enc (e: WireEvent) = WireCodec.encode e
    let h = HostStatus()
    line (sprintf "initial=%b" h.Current.IsSome)
    let redact (s: string) = s.Replace("sk-secret", "***")
    let errs = [ "bad key sk-secret"; String('x', 400); "e3"; "e4"; "e5"; "e6"; "e7" ]
    line (enc (h.Reject(redact, errs)))
    line (enc h.Current.Value)
    line (String.Join(" | ", HostStatus.Sanitize redact [ "  a  "; ""; "sk-secret" ]))
    h.Clear()
    line (sprintf "cleared=%b" h.Current.IsSome)
    let rid = Guid "dddddddd-0000-0000-0000-000000000004"
    line (enc (h.RunDoctor(rid, redact).Result))
    h.DoctorRunner <- Some(fun () -> failwith "boom sk-secret")
    line (enc (h.RunDoctor(rid, redact).Result))
    sb.ToString()
