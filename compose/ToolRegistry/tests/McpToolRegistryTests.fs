namespace Wanxiang.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Server

// 原样迁自旧仓库 d9651ed tests/Wanxiang.Tests/McpTests.fs「ToolRegistry 契约与配置联动测试」一节（只改了模块名与 open）。
module McpToolRegistryTests =

    /// 取自旧 Helpers.testConfig：只用到 tools 字段。
    let private testConfig () = { SessionConfig.empty with provider = "openai"; model = "test-model" }

    let private jsonOptions = System.Text.Json.JsonSerializerOptions(Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    let private defaultMcpConfig id =
        { id = id
          label = id
          enabled = true
          command = None
          args = []
          env = Map.empty
          url = None
          maxConcurrency = None
          callTimeoutSeconds = 5 }


    // =========================================================================
    // 6. ToolRegistry 契约与配置联动测试
    // =========================================================================

    [<Fact>]
    let ``ToolRegistry 支持 MCP 客户端缓存、指纹更新与失效剪裁`` () =
        let mutable currentAppConfig = AppConfig.defaults (Guid.NewGuid())
        let logs = ConcurrentQueue<string>()
        let registry = new ToolRegistry((fun () -> currentAppConfig), logs.Enqueue)

        // 初始无 MCP 服务
        Assert.True((registry.GetMcpClient "mcp1").IsNone)

        // 添加一个已禁用的 MCP 服务
        let srvDisabled = { defaultMcpConfig "mcp1" with enabled = false }
        currentAppConfig <- { currentAppConfig with mcpServers = Map.ofList [ "mcp1", srvDisabled ] }
        Assert.True((registry.GetMcpClient "mcp1").IsNone)

        // 启用 MCP 服务
        let srvEnabled = { defaultMcpConfig "mcp1" with enabled = true; command = Some "echo" }
        currentAppConfig <- { currentAppConfig with mcpServers = Map.ofList [ "mcp1", srvEnabled ] }
        let client1 = registry.GetMcpClient "mcp1"
        Assert.True(client1.IsSome)

        // 再次获取应命中指纹缓存
        let client1Cached = registry.GetMcpClient "mcp1"
        Assert.True(Object.ReferenceEquals(client1.Value, client1Cached.Value))

        // 修改配置（如 args 变化导致指纹变化），旧客户端排空，创建新客户端
        let srvModified = { srvEnabled with args = [ "arg1" ] }
        currentAppConfig <- { currentAppConfig with mcpServers = Map.ofList [ "mcp1", srvModified ] }
        let client2 = registry.GetMcpClient "mcp1"
        Assert.True(client2.IsSome)
        Assert.False(Object.ReferenceEquals(client1.Value, client2.Value))

        // 配置中删除，调用 PruneRemoved
        currentAppConfig <- { currentAppConfig with mcpServers = Map.empty }
        registry.PruneRemoved()
        Assert.True((registry.GetMcpClient "mcp1").IsNone)

        registry.Dispose()

    [<Fact>]
    let ``ToolRegistry 组装 Descriptors 与 DescriptorsJson 包含内置与有效 MCP 工具`` () =
        let instanceId = Guid.NewGuid()
        let mutable appCfg = AppConfig.defaults instanceId
        let registry = new ToolRegistry((fun () -> appCfg), ignore)

        // 纯内置工具
        let descList = registry.Descriptors()
        Assert.NotEmpty descList
        Assert.Contains(descList, fun d -> d.source = "builtin")

        let jsonArr = registry.DescriptorsJson()
        Assert.Equal(descList.Length, jsonArr.Count)

        // BuildTools 与 MissingTools 边界检查
        let sessionCfg =
            { testConfig () with
                tools = [ "builtin:calculator"; "mcp:nonexistent/tool"; "invalid_format_tool" ] }
        let missing = registry.MissingTools sessionCfg
        Assert.Contains("mcp:nonexistent/tool", missing)
        Assert.Contains("invalid_format_tool", missing)

        let builtTools = registry.BuildTools sessionCfg
        // 只有合法的内置 calculator 被加入，无效的被跳过并记录
        Assert.True(builtTools.Length >= 0)

        registry.Dispose()

