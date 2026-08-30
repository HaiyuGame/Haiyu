using System;
using System.Collections.Generic;
using System.Text;
using ABI.Models;
using Haiyu.ABI.Services;

namespace KuroGameDownloadProgram.Tests
{
    public static class ABITest
    {
        public static async Task MonitorTest()
        {
            ComputerMonitorService monitor = new ComputerMonitorService();
            var a = await monitor.ExecuteAsync(
                new CMonitorRequest(),
                new Progress<CMonitorProgress>(s =>
                {
                    Console.WriteLine(s);
                }),
                default
            );
        }
    }
}
