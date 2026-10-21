module RowFocusSidebarSelectionSamples
open System

let g (n: int) = Guid(n, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)
let a, b, c, d = g 1, g 2, g 3, g 4
let pinned = set [ a; b ]
let allPinned (ids: Guid list) = not (List.isEmpty ids) && ids |> List.forall pinned.Contains
let vAll = [ a; b; c ]
let vAb = [ a; b ]
let vNone : Guid list = []
/// 步骤：(名称, 可见行)
let steps =
    [ "chrome", vAll; "enter a", vAll; "enter b", vAll; "enter a", vAll; "chrome", vAb; "toggle c", vAll
      "allvis", vAll; "allvis", vAll; "allvis", vAb; "toggle a", vAb; "toggle b", vAb; "enter d", vAll
      "chrome", vAll; "allvis", vNone; "chrome", vNone; "toggle d", vAll; "enter c", vAll; "exit", vAll; "toggle a", vAll ]
