using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WPFApplication.Services
{
    public class ProcessRunner
    {
        public event Action<string>? OutputReceived;
        public event Action<string>? ErrorReceived;

        public async Task<(int ExitCode, string FullStdOut, string FullStdErr)> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken = default)
        {
            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var tcs = new TaskCompletionSource<int>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdOutBuilder.AppendLine(e.Data);
                    OutputReceived?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuilder.AppendLine(e.Data);
                    ErrorReceived?.Invoke(e.Data);
                }
            };

            process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch {  }
            });

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int exitCode = await tcs.Task;
            await Task.Run(() => process.WaitForExit(), CancellationToken.None);

            return (exitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
        }
    }
}
