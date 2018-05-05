﻿//
// TaskRunnerProjectExtension.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2018 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TaskRunnerExplorer;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace MonoDevelop.TaskRunner
{
	class TaskRunnerProjectExtension : ProjectExtension
	{
		protected override async Task<BuildResult> OnBuild (
			ProgressMonitor monitor,
			ConfigurationSelector configuration,
			OperationContext operationContext)
		{
			GroupedTaskRunnerInformation tasks = TaskRunnerServices.Workspace.GetGroupedTask (Project);
			if (tasks == null) {
				return await base.OnBuild (monitor, configuration, operationContext);
			}

			BuildResult preBuildTasksResult = await RunBuildTasks (tasks, TaskRunnerBindEvent.BeforeBuild);
			BuildResult result = await base.OnBuild (monitor, configuration, operationContext);
			BuildResult postBuildTasksResult = await RunBuildTasks (tasks, TaskRunnerBindEvent.AfterBuild);

			return CombineBuildResults (preBuildTasksResult, result, postBuildTasksResult);
		}

		BuildResult CombineBuildResults (params BuildResult[] results)
		{
			var combinedResult = new BuildResult ();
			combinedResult.SourceTarget = Project;
			combinedResult.Append (results);

			return combinedResult;
		}

		async Task<BuildResult> RunBuildTasks (GroupedTaskRunnerInformation tasks, TaskRunnerBindEvent bindEvent)
		{
			var buildResult = new BuildResult ();

			foreach (ITaskRunnerNode node in tasks.GetTasks (bindEvent)) {
				ITaskRunnerCommandResult result = await TaskRunnerServices.Workspace.RunTask (node);
				if (result.ExitCode != 0) {
					buildResult.AddWarning (GetBuildWarning (node, result));
				}
			}

			return buildResult;
		}

		static string GetBuildWarning (ITaskRunnerNode task, ITaskRunnerCommandResult result)
		{
			return GettextCatalog.GetString (
				"Task {0} failed with exit code {1}. Command: {2}",
				task.Name,
				result.ExitCode,
				task.Command.ToCommandLine ());
		}
	}
}