#if NET8_PLUS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using E2ETest.Core.Api;
using E2ETest.Core.Automation;
using E2ETest.Core.Events;
using E2ETest.Core.Logging;
using E2ETest.Core.Model;
using E2ETest.Core.ProcessMgr;
using E2ETest.Core.Recording;

namespace E2ETest.Core
{
    /// <summary>단일 TestCase를 실행하여 TestResult를 생성하는 엔진. YAML/코드 공통 진입점.</summary>
    public sealed class Runner
    {
        public ILogger Logger { get; set; }
        public Action<RunnerEvent> OnEvent { get; set; }
        public string FfmpegPath { get; set; }

        public Runner()
        {
            Logger = new ConsoleLogger();
        }

        public TestResult Run(TestCase testCase)
        {
            var result = new TestResult
            {
                Case = testCase,
                Status = TestStatus.Running,
                StartedAt = DateTime.Now
            };

            string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string runDir = Path.Combine(testCase.Options.OutputDirectory ?? "out", "runs", runId + "_" + SafeName(testCase.Name));
            Directory.CreateDirectory(runDir);
            result.ReportDirectory = runDir;
            string framesDir = Path.Combine(runDir, "frames");

            Emit(RunnerEvent.RunStarted(testCase.Name, testCase.Steps.Count, testCase.SourcePath));
            Log(LogLevel.Info, "=== Running: " + testCase.Name + " ===");
            Log(LogLevel.Info, "Output: " + runDir);

            AppLauncher launcher = null;
            IAutomationBackend backend = null;
            IRecorder recorder = null;
            AppImpl app = null;
            try
            {
                Log(LogLevel.Info, "Launching: " + testCase.App.Path + " " + testCase.App.Args);
                launcher = new AppLauncher(testCase.App.Path, testCase.App.Args, testCase.App.WorkingDirectory);
                backend = new HybridBackend(Logger);
                recorder = new ScreenshotRecorder(framesDir);
                if (testCase.Options.RecordVideo) recorder.Start();

                app = new AppImpl(launcher, backend, recorder, Logger, testCase.App.StartupTimeoutMs);
                app.DefaultTimeoutMs = testCase.Options.DefaultTimeoutMs;

                // 녹화 영역을 메인 윈도우로 제한
                try
                {
                    var r = backend.GetMainWindow(launcher.ProcessId, 2000).BoundingRectangle;
                    if (!r.IsEmpty) recorder.SetRegion(r.X, r.Y, r.Width, r.Height);
                }
                catch { /* fall back to fullscreen */ }

                int idx = 0;
                foreach (var step in testCase.Steps)
                {
                    var sr = ExecuteStep(app, backend, recorder, step, idx, testCase.Steps.Count, framesDir);
                    result.Steps.Add(sr);
                    if (sr.Status == TestStatus.Failed)
                    {
                        result.Status = TestStatus.Failed;
                        result.Message = sr.Message;
                        break;
                    }
                    idx++;
                }
                if (result.Status != TestStatus.Failed) result.Status = TestStatus.Passed;
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.Message = ex.Message;
                Log(LogLevel.Error, "Run aborted: " + ex);
            }
            finally
            {
                if (app != null) { try { app.Dispose(); } catch { } }
                else
                {
                    if (recorder != null) { try { recorder.Dispose(); } catch { } }
                    if (backend != null) { try { backend.Dispose(); } catch { } }
                    if (launcher != null) { try { launcher.Dispose(); } catch { } }
                }
                result.EndedAt = DateTime.Now;

                // 비디오 합성 시도
                if (testCase.Options.RecordVideo && Directory.Exists(framesDir))
                {
                    try
                    {
                        string mp4 = Path.Combine(runDir, "video.mp4");
                        VideoComposer.ComposeMp4(framesDir, mp4, 6, FfmpegPath);
                        result.VideoPath = mp4;
                        Log(LogLevel.Info, "Video: " + mp4);
                    }
                    catch (Exception vex)
                    {
                        Log(LogLevel.Warn, "Video composition skipped: " + vex.Message + " (frames preserved at " + framesDir + ")");
                    }
                }

                WriteReport(result, runDir);
                Emit(RunnerEvent.RunEnded(result));
                Log(LogLevel.Info, "=== " + result.Status + ": " + testCase.Name + " ===");
            }
            return result;
        }

        private StepResult ExecuteStep(IApp app, IAutomationBackend backend, IRecorder recorder, TestStep step, int index, int total, string framesDir)
        {
            var sr = new StepResult { Step = step, Status = TestStatus.Running, StartedAt = DateTime.Now };
            Log(LogLevel.Info, string.Format("  [{0}/{1}] {2}", index + 1, total, step));
            Emit(RunnerEvent.StepStarted(index, total, step));
            try
            {
                switch ((step.Action ?? "").ToLowerInvariant())
                {
                    case "click":
                        app.Find(step.Target).Click(); break;
                    case "type":
                        app.Find(step.Target).Type(step.Value); break;
                    case "fill":
                        app.Find(step.Target).Fill(step.Value); break;
                    case "focus":
                        app.Find(step.Target).Focus(); break;
                    case "wait":
                        int ms; int.TryParse(step.Value, out ms); app.Wait(ms > 0 ? ms : 1000); break;
                    case "screenshot":
                        app.Screenshot(step.Name ?? ("step_" + index)); break;
                    case "assert.visible":
                        app.Find(step.Target).ExpectVisible(); break;
                    case "assert.text":
                        app.Find(step.Target).ExpectText(step.Value); break;
                    case "assert.enabled":
                        app.Find(step.Target).ExpectEnabled(); break;
                    default:
                        throw new NotSupportedException("unknown action: " + step.Action);
                }
                sr.Status = TestStatus.Passed;

                // 스텝별 스크린샷
                string shotName = "step_" + (index + 1).ToString("D3") + "_" + SafeName(step.Action);
                recorder.CaptureFrame(shotName);
                sr.ScreenshotPath = Path.Combine(framesDir, shotName + ".png");
            }
            catch (Exception ex)
            {
                sr.Status = TestStatus.Failed;
                sr.Message = ex.Message;

                // 실패 스크린샷
                string shotName = "fail_" + (index + 1).ToString("D3");
                try { recorder.CaptureFrame(shotName); sr.ScreenshotPath = Path.Combine(framesDir, shotName + ".png"); } catch { }

                Log(LogLevel.Error, "    ✗ " + ex.Message);
            }
            finally
            {
                sr.EndedAt = DateTime.Now;
                Emit(RunnerEvent.StepEnded(index, sr));
            }
            return sr;
        }

        private void WriteReport(TestResult r, string dir)
        {
            string path = Path.Combine(dir, "report.json");
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("{");
                sw.WriteLine("  \"name\": " + Q(r.Case.Name) + ",");
                sw.WriteLine("  \"status\": " + Q(r.Status.ToString()) + ",");
                sw.WriteLine("  \"message\": " + Q(r.Message) + ",");
                sw.WriteLine("  \"startedAt\": " + Q(r.StartedAt.ToString("o")) + ",");
                sw.WriteLine("  \"endedAt\": " + Q(r.EndedAt.ToString("o")) + ",");
                sw.WriteLine("  \"videoPath\": " + Q(r.VideoPath) + ",");
                sw.WriteLine("  \"steps\": [");
                for (int i = 0; i < r.Steps.Count; i++)
                {
                    var s = r.Steps[i];
                    sw.Write("    {");
                    sw.Write("\"action\": " + Q(s.Step.Action));
                    sw.Write(", \"target\": " + Q(s.Step.Target));
                    sw.Write(", \"value\": " + Q(s.Step.Value));
                    sw.Write(", \"status\": " + Q(s.Status.ToString()));
                    sw.Write(", \"message\": " + Q(s.Message));
                    sw.Write(", \"durationMs\": " + s.DurationMs);
                    sw.Write(", \"screenshot\": " + Q(s.ScreenshotPath));
                    sw.WriteLine("}" + (i < r.Steps.Count - 1 ? "," : ""));
                }
                sw.WriteLine("  ]");
                sw.WriteLine("}");
            }
        }

        private static string Q(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }

        private static string SafeName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "test";
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Replace(' ', '_');
        }

        private void Log(LogLevel lv, string m)
        {
            Logger.Log(lv, m);
            Emit(RunnerEvent.Log(lv.ToString(), m));
        }

        private void Emit(RunnerEvent e) { if (OnEvent != null) OnEvent(e); }
    }
}
#endif
