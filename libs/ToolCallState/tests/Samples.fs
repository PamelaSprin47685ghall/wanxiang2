module Samples

let awaitings = [ Set.empty; Set.ofList [ "c1" ]; Set.ofList [ "c2"; "c3" ] ]
let ids = [ "c1"; "c2" ]
let outcomes : string option option list = [ None; Some None; Some(Some "fatal: boom") ]
