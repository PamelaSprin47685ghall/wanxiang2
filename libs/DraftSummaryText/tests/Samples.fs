module Samples

let cases = [ for p in [ true; false ] do for d in [ 0; 1; 5 ] do for u in [ 0; 2 ] -> p, d, u ]
