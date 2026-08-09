using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Wrapper
{
    public partial class SkipGameVerifyWrapper(string filePath):ObservableObject
    {
        [ObservableProperty]
        public partial string FilePath { get; set; } = filePath;


        [RelayCommand]
        void SenDelete()
        {
            WeakReferenceMessenger.Default.Send<SkipGameVerifyWrapper>(this);
        }

        public static ObservableCollection<SkipGameVerifyWrapper> FromSettings(List<string>? settingPath)
        {
            if (settingPath == null)
                return [];
            return settingPath.Select(x => new SkipGameVerifyWrapper(x)).ToObservableCollection();
        }
    }
}
