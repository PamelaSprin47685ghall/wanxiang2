module HttpRetryTests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``2xx is not an error; non-2xx carries status, body and Retry-After`` () =
    use ok = new HttpResponseMessage(HttpStatusCode.OK)
    Assert.True((HttpRetry.statusError ok "").IsNone)
    use bad = new HttpResponseMessage(HttpStatusCode.BadRequest)
    let ex = (HttpRetry.statusError bad "bad request body").Value
    Assert.Contains("(400) bad request body", ex.Message)
    Assert.Equal(Nullable HttpStatusCode.BadRequest, ex.StatusCode)
    use limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    limited.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds 30.0)
    Assert.Equal(30, unbox<int> (HttpRetry.statusError limited "").Value.Data["RetryAfterSeconds"])

type Scripted(codes: HttpStatusCode list) =
    inherit HttpMessageHandler()
    let mutable rest = codes
    member val Calls = 0 with get, set

    override this.SendAsync(_r, _ct) =
        this.Calls <- this.Calls + 1

        let code =
            match rest with
            | c :: tail ->
                rest <- tail
                c
            | [] -> HttpStatusCode.OK

        let r = new HttpResponseMessage(code)
        r.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds 1.0)
        Task.FromResult r

let send (codes: HttpStatusCode list) (retries: int) =
    let h = new Scripted(codes)
    use http = new HttpClient(h)

    let r =
        (HttpRetry.sendWithRetry
            http
            (fun () -> new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1/"))
            retries
            CancellationToken.None)
            .Result

    r.StatusCode, h.Calls

[<Fact>]
let ``transient failures are retried up to the limit, then the last response is returned`` () =
    Assert.Equal((HttpStatusCode.OK, 3), send [ HttpStatusCode.ServiceUnavailable; HttpStatusCode.TooManyRequests ] 2)

    Assert.Equal(
        (HttpStatusCode.ServiceUnavailable, 2),
        send
            [
                HttpStatusCode.ServiceUnavailable
                HttpStatusCode.ServiceUnavailable
                HttpStatusCode.ServiceUnavailable
            ]
            1
    )

[<Fact>]
let ``non-transient failures and zero retries are not retried`` () =
    Assert.Equal((HttpStatusCode.BadRequest, 1), send [ HttpStatusCode.BadRequest ] 3)
    Assert.Equal((HttpStatusCode.ServiceUnavailable, 1), send [ HttpStatusCode.ServiceUnavailable ] 0)

[<Theory>]
[<InlineData(1, 250.0)>]
[<InlineData(2, 500.0)>]
[<InlineData(3, 1000.0)>]
[<InlineData(6, 8000.0)>]
[<InlineData(31, 8000.0)>]
[<InlineData(32, 8000.0)>]
[<InlineData(64, 8000.0)>]
[<InlineData(1000, 8000.0)>]
let ``退避时长翻倍并收敛到八秒上限，位移不再溢出`` (attempt: int, expectedMs: float) =
    let delay = HttpRetry.backoffDelay attempt
    Assert.True(delay >= TimeSpan.Zero, $"attempt={attempt} 的退避时长为负：{delay}")

    Assert.Equal(expectedMs, delay.TotalMilliseconds)

[<Fact>]
let ``退避时长在所有尝试次数上都非负`` () =
    for attempt in 1..200 do
        Assert.True(HttpRetry.backoffDelay attempt >= TimeSpan.Zero)
