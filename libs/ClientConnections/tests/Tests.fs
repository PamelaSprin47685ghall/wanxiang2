module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``Q191 浏览器连接记录：缺 url 或 token 的跳过，坏 JSON 当作没有`` () =
    let json = """[{"instanceId":"i1","url":"wss://a/ws","token":"t1","name":"A"},{"instanceId":"i2","url":"","token":"t2"},{"url":"wss://c/ws","token":"t3"},null,3]"""
    Assert.Equal<(string * string * string) list>([ "wss://a/ws", "t1", "i1"; "wss://c/ws", "t3", "" ], CredentialStore.parseConnections json)
    Assert.Empty(CredentialStore.parseConnections "{bad")
    Assert.Empty(CredentialStore.parseConnections "{}")
    Assert.Empty(CredentialStore.parseConnections "")

[<Fact>]
let ``浏览器默认地址与页面同源：https 用 wss，http 用 ws`` () =
    Assert.Equal("wss://wx.example.com:8443/ws", CredentialStore.serverUrlOfPage "/ws" "https://wx.example.com:8443/app/?wxprobe=1")
    Assert.Equal("ws://localhost:5000/ws", CredentialStore.serverUrlOfPage "/ws" "http://localhost:5000/")
    Assert.Equal("ws://h/ws", CredentialStore.serverUrlOfPage "/ws" "file://h/x")
    Assert.Equal(CredentialStore.desktopServerUrl, CredentialStore.serverUrlOfPage "/ws" null)
