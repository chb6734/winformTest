using System;
using System.Collections.Generic;

namespace E2ETest.Core.Model
{
    /// <summary>YAML/코드 공통 중간 표현.</summary>
    public sealed class TestCase
    {
        public string Name { get; set; }
        public AppSpec App { get; set; }
        public List<TestStep> Steps { get; set; }
        public TestOptions Options { get; set; }

        public TestCase() { Steps = new List<TestStep>(); Options = new TestOptions(); }
    }

    public sealed class AppSpec
    {
        public string Path { get; set; }
        public string Args { get; set; }
        public string WorkingDirectory { get; set; }
        /// <summary>선택적으로 메인 윈도우 제목 예상치. 명시하면 런칭 후 이 윈도우를 타겟으로.</summary>
        public string MainWindowTitle { get; set; }
        public int StartupTimeoutMs { get; set; }

        public AppSpec() { StartupTimeoutMs = 15000; }
    }

    public sealed class TestStep
    {
        public string Action { get; set; }      // click, type, assert.visible, screenshot, wait, ...
        public string Target { get; set; }      // selector (when applicable)
        public string Value { get; set; }       // typed text, expected text, etc.
        public string Name { get; set; }        // screenshot file name, step display name
        public int? TimeoutMs { get; set; }

        public Dictionary<string, string> Extras { get; set; }

        public TestStep() { Extras = new Dictionary<string, string>(); }

        public override string ToString()
        {
            return Action + (string.IsNullOrEmpty(Target) ? "" : " " + Target) + (string.IsNullOrEmpty(Value) ? "" : " = " + Value);
        }
    }

    public sealed class TestOptions
    {
        public int DefaultTimeoutMs { get; set; }
        public bool RecordVideo { get; set; }
        public bool Headless { get; set; }
        public string OutputDirectory { get; set; }

        public TestOptions()
        {
            DefaultTimeoutMs = 10000;
            RecordVideo = true;
            OutputDirectory = "out";
        }
    }

    public enum TestStatus
    {
        Pending = 0,
        Running = 1,
        Passed = 2,
        Failed = 3,
        Skipped = 4
    }

    public sealed class StepResult
    {
        public TestStep Step { get; set; }
        public TestStatus Status { get; set; }
        public string Message { get; set; }
        public string ScreenshotPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public long DurationMs
        {
            get { return (long)(EndedAt - StartedAt).TotalMilliseconds; }
        }
    }

    public sealed class TestResult
    {
        public TestCase Case { get; set; }
        public TestStatus Status { get; set; }
        public string Message { get; set; }
        public List<StepResult> Steps { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string VideoPath { get; set; }
        public string ReportDirectory { get; set; }

        public TestResult() { Steps = new List<StepResult>(); }
    }
}
