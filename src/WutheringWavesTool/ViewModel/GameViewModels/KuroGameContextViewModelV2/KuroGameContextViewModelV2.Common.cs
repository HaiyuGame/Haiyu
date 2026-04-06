using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.GameViewModels
{
    partial class KuroGameContextViewModelV2
    {
        public bool ShouldReplaceActiveFilesItem(
            ObservableCollection<DownloadActiveFileItem>? current,
            ObservableCollection<DownloadActiveFileItem>? next
        )
        {
            if (ReferenceEquals(current, next))
            {
                return false;
            }

            if (current == null || next == null)
            {
                return true;
            }

            if (current.Count != next.Count)
            {
                return true;
            }

            for (var i = 0; i < current.Count; i++)
            {
                var left = current[i];
                var right = next[i];

                if (!string.Equals(left.FileName, right.FileName, StringComparison.Ordinal))
                {
                    return true;
                }

                if (
                    left.CurrentSize != right.CurrentSize
                    || left.TotalSize != right.TotalSize
                    || left.Progress != right.Progress
                )
                {
                    return true;
                }
            }

            return false;
        }

        public bool ShouldReplaceSetupItems(
            ObservableCollection<DownloadSetupItem>? current,
            ObservableCollection<DownloadSetupItem>? next
        )
        {
            if (ReferenceEquals(current, next))
            {
                return false;
            }

            if (current == null || next == null)
            {
                return true;
            }

            if (current.Count != next.Count)
            {
                return true;
            }

            for (var i = 0; i < current.Count; i++)
            {
                var left = current[i];
                var right = next[i];

                if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal))
                {
                    return true;
                }

                if (left.IsActive != right.IsActive || left.IsOK != right.IsOK)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
