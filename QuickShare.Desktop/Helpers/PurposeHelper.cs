﻿using CSharpAnalytics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace QuickShare.Desktop.Helpers
{
    internal static class PurposeHelper
    {
        static readonly int _maxTryCount = 3;
        static readonly TimeSpan _delayBetweenTries = TimeSpan.FromSeconds(1);

        internal static async Task<bool> ConfirmPurpose(int tryNumber = 1)
        {
#if ((!SQUIRREL) && (!DEBUG))
            try
            {
                AppServiceConnection connection = new AppServiceConnection()
                {
                    AppServiceName = "com.roamit.pcservice",
                    PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
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
                            if (response.Message.ContainsKey("AccountId"))
                            {
                                Settings.Data.AccountId = response.Message["AccountId"].ToString();
                                Settings.Save();
                            }

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
#if !DEBUG
                            AutoMeasurement.Client.TrackEvent("Exception", "CP_InvalidResponse2", "");
#endif
                            MessageBox.Show($"CP Error: Invalid response received. ({answer})", "Roamit");
                            System.Windows.Application.Current.Shutdown();
                            return false;
                        }
                    }
                    else
                    {
#if !DEBUG
                        AutoMeasurement.Client.TrackEvent("Exception", "CP_InvalidResponse", "");
#endif
                        MessageBox.Show("CP Error: Invalid response received.", "Roamit");
                        System.Windows.Application.Current.Shutdown();
                        return false;
                    }
                }
                else
                {
                    if (tryNumber < _maxTryCount)
                    {
#if !DEBUG
                        AutoMeasurement.Client.TrackEvent("CP_Purpose_Retry", "CannotConnect", "");
#endif
                        await Task.Delay(_delayBetweenTries);
                        return await ConfirmPurpose(tryNumber + 1);
                    }
                    else
                    {
#if !DEBUG
                        AutoMeasurement.Client.TrackEvent("Exception", "CP_CannotConnect", "");
#endif
                        MessageBox.Show("CP Error: Can't connect to app service.", "Roamit");
                        System.Windows.Application.Current.Shutdown();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (tryNumber < _maxTryCount)
                {
#if !DEBUG
                    AutoMeasurement.Client.TrackEvent("CP_Purpose_Retry", "UnknownError", ex.Message);
                    AutoMeasurement.Client.TrackEvent("CP_Purpose_Retry", "UnknownError_Details", ex.ToString());
#endif
                    await Task.Delay(_delayBetweenTries);
                    return await ConfirmPurpose(tryNumber + 1);
                }
                else
                {
#if !DEBUG
                    AutoMeasurement.Client.TrackEvent("Exception", "CP_Unknown", ex.Message);
                    AutoMeasurement.Client.TrackEvent("Exception", "CP_Unknown_Details", ex.ToString());
#endif
                    MessageBox.Show("CP Error: " + ex.Message, "Roamit");
                    return false;
                }
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

        private static List<Process> GetSiblings()
        {
            var currentProcess = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(currentProcess.ProcessName);

            var siblings = processes.Where(p => ProcessIsMySibling(p, currentProcess));
            return siblings.ToList();
        }

        private static bool ProcessIsMySibling(Process p, Process currentProcess)
        {
            try
            {
                return p.Id != currentProcess.Id && p.MainModule.FileName == currentProcess.MainModule.FileName;
            }
            catch (Exception ex)
            {
#if !DEBUG
                AutoMeasurement.Client.TrackEvent("Exception", "CP_ProcessIsMySibling", ex.Message);
                AutoMeasurement.Client.TrackEvent("Exception", "CP_ProcessIsMySibling_Details", ex.ToString());
#endif
                return false;
            }
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
