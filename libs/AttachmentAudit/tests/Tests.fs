module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``附件缺失与孤儿判定与旧实现一致`` () =
    Assert.Equal(((true, "ok"), (true, "ok")), AttachmentAudit.run [ "a", true; "a", true ] [ "a"; "a.meta" ])
    Assert.Equal(((false, "1 referenced blob(s) missing"), (false, "2 unreferenced blob(s)")),
                 AttachmentAudit.run [ "a", true; "b", false; "b", false ] [ "a"; "x"; "y"; "y.meta" ])
