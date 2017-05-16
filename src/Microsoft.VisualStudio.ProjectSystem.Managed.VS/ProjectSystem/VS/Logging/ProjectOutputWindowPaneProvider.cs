﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Logging
{
    [Export(typeof(IProjectOutputWindowPaneProvider))]
    internal class ProjectOutputWindowPaneProvider : IProjectOutputWindowPaneProvider
    {
        private static readonly Guid ProjectOutputWindowPaneGuid = new Guid("{A18568CC-CA90-4AEE-9D14-A7E9D753B544}");

        private readonly IProjectThreadingService _threadingService;
        private readonly IVsOptionalService<IVsOutputWindow> _outputWindow;
        private readonly AsyncLazy<IVsOutputWindowPane> _outputWindowPane;

        [ImportingConstructor]
        public ProjectOutputWindowPaneProvider(IProjectThreadingService threadingService, IVsOptionalService<SVsOutputWindow, IVsOutputWindow> outputWindow)
        {
            Requires.NotNull(threadingService, nameof(threadingService));
            Requires.NotNull(outputWindow, nameof(outputWindow));

            _threadingService = threadingService;
            _outputWindow = outputWindow;
            _outputWindowPane = new AsyncLazy<IVsOutputWindowPane>(CreateOutputWindowPaneAsync, threadingService.JoinableTaskFactory);
        }

        public Task<IVsOutputWindowPane> GetOutputWindowPaneAsync()
        {
            return _outputWindowPane.GetValueAsync();
        }

        private async Task<IVsOutputWindowPane> CreateOutputWindowPaneAsync()
        {
            await _threadingService.SwitchToUIThread();

            IVsOutputWindow outputWindow = _outputWindow.Value;
            if (outputWindow == null)
                return null;    // Command-line build

            Guid activePane = outputWindow.GetActivePane();

            IVsOutputWindowPane pane = CreateProjectOutputWindowPane(outputWindow);

            // Creating a pane causes it to be "active", reset the active pane back to the previously active pane
            if (activePane != Guid.Empty)
                outputWindow.ActivatePane(activePane);

            return pane;
        }

        private IVsOutputWindowPane CreateProjectOutputWindowPane(IVsOutputWindow outputWindow)
        {
            Guid paneGuid = ProjectOutputWindowPaneGuid;
            HResult hr = outputWindow.CreatePane(ref paneGuid, VSResources.OutputWindowPaneTitle, fInitVisible: 1, fClearWithSolution: 0);
            if (hr.Failed)
                throw hr.Exception;

            hr = outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane pane);
            if (hr.Failed)
                throw hr.Exception;

            return pane;
        }
    }
}