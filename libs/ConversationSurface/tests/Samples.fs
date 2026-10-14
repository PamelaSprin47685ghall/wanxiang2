module Samples

let flags = [ for a in [ false; true ] do for b in [ false; true ] do for c in [ false; true ] do for d in [ false; true ] -> a, b, c, d ]
