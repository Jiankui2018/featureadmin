﻿using Caliburn.Micro;
using FeatureAdmin.Core.Models.Enums;
using FeatureAdmin.Messages;
using System;

namespace FeatureAdmin.Core.Models.Tasks
{
    public abstract class BaseTaskActor : Actors.BaseActor
    {
        protected readonly IEventAggregator eventAggregator;

        public BaseTaskActor(IEventAggregator eventAggregator, Guid id)
            : this(eventAggregator, "Generic title to be overwritten", id)
        {
        }

        public BaseTaskActor(IEventAggregator eventAggregator, string title, Guid id)
        {
            this.eventAggregator = eventAggregator;

            Status = TaskStatus.Started;
            Id = id;
            Title = title;
            Start = null;
            End = null;

        }

        protected override void ReceiveCancelMessage(CancelMessage message)
        {
            if (!TaskCanceled) // prevent multiple log entries
            {
                HandleCancelation(message);
                Status = TaskStatus.Canceled;
                CancelationMessage = message.CancelationMessage;
                End = DateTime.Now;

                var logEndMsg = new LogMessage(message.LogLevel,
                string.Format("{0} {1}", Status.ToString(), message.CancelationMessage)
                );
                eventAggregator.PublishOnUIThread(logEndMsg);

                var progressMsg = new ProgressMessage(
                    Id,
                    1d,
                    string.Format("'{0}' '{1}'! {2}", Title, Status.ToString(), message.CancelationMessage));

                eventAggregator.PublishOnUIThread(progressMsg);
            }
        }

        protected abstract void HandleCancelation(CancelMessage cancelMessage);

        public string ElapsedTime
        {
            get
            {
                return (End == null ? DateTime.Now : End.Value).Subtract(
                        Start == null ? DateTime.Now : Start.Value).ToString("c");
            }
        }

        public DateTime? End { get; set; }
        public Guid Id { get; }

        public abstract double PercentCompleted { get; }
        public DateTime? Start { get; set; }
        public TaskStatus Status { get; protected set; }
        public abstract string StatusReport { get; }
        public string Title { get; protected set; }

        protected void SendProgress()
        {
            if (!TaskCanceled)
            {
                if (PercentCompleted > 0d && Status == TaskStatus.Started)
                {
                    Status = TaskStatus.InProgress;
                }

                if (PercentCompleted >= 1d && Status != TaskStatus.Failed && Status != TaskStatus.Canceled
                    && Status != TaskStatus.Completed)
                {
                    Status = TaskStatus.Completed;
                }

                if (PercentCompleted != 1d && Start == null)
                {
                    Start = DateTime.Now;
                    var logMsg = new LogMessage(LogLevel.Information,
                    string.Format("Started '{1}' (ID: '{0}')", Id, Title)
                    );
                    eventAggregator.PublishOnUIThread(logMsg);
                }

                if (PercentCompleted >= 1d && End == null)
                {
                    End = DateTime.Now;
                    var logEndMsg = new LogMessage(Core.Models.Enums.LogLevel.Information,
                    string.Format("{0} {1}", Status.ToString(), StatusReport)
                    );
                    eventAggregator.PublishOnUIThread(logEndMsg);

                    // as task list ist deleted after restart, no need to delete tasks here
                }

                ProgressMessage progressMsg;

                if (PercentCompleted < 1d)
                {
                    if (Status == TaskStatus.InProgress || (Status == TaskStatus.Started && PercentCompleted == 0))
                    {
                        progressMsg = new ProgressMessage(Id, PercentCompleted, string.Format("'{0}' in progress ({1:F0}%), please wait ...", Title, PercentCompleted * 100, Status.ToString()));
                    }
                    else
                    {
                        progressMsg = new ProgressMessage(Id, PercentCompleted, string.Format("Attention! '{0}' '{2}' ({1:F0}%), please wait ...", Title, PercentCompleted * 100, Status.ToString()));
                        var logMsg = new LogMessage(LogLevel.Warning, progressMsg.Title);
                        eventAggregator.PublishOnUIThread(logMsg);
                    }
                }
                else
                {
                    progressMsg = new ProgressMessage(Id, PercentCompleted, string.Format("'{0}' '{2}'! Elapsed time: {1}", Title, ElapsedTime, Status.ToString()));
                }

                eventAggregator.PublishOnUIThread(progressMsg);
            }
        }
    }
}
