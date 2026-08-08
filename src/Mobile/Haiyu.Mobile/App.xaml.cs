using Haiyu.KuroClient;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

namespace Haiyu.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            EnsureMemoryPackFormatters();
        }

        static void EnsureMemoryPackFormatters()
        {
            MemoryPackFormatterProvider.Register<RecordCardItemWrapper>();
            MemoryPackFormatterProvider.Register<RecordCacheDetily>();
            MemoryPackFormatterProvider.Register<WavesAnalysisPlayerCard>();
            MemoryPackFormatterProvider.Register<WavesAnalysisPlayerCardItem>();
            MemoryPackFormatterProvider.Register<LocalAccount>();
        }


        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
