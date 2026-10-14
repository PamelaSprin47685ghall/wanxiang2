module Tests

open Xunit
open Wanxiang.UI

// 迁自旧 LibraryTests。
[<Fact>]
let ``reveal targets resolve against the loaded window`` () =
    let loaded = [ 10UL; 12UL; 15UL ]
    Assert.Equal(Some 12UL, RevealTarget.resolve (RevealMessage 12UL) loaded)
    Assert.Equal(None, RevealTarget.resolve (RevealMessage 3UL) loaded)
    Assert.Equal(Some 10UL, RevealTarget.resolve (RevealAfter None) loaded)
    Assert.Equal(Some 15UL, RevealTarget.resolve (RevealAfter(Some 12UL)) loaded)
    Assert.Equal(None, RevealTarget.resolve (RevealAfter(Some 15UL)) loaded)
