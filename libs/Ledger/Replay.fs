namespace Ledger

/// 重放结果。
type internal Replayed<'S> =
    /// 日志完整。cp 为重放后的基线；starts 为基线之后每条提交的起始偏移（按 id 升序）。
    | Clean of cp: Checkpoint<'S> * starts: int64 list
    /// 发现损坏，未改动任何字节。
    | Broken of Damage<'S>
    /// 日志由更新版本写出，本版本不能读（不改动）。
    | TooNew of version: int

/// 只读重放：从基线偏移读出记录，逐条校验并折叠。
module internal Replay =
    let run (log: Log) (codec: Codec<'E>) (apply: 'S -> Commit<'E> -> Result<'S, string>) (from: Checkpoint<'S>) : Replayed<'S> =
        let records = log.readFrom from.offset |> Array.ofList
        let mutable cp = from
        let starts = ResizeArray<int64>()
        let mutable result = None
        let mutable i = 0
        let damage reason =
            result <- Some(Broken { reason = reason; validUntil = cp.offset; recordsAfter = records.Length - i - 1; prefix = cp })
        while result.IsNone && i < records.Length do
            let r = records[i]
            if not r.complete then damage "torn tail record"
            else
                match Envelope.decode codec r.payload with
                | Envelope.FutureVersion v -> result <- Some(TooNew v)
                | Envelope.Bad msg -> damage ("undecodable record: " + msg)
                | Envelope.Ok c ->
                    if c.id <> cp.throughId + 1UL then damage (sprintf "id gap: expected %d, got %d" (cp.throughId + 1UL) c.id)
                    else
                        match apply cp.state c with
                        | Error msg -> damage (sprintf "commit %d rejected on replay: %s" c.id msg)
                        | Ok s ->
                            let stamps =
                                match c.stamp with
                                | Some st -> cp.stamps |> Map.add st.commandId (st.hash, c.id)
                                | None -> cp.stamps
                            starts.Add r.startOffset
                            cp <- { state = s; throughId = c.id; offset = r.endOffset; stamps = stamps }
            i <- i + 1
        match result with
        | Some r -> r
        | None -> Clean(cp, List.ofSeq starts)
