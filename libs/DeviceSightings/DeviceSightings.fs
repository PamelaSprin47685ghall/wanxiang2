namespace Wanxiang.Server

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

/// 主机自有的设备记账：每个令牌哈希最近一次认证成功的时间。
/// 这是主机的诊断信息，不是用户配置，所以放在数据目录下主机独占的文件里，绝不写用户编辑的配置文件。
/// 私有状态：文件路径、内存表、锁。只依赖 BCL。
/// - Seen：记一次认证；距上次落盘的时间不到节流间隔就只改内存（与旧实现「1 分钟内不重复写盘」一致）。
/// - Seed：一次性迁移旧配置里的 lastSeen，只补缺、取较新者。
/// - 文件损坏或缺失当作空表（记账丢了只影响「最近连接」显示）。
type DeviceSightings(path: string, throttle: TimeSpan) =
    let gate = obj ()
    let seen = Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
    let persisted = Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
    let stamp (t: DateTimeOffset) = t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)

    do
        try
            if File.Exists path then
                match JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) with
                | :? JsonObject as root ->
                    match root["lastSeen"] with
                    | :? JsonObject as m ->
                        for kv in m do
                            match kv.Value with
                            | :? JsonValue as v ->
                                match v.TryGetValue<string>() with
                                | true, s ->
                                    match DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal) with
                                    | true, t -> seen[kv.Key] <- t; persisted[kv.Key] <- t
                                    | _ -> ()
                                | _ -> ()
                            | _ -> ()
                    | _ -> ()
                | _ -> ()
        with _ -> ()

    /// 原子写整张表（临时文件 + 改名）。调用方持锁。
    let save () : Result<unit, string> =
        try
            let dir = Path.GetDirectoryName(Path.GetFullPath path)
            Directory.CreateDirectory dir |> ignore
            let m = JsonObject()
            for kv in seen |> Seq.sortBy (fun kv -> kv.Key) do m[kv.Key] <- stamp kv.Value
            let root = JsonObject()
            root["version"] <- 1
            root["lastSeen"] <- m
            let tmp = path + ".tmp." + string Environment.ProcessId
            File.WriteAllText(tmp, root.ToJsonString(JsonSerializerOptions(WriteIndented = true)), UTF8Encoding(false))
            File.Move(tmp, path, true)
            persisted.Clear()
            for kv in seen do persisted[kv.Key] <- kv.Value
            Ok()
        with e -> Error(sprintf "device sightings write failed: %s" e.Message)

    new(path: string) = DeviceSightings(path, TimeSpan.FromMinutes 1.0)

    member _.Path = path

    /// 最近一次认证时间（含尚未落盘的）。
    member _.LastSeen(tokenHash: string) : DateTimeOffset option =
        lock gate (fun () -> match seen.TryGetValue tokenHash with | true, t -> Some t | _ -> None)

    /// 记一次认证成功。落盘失败返回 Error（调用方只记日志，不阻断认证）。
    member _.Seen(tokenHash: string, now: DateTimeOffset) : Result<unit, string> =
        lock gate (fun () ->
            seen[tokenHash] <- now
            let stale =
                match persisted.TryGetValue tokenHash with
                | true, t -> now - t >= throttle
                | _ -> true
            if stale then save () else Ok())

    /// 迁移：把旧配置里的 lastSeen 并进来（只补缺或更新为更晚的时间）；有变化才落盘。
    member _.Seed(entries: (string * DateTimeOffset) seq) : Result<unit, string> =
        lock gate (fun () ->
            let mutable changed = false
            for (h, t) in entries do
                match seen.TryGetValue h with
                | true, cur when cur >= t -> ()
                | _ -> seen[h] <- t; changed <- true
            if changed then save () else Ok())
