using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace SimpleWorkflow
{
    public partial class SwfWorkerAgent
    {
        private class StandardActivityAgent<TActivityWorker> : ISwfWorkerAgent
            where TActivityWorker : SwfActivityWorker
        {
            private bool _isRunning = false;
            private readonly IAmazonSimpleWorkflow _workflow;
            private readonly IServiceProvider _serviceProvider;

            public StandardActivityAgent(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
                _workflow = (IAmazonSimpleWorkflow)_serviceProvider.GetService(typeof(IAmazonSimpleWorkflow));
            }

            void ISwfWorkerAgent.Start()
            {
                _isRunning = true;
                var runWorker = RunWorker(typeof(TActivityWorker));
                Task.WaitAny(runWorker);
            }

            void ISwfWorkerAgent.Stop()
            {
                _isRunning = false;
            }

            private async Task RunWorker(Type workerType)
            {
                var domain = workerType.GetCustomAttribute<SwfWorkerAttribute>().Domain;
                var taskList = workerType.GetCustomAttribute<SwfWorkerAttribute>().TaskList;
                var workerId = Guid.NewGuid().ToString();
                while (_isRunning)
                {
                    var activityTask = await GetActivityTask(domain, taskList, workerId);
                    if (string.IsNullOrEmpty(activityTask.TaskToken))
                    {
                        continue;
                    }
                    await ExecuteActivityTaskHandler(workerType, activityTask);
                }
            }

            private async Task ExecuteActivityTaskHandler(Type workerType, ActivityTask activityTask)
            {
                Exception exception = null;
                try
                {
                    using (var worker = (SwfActivityWorker)_serviceProvider.GetService(workerType))
                    {
                        await worker.HandleActivityTask(activityTask);
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                if (exception != null)
                {
                    await _workflow.RespondActivityTaskFailedAsync(new RespondActivityTaskFailedRequest
                    {
                        Reason = "exception",
                        Details = string.Format("{0}\n\n{1}", exception.Message, exception.StackTrace),
                        TaskToken = activityTask.TaskToken
                    });
                }
            }

            private async Task<ActivityTask> GetActivityTask(string domain, string taskList, string workerId)
            {
                PollForActivityTaskResponse response = null;
                do
                {
                    response = await PollForActivityTaskAsync(domain, taskList, workerId);
                }
                while (_isRunning && string.IsNullOrEmpty(response.ActivityTask.TaskToken));
                return response.ActivityTask;
            }

            private async Task<PollForActivityTaskResponse> PollForActivityTaskAsync(string domain, string taskList, string workerId)
            {
                return await _workflow.PollForActivityTaskAsync(new PollForActivityTaskRequest
                {
                    Domain = domain,
                    Identity = workerId,
                    TaskList = new TaskList
                    {
                        Name = taskList
                    }
                });
            }
        }
    }
}
