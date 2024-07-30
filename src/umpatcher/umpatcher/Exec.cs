/*
    Copyright (C) 2018 de4dot@gmail.com

    This file is part of umpatcher

    umpatcher is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    umpatcher is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with umpatcher.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Diagnostics;
using System.Text;
using System.Threading;

namespace UnityMonoDllSourceCodePatcher {
	static class Exec {
		public static int Run(string workingDir, string filename, string args, out string standardOutput, out string standardError) {
			// Git status messages larger than the process output buffer are very likely,
			// in which case this method would just hang on WaitForExit.
			// To prevent this, drain the buffers to string builders as we go.
			var output = new StringBuilder();
            var error = new StringBuilder();
			
            // If the process DataReceived delegates are called after the wait handles are disposed,
            // an ObjectDisposedException is thrown.
            // To prevent this, we using the wait handles first so that they are disposed last.
            using var outputWaitHandle = new AutoResetEvent(false);
            using var errorWaitHandle = new AutoResetEvent(false);
            using var process = new Process {
	            StartInfo = {
		            FileName = filename,
		            Arguments = args,
		            WorkingDirectory = workingDir,
		            CreateNoWindow = true,
		            UseShellExecute = false,
					RedirectStandardOutput = true,
		            RedirectStandardError = true,
	            },
            };
            process.OutputDataReceived += (sender, e) => {
				if (e.Data == null) outputWaitHandle.Set();
				else output.AppendLine(e.Data);
			};
			process.ErrorDataReceived += (sender, e) => {
				if (e.Data == null) errorWaitHandle.Set();
				else error.AppendLine(e.Data);
			};

			if (!process.Start()) 
				throw new ProgramException($"Process did not start for command: {filename} {args}");
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			if (!process.WaitForExit(Constants.ProcessTimeoutMilliseconds) ||
			    !outputWaitHandle.WaitOne(Constants.ProcessTimeoutMilliseconds) ||
			    !errorWaitHandle.WaitOne(Constants.ProcessTimeoutMilliseconds))
				throw new ProgramException($"Process timed out for command: {filename} {args}");
			
			standardOutput = output.ToString();
			standardError = error.ToString();
			return process.ExitCode;
		}
	}
}
