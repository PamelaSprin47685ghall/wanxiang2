module StartReadySamples
let flags = [ for a in [ false; true ] do for c in [ false; true ] do for g in [ false; true ] do for e in [ false; true ] -> a, c, g, e ]
let actives = [ None, None; None, Some 3; Some 1, None; Some 1, Some 0; Some 1, Some 2 ]
