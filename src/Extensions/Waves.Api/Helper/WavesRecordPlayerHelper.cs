using System.Collections.ObjectModel;
using Waves.Api.Helper;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

public static class WavesRecordPlayerHelper
{
    static Dictionary<CardPoolType, (string, bool)> PoolTypeNames { get; } =
        new()
        {
            { CardPoolType.RoleActivity, ("角色活动", true) },
            { CardPoolType.WeaponsActivity, ("武器活动", false) },
            { CardPoolType.RoleResident, ("角色常驻", false) },
            { CardPoolType.WeaponsResident, ("武器常驻", false) },
            { CardPoolType.Beginner, ("新手换取", false) },
            { CardPoolType.BeginnerChoice, ("新手自选", false) },
            { CardPoolType.GratitudeOrientation, ("感恩定向", false) },
            { CardPoolType.CharacterNovice, ("角色新旅", false) },
            { CardPoolType.WeaponNovice, ("武器新旅", false) },
            { CardPoolType.CharacterCollaboration, ("角色联动", true) },
            { CardPoolType.WeaponCollaboration, ("武器联动", false) },
        };


    extension(WavesAnalysisPlayerCardItem analysisItem)
    {
        public GameRecordNavigationItem GetRecordNavItem()
        {
            return new GameRecordNavigationItem()
            {
                DisplayName = PoolTypeNames
                    .GetValueOrDefault((CardPoolType)analysisItem.PoolType, ("未知", false))
                    .Item1,
                Id = analysisItem.PoolType,
            };
        }

        /// <summary>
        /// 有小保底的池子
        /// </summary>
        /// <returns></returns>
        public bool IsFlage()
        {
            return PoolTypeNames
                .GetValueOrDefault((CardPoolType)analysisItem.PoolType, ("未知", false))
                .Item2;
        }

        /// <summary>
        /// 获得抽卡时间线
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<RecordTimeLine> GetTimeLine()
        {
            return new ObservableCollection<RecordTimeLine>(
                analysisItem
                    .Resource.GroupBy(x => x.RecordTime.Date)
                    .Select(x => new RecordTimeLine() { DateTime = x.Key, Values = x.Count() })
            );
        }
    }

    extension(WavesAnalysisPlayerCard card)
    {
        public (string Title, double Score, double doubleCount) EvaluateLuck()
        {
            var rates = new List<double>();
            var allResources = new List<RecordCardItemWrapper>();

            foreach (var item in card.Items)
            {
                var resources = item.Resource?.ToList() ?? [];
                allResources.AddRange(resources);

                if (item.IsFlage() && resources.Count > 0)
                {
                    var formatted = RecordHelper.FormatStartFive(resources, out _, checkResident: true);
                    if (formatted.Item1?.Count > 0)
                    {
                        rates.Add(RecordHelper.GetGuaranteedRange(formatted.Item1));
                    }
                }
            }

            double guaranteeScore;
            if (rates.Count > 0)
            {
                guaranteeScore = (1 - rates.Average() / 100) * 100;
            }
            else
            {
                guaranteeScore = 50;
            }

            var fiveStars = RecordHelper.FormatRecordFive(allResources);
            double efficiencyScore;
            if (fiveStars.Count > 0)
            {
                var avgPulls = fiveStars.CalculateAvg();
                efficiencyScore = Math.Max(0, (80 - avgPulls) / 40 * 100);
            }
            else
            {
                efficiencyScore = 50;
            }

            var totalPullsScore = Math.Min(allResources.Count / 1000.0, 1.0) * 100;

            var sorted = allResources.OrderBy(x => x.RecordTime).ToList();
            var fiveStarCount = sorted.Count(x => x.QualityLevel == 5);
            int doubleCount = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].QualityLevel != 5)
                    continue;
                for (int j = i + 1; j < Math.Min(i + 10, sorted.Count); j++)
                {
                    if (sorted[j].QualityLevel == 5)
                    {
                        doubleCount++;
                        break;
                    }
                }
            }

            double doubleScore = doubleCount switch
            {
                0 => 0,
                1 => 70,
                2 => 85,
                _ => 100,
            };

            var rawScore =
                guaranteeScore * 0.2
                + efficiencyScore * 0.4
                + totalPullsScore * 0.1
                + doubleScore * 0.3;
            rawScore = Math.Clamp(rawScore, 0, 100);

            var title = rawScore switch
            {
                < 20 => "大非酋",
                < 40 => "非酋",
                < 60 => "平民",
                < 80 => "小欧皇",
                _ => "至尊无敌欧皇",
            };

            return (title, Math.Round(rawScore, 1), doubleCount);
        }

        /// <summary>
        /// 获取整体抽卡时间线
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<RecordTimeLine> GetTimeLine()
        {
            return new ObservableCollection<RecordTimeLine>(
                card.Items.SelectMany(x=>x.Resource)
                    .GroupBy(x => x.RecordTime.Date)
                    .Select(x => new RecordTimeLine() { DateTime = x.Key, Values = x.Count() })
            );
        }
    }
}

public class RecordTimeLine
{
    public DateTime DateTime { get; set; }

    public int Values { get; set; }
}
