﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace Unigram.Core.Managers
{
    public class BackgroundTaskManager
    {
        public static bool Register(string name, string entryPoint, IBackgroundTrigger trigger, Action onCompleted = null)
        {
            //var access = await BackgroundExecutionManager.RequestAccessAsync();
            //if (access == BackgroundAccessStatus.DeniedByUser || access == BackgroundAccessStatus.DeniedBySystemPolicy)
            //{
            //    return false;
            //}
            try
            {
                foreach (var t in BackgroundTaskRegistration.AllTasks)
                {
                    if (t.Value.Name == name)
                    {
                        //t.Value.Unregister(false);
                        return false;
                    }
                }

                var builder = new BackgroundTaskBuilder();
                builder.Name = name;

                if (entryPoint != null)
                {
                    builder.TaskEntryPoint = entryPoint;
                }

                builder.SetTrigger(trigger);

                var registration = builder.Register();
                if (onCompleted != null)
                {
                    registration.Completed += (s, a) =>
                    {
                        onCompleted();
                    };
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
