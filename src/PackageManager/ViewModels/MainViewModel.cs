﻿using Neptuo.Observables;
using PackageManager.Services;
using PackageManager.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackageManager.ViewModels
{
    public class MainViewModel : ObservableObject, IPackageSourceProvider
    {
        private string packageSourceUrl;
        public string PackageSourceUrl
        {
            get { return packageSourceUrl; }
            set
            {
                if (packageSourceUrl != value)
                {
                    packageSourceUrl = value;
                    RaisePropertyChanged();
                }
            }
        }

        string IPackageSourceProvider.Url => PackageSourceUrl;

        public BrowserViewModel Browser { get; }
        public InstalledViewModel Installed { get; }
        public UpdatesViewModel Updates { get; }

        public CancelCommand Cancel { get; }

        private bool isLoading;
        public bool IsLoading
        {
            get { return isLoading; }
            set
            {
                if (isLoading != value)
                {
                    isLoading = value;
                    RaisePropertyChanged();
                }
            }
        }

        public MainViewModel(ISearchService search, IInstallService install)
        {
            Browser = new BrowserViewModel(this, search, install);
            Installed = new InstalledViewModel(install);
            Updates = new UpdatesViewModel(this, install, search);

            Cancel = new CancelCommand(
                Browser.Search, 
                Browser.Install, 
                Browser.Uninstall,
                Installed.Uninstall,
                Updates.Update,
                Updates.Refresh
            );
            Cancel.CanExecuteChanged += OnCancelCanExecuteChanged;
        }

        private void OnCancelCanExecuteChanged(object sender, EventArgs e)
            => IsLoading = Cancel.CanExecute();
    }
}
