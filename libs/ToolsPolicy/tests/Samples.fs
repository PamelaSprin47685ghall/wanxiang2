module Samples

let policies = [ "auto"; "first"; "always"; ""; "weird" ]
let rootsList = [ []; [ "/home/a"; "C:\\data" ] ]
let maps = [ Map.empty; Map.ofList [ "shell", "always"; "read", "auto" ] ]
