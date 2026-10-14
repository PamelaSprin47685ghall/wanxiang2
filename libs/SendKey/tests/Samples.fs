module Samples

let combos = [ for e in [ true; false ] do for s in [ false; true ] do for c in [ false; true ] -> e, s, c ]
