module RowFocusSearchSegmentsSamples
open System
let queries = [ ""; "   "; "模型"; "存储 模型"; "aa"; "AB"; "x\ty x"; "不存在" ]
let texts = [ ""; "存储模型的方案"; "aaaa"; "abAB ab"; "x y z"; "万象 模型 设计草案" ]
let args = [ ""; "   "; "{\"path\":\"\\u4e2d\\u6587.txt\",\"n\":1}"; "[1,2,{\"a\":null}]"; "not json"; "null"; "\"s<>&\"" ]
let policies = [ "first"; "always"; ""; "never" ]
let statuses = [ "approved"; "denied"; "expired"; "cancelled"; "other" ]
let spans = [ -5.0; 0.0; 0.2; 59.0; 59.1; 60.0; 61.0; 299.5; 3600.0 ]
let codes = [ "123456"; "12345"; ""; "abcdefg" ]
let urls = [ "ws://127.0.0.1:8787/ws"; "wss://host.example:443/ws"; "ws://localhost:9000/ws"; "  ws://192.168.1.5:8787/ws  "; "http://x/"; ""; "ws://[::1]:8787/ws" ]
let now = DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero)
let deviceJson = """[{"id":"a","name":"Phone","createdAt":"2026-10-01T08:00:00Z","lastSeen":"2026-10-14T11:59:30Z","online":false},
 {"id":"b","name":"Pad","lastSeen":"2026-10-14T11:20:00Z"},{"id":"c","lastSeen":"2026-10-14T03:00:00Z","current":true},
 {"id":"d","lastSeen":"2026-10-01T03:00:00Z","desktop":true},{"id":"e","revoked":true},{"id":"f","online":true},{"id":"g"},
 {"id":5,"name":null,"createdAt":"bad","revoked":"yes"}, 7, "x"]"""
