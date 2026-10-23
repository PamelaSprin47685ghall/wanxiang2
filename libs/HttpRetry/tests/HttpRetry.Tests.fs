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

[<Fact>]
let ``Int32.MaxValue retries still retries transient failures instead of overflowing to one attempt`` () =
    Assert.Equal((HttpStatusCode.OK, 2), send [ HttpStatusCode.ServiceUnavailable ] Int32.MaxValue)

type TrackedContent() =
    inherit HttpContent()
    member val Disposed = false with get, set

    override _.SerializeToStreamAsync(_stream, _context) = Task.CompletedTask

    override _.TryComputeLength(length: byref<int64>) =
        length <- 0L
        true

    override this.Dispose(disposing: bool) =
        if disposing then
            this.Disposed <- true

        base.Dispose(disposing)

[<Fact>]
let ``each request message and its content are disposed after sending`` () =
    let contents = ResizeArray<TrackedContent>()
    let h = new Scripted([ HttpStatusCode.ServiceUnavailable ])
    use http = new HttpClient(h)

    use response =
        (HttpRetry.sendWithRetry
            http
            (fun () ->
                let c = new TrackedContent()
                contents.Add c
                new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1/", Content = c))
            2
            CancellationToken.None)
            .Result

    Assert.Equal(HttpStatusCode.OK, response.StatusCode)
    Assert.Equal(2, contents.Count)
    Assert.All(contents, fun c -> Assert.True(c.Disposed, "request content was not disposed"))

[<Fact>]
let ``expired or non-positive Retry-After is not recorded as RetryAfterSeconds`` () =
    use expiredDate = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    expiredDate.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30.0))
    let ex1 = (HttpRetry.statusError expiredDate "rate limited").Value
    Assert.False(ex1.Data.Contains("RetryAfterSeconds"))

    use zeroDelta = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    zeroDelta.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(TimeSpan.Zero)
    let ex2 = (HttpRetry.statusError zeroDelta "rate limited").Value
    Assert.False(ex2.Data.Contains("RetryAfterSeconds"))

    use futureDate = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    futureDate.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(12.0))
    let ex3 = (HttpRetry.statusError futureDate "rate limited").Value
    let recorded = unbox<int> ex3.Data["RetryAfterSeconds"]
    Assert.InRange(recorded, 10, 13)

    use hugeDate = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    hugeDate.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(DateTimeOffset.MaxValue)
    let ex4 = (HttpRetry.statusError hugeDate "rate limited").Value
    Assert.Equal(Int32.MaxValue, unbox<int> ex4.Data["RetryAfterSeconds"])

type HugeRetryAfterHandler() =
    inherit HttpMessageHandler()
    member val Calls = 0 with get, set

    override this.SendAsync(_r, _ct) =
        this.Calls <- this.Calls + 1
        let r = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        r.Headers.RetryAfter <- Headers.RetryConditionHeaderValue(TimeSpan.FromDays 60.0)
        Task.FromResult r

[<Fact>]
let ``Retry-After larger than Task.Delay maximum does not throw ArgumentOutOfRangeException`` () =
    use cts = new CancellationTokenSource()
    use handler = new HugeRetryAfterHandler()
    use http = new HttpClient(handler)

    // 处理器同步返回，调用只会在真正进入重试等待后交回未完成任务，不依赖定时取消抢跑。
    let pending =
        HttpRetry.sendWithRetry
            http
            (fun () -> new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1/"))
            1
            cts.Token

    try
        Assert.Equal(1, handler.Calls)
        Assert.False(pending.IsCompleted, "应进入重试等待，而不是在创建延迟时失败")
    finally
        cts.Cancel()

    Assert.ThrowsAny<OperationCanceledException>(fun () -> pending.GetAwaiter().GetResult() |> ignore)
    |> ignore

    Assert.Equal(1, handler.Calls)
