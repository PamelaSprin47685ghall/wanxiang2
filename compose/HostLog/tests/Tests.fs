module HostLogTests

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Server

[<Fact>]
let ``runtime truncation record carries the decision-40 fields with the raw commit redacted`` () =
    Stderr.registerSecrets [ "title-secret-xyz" ]
    let g = Guid("11111111-2222-3333-4444-555555555555")

    let commit =
        Events.Commit.create
            7UL
            (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            [
                EventData.ConversationRenamed
                    {
                        conversationId = g
                        title = "title-secret-xyz"
                    }
            ]

    Stderr.truncated commit "log.jsonseq" 42L (Poisoned "disk")
    let line = List.last (Stderr.recentErrorLines ())
    Stderr.clearSecrets ()

    let expectedRaw =
        (CommitCodec.commitToJsonLine commit).Replace("title-secret-xyz", "***")

    let o = Text.Json.Nodes.JsonNode.Parse(line).AsObject()
    Assert.Equal("jsonseq-tail-truncated", string o["event"])
    Assert.Equal("runtime-commit", string o["phase"])
    Assert.Equal(7UL, o["commitId"].GetValue<uint64>())
    Assert.Equal(42L, o["byteOffset"].GetValue<int64>())
    Assert.Equal("poisoned", string o["exceptionType"])
    Assert.Equal("truncated-and-id-reused", string o["action"])
    Assert.Equal(expectedRaw, string o["rawCommit"])
    Assert.DoesNotContain("title-secret-xyz", line)
