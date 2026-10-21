module ContextTrimTests

open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Server

let private msg role (t: string) = ChatMessage(role, t)

[<Fact>]
let ``count limit keeps the tail and drops a leading orphan tool result`` () =
    let msgs =
        [
            msg ChatRole.User "a"
            msg ChatRole.Assistant "b"
            msg ChatRole.Tool "r"
            msg ChatRole.User "c"
        ]

    Assert.Equal<string list>([ "c" ], ContextTrim.trim 2 0 msgs |> List.map _.Text)
    Assert.Equal(4, (ContextTrim.trim 0 0 msgs).Length)

[<Fact>]
let ``token budget keeps at least eight recent messages`` () =
    let msgs =
        [ for i in 1..20 -> msg ChatRole.User (string i + System.String('x', 200)) ]

    let kept = ContextTrim.trim 0 10 msgs
    Assert.Equal(8, kept.Length)
    Assert.StartsWith("13", kept.Head.Text)
