﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using aspnetcore_aad.Models;
using Microsoft.Extensions.Configuration;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Net;
using System.Runtime.InteropServices;
using System.IO;

namespace aspnetcore_aad.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        const long Mebi = 1024 * 1024;
        const long Gibi = Mebi * 1024;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var indexViewModel = await GetIndexViewModel();

            return View(indexViewModel);
        }

        static async Task<IndexViewModel> GetIndexViewModel()
        {
            var indexViewModel = new IndexViewModel();
            GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
            indexViewModel.TotalAvailableMemory = GetInBestUnit(gcInfo.TotalAvailableMemoryBytes);
            indexViewModel.HostName = Dns.GetHostName();
            indexViewModel.IpList = await Dns.GetHostAddressesAsync(indexViewModel.HostName);

            indexViewModel.cGroup = RuntimeInformation.OSDescription.StartsWith("Linux") && Directory.Exists("/sys/fs/cgroup/memory");
            if (indexViewModel.cGroup)
            {
                string usage = System.IO.File.ReadAllLines("/sys/fs/cgroup/memory/memory.usage_in_bytes")[0];
                string limit = System.IO.File.ReadAllLines("/sys/fs/cgroup/memory/memory.limit_in_bytes")[0];
                string cpuUsage = System.IO.File.ReadAllLines("/sys/fs/cgroup/cpu/cpuacct.usage")[0];
                indexViewModel.MemoryUsage = GetInBestUnit(long.Parse(usage));
                indexViewModel.MemoryLimit = GetInBestUnit(long.Parse(limit));
                indexViewModel.CpuUsage = GetMillisecond(long.Parse(cpuUsage));
            }

            return indexViewModel;
        }

        static string GetMillisecond(long nanosecond)
        {
            decimal millisecond = Decimal.Divide(nanosecond, 1000000);
            return $"{millisecond:F} ms";
        }

        static string GetInBestUnit(long size)
        {
            if (size < Mebi)
            {
                return $"{size} bytes";
            }
            else if (size < Gibi)
            {
                decimal mebibytes = Decimal.Divide(size, Mebi);
                return $"{mebibytes:F} MiB";
            }
            else
            {
                decimal gibibytes = Decimal.Divide(size, Gibi);
                return $"{gibibytes:F} GiB";
            }
        }

        // Get: /GetSecretFromKV
        public async Task<IActionResult> GetSecretFromKV()
        {
            // Get the credential of user assigned identity
            var credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(_configuration["UserAssignedIdentityClientId"]), 
                new AzureCliCredential());
            // Get secret from key vault
            var kvClient = new SecretClient(new Uri(_configuration["KeyVaultUri"]), credential);
            var secretBundle = await kvClient.GetSecretAsync(_configuration["SecretName"]);
            var indexViewModel = await GetIndexViewModel();
            indexViewModel.KVSecret = secretBundle.Value.Name + ": " + secretBundle.Value.Value;

            return View("Index", indexViewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
