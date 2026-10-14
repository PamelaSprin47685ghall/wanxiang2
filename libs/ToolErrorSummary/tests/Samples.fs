module Samples

let results : string option list =
    [ None; Some ""; Some "   "; Some "ok"; Some "error: boom"; Some "Error:"; Some "{\"error\":\"bad\"}"
      Some "{\"error\":{\"message\":\"inner\"}}"; Some "{\"error\":{\"code\":1}}"; Some "{\"error\":true}"; Some "{\"error\":false}"
      Some "{\"error\":null,\"ok\":1}"; Some "{\"error\":5}"; Some "{\"isError\":true,\"message\":\"m\"}"; Some "{\"is_error\":true}"
      Some "{\"isError\":true,\"error\":{\"message\":\"x\"}}"; Some "{\"success\":false,\"msg\":\" why \"}"; Some "{\"success\":true}"
      Some "{\"status\":\"Failed\",\"reason\":\"r\"}"; Some "{\"status\":\"ok\"}"; Some "{\"status\":\"error\"}"; Some "{bad json}"
      Some "Traceback (most recent call last):\n  File \"x\"\nValueError: nope\n"; Some "Command failed: exit 1\nmore"
      Some "500 Internal Server Error"; Some "oops internal server error here"; Some "line\nfatal: not a git repository\n"; Some "fatal:"
      Some "HTTP/1.1 401 Unauthorized\nbody"; Some "HTTP 403: Forbidden"; Some "x\n404 Not Found"; Some "x\n404 Not Found extra"
      Some "ls: /root: Permission denied"; Some "Access denied."; Some "bash: foo: command not found"; Some "command not found"
      Some "[1,2]"; Some "plain output\nwith lines" ]
