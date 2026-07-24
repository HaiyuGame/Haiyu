using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Models.Tasks
{
    public partial class TaskWrapper : ObservableObject
    {
        public TaskWrapper(
            string guid,
            string header,
            string description,
            bool isRun,
            string settingName,
            bool autoLaunche
        )
        {
            this.Guid = guid;
            this.Header = header;
            this.Description = description;
            IsRun = isRun;
            SettingName = settingName;
            AutoLaunche = autoLaunche;
        }

        public string Guid { get; }

        public string Header { get; }

        public string Description { get; }

        public string SettingName { get; }

        [ObservableProperty]
        public partial bool AutoLaunche { get; set; }

        public bool IsRun { get; }

        public IRelayCommand SendStartCommand =>
            new RelayCommand(() =>
            {
                WeakReferenceMessenger.Default.Send<SendTaskMessager>(
                    new(SendTaskType.Start, this)
                );
            });

        public IRelayCommand SendStopCommand =>
            new RelayCommand(() =>
            {
                WeakReferenceMessenger.Default.Send<SendTaskMessager>(new(SendTaskType.Stop, this));
            });

        public IRelayCommand SendInvokeCommand =>
            new RelayCommand(() =>
            {
                WeakReferenceMessenger.Default.Send<SendTaskMessager>(
                    new(SendTaskType.Invoke, this)
                );
            });

        partial void OnAutoLauncheChanged(bool value)
        {
            WeakReferenceMessenger.Default.Send<SendTaskMessager>(
                new(SendTaskType.Launche, this, value)
            );
        }
    }

    public record SendTaskMessager(SendTaskType type, TaskWrapper wrapper, bool launche = false);

    public enum SendTaskType : uint
    {
        Start = 0,
        Stop = 1,
        Invoke = 2,
        Launche = 3,
    }
}
