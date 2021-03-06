﻿using System;
using Akka.Actor;
using Caliburn.Micro;
using FeatureAdmin.Common;
using FeatureAdmin.Core.Models;
using FeatureAdmin.Messages;
using FeatureAdmin.Core.Repository;
using FeatureAdmin.Core.Services;

namespace FeatureAdmin.ViewModels
{

    public class AppViewModel : Conductor<Screen>.Collection.OneActive,
        Caliburn.Micro.IHandle<ConfirmationRequest>,
        Caliburn.Micro.IHandle<DecisionRequest>,
        Caliburn.Micro.IHandle<OpenWindow<ActivatedFeature>>,
        Caliburn.Micro.IHandle<OpenWindow<ActivatedFeatureSpecial>>,
        Caliburn.Micro.IHandle<OpenWindow<FeatureDefinition>>,
        Caliburn.Micro.IHandle<OpenWindow<Location>>,
        Caliburn.Micro.IHandle<ProgressMessage>,
        Caliburn.Micro.IHandle<ShowActivatedFeatureWindowMessage>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly IWindowManager windowManager;
        IDataService dataService;
        private bool elevatedPrivileges;
        private bool force;
        private Guid recentLoadTask;
        IFeatureRepository repository;
        private Akka.Actor.IActorRef taskManagerActorRef;
        // private IActorRef viewModelSyncActorRef;
        public AppViewModel(
            IWindowManager windowManager,
            IEventAggregator eventAggregator,
            IFeatureRepository repository,
            IDataService dataService)
        {
            // Load settings at the very beginning, so that they are up to date
            LoadSettings();

            this.windowManager = windowManager;

            this.eventAggregator = eventAggregator;
            this.eventAggregator.Subscribe(this);

            this.repository = repository;
            this.dataService = dataService;

            DisplayName = Core.Common.StringHelper.GetApplicationDisplayName(dataService.CurrentBackend);

            StatusBarVm = new StatusBarViewModel(eventAggregator);

            FeatureDefinitionListVm = new FeatureDefinitionListViewModel(eventAggregator, repository);

            LocationListVm = new LocationListViewModel(eventAggregator, repository);
            UpgradeListVm = new UpgradeListViewModel(eventAggregator, repository);
            CleanupListVm = new CleanupListViewModel(eventAggregator, repository);

            Items.Add(LocationListVm);
            Items.Add(UpgradeListVm);
            Items.Add(CleanupListVm);

            ActivateItem(LocationListVm);

            ActivatedFeatureVm = new ActivatedFeatureViewModel(eventAggregator, repository);

            LogVm = new LogViewModel(eventAggregator);

            InitializeActors();

            TriggerFarmLoadTask(Common.Constants.Tasks.TaskTitleInitialLoad);
        }

        public ActivatedFeatureViewModel ActivatedFeatureVm { get; private set; }
        public bool CanReLoad { get; private set; }
        public CleanupListViewModel CleanupListVm { get; private set; }
        public bool ElevatedPrivileges
        {
            get
            {
                return elevatedPrivileges;
            }
            set
            {
                elevatedPrivileges = value;
                UpdateSettings();
            }
        }

        public FeatureDefinitionListViewModel FeatureDefinitionListVm { get; private set; }

        public bool Force
        {
            get
            {
                return force;
            }
            set
            {
                force = value;
                UpdateSettings();
            }
        }

        public LocationListViewModel LocationListVm { get; private set; }
        public LogViewModel LogVm { get; private set; }
        public StatusBarViewModel StatusBarVm { get; private set; }
        public UpgradeListViewModel UpgradeListVm { get; private set; }
        public void Handle<T>(OpenWindow<T> message) where T : class
        {
            throw new System.Exception("Todo - convert dto to object and then detail view ...");
            // OpenWindow(message.ViewModel);
        }

        public void Handle(ProgressMessage message)
        {
            if (message.Progress >= 1d && message.TaskId == recentLoadTask)
            {
                CanReLoad = true;
            }
        }
        public void Handle(ShowActivatedFeatureWindowMessage message)
        {
            if (message.ShowWindow && ActivatedFeatureVm == null)
            {
                ActivatedFeatureVm = new ActivatedFeatureViewModel(eventAggregator, repository);
                var resendSelectedDefinition = new ResendItemSelectedRequest<FeatureDefinition>();
                eventAggregator.BeginPublishOnUIThread(resendSelectedDefinition);
            }
            
            if(!message.ShowWindow && ActivatedFeatureVm!= null)
            {
                ActivatedFeatureVm = null;
            }
        }

        public void Handle(ConfirmationRequest message)
        {
            DialogViewModel dialogVm = new DialogViewModel(eventAggregator, message);

            this.windowManager.ShowDialog(dialogVm, null, GetDialogSettings());
        }

        public void Handle(DecisionRequest message)
        {
            DialogViewModel dialogVm = new DialogViewModel(eventAggregator, message);

            this.windowManager.ShowDialog(dialogVm, null, GetDialogSettings());
        }

        public void Handle(OpenWindow<ActivatedFeature> message)
        {
            OpenWindow(message.ViewModel.ToDetailViewModel());
        }

        public void Handle(OpenWindow<FeatureDefinition> message)
        {
            var vm = message.ViewModel;
            var activatedFeatures = repository.GetActivatedFeatures(vm);
            OpenWindow(vm.ToDetailViewModel(activatedFeatures));
        }

        public void Handle(OpenWindow<Location> message)
        {
            var vm = message.ViewModel;
            var activatedFeatures = repository.GetActivatedFeatures(vm);
            OpenWindow(vm.ToDetailViewModel(activatedFeatures));
        }

        public void Handle(OpenWindow<ActivatedFeatureSpecial> message)
        {
            OpenWindow(message.ViewModel.ToDetailViewModel()); ;
        }

        public void OpenWindow(DetailViewModel viewModel)
        {
            dynamic settings = new System.Dynamic.ExpandoObject();
            settings.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;

            this.windowManager.ShowWindow(viewModel, null, settings);
        }

        public void ReLoad()
        {
            TriggerFarmLoadTask(Common.Constants.Tasks.TaskTitleReload);
        }

        public void OpenUrl()
        {
            // see also https://support.microsoft.com/en-us/help/305703/how-to-start-the-default-internet-browser-programmatically-by-using-vi

            string target = "https://www.featureadmin.com";
            
            try
            {
                System.Diagnostics.Process.Start(target);
            }
            catch
                (
                 System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                {
                    var dialog = new ConfirmationRequest(
                        "No Browser found",
                        noBrowser.Message
                        );
                    DialogViewModel dialogVm = new DialogViewModel(eventAggregator, dialog);
                    this.windowManager.ShowDialog(dialogVm, null, GetDialogSettings());
                }
            }
            catch (System.Exception other)
            {
                var dialog = new ConfirmationRequest(
                        "System Exception when opening browser",
                        other.Message
                        );
                DialogViewModel dialogVm = new DialogViewModel(eventAggregator, dialog);
                this.windowManager.ShowDialog(dialogVm, null, GetDialogSettings());
            }

        }

        public void About()
        {

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            var year = System.IO.File.GetCreationTime(
                System.Reflection.Assembly.GetExecutingAssembly().Location).Year;

                    var dialog = new ConfirmationRequest(
                        "About Feature Admin",
                        "SharePoint Feature Admin\n" + 
                        "Current version " + version + "\n\n" + 
                        "Created by Achim Ismaili in " + year + "\n" +
                        "https://www.featureadmin.com"
                        );
                    DialogViewModel dialogVm = new DialogViewModel(eventAggregator, dialog);
                    this.windowManager.ShowDialog(dialogVm, null, GetDialogSettings());
           
        }

        private dynamic GetDialogSettings()
        {
            dynamic settings = new System.Dynamic.ExpandoObject();
            settings.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            settings.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
            settings.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
            // settings.Title = "window title";
            // settings.Icon = new BitmapImage(new Uri("pack://application:,,,/MyApplication;component/Assets/myicon.ico"));

            return settings;
        }
        private void InitializeActors()
        {
            taskManagerActorRef = ActorSystemReference.ActorSystem.ActorOf(
                Akka.Actor.Props.Create(() =>
                new Actors.Tasks.TaskManagerActor(
                    eventAggregator,
                    repository,
                    dataService,
                    elevatedPrivileges,
                    force)));
        }

        private void LoadSettings()
        {
            // Handling Application Settings in WPF, see https://msdn.microsoft.com/en-us/library/a65txexh(v=vs.140).aspx
            elevatedPrivileges = Properties.Settings.Default.elevatedPrivileges;
            force = Properties.Settings.Default.force;
        }

        private void TriggerFarmLoadTask(string taskTitle)
        {
            CanReLoad = false;
            recentLoadTask = Guid.NewGuid();
            taskManagerActorRef.Tell(new Core.Messages.Request.LoadTask(recentLoadTask, taskTitle, Core.Factories.LocationFactory.GetDummyFarmForLoadCommand()));

            // fyi - to do this via eventAggregator would also allow to trigger reload from other viewModels, e.g. also for single locations, 
            // but at least the initial farm load cannot be triggered by eventaggregator, because it is not listening at that 
            // early point in time, so all triggers from this viewModel directly call the actor ...
            // eventAggregator.PublishOnUIThread(new LoadTask(taskTitle, Core.Factories.LocationFactory.GetDummyFarmForLoadCommand()));
        }
        private void UpdateSettings()
        {
            var settings = new Core.Messages.Completed.SettingsChanged(elevatedPrivileges, force);

            eventAggregator.PublishOnUIThread(settings);

            // Handling Application Settings in WPF, see https://msdn.microsoft.com/en-us/library/a65txexh(v=vs.140).aspx
            Properties.Settings.Default.elevatedPrivileges = elevatedPrivileges;
            Properties.Settings.Default.force = force;
            Properties.Settings.Default.Save();
        }
    }
}
