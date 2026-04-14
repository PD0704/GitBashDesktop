using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace GitBashDesktop.Services
{
    public class GitService
    {
        private string _repoPath = "";
        private Action<string>? _terminalOutput;

        public bool HasRepo => !string.IsNullOrEmpty(_repoPath);
        public string RepoPath => _repoPath;

        public void Initialize(string repoPath, Action<string>? terminalOutput)
        {
            _repoPath = repoPath;
            _terminalOutput = terminalOutput;
        }

        // ── Core runner ──────────────────────────────────────────────────────
        public async Task<GitResult> RunAsync(string arguments)
        {
            PrintToTerminal($"$ git {arguments}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = _repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                output.AppendLine(e.Data);
                PrintToTerminal(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                error.AppendLine(e.Data);
                PrintToTerminal(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var result = new GitResult
            {
                Output = output.ToString().Trim(),
                Error = error.ToString().Trim(),
                ExitCode = process.ExitCode,
                Success = process.ExitCode == 0
            };

            if (!result.Success && !string.IsNullOrEmpty(result.Error))
                PrintToTerminal($"[Error] {result.Error}");

            PrintToTerminal("");
            return result;
        }

        // ── Convenience methods ──────────────────────────────────────────────
        public async Task<GitResult> StatusAsync()
            => await RunAsync("status");

        public async Task<GitResult> StatusShortAsync()
            => await RunAsync("status --short");

        public async Task<GitResult> AddAsync(string files)
            => await RunAsync($"add {files}");

        public async Task<GitResult> AddAllAsync()
            => await RunAsync("add -A");

        public async Task<GitResult> CommitAsync(string message)
            => await RunAsync($"commit -m \"{message}\"");

        public async Task<GitResult> PushAsync(string remote = "origin", string branch = "")
        {
            var target = string.IsNullOrEmpty(branch)
                ? remote
                : $"{remote} {branch}";
            return await RunAsync($"push {target}");
        }

        public async Task<GitResult> PullAsync()
            => await RunAsync("pull");

        public async Task<GitResult> FetchAsync()
            => await RunAsync("fetch --all");

        public async Task<GitResult> GetBranchesAsync()
            => await RunAsync("branch -a");

        public async Task<GitResult> GetCurrentBranchAsync()
            => await RunAsync("rev-parse --abbrev-ref HEAD");

        public async Task<GitResult> SwitchBranchAsync(string branch)
            => await RunAsync($"checkout {branch}");

        public async Task<GitResult> CreateBranchAsync(string branch)
            => await RunAsync($"checkout -b {branch}");

        public async Task<GitResult> DeleteBranchAsync(string branch)
            => await RunAsync($"branch -d {branch}");

        public async Task<GitResult> GetLogAsync(int count = 50)
            => await RunAsync($"log --oneline --graph --decorate -n {count}");

        public async Task<GitResult> GetLogDetailedAsync(int count = 50)
            => await RunAsync(
                $"log --pretty=format:\"%H|%an|%ae|%ad|%s\" --date=short -n {count}");

        public async Task<GitResult> GetConflictsAsync()
            => await RunAsync("diff --name-only --diff-filter=U");

        public async Task<GitResult> GetDiffAsync(string file = "")
            => await RunAsync(string.IsNullOrEmpty(file) ? "diff" : $"diff {file}");

        public async Task<GitResult> InitAsync(string path)
        {
            _repoPath = path;
            return await RunAsync("init");
        }

        public async Task<GitResult> CloneAsync(string url, string targetPath)
        {
            // Clone runs outside any repo so no working directory needed
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {url} \"{targetPath}\"",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath) ?? "",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            PrintToTerminal($"$ git clone {url}");

            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                output.AppendLine(e.Data);
                PrintToTerminal(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                PrintToTerminal(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
                _repoPath = targetPath;

            return new GitResult
            {
                Output = output.ToString().Trim(),
                ExitCode = process.ExitCode,
                Success = process.ExitCode == 0
            };
        }

        public async Task<string> GetUserNameAsync()
        {
            var r = await RunAsync("config user.name");
            return r.Output.Trim();
        }

        public async Task<string> GetUserEmailAsync()
        {
            var r = await RunAsync("config user.email");
            return r.Output.Trim();
        }

        public async Task<bool> IsGitRepoAsync(string path)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    WorkingDirectory = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        // ── Terminal helper ──────────────────────────────────────────────────
        private void PrintToTerminal(string text)
        {
            _terminalOutput?.Invoke(text);
        }
    }

    // ── Result model ─────────────────────────────────────────────────────────
    public class GitResult
    {
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }
        public bool Success { get; set; }
    }
}