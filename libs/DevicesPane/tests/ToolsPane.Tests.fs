module Wanxiang2.ToolsPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ToolsPane
open Wanxiang2.ToolsPane.Tests.Probe

let private empty =
    {
        Tools = []
        Folders = []
        SandboxNote = "文件读取工具当前不可用：还没有授权任何文件夹。"
        ExposeDebug = false
        Servers = []
    }

let private pane () =
    let p = ToolsPane()
    let _ = host p.View
    p.Render empty
    p

let private server id label enabled : ToolsPaneServer =
    {
        Id = id
        Label = label
        ToolCount = 1
        Transport = "本地 · python main.py"
        Enabled = enabled
    }

[<Fact>]
let ``SettingsTools SetCatalog renders tool list, mcp list and sandbox notes`` () =
    Headless.run (fun () ->
        let p = pane ()
        Assert.Contains("当前没有可用工具", textOf p.View)
        Assert.Contains("还没有 MCP 服务", textOf p.View)

        p.Render
            { empty with
                Tools =
                    [
                        {
                            Id = "read_file"
                            Label = "读取文件"
                            Description = "读取本地沙箱中的文件。"
                            FromMcp = false
                            PolicyLabel = "首次询问"
                        }
                        {
                            Id = "mcp_tool"
                            Label = "MCP 工具"
                            Description = ""
                            FromMcp = true
                            PolicyLabel = "自动执行"
                        }
                    ]
                Servers =
                    [
                        server "mcp-srv" "示例 MCP" true
                        { server "mcp-remote" "远程 MCP" false with
                            Transport = "远程 · https://mcp.remote.com"
                        }
                    ]
                Folders = [ "/data" ]
                SandboxNote = "文件读取工具的沙箱根目录：/data"
            }

        let text = textOf p.View
        Assert.DoesNotContain("当前没有可用工具", text)
        Assert.Contains("（MCP 服务未提供描述）", text)
        Assert.Contains("[server]", text)
        Assert.Contains("[wrench]", text)
        Assert.Contains("1 个工具", text)
        Assert.Contains("已停用", text)
        Assert.Contains("远程 · https://mcp.remote.com", text)
        Assert.Contains("文件读取工具的沙箱根目录：/data", text))

[<Fact>]
let ``工具设置：每个工具有审批方式入口，授权文件夹有入口；空授权时文案指向入口`` () =
    Headless.run (fun () ->
        let p = pane ()

        let policies, removes, adds =
            ResizeArray<string>(), ResizeArray<string>(), ResizeArray<unit>()

        p.PolicyClicked.Add(fun (id, _) -> policies.Add id)
        p.RemoveFolderClicked.Add removes.Add
        p.AddFolderClicked.Add adds.Add
        Assert.True((byId p.View "tools-add-folder").IsSome)
        Assert.Contains("还没有授权任何文件夹", textOf p.View)

        p.Render
            { empty with
                Tools =
                    [
                        {
                            Id = "builtin:time"
                            Label = "当前时间"
                            Description = "返回时间"
                            FromMcp = false
                            PolicyLabel = "每次询问"
                        }
                    ]
                Folders = [ "/data" ]
            }

        let policy = (byId p.View "tool-policy-builtin:time").Value
        Assert.Equal("当前时间 的审批方式：每次询问", AutomationProperties.GetName policy)
        click policy
        click (named p.View "移除文件夹授权 /data").Value
        click (byId p.View "tools-add-folder").Value
        Assert.Equal<string list>([ "builtin:time" ], List.ofSeq policies)
        Assert.Equal<string list>([ "/data" ], List.ofSeq removes)
        Assert.Equal(1, adds.Count))

[<Fact>]
let ``disabled mcp card dims its text while keeping row actions alive`` () =
    Headless.run (fun () ->
        let p = ToolsPane()
        let _ = host p.View
        let edits, mores = ResizeArray<string>(), ResizeArray<string>()
        p.EditServerClicked.Add edits.Add
        p.ServerMoreClicked.Add(fun (id, _) -> mores.Add id)

        p.Render
            { empty with
                Servers = [ server "filesystem" "文件系统" false ]
            }

        let _name =
            descendants p.View
            |> Seq.pick (fun c ->
                match c with
                | :? TextBlock as t when t.Text = "文件系统" -> Some t
                | _ -> None)

        let edit = (named p.View "编辑").Value
        let more = (named p.View "更多").Value
        Assert.True(edit.IsEnabled && more.IsEnabled)
        click edit
        click more
        Assert.Equal<string list>([ "filesystem" ], List.ofSeq edits)
        Assert.Equal<string list>([ "filesystem" ], List.ofSeq mores))

[<Fact>]
let ``built-in tool row title shares the row-title body font`` () =
    Headless.run (fun () ->
        let p = ToolsPane()
        let _ = host p.View

        p.Render
            { empty with
                Tools =
                    [
                        {
                            Id = "t"
                            Label = "当前时间"
                            Description = "d"
                            FromMcp = false
                            PolicyLabel = "自动执行"
                        }
                    ]
            }

        let title =
            descendants p.View
            |> Seq.pick (fun c ->
                match c with
                | :? TextBlock as t when t.Text = "当前时间" -> Some t
                | _ -> None)

        Assert.Equal(14.0, title.FontSize))

[<Fact>]
let ``switch row consumes the shared settings row height and toggle semantics`` () =
    Headless.run (fun () ->
        let p = pane ()
        let toggles = ResizeArray<bool>()
        p.DebugToggled.Add toggles.Add
        let row = (named p.View "显示调试工具").Value
        Assert.Equal(52.0, row.MinHeight)
        Assert.Equal("关闭", AutomationProperties.GetItemStatus row)
        click row
        Assert.Equal<bool list>([ true ], List.ofSeq toggles)
        Assert.Equal("开启", AutomationProperties.GetItemStatus row)
        // 组合者写回失败时按目录复位：写值不再触发事件
        p.Render { empty with ExposeDebug = false }
        Assert.Equal("关闭", AutomationProperties.GetItemStatus row)
        Assert.Single toggles |> ignore)

[<Fact>]
let ``SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`` () =
    Headless.run (fun () ->
        let messages = Collections.Generic.Dictionary<TextBox, TextBlock>()

        let validation (box: TextBox) =
            let t = TextBlock(IsVisible = false)
            messages[box] <- t
            Some(t :> Control)

        let editor = McpEditor(None, validation)
        let _ = host editor.View
        Assert.Equal(6, messages.Count)
        Assert.Same(editor.IdBox, editor.InitialFocus)
        Assert.Equal("60", editor.TimeoutBox.Text)
        let saves = ResizeArray<unit>()
        editor.SaveRequested.Add saves.Add
        editor.IdBox.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter))
        Assert.Equal(1, saves.Count)
        // 参数框里的回车是换行，不提交
        editor.ArgsBox.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter))
        Assert.Equal(1, saves.Count)
        editor.ApplyPending true
        let saveButton = (named editor.View "正在保存…").Value
        Assert.False saveButton.IsEnabled
        Assert.False (named editor.View "取消").Value.IsEnabled

        editor.Summary.RaiseEvent(
            KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control)
        )

        Assert.Equal(1, saves.Count)
        editor.ApplyPending false

        editor.Summary.RaiseEvent(
            KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control)
        )

        Assert.Equal(2, saves.Count)
        Assert.True (named editor.View "添加").Value.IsEnabled
        Assert.True editor.Enabled)

[<Fact>]
let ``editing an existing mcp server locks its id and focuses the label`` () =
    Headless.run (fun () ->
        let v =
            {
                Id = "fs"
                Label = "文件"
                Command = "npx"
                Args = "-y\nserver"
                Url = ""
                Timeout = "30"
                Enabled = false
            }

        let editor = McpEditor(Some v, fun _ -> None)
        let _ = host editor.View
        Assert.False editor.IdBox.IsEnabled
        Assert.Same(editor.LabelBox, editor.InitialFocus)
        Assert.Equal("-y\nserver", editor.ArgsBox.Text)
        Assert.False editor.Enabled
        Assert.True((named editor.View "保存").IsSome))

[<Fact>]
let ``folder editor sends the trimmed path on enter`` () =
    Headless.run (fun () ->
        let editor = FolderEditor(fun _ -> None)
        let _ = host editor.View
        let paths = ResizeArray<string>()
        editor.SaveRequested.Add paths.Add
        editor.PathBox.Text <- "  /home/me  "
        editor.PathBox.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter))
        click (named editor.View "授权").Value
        Assert.Equal<string list>([ "/home/me"; "/home/me" ], List.ofSeq paths))
