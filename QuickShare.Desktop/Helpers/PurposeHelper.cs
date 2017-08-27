﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace QuickShare.Desktop.Helpers
{
    internal static class PurposeHelper
    {
        internal static async Task<bool> ConfirmPurpose()
        {
#if ((!SQUIRREL) && (!DEBUG))
            AppServiceConnection connection = new AppServiceConnection()
            {
                AppServiceName = "com.roamit.pcservice",
                PackageFamilyName = "36835MahdiGhiasi.Roamit_yddpmccgg2mz2" //Windows.ApplicationModel.Package.Current.Id.FamilyName
            };
            var result = await connection.OpenAsync();
            if (result == AppServiceConnectionStatus.Success)
            {
                ValueSet valueSet = new ValueSet
                {
                    { "Action", "WhyImHere" },
                };
                var response = await connection.SendMessageAsync(valueSet);

                if (response.Message.ContainsKey("Answer"))
                {
                    var answer = response.Message["Answer"] as string;

                    if (answer == "Die")
                    {
                        // Goodbye cruel world...
                        System.Windows.Application.Current.Shutdown();
                        return false;
                    }
                    else if (answer == "Genocide")
                    {
                        // Gonna kill everyone, and then myself.

                        await StopRunningOnStartup();
                        await Genocide();

                        return false;
                    }
                    else if (answer == "Alone")
                    {
                        // Do I have any brothers or sisters?

                        var siblings = GetSiblings();

                        if (siblings.Count() > 0)
                        {
                            // I do.
                            // I'll die then...
                            System.Windows.Application.Current.Shutdown();
                            
                            //// I will kill them all.
                            //CloseSiblings();

                            return false;
                        }
                        else
                        {
                            // I don't, so I shall live in peace
                            return true;
                        }
                    }
                    else if (answer == "ForgetEverything")
                    {
                        Settings.Data.AccountId = "";
                        Settings.Save();

                        await StopRunningOnStartup();
                        await Genocide();

                        return false;
                    }
                    else
                    {
                        System.Windows.Application.Current.Shutdown();
                        return false;
                    }
                }
                else
                {
                    System.Windows.Application.Current.Shutdown();
                    return false;
                }
            }
            else
            {
                System.Windows.Application.Current.Shutdown();
                return false;
            }
#else
            return true;
#endif
        }

        private static async Task StopRunningOnStartup()
        {
            var startupTask = await Windows.ApplicationModel.StartupTask.GetAsync("RoamitStartupTask");
            startupTask.Disable();
        }

        private static IEnumerable<Process> GetSiblings()
        {
            var currentProcess = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(currentProcess.ProcessName);

            var siblings = processes.Where(p => p.Id != currentProcess.Id && p.MainModule.FileName == currentProcess.MainModule.FileName);
            return siblings;
        }

        private static async Task Genocide()
        {
            CloseSiblings();

            await Task.Delay(500);

            System.Windows.Application.Current.Shutdown();
        }

        private static void CloseSiblings()
        {
            var siblings = GetSiblings();

            foreach (var item in siblings)
            {
                item.CloseApp();
            }
        }
    }
}
