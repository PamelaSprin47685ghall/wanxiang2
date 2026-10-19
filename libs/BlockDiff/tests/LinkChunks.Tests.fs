module LinkChunksTests
open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

let private js (s: string) = Text.Json.JsonSerializer.Serialize s

/// 金样由旧 MarkdownRenderer.SafeChunk / RebalanceLinkChunks 生成
[<Fact>]
let ``golden: chunking and rebalancing match the old renderer`` () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    for (s, n) in LinkChunksSamples.chunkInputs do
        let chunks = LinkChunks.safeChunk n s
        line (sprintf "== chunk %s %d" (js s) n)
        line ("safe: " + (chunks |> List.map js |> String.concat ","))
        line ("rebalanced: " + (LinkChunks.rebalance chunks |> List.map js |> String.concat ","))
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "LinkChunks", "chunks.txt")), sb.ToString())

[<Fact>]
let ``empty input or non-positive size yields no chunks`` () =
    Assert.Empty(LinkChunks.safeChunk 3 null)
    Assert.Empty(LinkChunks.safeChunk 3 "")
    Assert.Empty(LinkChunks.safeChunk -1 "abc")

[<Fact>]
let ``surrogate pairs are never split`` () =
    let chunks = LinkChunks.safeChunk 1 "😀😁"
    Assert.Equal<string list>([ "😀"; "😁" ], chunks)

[<Fact>]
let ``chunks concatenate back to the input`` () =
    let s = "https://example.com/路径，含（标点）。"
    Assert.Equal(s, String.Concat(LinkChunks.rebalance (LinkChunks.safeChunk 3 s)))

[<Fact>]
let ``a closer at the head moves to the previous chunk`` () =
    Assert.Equal<string list>([ "ab。"; "cd" ], LinkChunks.rebalance [ "ab"; "。cd" ])

[<Fact>]
let ``an opener at the tail moves to the next chunk`` () =
    Assert.Equal<string list>([ "ab"; "（cd" ], LinkChunks.rebalance [ "ab（"; "cd" ])

[<Fact>]
let ``a lone punctuation chunk stays in place`` () =
    Assert.Equal<string list>([ "ab"; "。"; "cd" ], LinkChunks.rebalance [ "ab"; "。"; "cd" ])

// ---- 旧 CraftsmanshipPolishTests（去掉 Headless 包装）
[<Fact>]
let ``LinkChunks.safeChunk does not slice surrogate pairs into separate chunks`` () =
    // A long text mixing emojis, surrogate pairs, and ZWJ sequences
    let emojiText =
        "👨‍👩‍👧‍👦🇨🇳𠮷野家👍🏽"
        + "🎉🚀💡🔥✨🎈"
        + "𠮷𠮷𠮷𠮷𠮷"
        + "👩‍💻👨‍💻🧑‍🌾"
        + "❤️🧡💛💚💙💜"

    let chunks = LinkChunks.safeChunk 5 emojiText
    Assert.NotEmpty chunks
    
    // Total text elements must match original
    let rejoined = String.Concat chunks
    Assert.Equal(emojiText, rejoined)

    // Check each chunk boundary for surrogate integrity
    for chunk in chunks do
        Assert.NotEmpty chunk
        // Chunk cannot start with low surrogate
        Assert.False(Char.IsLowSurrogate chunk[0], sprintf "Chunk starts with lone low surrogate: %s" chunk)
        // Chunk cannot end with high surrogate
        Assert.False(Char.IsHighSurrogate chunk[chunk.Length - 1], sprintf "Chunk ends with lone high surrogate: %s" chunk)

        // Internal surrogate integrity
        for i in 0 .. chunk.Length - 1 do
            let ch = chunk[i]
            if Char.IsHighSurrogate ch then
                Assert.True(i + 1 < chunk.Length, "High surrogate must have following low surrogate in chunk")
                Assert.True(Char.IsLowSurrogate chunk[i + 1], "High surrogate must be followed by low surrogate")
            elif Char.IsLowSurrogate ch then
                Assert.True(i > 0, "Low surrogate must have preceding high surrogate in chunk")
                Assert.True(Char.IsHighSurrogate chunk[i - 1], "Low surrogate must be preceded by high surrogate")
