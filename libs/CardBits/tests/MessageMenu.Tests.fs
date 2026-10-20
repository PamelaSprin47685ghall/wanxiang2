module MessageMenuTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let private settled role position text commitId linkSink =
    { generation = MenuGeneration.Settled
      role = role
      position = position
      text = text
      commitId = commitId
      linkSink = linkSink }

[<Fact>]
let ``streaming hides the whole menu`` () =
    Assert.Empty(MessageMenu.entries { generation = MenuGeneration.Streaming
                                       role = MessageRole.Assistant
                                       position = ReplyPosition.LastAssistant
                                       text = "x"
                                       commitId = Some 1UL
                                       linkSink = LinkSink.Available })

[<Fact>]
let ``user gets edit, last assistant gets regenerate with hints`` () =
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Copy; MessageMenuItem.EditAndFork; MessageMenuItem.CopyLink 7UL; MessageMenuItem.Delete 7UL ],
        MessageMenu.entries (settled MessageRole.User ReplyPosition.Other "hi" (Some 7UL) LinkSink.Available))
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Copy; MessageMenuItem.Regenerate ],
        MessageMenu.entries (settled MessageRole.Assistant ReplyPosition.LastAssistant "hi" None LinkSink.Available))
    Assert.Equal("Ctrl+Shift+C", MessageMenu.hint (settled MessageRole.Assistant ReplyPosition.LastAssistant "hi" None LinkSink.Available) MessageMenuItem.Copy)
    Assert.Equal("", MessageMenu.hint (settled MessageRole.User ReplyPosition.Other "hi" None LinkSink.Available) MessageMenuItem.Copy)

[<Fact>]
let ``blank text has no copy; link needs a sink; delete is danger`` () =
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Delete 3UL ],
        MessageMenu.entries (settled MessageRole.Assistant ReplyPosition.Other " " (Some 3UL) LinkSink.Absent))
    Assert.True(MessageMenu.isDanger (MessageMenuItem.Delete 3UL))

[<Fact>]
let ``tool and system are reply-side, regenerate only for the last one`` () =
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Regenerate ],
        MessageMenu.entries (settled MessageRole.Tool ReplyPosition.LastAssistant "" None LinkSink.Absent))
    Assert.Equal<MessageMenuItem list>([],
        MessageMenu.entries (settled MessageRole.System ReplyPosition.Other "" None LinkSink.Absent))

[<Fact>]
let ``roleOfText maps the wire roles`` () =
    Assert.Equal(MessageRole.User, MessageMenu.roleOfText "user")
    Assert.Equal(MessageRole.Tool, MessageMenu.roleOfText "tool")
    Assert.Equal(MessageRole.System, MessageMenu.roleOfText "system")
    Assert.Equal(MessageRole.Assistant, MessageMenu.roleOfText "assistant")
    Assert.Equal(MessageRole.Assistant, MessageMenu.roleOfText "")
