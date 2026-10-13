module WsHandshakeTests

open Xunit
open WsHandshake

// SPEC 1：Origin 同源规则
[<Theory>]
[<InlineData("", "anything", true)>]
[<InlineData("http://localhost:9999", "localhost:9999", true)>]
[<InlineData("https://example.com", "example.com:443", true)>]
[<InlineData("http://example.com", "example.com", true)>]
[<InlineData("http://evil.com", "localhost:9999", false)>]
[<InlineData("null", "localhost:9999", false)>]
[<InlineData("http://localhost:9999/path", "localhost:9999", false)>]
[<InlineData("http://u@localhost:9999", "localhost:9999", false)>]
[<InlineData("ftp://localhost:9999", "localhost:9999", false)>]
[<InlineData("http://localhost:9999", "", false)>]
let ``origin must be same-origin http(s)`` (origin: string, host: string, ok: bool) =
    Assert.Equal(ok, WsHandshake.isAllowedOrigin origin host)

// SPEC 2：Host 白名单（防 DNS 重绑定）
[<Theory>]
[<InlineData("localhost:9999", true)>]
[<InlineData("127.0.0.1:9999", true)>]
[<InlineData("[::1]:9999", true)>]
[<InlineData("192.168.1.5", true)>]
[<InlineData("rebind.evil.com:9999", false)>]
[<InlineData("my.proxy.net", true)>]
[<InlineData("", false)>]
let ``host must be an ip, localhost or listed`` (host: string, ok: bool) =
    Assert.Equal(ok, WsHandshake.isAllowedHost host [ " My.Proxy.Net " ])
