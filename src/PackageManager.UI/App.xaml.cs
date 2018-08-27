﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Neptuo.Exceptions.Handlers;
using Neptuo.Logging;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using PackageManager.Exceptions;
using PackageManager.Models;
using PackageManager.Services;
using PackageManager.ViewModels;
using PackageManager.Views;
using PackageManager.Views.Converters;

namespace PackageManager
{
    public partial class App : Application, SelfUpdateService.IApplication, ProcessService.IApplication
    {
        public Args Args { get; private set; }
        internal ProcessService ProcessService { get; private set; }
        internal IExceptionHandler ExceptionHandler { get; private set; }
        internal Navigator Navigator { get; private set; }
        internal ILogFactory LogFactory { get; private set; }

        SelfUpdateService.IArgs SelfUpdateService.IApplication.Args => Args;
        object ProcessService.IApplication.Args => Args;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            LogFactory = new DefaultLogFactory()
                .AddConsole();

            Args = new Args(e.Args);
            if (!Directory.Exists(Args.Path))
            {
                MessageBox.Show("Missing argument '--path' - a target path to install packages to.", "Packages");
                Shutdown();
                return;
            }

            ProcessService = new ProcessService(this);

            base.OnStartup(e);

            NuGetSourceRepositoryFactory repositoryFactory = new NuGetSourceRepositoryFactory();
            NuGetSearchService.IFilter searchFilter = null;
            if (Args.Dependencies.Any())
                searchFilter = new DependencyNuGetSearchFilter(Args.Dependencies, Args.Monikers);

            NuGetPackageContent.IFrameworkFilter frameworkFilter = null;
            if (Args.Monikers.Any())
                frameworkFilter = new NuGetFrameworkFilter(Args.Monikers);

            SelfPackageConfiguration selfPackageConfiguration = new SelfPackageConfiguration(Args.SelfPackageId);

            SelfPackageConverter.Configuration = selfPackageConfiguration;

            NuGetSearchService searchService = new NuGetSearchService(repositoryFactory, searchFilter, frameworkFilter);
            NuGetInstallService installService = new NuGetInstallService(repositoryFactory, Args.Path, frameworkFilter);
            SelfUpdateService selfUpdateService = new SelfUpdateService(this, ProcessService);

            EnsureSelfPackageInstalled(installService);

            MainViewModel viewModel = new MainViewModel(
                searchService,
                installService,
                selfPackageConfiguration,
                selfUpdateService
            );
            viewModel.PackageSourceUrl = Args.PackageSourceUrl;

            MainWindow wnd = new MainWindow(viewModel);

            Navigator = new Navigator(wnd);

            BuildExceptionHandler();

            wnd.Show();

            if (Args.IsSelfUpdate)
                RunSelfUpdate(wnd);
        }

        private void BuildExceptionHandler()
        {
            var exceptionBuilder = new ExceptionHandlerBuilder();

            exceptionBuilder
                .Handler(new LogExceptionHandler(LogFactory.Scope("Exception")));

            var unauthorized = new UnauthorizedExceptionHandler(ProcessService);

            exceptionBuilder
                .Filter<UnauthorizedAccessException>()
                .Handler(unauthorized);

            exceptionBuilder
                .Filter<PackagesConfigWriterException>()
                .Filter(e => e.InnerException is FileNotFoundException)
                .Handler(unauthorized);

            exceptionBuilder
                .Filter<FatalProtocolException>()
                .Handler(new NuGetFatalProtocolExceptionHandler(Navigator));

            exceptionBuilder
                .Handler(new MessageExceptionHandler(Navigator));

            exceptionBuilder
                .Handler(new ShutdownExceptionHandler(this));

            ExceptionHandler = exceptionBuilder;
        }

        private void EnsureSelfPackageInstalled(NuGetInstallService installService)
        {
            if (Args.SelfPackageId != null)
            {
                SelfPackage package = new SelfPackage(Args.SelfPackageId);
                if (!installService.IsInstalled(package))
                    installService.Install(package);
            }
        }

        private void RunSelfUpdate(MainWindow wnd)
        {
            wnd.ViewModel.Updates.Refresh.Completed += async () =>
            {
                PackageUpdateViewModel package = wnd.ViewModel.Updates.Packages.FirstOrDefault(p => p.Current.Id == Args.SelfPackageId);
                if (package != null)
                    await wnd.ViewModel.Updates.Update.ExecuteAsync(package);
                else
                    Navigator.Notify("Self Update Error", $"Unnable to find update package for PackageManager in feed '{wnd.ViewModel.PackageSourceUrl}'.", Navigator.MessageType.Error);

                Shutdown();
            };
            wnd.SelectUpdatesTab();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ExceptionHandler.Handle(e.Exception);
            e.Handled = true;
        }
    }
}
