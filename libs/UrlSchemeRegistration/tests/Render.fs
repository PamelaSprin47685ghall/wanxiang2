module Render

open Wanxiang.UI

let all () =
    String.concat
        ""
        [
            yield UrlSchemeRegistration.desktopFileName + "\n"
            yield UrlSchemeRegistration.desktopEntry "/opt/wanxiang/wanxiang"
            yield UrlSchemeRegistration.desktopEntry (UrlSchemeRegistration.quote "/home/a b/万象")
            for p in [ "/a b/w"; "/a/w"; ""; "C:\\Program Files\\Wanxiang\\wanxiang.exe" ] ->
                UrlSchemeRegistration.quote p + "\n"
        ]
