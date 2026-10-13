module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``附件缺失与孤儿判定与旧实现一致`` () =
    Assert.Equal(((true, "ok"), (true, "ok")), AttachmentAudit.run [ "a", true; "a", true ] [ "a"; "a.meta" ])
    Assert.Equal(((false, "1 referenced blob(s) missing"), (false, "2 unreferenced blob(s)")),
                 AttachmentAudit.run [ "a", true; "b", false; "b", false ] [ "a"; "x"; "y"; "y.meta" ])

[<Fact>]
let ``无引用无 blob 时两项都通过`` () =
    Assert.Equal(((true, "ok"), (true, "ok")), AttachmentAudit.run [] [])

[<Fact>]
let ``同一缺失 blob 被多次引用只计一次`` () =
    let (m, _) = AttachmentAudit.run [ "a", false; "a", false; "b", true ] [ "b" ]
    Assert.Equal((false, "1 referenced blob(s) missing"), m)

[<Fact>]
let ``meta 文件不算孤儿，未引用的 blob 才算`` () =
    let (_, o) = AttachmentAudit.run [ "a", true ] [ "a"; "a.meta"; "z"; "y" ]
    Assert.Equal((false, "2 unreferenced blob(s)"), o)
