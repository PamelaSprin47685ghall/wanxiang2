module ListenAddressTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``回环判定与旧实现一致`` () =
    for l in
        [
            "127.0.0.1:8765"
            "127.9.9.9:1"
            "[::1]:8765"
            "localhost:80"
            "LocalHost:1"
            "127.0.0.1"
        ] do
        Assert.True(ListenAddress.isLoopback l, l)

    for l in [ "0.0.0.0:8765"; "192.168.1.2:80"; "[::]:8765"; "example.com:443"; ""; null ] do
        Assert.False(ListenAddress.isLoopback l, string l)
