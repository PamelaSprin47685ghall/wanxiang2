module HttpFailureFactsTests

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``retry after values, headers and data`` () =
    let now = DateTimeOffset.Parse "2026-01-01T00:00:00Z"
    Assert.Equal(Some 5, HttpFailureFacts.retryAfterOfValue now "5")
    Assert.Equal(Some 60, HttpFailureFacts.retryAfterOfValue now "2026-01-01T00:01:00Z")
    Assert.Equal(None, HttpFailureFacts.retryAfterOfValue now "0")
    let r = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    r.Headers.TryAddWithoutValidation("Retry-After", "12") |> ignore
    Assert.Equal(Some 12, HttpFailureFacts.retryAfterOfResponse r)
    let ex = Exception "x"
    ex.Data["RetryAfterSeconds"] <- box 3
    Assert.Equal(Some 3, HttpFailureFacts.retryAfterOfData ex)

[<Fact>]
let ``network, 5xx text and detail`` () =
    Assert.True(HttpFailureFacts.isNetwork (Exception("outer", SocketException 111)))
    Assert.False(HttpFailureFacts.isNetwork (HttpRequestException("x", null, Nullable HttpStatusCode.BadRequest)))
    Assert.Equal(Some 503, HttpFailureFacts.fiveXxIn "Service Unavailable")
    Assert.Equal("a b", HttpFailureFacts.detail "<p>a</p>\n\n b")
    Assert.EndsWith("…", HttpFailureFacts.detail (String('x', 700)))
