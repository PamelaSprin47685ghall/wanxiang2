module Samples

open System

let now = DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours 8.0)
let ats =
    [ now; now.AddMinutes -5.0; DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours 8.0)
      DateTimeOffset(2026, 10, 13, 23, 59, 0, TimeSpan.FromHours 8.0); DateTimeOffset(2026, 10, 13, 0, 1, 0, TimeSpan.FromHours 8.0)
      DateTimeOffset(2026, 10, 12, 23, 59, 0, TimeSpan.FromHours 8.0); DateTimeOffset(2026, 1, 1, 7, 5, 0, TimeSpan.FromHours 8.0)
      DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.FromHours 8.0); now.AddHours 3.0 ]
