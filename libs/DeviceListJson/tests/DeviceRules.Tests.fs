module DeviceRulesTests
open Xunit
open Wanxiang.Server

let private reserved (n: string) = n = "本机"
let private ck = DeviceRules.check reserved

[<Fact>]
let ``改名与吊销规则与旧实现一致`` () =
    Assert.Equal(Error "设备名称不能为空", ck "手机" false false (DeviceChange.Rename "  "))
    Assert.Equal(Error "设备名称最多 64 个字符", ck "手机" false false (DeviceChange.Rename(String.replicate 65 "a")))
    Assert.Equal(Error "该名称为本机桌面客户端保留", ck "手机" false false (DeviceChange.Rename "本机"))
    Assert.Equal(Error "本机桌面客户端不能重命名", ck "本机" false false (DeviceChange.Rename "新"))
    Assert.Equal(Error "本机桌面客户端不能在这里吊销", ck "本机" false false DeviceChange.Revoke)
    Assert.Equal(Error "不能吊销当前正在使用的设备", ck "手机" true false DeviceChange.Revoke)
    Assert.Equal(Error "该设备已被吊销", ck "手机" false true DeviceChange.Revoke)
    Assert.Equal(Ok(), ck "手机" false false (DeviceChange.Rename " 新名 "))
    Assert.Equal(Ok(), ck "手机" false false DeviceChange.Revoke)
