module ShutdownSignalsTests

open System.Threading.Tasks
open Xunit
open Wanxiang.App

[<Fact>]
let ``第一次优雅，之后都强制`` () =
    let s = ShutdownSignals()
    Assert.Equal(ShutdownStep.Graceful, s.Signal())
    Assert.Equal(ShutdownStep.Force, s.Signal())
    Assert.Equal(ShutdownStep.Force, s.Signal())
    Assert.Equal(130, ShutdownSignals.ForceExitCode)

[<Fact>]
let ``并发到达的信号恰好一次优雅`` () =
    for _ in 1..50 do
        let s = ShutdownSignals()
        let results = [| for _ in 1..16 -> Task.Run(fun () -> s.Signal()) |]
        let steps = Task.WhenAll(results).Result
        Assert.Equal(1, steps |> Array.filter ((=) ShutdownStep.Graceful) |> Array.length)
