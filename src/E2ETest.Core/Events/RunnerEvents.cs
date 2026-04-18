using System;
using E2ETest.Core.Model;

namespace E2ETest.Core.Events
{
    /// <summary>Runner → Dashboard IPC 이벤트. JSON 직렬화 가능한 단순 POCO.</summary>
    public sealed class RunnerEvent
    {
        public string Type { get; set; }        // run.started, step.started, step.ended, run.ended, log, screenshot
        public string Timestamp { get; set; }

        // 범용 payload 필드 — 타입별로 다른 필드만 채워짐.
        public string TestName { get; set; }
        public string StepAction { get; set; }
        public string StepTarget { get; set; }
        public string StepName { get; set; }
        public string Status { get; set; }       // Pending/Running/Passed/Failed/Skipped
        public string Message { get; set; }
        public string ScreenshotBase64 { get; set; }
        public string ScreenshotPath { get; set; }
        public string LogLevel { get; set; }
        public int? StepIndex { get; set; }
        public int? TotalSteps { get; set; }
        public long? DurationMs { get; set; }

        public static RunnerEvent RunStarted(string testName, int totalSteps)
        {
            return new RunnerEvent { Type = "run.started", Timestamp = Now(), TestName = testName, TotalSteps = totalSteps };
        }
        public static RunnerEvent StepStarted(int index, int total, TestStep step)
        {
            return new RunnerEvent { Type = "step.started", Timestamp = Now(), StepIndex = index, TotalSteps = total, StepAction = step.Action, StepTarget = step.Target, StepName = step.Name };
        }
        public static RunnerEvent StepEnded(int index, StepResult result)
        {
            return new RunnerEvent { Type = "step.ended", Timestamp = Now(), StepIndex = index, Status = result.Status.ToString(), Message = result.Message, DurationMs = result.DurationMs, ScreenshotPath = result.ScreenshotPath };
        }
        public static RunnerEvent RunEnded(TestResult result)
        {
            return new RunnerEvent { Type = "run.ended", Timestamp = Now(), TestName = result.Case != null ? result.Case.Name : null, Status = result.Status.ToString(), Message = result.Message };
        }
        public static RunnerEvent Log(string level, string message)
        {
            return new RunnerEvent { Type = "log", Timestamp = Now(), LogLevel = level, Message = message };
        }
        public static RunnerEvent Screenshot(string path, string base64)
        {
            return new RunnerEvent { Type = "screenshot", Timestamp = Now(), ScreenshotPath = path, ScreenshotBase64 = base64 };
        }

        private static string Now()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }
}
